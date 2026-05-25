using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("API/V1")]
public class ProdutoFiscalAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public ProdutoFiscalAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/ProdutoFiscal")]
    public async Task<IActionResult> CreateProdutoFiscal([FromBody] ProdutoFiscalDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<object>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                INSERT INTO produto_regra_fiscal 
                (empresa_id, produto_id, cfop_id, icms_cst, icms_aliquota, icms_reducao_bc, pis_cst, pis_aliquota, cofins_cst, cofins_aliquota, ipi_cst, ipi_aliquota)
                VALUES
                (@empresa_id, @produto_id, @cfop_id, @icms_cst, @icms_aliquota, @icms_reducao_bc, @pis_cst, @pis_aliquota, @cofins_cst, @cofins_aliquota, @ipi_cst, @ipi_aliquota)
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@produto_id", dto.produto_id);
            cmd.Parameters.AddWithValue("@cfop_id", dto.cfop_id);
            cmd.Parameters.AddWithValue("@icms_cst", string.IsNullOrEmpty(dto.icms_cst) ? DBNull.Value : dto.icms_cst.Trim());
            cmd.Parameters.AddWithValue("@icms_aliquota", dto.icms_aliquota.HasValue ? dto.icms_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@icms_reducao_bc", dto.icms_reducao_bc.HasValue ? dto.icms_reducao_bc.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pis_cst", string.IsNullOrEmpty(dto.pis_cst) ? DBNull.Value : dto.pis_cst.Trim());
            cmd.Parameters.AddWithValue("@pis_aliquota", dto.pis_aliquota.HasValue ? dto.pis_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@cofins_cst", string.IsNullOrEmpty(dto.cofins_cst) ? DBNull.Value : dto.cofins_cst.Trim());
            cmd.Parameters.AddWithValue("@cofins_aliquota", dto.cofins_aliquota.HasValue ? dto.cofins_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ipi_cst", string.IsNullOrEmpty(dto.ipi_cst) ? DBNull.Value : dto.ipi_cst.Trim());
            cmd.Parameters.AddWithValue("@ipi_aliquota", dto.ipi_aliquota.HasValue ? dto.ipi_aliquota.Value : DBNull.Value);

            var id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Regra fiscal vinculada ao produto com sucesso.";
            response.Data = new { id };
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma regra fiscal com esta CFOP para este produto.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar regra fiscal: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/ProdutoFiscal")]
    public async Task<IActionResult> UpdateProdutoFiscal([FromBody] ProdutoFiscalEntity dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                UPDATE produto_regra_fiscal SET
                    cfop_id = @cfop_id,
                    icms_cst = @icms_cst,
                    icms_aliquota = @icms_aliquota,
                    icms_reducao_bc = @icms_reducao_bc,
                    pis_cst = @pis_cst,
                    pis_aliquota = @pis_aliquota,
                    cofins_cst = @cofins_cst,
                    cofins_aliquota = @cofins_aliquota,
                    ipi_cst = @ipi_cst,
                    ipi_aliquota = @ipi_aliquota
                WHERE id = @id AND empresa_id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@cfop_id", dto.cfop_id);
            cmd.Parameters.AddWithValue("@icms_cst", string.IsNullOrEmpty(dto.icms_cst) ? DBNull.Value : dto.icms_cst.Trim());
            cmd.Parameters.AddWithValue("@icms_aliquota", dto.icms_aliquota.HasValue ? dto.icms_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@icms_reducao_bc", dto.icms_reducao_bc.HasValue ? dto.icms_reducao_bc.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@pis_cst", string.IsNullOrEmpty(dto.pis_cst) ? DBNull.Value : dto.pis_cst.Trim());
            cmd.Parameters.AddWithValue("@pis_aliquota", dto.pis_aliquota.HasValue ? dto.pis_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@cofins_cst", string.IsNullOrEmpty(dto.cofins_cst) ? DBNull.Value : dto.cofins_cst.Trim());
            cmd.Parameters.AddWithValue("@cofins_aliquota", dto.cofins_aliquota.HasValue ? dto.cofins_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ipi_cst", string.IsNullOrEmpty(dto.ipi_cst) ? DBNull.Value : dto.ipi_cst.Trim());
            cmd.Parameters.AddWithValue("@ipi_aliquota", dto.ipi_aliquota.HasValue ? dto.ipi_aliquota.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Regra fiscal não encontrada ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Regra fiscal atualizada com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma regra fiscal com esta CFOP para este produto.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar regra fiscal: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/ProdutoFiscal/{produtoId}")]
    public async Task<IActionResult> FetchProdutoFiscal(int produtoId)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<ProdutoFiscalEntity>>();
        var regras = new List<ProdutoFiscalEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                SELECT 
                    prf.id, prf.produto_id, prf.cfop_id,
                    c.codigo AS cfop_codigo, c.descricao AS cfop_descricao, c.tipo_operacao,
                    prf.icms_cst, prf.icms_aliquota, prf.icms_reducao_bc,
                    prf.pis_cst, prf.pis_aliquota,
                    prf.cofins_cst, prf.cofins_aliquota,
                    prf.ipi_cst, prf.ipi_aliquota,
                    prf.data_cadastro
                FROM produto_regra_fiscal prf
                INNER JOIN cfop c ON c.id = prf.cfop_id
                WHERE prf.empresa_id = @empresa_id AND prf.produto_id = @produto_id
                ORDER BY c.tipo_operacao, c.codigo;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@produto_id", produtoId);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                regras.Add(new ProdutoFiscalEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    produto_id = reader.GetInt32(reader.GetOrdinal("produto_id")),
                    cfop_id = reader.GetInt32(reader.GetOrdinal("cfop_id")),
                    cfop_codigo = reader.IsDBNull(reader.GetOrdinal("cfop_codigo")) ? null : reader.GetString(reader.GetOrdinal("cfop_codigo")).Trim(),
                    cfop_descricao = reader.IsDBNull(reader.GetOrdinal("cfop_descricao")) ? null : reader.GetString(reader.GetOrdinal("cfop_descricao")).Trim(),
                    tipo_operacao = reader.IsDBNull(reader.GetOrdinal("tipo_operacao")) ? null : reader.GetString(reader.GetOrdinal("tipo_operacao")).Trim(),
                    icms_cst = reader.IsDBNull(reader.GetOrdinal("icms_cst")) ? null : reader.GetString(reader.GetOrdinal("icms_cst")).Trim(),
                    icms_aliquota = reader.IsDBNull(reader.GetOrdinal("icms_aliquota")) ? null : reader.GetDecimal(reader.GetOrdinal("icms_aliquota")),
                    icms_reducao_bc = reader.IsDBNull(reader.GetOrdinal("icms_reducao_bc")) ? null : reader.GetDecimal(reader.GetOrdinal("icms_reducao_bc")),
                    pis_cst = reader.IsDBNull(reader.GetOrdinal("pis_cst")) ? null : reader.GetString(reader.GetOrdinal("pis_cst")).Trim(),
                    pis_aliquota = reader.IsDBNull(reader.GetOrdinal("pis_aliquota")) ? null : reader.GetDecimal(reader.GetOrdinal("pis_aliquota")),
                    cofins_cst = reader.IsDBNull(reader.GetOrdinal("cofins_cst")) ? null : reader.GetString(reader.GetOrdinal("cofins_cst")).Trim(),
                    cofins_aliquota = reader.IsDBNull(reader.GetOrdinal("cofins_aliquota")) ? null : reader.GetDecimal(reader.GetOrdinal("cofins_aliquota")),
                    ipi_cst = reader.IsDBNull(reader.GetOrdinal("ipi_cst")) ? null : reader.GetString(reader.GetOrdinal("ipi_cst")).Trim(),
                    ipi_aliquota = reader.IsDBNull(reader.GetOrdinal("ipi_aliquota")) ? null : reader.GetDecimal(reader.GetOrdinal("ipi_aliquota")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro"))
                });
            }

            response.Success = true;
            response.Data = regras;
            response.Message = regras.Count == 0
                ? "Nenhuma regra fiscal encontrada para este produto."
                : "Regras fiscais encontradas com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar regras fiscais: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpDelete("Delete/ProdutoFiscal/{id}")]
    public async Task<IActionResult> DeleteProdutoFiscal(int id)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                DELETE FROM produto_regra_fiscal
                WHERE id = @id AND empresa_id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Regra fiscal não encontrada ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Regra fiscal removida com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao remover regra fiscal: {ex.Message}";
            return StatusCode(500, response);
        }
    }
}
