using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("API/V1")]
public class CfopAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public CfopAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/Cfop")]
    public async Task<IActionResult> CreateCfop([FromBody] CfopDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<object>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                INSERT INTO cfop (empresa_id, codigo, descricao, natureza_operacao, tipo_operacao, ativo)
                VALUES (@empresa_id, @codigo, @descricao, @natureza_operacao, @tipo_operacao, @ativo)
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@codigo", string.IsNullOrEmpty(dto.codigo) ? DBNull.Value : dto.codigo.Trim());
            cmd.Parameters.AddWithValue("@descricao", string.IsNullOrEmpty(dto.descricao) ? DBNull.Value : dto.descricao.Trim());
            cmd.Parameters.AddWithValue("@natureza_operacao", string.IsNullOrEmpty(dto.natureza_operacao) ? DBNull.Value : dto.natureza_operacao.Trim());
            cmd.Parameters.AddWithValue("@tipo_operacao", string.IsNullOrEmpty(dto.tipo_operacao) ? DBNull.Value : dto.tipo_operacao.Trim());
            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "CFOP cadastrada com sucesso.";
            response.Data = new { id };
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma CFOP com este código cadastrada para esta empresa.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar CFOP: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/Cfop")]
    public async Task<IActionResult> UpdateCfop([FromBody] CfopEntity dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                UPDATE cfop SET
                    codigo = @codigo,
                    descricao = @descricao,
                    natureza_operacao = @natureza_operacao,
                    tipo_operacao = @tipo_operacao,
                    ativo = @ativo
                WHERE id = @id AND empresa_id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@codigo", string.IsNullOrEmpty(dto.codigo) ? DBNull.Value : dto.codigo.Trim());
            cmd.Parameters.AddWithValue("@descricao", string.IsNullOrEmpty(dto.descricao) ? DBNull.Value : dto.descricao.Trim());
            cmd.Parameters.AddWithValue("@natureza_operacao", string.IsNullOrEmpty(dto.natureza_operacao) ? DBNull.Value : dto.natureza_operacao.Trim());
            cmd.Parameters.AddWithValue("@tipo_operacao", string.IsNullOrEmpty(dto.tipo_operacao) ? DBNull.Value : dto.tipo_operacao.Trim());
            cmd.Parameters.AddWithValue("@ativo", dto.ativo);
            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "CFOP não encontrada ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "CFOP atualizada com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma CFOP com este código cadastrada para esta empresa.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar CFOP: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Cfop")]
    public async Task<IActionResult> FetchCfop([FromQuery] string? tipo_operacao)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<CfopEntity>>();
        var cfops = new List<CfopEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
                SELECT id, codigo, descricao, natureza_operacao, tipo_operacao, ativo, data_cadastro
                FROM cfop
                WHERE empresa_id = @empresa_id";

            if (!string.IsNullOrWhiteSpace(tipo_operacao))
            {
                query += " AND tipo_operacao = @tipo_operacao";
            }

            query += " ORDER BY codigo;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(tipo_operacao))
            {
                cmd.Parameters.AddWithValue("@tipo_operacao", tipo_operacao.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                cfops.Add(new CfopEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    codigo = reader.IsDBNull(reader.GetOrdinal("codigo")) ? null : reader.GetString(reader.GetOrdinal("codigo")).Trim(),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString(reader.GetOrdinal("descricao")).Trim(),
                    natureza_operacao = reader.IsDBNull(reader.GetOrdinal("natureza_operacao")) ? null : reader.GetString(reader.GetOrdinal("natureza_operacao")).Trim(),
                    tipo_operacao = reader.IsDBNull(reader.GetOrdinal("tipo_operacao")) ? null : reader.GetString(reader.GetOrdinal("tipo_operacao")).Trim(),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro"))
                });
            }

            response.Success = true;
            response.Data = cfops;
            response.Message = cfops.Count == 0
                ? "Nenhuma CFOP encontrada."
                : "CFOPs encontradas com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar CFOPs: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpDelete("Delete/Cfop/{id}")]
    public async Task<IActionResult> DeleteCfop(int id)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                UPDATE cfop SET ativo = FALSE
                WHERE id = @id AND empresa_id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "CFOP não encontrada ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "CFOP desativada com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao desativar CFOP: {ex.Message}";
            return StatusCode(500, response);
        }
    }
}
