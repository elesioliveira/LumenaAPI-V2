using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;

[ApiController]
[Route("API/V1")]
public class UserAPI : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly CacheHelper _cacheHelper;

    public UserAPI(IConfiguration config, CacheHelper cacheHelper)
    {
        _config = config;
        _cacheHelper = cacheHelper;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/User")]
    public async Task<IActionResult> CreateUser([FromBody] UserDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            // Gerar HASH + SALT da senha
            var (senhaHash, salt) = PasswordHasher.HashPassword(dto.senha);
            const string sqlInsertNewUser = @"insert into usuario (empresa_id, nome, email, senha_hash, salt, ativo, perfil) values 
            (@empresa_id, @nome, @email, @senha_hash, @salt, @ativo, @perfil)";

            await using (var cmd = new NpgsqlCommand(sqlInsertNewUser, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome);
                cmd.Parameters.AddWithValue("@email", dto.email);
                cmd.Parameters.AddWithValue("@senha_hash", senhaHash);
                cmd.Parameters.AddWithValue("@salt", salt);
                cmd.Parameters.AddWithValue("@ativo", dto.ativo);
                cmd.Parameters.AddWithValue("@perfil", dto.perfil);
                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    response.Success = false;
                    response.Message = "Nenhum registro foi inserido.";
                    return BadRequest(response);
                }
            }

            await transaction.CommitAsync();
            try
            {
                _cacheHelper.Remove(CacheKeys.FormUsers(empresaId));
            }
            catch (Exception ex)
            {
                // cache nunca deve derrubar a API
                // log opcional
            }
            response.Success = true;
            response.Message = "Usuário cadastrado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar Usuário: {ex.Message}";
            return StatusCode(500, response);
        }
    }

//     [Authorize]
//     [HttpPut("Put/Update/Mark")]
//     public async Task<IActionResult> UpdateMark([FromBody] MarkEntity dto)
//     {
//         await using var conn = NovaConexao(); // NpgsqlConnection
//         await conn.OpenAsync();

//         var response = new Response<string>();
//         await using var transaction = await conn.BeginTransactionAsync();

//         var empresaId = User.GetEmpresaId();

//         try
//         {
//             _cacheHelper.RemoveByEmpresa(User.GetEmpresaId());
//             var fields = new List<string>();

//             if (!string.IsNullOrWhiteSpace(dto.nome))
//                 fields.Add("nome = @nome");

//             if (!string.IsNullOrWhiteSpace(dto.website))
//                 fields.Add("website = @website");

//             fields.Add("ativo = @ativo");


//             if (!fields.Any())
//             {
//                 await transaction.RollbackAsync();
//                 response.Success = false;
//                 response.Message = "Nenhum campo válido foi informado para atualização.";
//                 return BadRequest(response);
//             }

//             var queryUpdate = $@"
//             UPDATE marca
//             SET {string.Join(", ", fields)}
//             WHERE id = @id
//               AND empresa_id = @empresa_id;
//         ";

//             await using var cmd = new NpgsqlCommand(queryUpdate, conn, transaction);

//             if (!string.IsNullOrWhiteSpace(dto.nome))
//                 cmd.Parameters.AddWithValue("@nome", dto.nome.Trim());

//             if (!string.IsNullOrWhiteSpace(dto.website))
//                 cmd.Parameters.AddWithValue("@website", dto.website.Trim());

//             cmd.Parameters.AddWithValue("@ativo", dto.ativo);

//             cmd.Parameters.AddWithValue("@id", dto.id);
//             cmd.Parameters.AddWithValue("@empresa_id", empresaId);

//             var rowsAffected = await cmd.ExecuteNonQueryAsync();

//             if (rowsAffected == 0)
//             {
//                 await transaction.RollbackAsync();
//                 response.Success = false;
//                 response.Message = "Marca não encontrado ou não pertence à empresa.";
//                 return NotFound(response);
//             }

