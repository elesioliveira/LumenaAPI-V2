using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("API/V1")]
public class WalletCategoryAPI : ControllerBase
{
    private const string CacheKey = "PRODUCT_FORM_";
    private readonly IConfiguration _config;
    private readonly IGtinProdutoService _service;
    private readonly IGtinTokenService _tokenService;
    private readonly IMemoryCache _cache;
    public WalletCategoryAPI(IConfiguration config, IMemoryCache cache)
    {
        _config = config;
        _cache = cache;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));


[Authorize]
[HttpPut("Put/Category/Wallet/{id}")]
public async Task<IActionResult> UpdateCategory([FromBody] CategoryWalletDTO dto,int id)
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    await using var transaction = await conn.BeginTransactionAsync();

    var empresaId = User.GetEmpresaId();
    var response = new Response<string>();

    try
    {
        const string sql = @"
            UPDATE categoria_wallet
            SET
                nome = @nome,
                tipo = @tipo,
                descricao = @descricao,
                cor = @cor,
                ativo = @ativo
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

        await using var cmd = new NpgsqlCommand(sql, conn, transaction);

        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);
        cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@descricao", (object?)dto.descricao ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cor", (object?)dto.cor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ativo", (object?)dto.ativo ?? true);

        var rowsAffected = await cmd.ExecuteNonQueryAsync();

        if (rowsAffected == 0)
            throw new Exception("Categoria não encontrada ou não pertence à empresa.");

        await transaction.CommitAsync();

        response.Success = true;
        response.Message = "Categoria atualizada com sucesso.";
        return Ok(response);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        response.Success = false;
        response.Message = ex.Message;
        return StatusCode(500, response);
    }
}

    [Authorize]
    [HttpPost("Post/Category/Wallet")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryWalletDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();
        var usuarioId = User.GetUserId();

        var response = new Response<string>();

        try
        {
            // Inserir cabeçalho da movimentação
            const string sqlInsert = @"
            insert into categoria_wallet (empresa_id, nome, tipo, descricao, cor) values (@empresa_id, @nome, @tipo, @descricao, @cor)
            RETURNING id; ";

            int categoryId;

            await using (var cmd = new NpgsqlCommand(sqlInsert, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descricao", (object?)dto.descricao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cor", (object?)dto.cor ?? DBNull.Value);
                categoryId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Categoria criada com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = ex.Message;
            return StatusCode(500, response);
        }
    }



    [Authorize]
    [HttpGet("Get/Category/Wallet")]
    public async Task<IActionResult> FetchStockEntrada([FromQuery] string? nome, [FromQuery] string? tipo)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<CategoryWalletEntity>>();
        var categorias = new List<CategoryWalletEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            select id, data_cadastro, nome, tipo, descricao, cor, ativo from categoria_wallet
            where empresa_id = @empresa_id
        ";
            if (!string.IsNullOrEmpty(nome))
            {
                query += @" AND  nome ILIKE '%' || @nome || '%'";
            }
            if (!string.IsNullOrEmpty(tipo))
            {
                query += @" AND tipo = @tipo";
            }

            query += @" order by nome asc;";


            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            if (!string.IsNullOrEmpty(nome))
            {
                cmd.Parameters.AddWithValue("@nome",nome);
            }
            if (!string.IsNullOrEmpty(tipo))
            {
                cmd.Parameters.AddWithValue("@tipo",tipo);
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                categorias.Add(new CategoryWalletEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    data_cadastro=  reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    nome = reader.GetString(reader.GetOrdinal("nome")),
                    tipo = reader.GetString(reader.GetOrdinal("tipo")),
                    descricao =reader.IsDBNull(reader.GetOrdinal("descricao"))? null: reader.GetString(reader.GetOrdinal("descricao")),
                    cor =reader.IsDBNull(reader.GetOrdinal("cor"))? null: reader.GetString(reader.GetOrdinal("cor")),
                    ativo= reader.GetBoolean(reader.GetOrdinal("ativo")),
                });
            }

            response.Success = true;
            response.Data = categorias;
            response.Message = categorias.Count == 0
                ? "Nenhuma categoria financeira encontrada."
                : "Categorias financeira encontradas com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar movimentações de entrada: {ex.Message}";
            return StatusCode(500, response);
        }
    }

[Authorize]
[HttpGet("Get/Category/Active")]
public async Task<IActionResult> FetchCategoriesActive()
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    var response = new Response<CategoryActiveCountDTO>();
    var empresaId = User.GetEmpresaId();

    try
    {
        const string query = @"
            SELECT
                COUNT(*) FILTER (WHERE tipo = 'Receita' AND ativo = true) AS total_receita,
                COUNT(*) FILTER (WHERE tipo = 'Despesa' AND ativo = true) AS total_despesa
            FROM categoria_wallet
            WHERE empresa_id = @empresa_id;
        ";

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            response.Data = new CategoryActiveCountDTO
            {
                TotalReceita = reader.GetInt32(reader.GetOrdinal("total_receita")),
                TotalDespesa = reader.GetInt32(reader.GetOrdinal("total_despesa"))
            };
        }

        response.Success = true;
        response.Message = "Categorias ativas encontradas com sucesso.";
        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar categorias ativas: {ex.Message}";
        return StatusCode(500, response);
    }
}




}


