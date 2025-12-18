using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class EntregaController : ControllerBase
{
    private readonly IConfiguration _config;

    public EntregaController(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/Entrega")]
    public async Task<IActionResult> CreateEntrega([FromBody] EntregaDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string queryInsertFornecedor = @"insert into metodo_entrega (empresa_id, nome, prazo, custo_base) values
            (@empresa_id, @nome, @prazo, @custo_base)";

            await using (var cmd = new NpgsqlCommand(queryInsertFornecedor, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome);
                cmd.Parameters.AddWithValue("@prazo", string.IsNullOrEmpty(dto.prazo) ? DBNull.Value : dto.prazo.Trim());
                cmd.Parameters.AddWithValue("@custo_base", dto.custo_base);

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
            response.Message = "Entrega cadastrado com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma entrega cadastrado com este nome.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar entrega: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/Entrega")]
    public async Task<IActionResult> UpdateEntrega([FromBody] EntregaEntity dto)
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

            if (!string.IsNullOrWhiteSpace(dto.prazo))
                fields.Add("prazo = @prazo");
            if (!string.IsNullOrWhiteSpace(dto.custo_base.ToString()))
                fields.Add("custo_base = @custo_base");

            fields.Add("ativo = @ativo");


            if (!fields.Any())
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Nenhum campo válido foi informado para atualização.";
                return BadRequest(response);
            }

            var queryUpdate = $@"
            UPDATE metodo_entrega
            SET {string.Join(", ", fields)}
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(queryUpdate, conn, transaction);

            if (!string.IsNullOrWhiteSpace(dto.nome))
                cmd.Parameters.AddWithValue("@nome", dto.nome.Trim());

            if (!string.IsNullOrWhiteSpace(dto.prazo))
                cmd.Parameters.AddWithValue("@prazo", dto.prazo.Trim());
            if (!string.IsNullOrWhiteSpace(dto.custo_base.ToString()))
                cmd.Parameters.AddWithValue("@custo_base", dto.custo_base);

            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Entrega não encontrado ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Entrega atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Entrega: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Entrega")]
    public async Task<IActionResult> FetchEntrega([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<EntregaEntity>>();
        var entregas = new List<EntregaEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"select id, data_cadastro, ativo, nome, prazo, custo_base from metodo_entrega
            where empresa_id =@empresa_id ";


            if (!string.IsNullOrWhiteSpace(search))
            {
                query += " AND nome ILIKE '%' || @search || '%' ";
            }

            query += "  order by nome asc limit 100; ";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(search))
            {
                cmd.Parameters.AddWithValue("@search", search.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                entregas.Add(new EntregaEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    prazo = reader.IsDBNull(reader.GetOrdinal("prazo")) ? null : reader.GetString(reader.GetOrdinal("prazo")).Trim(),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    custo_base = reader.GetDecimal(reader.GetOrdinal("custo_base")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                });
            }

            response.Success = true;
            response.Data = entregas;
            response.Message = entregas.Count == 0
                ? "Nenhuma entrega encontrado."
                : "Entregas encontradas com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar Entregas: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