//             await transaction.CommitAsync();

//             response.Success = true;
//             response.Message = "Marca atualizado com sucesso.";
//             return Ok(response);
//         }
//         catch (Exception ex)
//         {
//             await transaction.RollbackAsync();
//             response.Success = false;
//             response.Message = $"Erro ao atualizar Marca: {ex.Message}";
//             return StatusCode(500, response);
//         }
//     }

[Authorize]
[HttpGet("Get/Users")]
public async Task<IActionResult> FetchUser(
    [FromQuery] string? search,
    [FromQuery] string? perfil,
    [FromQuery] bool? ativo
)
{
    var response = new Response<List<UserEntityV2>>();
    var empresaId = User.GetEmpresaId();

    // cache só será usado se NÃO houver filtros
    var canUseCache =
        string.IsNullOrWhiteSpace(search)
        && string.IsNullOrWhiteSpace(perfil)
        && ativo is null;

    var cacheKey = CacheKeys.FormUsers(empresaId);

    // ---------- CACHE GET ----------
    if (canUseCache &&
        _cacheHelper.TryGet(cacheKey, out List<UserEntityV2>? cachedUsers))
    {
        response.Success = true;
        response.Data = cachedUsers!;
        response.Message = cachedUsers!.Count == 0
            ? "Nenhum usuário encontrado."
            : "Usuários encontrados com sucesso.";

        return Ok(response);
    }

    // ---------- DB ----------
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    var users = new List<UserEntityV2>();

    try
    {
        var query = @"
            SELECT 
                id,
                nome,
                email,
                perfil,
                CASE 
                    WHEN ativo = true THEN 'Ativo'
                    ELSE 'Inativo'
                END AS status,
                ultimo_acesso
            FROM usuario
            WHERE empresa_id = @empresa_id
              AND (@search IS NULL
                   OR nome ILIKE '%' || @search || '%'
                   OR email ILIKE '%' || @search || '%')
              AND (@perfil IS NULL
                   OR perfil ILIKE '%' || @perfil || '%')
              AND (@status IS NULL
                   OR ativo = @status)
            ORDER BY nome ASC
            LIMIT 100;
        ";

        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer).Value = empresaId;
        cmd.Parameters.Add("@search", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)search ?? DBNull.Value;
        cmd.Parameters.Add("@perfil", NpgsqlTypes.NpgsqlDbType.Text).Value = (object?)perfil ?? DBNull.Value;
        cmd.Parameters.Add("@status", NpgsqlTypes.NpgsqlDbType.Boolean).Value = (object?)ativo ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            users.Add(new UserEntityV2
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                nome =reader.IsDBNull(reader.GetOrdinal("nome"))? null: reader.GetString(reader.GetOrdinal("nome")).Trim(),
                email =reader.IsDBNull(reader.GetOrdinal("email"))? null: reader.GetString(reader.GetOrdinal("email")).Trim(),
                perfil =reader.IsDBNull(reader.GetOrdinal("perfil"))? null: reader.GetString(reader.GetOrdinal("perfil")).Trim(),
                status =reader.IsDBNull(reader.GetOrdinal("status"))? null: reader.GetString(reader.GetOrdinal("status")),
                ultimo_acesso = reader.IsDBNull(reader.GetOrdinal("ultimo_acesso"))
                    ? DateTime.MinValue
                    : reader.GetDateTime(reader.GetOrdinal("ultimo_acesso"))
            });
        }

        // ---------- CACHE SET (somente sem filtros) ----------
        if (canUseCache)
        {
            _cacheHelper.Set(
                cacheKey,
                users,
                TimeSpan.FromMinutes(5)
            );
        }

        response.Success = true;
        response.Data = users;
        response.Message = users.Count == 0
            ? "Nenhum usuário encontrado."
            : "Usuários encontrados com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar Usuários: {ex.Message}";
        return StatusCode(500, response);
    }
}

}
