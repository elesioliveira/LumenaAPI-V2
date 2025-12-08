using Microsoft.AspNetCore.Mvc;
using Npgsql;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("API/V1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly string _conn;
    private readonly JwtService _jwt;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config, JwtService jwt, IRefreshTokenRepository refreshRepo)
    {
        _config = config;
        _conn = config.GetConnectionString("DefaultConnection");
        _jwt = jwt;
        _refreshRepo = refreshRepo;
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        // validar entrada básica omitted...
        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();

        var sql = @"
            SELECT id, empresa_id, data_cadastro, nome, email, senha_hash, salt, ativo
            FROM usuario
            WHERE email = @email
            LIMIT 1;
        ";

        UsuarioEntity usuario;
        byte[] senhaHash;
        byte[] salt;

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue("email", dto.Email.Trim());
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return Unauthorized(new Response<UsuarioEntity>(false, "Usuário ou senha incorretos."));

            usuario = new UsuarioEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                empresaid = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                datacadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                nome = reader.GetString(reader.GetOrdinal("nome")),
                email = reader.GetString(reader.GetOrdinal("email")),
                ativo = reader.GetBoolean(reader.GetOrdinal("ativo"))
            };

            senhaHash = (byte[])reader["senha_hash"];
            salt = (byte[])reader["salt"];
        }

        if (!usuario.ativo)
            return Unauthorized(new Response<UsuarioEntity>(false, "Usuário inexistente/desativado."));

        if (!PasswordHasher.VerifyPassword(dto.Senha, senhaHash, salt))
            return Unauthorized(new Response<UsuarioEntity>(false, "Usuário ou senha incorretos."));

        // Gerar access token
        var jwtToken = _jwt.GenerateToken(usuario.id, usuario.empresaid);

        // Gerar refresh token (texto)
        var refreshTokenPlain = RefreshTokenHelper.GenerateRefreshToken();
        var refreshHash = RefreshTokenHelper.HashToken(refreshTokenPlain);
        var refreshExpires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7"));

        // Salvar hash no banco
        await _refreshRepo.SaveAsync(usuario.id, refreshHash, refreshExpires);

        // Set cookies
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // production: true (requires HTTPS)
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15"))
        };
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = refreshExpires
        };

        Response.Cookies.Append("access_token", jwtToken, accessCookieOptions);
        Response.Cookies.Append("refresh_token", refreshTokenPlain, refreshCookieOptions); // plain cookie; server stores only hash

        return Ok(new Response<UsuarioEntity>(true, "Login realizado com sucesso.", usuario));
    }

    [HttpPost("Refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshPlain))
            return Unauthorized(new Response<string>(false, "Refresh token ausente."));

        var refreshHash = RefreshTokenHelper.HashToken(refreshPlain);

        var (tokenId, usuarioId) = await _refreshRepo.ValidateAndGetOwnerAsync(refreshHash);
        if (tokenId == 0)
            return Unauthorized(new Response<string>(false, "Refresh token inválido ou expirado."));

        // Re-generate tokens
        // (fetch minimal user info for JWT claims)
        UsuarioEntity usuario;
        await using (var conn = new NpgsqlConnection(_conn))
        {
            await conn.OpenAsync();
            const string sqlUser = "SELECT id, empresa_id, nome, email, rotas, ativo, data_cadastro FROM usuario WHERE id = @id LIMIT 1;";
            await using var cmd = new NpgsqlCommand(sqlUser, conn);
            cmd.Parameters.AddWithValue("id", usuarioId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Unauthorized(new Response<string>(false, "Usuário não encontrado."));

            usuario = new UsuarioEntity
            {
                id = reader.GetInt32(0),
                empresaid = reader.GetInt32(1),
                datacadastro = reader.GetDateTime(2),
                nome = reader.GetString(3),
                email = reader.GetString(4),
                ativo = reader.GetBoolean(6)
            };
        }

        if (!usuario.ativo)
            return Unauthorized(new Response<string>(false, "Usuário desativado."));

        var newJwt = _jwt.GenerateToken(usuario.id, usuario.empresaid);
        var newRefreshPlain = RefreshTokenHelper.GenerateRefreshToken();
        var newRefreshHash = RefreshTokenHelper.HashToken(newRefreshPlain);
        var newRefreshExpires = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7"));

        // Replace old token (revoke old + insert new) safely
        await _refreshRepo.ReplaceTokenAsync(usuario.id, refreshHash, newRefreshHash, newRefreshExpires);

        // Set cookies again
        var accessCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15"))
        };
        var refreshCookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Strict,
            Expires = newRefreshExpires
        };

        Response.Cookies.Append("access_token", newJwt, accessCookieOptions);
        Response.Cookies.Append("refresh_token", newRefreshPlain, refreshCookieOptions);

        return Ok(new Response<string>(true, "Tokens atualizados."));
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        // Optionally revoke refresh token from DB based on cookie
        if (Request.Cookies.TryGetValue("refresh_token", out var refreshPlain))
        {
            var refreshHash = RefreshTokenHelper.HashToken(refreshPlain);
            // Optional: find owner and revoke
            var (id, usuarioId) = await _refreshRepo.ValidateAndGetOwnerAsync(refreshHash);
            if (id != 0 && usuarioId != 0)
            {
                await _refreshRepo.RevokeAsync(usuarioId, refreshHash);
            }
        }

        // Remove cookies
        Response.Cookies.Delete("access_token");
        Response.Cookies.Delete("refresh_token");

        return Ok(new Response<string>(true, "Logout realizado."));
    }
}
