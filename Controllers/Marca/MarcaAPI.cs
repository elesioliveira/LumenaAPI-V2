using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class MarcaController : ControllerBase
{
    private readonly IConfiguration _config;

    public MarcaController(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/Mark")]
    public async Task<IActionResult> CreateMark([FromBody] MarcaDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string queryInsertFornecedor = @"insert into marca (empresa_id,nome, website) values (@empresa_id,@nome, @website)";

            await using (var cmd = new NpgsqlCommand(queryInsertFornecedor, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome);
                cmd.Parameters.AddWithValue("@website", string.IsNullOrEmpty(dto.website) ? DBNull.Value : dto.website.Trim());



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

            response.Success = true;
            response.Message = "Marca cadastrado com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma marca cadastrado com este nome.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar fornecedor: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/Mark")]
    public async Task<IActionResult> UpdateMark([FromBody] MarkEntity dto)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();

        try
        {
            var fields = new List<string>();

            if (!string.IsNullOrWhiteSpace(dto.nome))
                fields.Add("nome = @nome");

            if (!string.IsNullOrWhiteSpace(dto.website))
                fields.Add("website = @website");

            fields.Add("ativo = @ativo");


            if (!fields.Any())
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Nenhum campo válido foi informado para atualização.";
                return BadRequest(response);
            }

            var queryUpdate = $@"
            UPDATE marca
            SET {string.Join(", ", fields)}
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(queryUpdate, conn, transaction);

            if (!string.IsNullOrWhiteSpace(dto.nome))
                cmd.Parameters.AddWithValue("@nome", dto.nome.Trim());

            if (!string.IsNullOrWhiteSpace(dto.website))
                cmd.Parameters.AddWithValue("@website", dto.website.Trim());

            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Marca não encontrado ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Marca atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Marca: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Mark")]
    public async Task<IActionResult> FetchMark([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<MarkEntity>>();
        var fornecedores = new List<MarkEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
SELECT
    m.id,
    m.empresa_id,
    m.nome,
    m.website,
    m.data_cadastro,
    m.ativo,
    COUNT(p.id) AS qtd
FROM marca m
LEFT JOIN produto p
    ON p.marca_id = m.id
WHERE m.empresa_id = @empresa_id

        ";


            if (!string.IsNullOrWhiteSpace(search))
            {
                query += " AND m.nome ILIKE '%' || @search || '%' ";
            }

            query += " GROUP BY m.id,m.empresa_id, m.nome, m.website, m.data_cadastro, m.ativo ORDER BY m.nome LIMIT 100;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(search))
            {
                cmd.Parameters.AddWithValue("@search", search.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                fornecedores.Add(new MarkEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    website = reader.IsDBNull(reader.GetOrdinal("website")) ? null : reader.GetString(reader.GetOrdinal("website")).Trim(),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    qtd = reader.GetInt32("qtd")
                });
            }

            response.Success = true;
            response.Data = fornecedores;
            response.Message = fornecedores.Count == 0
                ? "Nenhuma marca encontrado."
                : "Marcas encontradas com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar Marcas: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
