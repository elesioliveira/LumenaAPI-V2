using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class GrupoClientAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public GrupoClientAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));


    [Authorize]
    [HttpGet("Get/GrupoCliente")]
    public async Task<IActionResult> FetchGrupoCliente([FromQuery] string? search, [FromQuery] bool? ativo)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<GrupoClienteEntity>>();
        var grupos = new List<GrupoClienteEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            string sql = @"
           SELECT 
            g.id,
            g.empresa_id,
            g.data_cadastro,
            g.ativo,
            g.nome,
            g.descricao,
            g.desconto,
            COUNT(c.id) AS clients
        FROM grupo_cliente g
        LEFT JOIN cliente c 
            ON c.grupo_id = g.id
        AND c.empresa_id = g.empresa_id
        WHERE g.empresa_id = @empresa_id
        ";

            if (ativo != null)
            {
                sql += @" AND g.ativo =@ativo";
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @" AND g.nome ILIKE '%' || @search || '%' ";
            }

            sql += @" GROUP BY 
    g.id,
    g.empresa_id,
    g.data_cadastro,
    g.ativo,
    g.nome,
    g.descricao,
    g.desconto ORDER BY g.nome ASC LIMIT 100; ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search);
            if (ativo != null)
            {
                cmd.Parameters.AddWithValue("@ativo", ativo);
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                grupos.Add(new GrupoClienteEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    nome = reader.GetString(reader.GetOrdinal("nome")),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader["descricao"] as string,
                    desconto = reader.IsDBNull(reader.GetOrdinal("descricao")) ? 0 : reader.GetDecimal(reader.GetOrdinal("desconto")),
                    clients = reader.IsDBNull(reader.GetOrdinal("clients")) ? 0 : reader.GetInt32(reader.GetOrdinal("clients"))
                });
            }

            response.Success = true;
            response.Data = grupos;
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar grupos de cliente: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/GrupoCliente/{id}")]
    public async Task<IActionResult> FetchGrupoClienteById(int id)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<GrupoClienteEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string sql = @"
            SELECT 
                id,
                empresa_id,
                data_cadastro,
                ativo,
                nome,
                descricao,
                desconto
            FROM grupo_cliente
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound();

            response.Success = true;
            response.Data = new GrupoClienteEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                nome = reader.GetString(reader.GetOrdinal("nome")),
                descricao = reader["descricao"] as string,
                desconto = reader.GetDecimal(reader.GetOrdinal("desconto"))
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar grupo de cliente: {ex.Message}";
            return StatusCode(500, response);
        }
    }
    [Authorize]
    [HttpPost("Post/Create/GrupoCliente")]
    public async Task<IActionResult> CreateGrupoCliente([FromBody] GrupoClienteDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string sql = @"
            INSERT INTO grupo_cliente
            (empresa_id, nome, descricao, desconto, ativo)
            VALUES
            (@empresa_id, @nome, @descricao, @desconto, @ativo);
        ";

            await using var cmd = new NpgsqlCommand(sql, conn, transaction);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@nome", dto.nome);
            cmd.Parameters.AddWithValue("@descricao", (object?)dto.descricao ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desconto", (object?)dto.desconto ?? 0);
            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Grupo de cliente cadastrado com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe um grupo de cliente com este nome.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar grupo de cliente: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/GrupoCliente/{id}")]
    public async Task<IActionResult> UpdateGrupoCliente(int id, [FromBody] GrupoClienteDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string sql = @"
            UPDATE grupo_cliente SET
                nome = @nome,
                descricao = @descricao,
                desconto = @desconto,
                ativo = @ativo
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@nome", dto.nome);
            cmd.Parameters.AddWithValue("@descricao", (object?)dto.descricao ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@desconto", dto.desconto);
            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows == 0)
                return NotFound();

            response.Success = true;
            response.Message = "Grupo de cliente atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao atualizar grupo de cliente: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
