using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("API/V1")]
public class ProductAPI : ControllerBase
{
    private const string CacheKey = "PRODUCT_FORM_";
    private readonly IConfiguration _config;
    private readonly IGtinProdutoService _service;
    private readonly IGtinTokenService _tokenService;
    private readonly IMemoryCache _cache;
    public ProductAPI(IConfiguration config, IGtinProdutoService service, IGtinTokenService tokenService, IMemoryCache cache)
    {
        _config = config;
        _service = service;
        _tokenService = tokenService;
        _cache = cache;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));


[Authorize]
[HttpPost("Post/Create/Product")]
public async Task<IActionResult> CreateProduct([FromBody] ProductDTO dto)
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    var response = new Response<object>();
    await using var transaction = await conn.BeginTransactionAsync();
    var empresaId = User.GetEmpresaId();

    try
    {
        const string queryInsert = @"
            INSERT INTO produto 
            (empresa_id, nome, descricao, ativo, un, codigo_barras, marca_id, fornecedor_id, categoria_id, preco_custo, preco_venda, estoque_minimo) 
            VALUES
            (@empresa_id, @nome, @descricao, @ativo, @un, @codigo_barras, @marca_id, @fornecedor_id, @categoria_id, @preco_custo, @preco_venda, @estoque_minimo)
            RETURNING id;";

        int produtoId;

        await using (var cmd = new NpgsqlCommand(queryInsert, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@nome", string.IsNullOrEmpty(dto.nome) ? DBNull.Value : dto.nome.Trim());
            cmd.Parameters.AddWithValue("@descricao", string.IsNullOrEmpty(dto.descricao) ? DBNull.Value : dto.descricao.Trim());
            cmd.Parameters.AddWithValue("@ativo", dto.ativo);
            cmd.Parameters.AddWithValue("@un", string.IsNullOrEmpty(dto.un) ? DBNull.Value : dto.un.Trim());
            cmd.Parameters.AddWithValue("@codigo_barras", string.IsNullOrEmpty(dto.eanCode) ? DBNull.Value : dto.eanCode.Trim());
            cmd.Parameters.AddWithValue("@marca_id", dto.marca_id.HasValue && dto.marca_id.Value > 0 ? dto.marca_id.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@fornecedor_id", dto.fornecedor_id.HasValue && dto.fornecedor_id.Value > 0 ? dto.fornecedor_id.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@categoria_id", dto.categoria_id.HasValue && dto.categoria_id.Value > 0 ? dto.categoria_id.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@preco_custo", dto.preco_custo ?? 0);
            cmd.Parameters.AddWithValue("@preco_venda", dto.preco_venda ?? 0);
            cmd.Parameters.AddWithValue("@estoque_minimo", dto.estoque_minimo ?? 0);

            produtoId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        await transaction.CommitAsync();

        response.Success = true;
        response.Message = "Produto cadastrado com sucesso.";
        response.Data = new { id = produtoId };
        return Ok(response);
    }
    catch (PostgresException ex) when (ex.SqlState == "23505")
    {
        await transaction.RollbackAsync();
        response.Success = false;
        response.Message = "Já existe um produto cadastrado com este nome.";
        return BadRequest(response);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        response.Success = false;
        response.Message = $"Erro ao cadastrar produto: {ex.Message}";
        return StatusCode(500, response);
    }
}
    [Authorize]
    [HttpPut("Put/Update/Product")]
    public async Task<IActionResult> UpdateMark([FromBody] ProductEntity dto)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();

        try
        {
            string queryUpdateProduct = @"update produto set  nome=@nome,descricao=@descricao,ativo=@ativo,un=@un,codigo_barras=@eanCode,marca_id=@marca_id,fornecedor_id=@fornecedor_id,categoria_id=@categoria_id,preco_custo=@preco_custo,preco_venda=@preco_venda,estoque_minimo=@estoque_minimo
            where id =@id and empresa_id=@empresa_id";

            await using var cmd = new NpgsqlCommand(queryUpdateProduct, conn, transaction);
            cmd.Parameters.AddWithValue("@nome", string.IsNullOrEmpty(dto.nome) ? DBNull.Value : dto.nome.Trim());
            cmd.Parameters.AddWithValue("@descricao", string.IsNullOrEmpty(dto.descricao) ? DBNull.Value : dto.descricao.Trim());
            cmd.Parameters.AddWithValue("@ativo", dto.ativo);
            cmd.Parameters.AddWithValue("@un", string.IsNullOrEmpty(dto.un) ? DBNull.Value : dto.un.Trim());
            cmd.Parameters.AddWithValue("@eanCode", string.IsNullOrEmpty(dto.eanCode) ? DBNull.Value : dto.eanCode.Trim());
            cmd.Parameters.AddWithValue("@marca_id", dto.marca_id);
            cmd.Parameters.AddWithValue("@fornecedor_id", dto.fornecedor_id);
            cmd.Parameters.AddWithValue("@categoria_id", dto.categoria_id);
            cmd.Parameters.AddWithValue("@preco_custo", dto.preco_custo ?? 0);
            cmd.Parameters.AddWithValue("@preco_venda", dto.preco_venda ?? 0);
            cmd.Parameters.AddWithValue("@estoque_minimo", dto.estoque_minimo ?? 0);
            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Produto não encontrado ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Produto atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Produto: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Product")]
    public async Task<IActionResult> FetchMark([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<ProductEntity>>();
        var products = new List<ProductEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            select p.id, p.data_cadastro, p.nome,p.ativo,p.un,p.descricao,p.codigo_barras as eanCode,m.nome as marca,p.marca_id, p.fornecedor_id, f.nome as fornecedor, 
            c.nome as categoria,p.categoria_id,
            p.preco_custo, p.estoque, p.preco_venda, p.estoque_minimo
            from produto p
            left join marca m on m.id = p.marca_id
            left join categoria c on c.id = p.categoria_id
            left join fornecedores f on f.id = p.fornecedor_id
            where p.empresa_id = @empresa_id  ";


            if (!string.IsNullOrWhiteSpace(search))
            {
                query += " AND p.nome ILIKE '%' || @search || '%' ";
            }

            query += " ORDER BY p.nome LIMIT 100;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(search))
            {
                cmd.Parameters.AddWithValue("@search", search.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                products.Add(new ProductEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString(reader.GetOrdinal("descricao")).Trim(),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    un = reader.IsDBNull(reader.GetOrdinal("un")) ? null : reader.GetString(reader.GetOrdinal("un")).Trim(),
                    eanCode = reader.IsDBNull(reader.GetOrdinal("eanCode")) ? null : reader.GetString(reader.GetOrdinal("eanCode")).Trim(),
                    categoria_id = reader.IsDBNull(reader.GetOrdinal("categoria_id")) ? null : reader.GetInt32(reader.GetOrdinal("categoria_id")),
                    categoria = reader.IsDBNull(reader.GetOrdinal("categoria")) ? null : reader.GetString(reader.GetOrdinal("categoria")).Trim(),
                    marca = reader.IsDBNull(reader.GetOrdinal("marca")) ? null : reader.GetString(reader.GetOrdinal("marca")).Trim(),
                    marca_id = reader.IsDBNull(reader.GetOrdinal("marca_id")) ? null : reader.GetInt32(reader.GetOrdinal("marca_id")),
                    fornecedor = reader.IsDBNull(reader.GetOrdinal("fornecedor")) ? null : reader.GetString(reader.GetOrdinal("fornecedor")).Trim(),
                    fornecedor_id = reader.IsDBNull(reader.GetOrdinal("fornecedor_id")) ? null : reader.GetInt32(reader.GetOrdinal("fornecedor_id")),
                    preco_custo = reader.IsDBNull(reader.GetOrdinal("preco_custo")) ? null : reader.GetDecimal(reader.GetOrdinal("preco_custo")),
                    preco_venda = reader.IsDBNull(reader.GetOrdinal("preco_venda")) ? null : reader.GetDecimal(reader.GetOrdinal("preco_venda")),
                    estoque_minimo = reader.IsDBNull(reader.GetOrdinal("estoque_minimo")) ? null : reader.GetDecimal(reader.GetOrdinal("estoque_minimo")),

                });
            }

            response.Success = true;
            response.Data = products;
            response.Message = products.Count == 0
                ? "Nenhuma produto encontrado."
                : "Produtos encontrados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar Produtos: {ex.Message}";
            return StatusCode(500, response);
        }
    }



    [HttpGet("Get/Product/ByGtin")]
    public async Task<IActionResult> GetProduto([FromQuery] string gtin)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<ProductGtinEntity>();

        try
        {
            // 1️⃣ BUSCA NO BANCO LOCAL
            const string queryProductByGtin = @"SELECT * FROM product_gtin WHERE ean = @gtin";

            ProductGtinEntity? productLocal = null;

            await using (var cmd = new NpgsqlCommand(queryProductByGtin, conn))
            {
                cmd.Parameters.AddWithValue("@gtin", gtin);

                await using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    productLocal = new ProductGtinEntity
                    {
                        ean = reader.IsDBNull(reader.GetOrdinal("ean")) ? null : reader.GetString(reader.GetOrdinal("ean")).Trim(),
                        ean_tipo = reader.IsDBNull(reader.GetOrdinal("ean_tipo")) ? null : reader.GetString(reader.GetOrdinal("ean_tipo")).Trim(),
                        cest = reader.IsDBNull(reader.GetOrdinal("cest")) ? null : reader.GetString(reader.GetOrdinal("cest")).Trim(),
                        ncm = reader.IsDBNull(reader.GetOrdinal("ncm")) ? null : reader.GetString(reader.GetOrdinal("ncm")).Trim(),
                        nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader.GetString(reader.GetOrdinal("nome")).Trim(),
                        marca = reader.IsDBNull(reader.GetOrdinal("marca")) ? null : reader.GetString(reader.GetOrdinal("marca")).Trim(),
                        categoria = reader.IsDBNull(reader.GetOrdinal("categoria")) ? null : reader.GetString(reader.GetOrdinal("categoria")).Trim()
                    };
                }
            }

            if (productLocal != null)
            {
                response.Success = true;
                response.Message = "Produto encontrado no banco local.";
                response.Data = productLocal;
                return Ok(response);
            }

            // CONSULTA SERVIÇO EXTERNO
            var result = await _service.ConsultarProdutoAsync(gtin);
            var product = JsonSerializer.Deserialize<ProductGtinEntity>(result);

            if (product == null)
            {
                response.Success = false;
                response.Message = "Produto não encontrado.";
                return NotFound(response);
            }

            //  SALVA NO BANCO LOCAL
            await using var transaction = await conn.BeginTransactionAsync();

            const string insertProductQuery = @"
            INSERT INTO product_gtin
            (ean, ean_tipo, cest, ncm, nome, marca, categoria)
            VALUES
            (@ean, @ean_tipo, @cest, @ncm, @nome, @marca, @categoria);
        ";

            await using (var cmdInsert = new NpgsqlCommand(insertProductQuery, conn, transaction))
            {
                cmdInsert.Parameters.AddWithValue("@ean", product.ean ?? (object)DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@ean_tipo", product.ean_tipo ?? (object)DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@cest", product.cest ?? (object)DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@ncm", product.ncm ?? (object)DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@nome", product.nome ?? (object)DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@marca", product.marca ?? (object)DBNull.Value);
                cmdInsert.Parameters.AddWithValue("@categoria", product.categoria ?? (object)DBNull.Value);

                await cmdInsert.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Produto encontrado no serviço externo.";
            response.Data = product;
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar produto: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [HttpDelete("token-cache")]
    public IActionResult ClearTokenCache()
    {
        _tokenService.ClearTokenCache();
        return Ok(new { message = "Cache do token GTIN limpo com sucesso." });
    }


    [Authorize]
    [HttpGet("Get/Product/Form")]
    public async Task<IActionResult> FetchFormProduct()
    {
        var empresaId = User.GetEmpresaId();
        var cacheKey = $"form-product:{empresaId}";

        //  1. Tenta pegar do cache
        if (_cache.TryGetValue(cacheKey, out FormProduct cachedForm))
        {
            return Ok(new Response<FormProduct>
            {
                Success = true,
                Data = cachedForm,
                Message = "Dados carregados do cache."
            });
        }

        await using var conn = NovaConexao();
        await conn.OpenAsync();

        try
        {
            const string query = @"SELECT
    COALESCE(
        (
            SELECT jsonb_agg(
                jsonb_build_object(
                    'id', c.id,
                    'nome', c.nome
                )
                ORDER BY c.nome
            )
            FROM categoria c
            WHERE c.empresa_id = e.id
              AND c.ativo = TRUE
        ),
        '[]'::jsonb
    ) AS categorias,

    COALESCE(
        (
            SELECT jsonb_agg(
                jsonb_build_object(
                    'id', f.id,
                    'nome', f.nome
                )
                ORDER BY f.nome
            )
            FROM fornecedores f
            WHERE f.empresa_id = e.id
              AND f.ativo = TRUE
        ),
        '[]'::jsonb
    ) AS fornecedores,

    COALESCE(
        (
            SELECT jsonb_agg(
                jsonb_build_object(
                    'id', m.id,
                    'nome', m.nome
                )
                ORDER BY m.nome
            )
            FROM marca m
            WHERE m.empresa_id = e.id
              AND m.ativo = TRUE
        ),
        '[]'::jsonb
    ) AS marcas
FROM empresa e
WHERE e.id = @empresa_id;
 ";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Ok(new Response<FormProduct>
                {
                    Success = true,
                    Data = new FormProduct(),
                    Message = "Nenhum dado encontrado."
                });
            }

            var formProduct = new FormProduct
            {
                categorias = JsonSerializer.Deserialize<List<LookupItem>>(
                    reader.GetString(reader.GetOrdinal("categorias"))
                ) ?? new(),

                fornecedores = JsonSerializer.Deserialize<List<LookupItem>>(
                    reader.GetString(reader.GetOrdinal("fornecedores"))
                ) ?? new(),

                marcas = JsonSerializer.Deserialize<List<LookupItem>>(
                    reader.GetString(reader.GetOrdinal("marcas"))
                ) ?? new()
            };

            //  2. Salva no cache
            _cache.Set(
                cacheKey,
                formProduct,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
                    SlidingExpiration = TimeSpan.FromDays(5)
                }
            );

            return Ok(new Response<FormProduct>
            {
                Success = true,
                Data = formProduct,
                Message = "Dados carregados do banco."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new Response<FormProduct>
            {
                Success = false,
                Message = $"Erro ao carregar formulário: {ex.Message}"
            });
        }
    }




}


public class ProductGtinEntity
{
    public string? ean { get; set; }
    public string? ean_tipo { get; set; }
    public string? cest { get; set; }
    public string? ncm { get; set; }
    public string? nome { get; set; }
    public string? marca { get; set; }
    public string? categoria { get; set; }

}