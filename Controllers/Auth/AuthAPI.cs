using Microsoft.AspNetCore.Mvc;
using Npgsql;

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Route("API/V1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly string _conn;
    private readonly JwtService _jwt;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly IConfiguration _config;
    private readonly CacheHelper _cacheHelper;

    public AuthController(IConfiguration config, JwtService jwt, IRefreshTokenRepository refreshRepo,CacheHelper cacheHelper)
    {
        _config = config;
        _conn = config.GetConnectionString("DefaultConnection")!;
        _jwt = jwt;
        _refreshRepo = refreshRepo;
        _cacheHelper = cacheHelper;
    }

  [HttpPost("Login")]
public async Task<IActionResult> Login([FromBody] LoginDTO dto)
{
    await using var conn = new NpgsqlConnection(_conn);
    await conn.OpenAsync();

    var response = new Response<UsuarioEntity>();

    var sql = @"
    SELECT
        u.id,
        u.empresa_id,
        u.data_cadastro,
        u.nome,
        u.email,
        u.senha_hash,
        u.salt,
        u.ativo,
        COALESCE(
            (
                SELECT json_agg(r.rota)
                FROM rota_usuario r
                WHERE r.id_usuario = u.id
            ),
            '[]'::json
        ) AS rotas
    FROM usuario u
    WHERE u.email = @email
    LIMIT 1;
        ";

    UsuarioEntity usuario;
    byte[] senhaHash;
    byte[] salt;

    await using (var cmd = new NpgsqlCommand(sql, conn))
    {
        cmd.Parameters.AddWithValue("@email", dto.Email.Trim());

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            response.Success = false;
            response.Message = "Usuário ou senha incorretos.";
            return Unauthorized(response);
        }
        var rotasJson = reader.GetString(reader.GetOrdinal("rotas"));
        usuario = new UsuarioEntity
        {
            id = reader.GetInt32(reader.GetOrdinal("id")),
            empresaid = reader.GetInt32(reader.GetOrdinal("empresa_id")),
            datacadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
            nome = reader.GetString(reader.GetOrdinal("nome")),
            email = reader.GetString(reader.GetOrdinal("email")),
            ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
            rotas = JsonSerializer.Deserialize<List<int>>(rotasJson) ?? new()
        };

        senhaHash = (byte[])reader["senha_hash"];
        salt = (byte[])reader["salt"];
    }

    if (!usuario.ativo)
    {
        response.Success = false;
        response.Message = "Usuário inexistente/desativado.";
        return Unauthorized(response);
    }

    if (!PasswordHasher.VerifyPassword(dto.Senha, senhaHash, salt))
    {
        response.Success = false;
        response.Message = "Usuário ou senha incorretos.";
        return Unauthorized(response);
    }

    // 🔐 Geração de tokens
    var jwtToken = _jwt.GenerateToken(usuario.id, usuario.empresaid);

    var refreshTokenPlain = RefreshTokenHelper.GenerateRefreshToken();
    var refreshHash = RefreshTokenHelper.HashToken(refreshTokenPlain);
    var refreshExpires = DateTime.UtcNow.AddDays(
        int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7")
    );

    await _refreshRepo.SaveAsync(usuario.id, refreshHash, refreshExpires);

    // 🍪 Cookies
    Response.Cookies.Append(
        "access_token",
        jwtToken,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15")
            )
        }
    );

    Response.Cookies.Append(
        "refresh_token",
        refreshTokenPlain,
        new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = refreshExpires
        }
    );

    // 🕒 UPDATE ultimo_acesso
    const string updateUltimoAcessoSql =
        @"UPDATE usuario SET ultimo_acesso = NOW() WHERE id = @id;";

    await using (var updateCmd = new NpgsqlCommand(updateUltimoAcessoSql, conn))
    {
        updateCmd.Parameters.AddWithValue("@id", usuario.id);
        await updateCmd.ExecuteNonQueryAsync();
    }

    // 🧹 INVALIDA CACHE (não pode quebrar o login)
    try
    {
        _cacheHelper.Remove(CacheKeys.FormUsers(usuario.empresaid));
    }
    catch
    {
        // cache nunca deve derrubar login
        // log opcional
    }

    response.Success = true;
    response.Message = "Login realizado com sucesso.";
    response.Data = usuario;

    return Ok(response);
}
    
    
    [HttpPost("Refresh")]
    public async Task<IActionResult> Refresh()
    {
        var response = new Response<string>();

        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshPlain))
        {
            response.Success = false;
            response.Message = "Refresh token ausente.";
            return Unauthorized(response);
        }

        var refreshHash = RefreshTokenHelper.HashToken(refreshPlain);

        var (tokenId, usuarioId) =
            await _refreshRepo.ValidateAndGetOwnerAsync(refreshHash);

        if (tokenId == 0 || usuarioId == 0)
        {
            response.Success = false;
            response.Message = "Refresh token inválido ou expirado.";
            return Unauthorized(response);
        }

        UsuarioEntity usuario;
        await using (var conn = new NpgsqlConnection(_conn))
        {
            await conn.OpenAsync();
            const string sql = @"
            SELECT id, empresa_id, nome, email, ativo, data_cadastro
            FROM usuario
            WHERE id = @id
            LIMIT 1;
        ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", usuarioId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Usuário não encontrado.";
                return Unauthorized(response);
            }

            usuario = new UsuarioEntity
            {
                id = reader.GetInt32(0),
                empresaid = reader.GetInt32(1),
                nome = reader.GetString(2),
                email = reader.GetString(3),
                ativo = reader.GetBoolean(4),
                datacadastro = reader.GetDateTime(5)
            };
        }

        if (!usuario.ativo)
        {
            response.Success = false;
            response.Message = "Usuário desativado.";
            return Unauthorized(response);
        }

        // Tokens
        var newJwt = _jwt.GenerateToken(usuario.id, usuario.empresaid);
        var newRefreshPlain = RefreshTokenHelper.GenerateRefreshToken();
        var newRefreshHash = RefreshTokenHelper.HashToken(newRefreshPlain);
        var newRefreshExpires = DateTime.UtcNow.AddDays(
            int.Parse(_config["Jwt:RefreshTokenDays"] ?? "7")
        );

        await _refreshRepo.ReplaceTokenAsync(
            usuario.id,
            refreshHash,
            newRefreshHash,
            newRefreshExpires
        );

        var accessOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true, // PROD
            SameSite = SameSiteMode.None,
            Expires = DateTime.UtcNow.AddMinutes(
                int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "15")
            )
        };

        var refreshOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Expires = newRefreshExpires
        };

        Response.Cookies.Append("access_token", newJwt, accessOptions);
        Response.Cookies.Append("refresh_token", newRefreshPlain, refreshOptions);

        response.Success = true;
        response.Message = "Tokens atualizados.";
        return Ok(response);
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        var response = new Response<string>();
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
        response.Success = true;
        response.Message = "Logout realizado.";

        return Ok(response);
    }


    [HttpPost("Post/ForgotPassword")]
    public async Task<IActionResult> ForgotPassword([FromBody] string email, [FromServices] IEmailService emailService)
    {
        var response = new Response<string>();

        if (string.IsNullOrWhiteSpace(email))
        {
            response.Success = false;
            response.Message = "E-mail inválido.";
            return BadRequest(response);
        }

        await using var conn = new NpgsqlConnection(_conn);
        await conn.OpenAsync();

        try
        {
            await using var transaction = await conn.BeginTransactionAsync();

            //  Buscar usuário pelo email
            const string queryUser = "SELECT id FROM usuario WHERE email = @email;";
            int usuarioId = 0;

            await using (var cmd = new NpgsqlCommand(queryUser, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@email", email.Trim());
                var result = await cmd.ExecuteScalarAsync();

                if (result == null)
                {
                    response.Success = false;
                    response.Message = "E-mail não encontrado.";
                    return NotFound(response);
                }

                usuarioId = Convert.ToInt32(result);
            }

            //  Gerar token de recuperação seguro
            string token = TokenGenerator.GenerateRecoveryToken();
            DateTime expiraEm = DateTime.UtcNow.AddMinutes(30);

            //  Salvar token no banco
            const string queryInsert = @"insert into forgot_password_keys (chave_gerada, email) values (@chave_gerada, @email); ";

            await using (var cmd = new NpgsqlCommand(queryInsert, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@chave_gerada", token);
                cmd.Parameters.AddWithValue("@email", email);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            //  Criar link público
            string linkRecuperacao = $"http://localhost:3000/reset-password/{token}";

            //  Montar corpo do email
            string corpoHtml = $@"
            <h2>Recuperação de Senha</h2>
            <p>Você solicitou a recuperação de sua senha.</p>
            <p>Clique no link abaixo para criar uma nova senha:</p>
            <p><a href='{linkRecuperacao}'>{linkRecuperacao}</a></p>
            <p>Este link expira em 30 minutos.</p>
        ";

            //  Enviar email
            await emailService.EnviarEmailAsync(email, "Recuperação de Senha", corpoHtml);

            response.Success = true;
            response.Message = "Link de recuperação enviado para o e-mail informado.";
            response.Data = null;

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao processar solicitação: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
