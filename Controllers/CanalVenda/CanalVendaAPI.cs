
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class CanalVendaAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public CanalVendaAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/CanalVenda")]
    public async Task<IActionResult> CreateCanalVenda([FromBody] CanalVendaDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string queryInsertFornecedor = @"insert into canal_de_venda (empresa_id,nome, tipo) values (@empresa_id,@nome, @tipo)";

            await using (var cmd = new NpgsqlCommand(queryInsertFornecedor, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome);
                cmd.Parameters.AddWithValue("@tipo", string.IsNullOrEmpty(dto.tipo) ? DBNull.Value : dto.tipo.Trim());



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
            response.Message = "Canal de venda cadastrado com sucesso.";
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
            response.Message = $"Erro ao cadastrar canal de venda: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/CanalVenda")]
    public async Task<IActionResult> UpdateCanalVenda([FromBody] CanalVendaEntity dto)
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

            if (!string.IsNullOrWhiteSpace(dto.tipo))
                fields.Add("tipo = @tipo");

            fields.Add("ativo = @ativo");


            if (!fields.Any())
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Nenhum campo válido foi informado para atualização.";
                return BadRequest(response);
            }

            var queryUpdate = $@"
            UPDATE  canal_de_venda
            SET {string.Join(", ", fields)}
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(queryUpdate, conn, transaction);

            if (!string.IsNullOrWhiteSpace(dto.nome))
                cmd.Parameters.AddWithValue("@nome", dto.nome.Trim());

            if (!string.IsNullOrWhiteSpace(dto.tipo))
                cmd.Parameters.AddWithValue("@tipo", dto.tipo.Trim());

            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Canal de venda não encontrado ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Canal de venda atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Canal de venda: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/CanalVenda")]
    public async Task<IActionResult> FetchCanalVenda([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<CanalVendaEntity>>();
        var fornecedores = new List<CanalVendaEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
SELECT
   c.id,
   c.empresa_id,
   c.nome,
   c.tipo,
   c.data_cadastro,
   c.ativo
FROM canal_de_venda c
WHERE c.empresa_id = @empresa_id

        ";


            if (!string.IsNullOrWhiteSpace(search))
            {
                query += " AND c.nome ILIKE '%' || @search || '%' ";
            }

            query += " GROUP BY c.id,c.empresa_id, c.nome, c.tipo, c.data_cadastro, c.ativo ORDER BY c.nome LIMIT 100;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(search))
            {
                cmd.Parameters.AddWithValue("@search", search.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                fornecedores.Add(new CanalVendaEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    tipo = reader.IsDBNull(reader.GetOrdinal("tipo")) ? null : reader.GetString(reader.GetOrdinal("tipo")).Trim(),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                });
            }

            response.Success = true;
            response.Data = fornecedores;
            response.Message = fornecedores.Count == 0
                ? "Nenhum canal de venda encontrado."
                : "Canais de venda encontrados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar Canais de Venda: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
