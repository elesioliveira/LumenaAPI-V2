using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("API/V1")]
public class WalletReceberAPI : ControllerBase
{
    private const string CacheKey = "PRODUCT_FORM_";
    private readonly IConfiguration _config;
    private readonly IGtinProdutoService _service;
    private readonly IGtinTokenService _tokenService;
    private readonly IMemoryCache _cache;
    public WalletReceberAPI(IConfiguration config, IMemoryCache cache)
    {
        _config = config;
        _cache = cache;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));


// [Authorize]
// [HttpPut("Put/Category/Wallet/{id}")]
// public async Task<IActionResult> UpdateCategory([FromBody] CategoryWalletDTO dto,int id)
// {
//     await using var conn = NovaConexao();
//     await conn.OpenAsync();

//     await using var transaction = await conn.BeginTransactionAsync();

//     var empresaId = User.GetEmpresaId();
//     var response = new Response<string>();

//     try
//     {
//         const string sql = @"
//             UPDATE categoria_wallet
//             SET
//                 nome = @nome,
//                 tipo = @tipo,
//                 descricao = @descricao,
//                 cor = @cor,
//                 ativo = @ativo
//             WHERE id = @id
//               AND empresa_id = @empresa_id;
//         ";

//         await using var cmd = new NpgsqlCommand(sql, conn, transaction);

//         cmd.Parameters.AddWithValue("@id", id);
//         cmd.Parameters.AddWithValue("@empresa_id", empresaId);
//         cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("@descricao", (object?)dto.descricao ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("@cor", (object?)dto.cor ?? DBNull.Value);
//         cmd.Parameters.AddWithValue("@ativo", (object?)dto.ativo ?? true);

//         var rowsAffected = await cmd.ExecuteNonQueryAsync();

//         if (rowsAffected == 0)
//             throw new Exception("Categoria não encontrada ou não pertence à empresa.");

//         await transaction.CommitAsync();

//         response.Success = true;
//         response.Message = "Categoria atualizada com sucesso.";
//         return Ok(response);
//     }
//     catch (Exception ex)
//     {
//         await transaction.RollbackAsync();
//         response.Success = false;
//         response.Message = ex.Message;
//         return StatusCode(500, response);
//     }
// }

    [Authorize]
    [HttpPost("Post/Wallet/Nova/Conta")]
    public async Task<IActionResult> CreateNovaConta([FromBody] WalletReceberDTO dto)
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
            insert into wallet (empresa_id, categoria_id, fornecedor_id, cliente_id, valor_total, status, origem_tipo, observacao, tipo_pagamento, descricao, data_vencimento) 
            VALUES (@empresa_id, @categoria_id, @fornecedor_id, @cliente_id, @valor_total, @status, @origem_tipo, @observacao, @tipo_pagamento, @descricao,  @data_vencimento)
              RETURNING id; ";

            int id;

            await using (var cmd = new NpgsqlCommand(sqlInsert, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@categoria_id", (object?)dto.categoria_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fornecedor_id", (object?)dto.fornecedor_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@cliente_id", (object?)dto.cliente_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@valor_total", (object?)dto.valor_total ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@status", (object?)dto.status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@origem_tipo", (object?)dto.origem_tipo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo_pagamento", (object?)dto.tipo_pagamento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@descricao", (object?)dto.descricao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@data_vencimento", (object)dto.data_vencimento ?? DBNull.Value);
                id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
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
[HttpPut("Put/Wallet/Conta/{id:int}")]
public async Task<IActionResult> UpdateConta(
    int id,
    [FromBody] WalletReceberDTO dto)
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    await using var transaction = await conn.BeginTransactionAsync();

    var empresaId = User.GetEmpresaId();
    var usuarioId = User.GetUserId();

    var response = new Response<string>();

    try
    {
        const string sqlUpdate = @"
        UPDATE wallet
        SET
            categoria_id   = @categoria_id,
            fornecedor_id  = @fornecedor_id,
            cliente_id     = @cliente_id,
            valor_total    = @valor_total,
            status         = @status,
            origem_tipo    = @origem_tipo,
            observacao     = @observacao,
            tipo_pagamento = @tipo_pagamento,
            descricao      = @descricao,
            data_vencimento = @data_vencimento,
            data_atualizacao = NOW(),
            usuario_atualizacao_id = @usuario_id
        WHERE
            id = @id
            AND empresa_id = @empresa_id;
        ";

        await using var cmd = new NpgsqlCommand(sqlUpdate, conn, transaction);

        cmd.Parameters.Add("@id", NpgsqlTypes.NpgsqlDbType.Integer).Value = id;
        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer).Value = empresaId;
        cmd.Parameters.Add("@usuario_id", NpgsqlTypes.NpgsqlDbType.Integer).Value = usuarioId;

        cmd.Parameters.Add("@categoria_id", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = (object?)dto.categoria_id ?? DBNull.Value;

        cmd.Parameters.Add("@fornecedor_id", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = (object?)dto.fornecedor_id ?? DBNull.Value;

        cmd.Parameters.Add("@cliente_id", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = (object?)dto.cliente_id ?? DBNull.Value;

        cmd.Parameters.Add("@valor_total", NpgsqlTypes.NpgsqlDbType.Numeric)
            .Value = (object?)dto.valor_total ?? DBNull.Value;

        cmd.Parameters.Add("@status", NpgsqlTypes.NpgsqlDbType.Text)
            .Value = (object?)dto.status ?? DBNull.Value;

        cmd.Parameters.Add("@origem_tipo", NpgsqlTypes.NpgsqlDbType.Text)
            .Value = (object?)dto.origem_tipo ?? DBNull.Value;

        cmd.Parameters.Add("@observacao", NpgsqlTypes.NpgsqlDbType.Text)
            .Value = (object?)dto.observacao ?? DBNull.Value;

        cmd.Parameters.Add("@tipo_pagamento", NpgsqlTypes.NpgsqlDbType.Text)
            .Value = (object?)dto.tipo_pagamento ?? DBNull.Value;

        cmd.Parameters.Add("@descricao", NpgsqlTypes.NpgsqlDbType.Text)
            .Value = (object?)dto.descricao ?? DBNull.Value;

        cmd.Parameters.Add("@data_vencimento", NpgsqlTypes.NpgsqlDbType.Date)
            .Value = (object?)dto.data_vencimento ?? DBNull.Value;

        var affectedRows = await cmd.ExecuteNonQueryAsync();

        if (affectedRows == 0)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Conta não encontrada ou não pertence à empresa.";
            return NotFound(response);
        }

        await transaction.CommitAsync();

        response.Success = true;
        response.Message = "Conta atualizada com sucesso.";
        return Ok(response);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        response.Success = false;
        response.Message = $"Erro ao atualizar conta: {ex.Message}";
        return StatusCode(500, response);
    }
}



[Authorize]
[HttpGet("Get/Wallet/Conta")]
public async Task<IActionResult> FetchWalletConta( [FromQuery] string? search,[FromQuery] string? status, [FromQuery] string origem)
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    var response = new Response<List<WalletEntity>>();
    var contas = new List<WalletEntity>();
    var empresaId = User.GetEmpresaId();

    try
    {
        var query = @"
        SELECT
        w.id,
        w.data_cadastro,
        w.data_vencimento,
        w.descricao,
        w.cliente_id,
        w.categoria_id,
        c.nome  AS cliente,
        ca.nome AS categoria,
        w.status,
        w.valor_total AS valor,
        w.tipo_pagamento,
        w.observacao,
        w.origem_tipo
    FROM wallet w
    LEFT JOIN cliente c ON c.id = w.cliente_id
    INNER JOIN categoria_wallet ca ON ca.id = w.categoria_id
    WHERE
        w.empresa_id = @empresa_id
        AND (
            @search IS NULL
            OR w.descricao ILIKE '%' || @search || '%'
            OR c.nome ILIKE '%' || @search || '%'
            OR ca.nome ILIKE '%' || @search || '%'
        )
        AND (
            @status IS NULL
            OR (
                @status <> 'Vencido'
                AND w.status = @status
            )
            OR (
                @status = 'Vencido'
                AND w.status = 'Pendente'
                AND w.data_vencimento < CURRENT_DATE
            )
        )
        AND LOWER(ca.tipo) = LOWER(@origem)
    order by w.data_cadastro DESC
    LIMIT 100;
        ";

        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer)
        .Value = empresaId;


        cmd.Parameters.Add("@origem", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = origem;


        cmd.Parameters.Add("@search", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = (object?)search ?? DBNull.Value;


        cmd.Parameters.Add("@status", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = (object?)status ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            contas.Add(new WalletEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),

                data_cadastro = reader.GetDateTime(
                    reader.GetOrdinal("data_cadastro")),

                vencimento = reader.GetFieldValue<DateOnly>(
                    reader.GetOrdinal("data_vencimento")),

                descricao = reader.IsDBNull(reader.GetOrdinal("descricao"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("descricao")),

                cliente_id = reader.GetInt32(
                    reader.GetOrdinal("cliente_id")),

                categoria_id = reader.GetInt32(
                    reader.GetOrdinal("categoria_id")),

                cliente = reader.IsDBNull(reader.GetOrdinal("cliente"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("cliente")),

                categoria = reader.IsDBNull(reader.GetOrdinal("categoria"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("categoria")),

                status = reader.IsDBNull(reader.GetOrdinal("status"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("status")),

                valor = reader.GetDecimal(
                    reader.GetOrdinal("valor")),

                tipo_pagamento = reader.IsDBNull(reader.GetOrdinal("tipo_pagamento"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("tipo_pagamento")),

                observacao = reader.IsDBNull(reader.GetOrdinal("observacao"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("observacao")),

                origem_tipo = reader.IsDBNull(reader.GetOrdinal("origem_tipo"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("origem_tipo"))
            });
        }

        response.Success = true;
        response.Data = contas;
        response.Message = contas.Count == 0
            ? "Nenhuma conta encontrada."
            : "Contas encontradas com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar contas: {ex.Message}";
        return StatusCode(500, response);
    }
}
[Authorize]
[HttpGet("Get/Wallet/Conta-Pagar")]
public async Task<IActionResult> FetchWalletContaPagar( [FromQuery] string? search,[FromQuery] string? status, [FromQuery] string origem)
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    var response = new Response<List<WalletEntity>>();
    var contas = new List<WalletEntity>();
    var empresaId = User.GetEmpresaId();

    try
    {
        var query = @"
        SELECT
        w.id,
        w.data_cadastro,
        w.data_vencimento,
        w.descricao,
        w.fornecedor_id,
        w.categoria_id,
        f.nome  AS fornecedor,
        ca.nome AS categoria,
        w.status,
        w.valor_total AS valor,
        w.tipo_pagamento,
        w.observacao,
        w.origem_tipo
    FROM wallet w
    INNER JOIN fornecedores f ON f.id = w.fornecedor_id
    INNER JOIN categoria_wallet ca ON ca.id = w.categoria_id
    WHERE
        w.empresa_id = @empresa_id
        AND (
            @search IS NULL
            OR w.descricao ILIKE '%' || @search || '%'
            OR f.nome ILIKE '%' || @search || '%'
            OR ca.nome ILIKE '%' || @search || '%'
        )
        AND (
            @status IS NULL
            OR (
                @status <> 'Vencido'
                AND w.status = @status
            )
            OR (
                @status = 'Vencido'
                AND w.status = 'Pendente'
                AND w.data_vencimento < CURRENT_DATE
            )
        )
        AND LOWER(ca.tipo) = LOWER(@origem)
    ORDER BY w.data_vencimento DESC
    LIMIT 100;
        ";

        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer)
        .Value = empresaId;


        cmd.Parameters.Add("@origem", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = origem;


        cmd.Parameters.Add("@search", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = (object?)search ?? DBNull.Value;


        cmd.Parameters.Add("@status", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = (object?)status ?? DBNull.Value;

        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            contas.Add(new WalletEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),

                data_cadastro = reader.GetDateTime(
                    reader.GetOrdinal("data_cadastro")),

                vencimento = reader.GetFieldValue<DateOnly>(
                    reader.GetOrdinal("data_vencimento")),

                descricao = reader.IsDBNull(reader.GetOrdinal("descricao"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("descricao")),

                fornecedor_id = reader.GetInt32(
                    reader.GetOrdinal("fornecedor_id")),

                categoria_id = reader.GetInt32(
                    reader.GetOrdinal("categoria_id")),

                fornecedor = reader.IsDBNull(reader.GetOrdinal("fornecedor"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("fornecedor")),

                categoria = reader.IsDBNull(reader.GetOrdinal("categoria"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("categoria")),

                status = reader.IsDBNull(reader.GetOrdinal("status"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("status")),

                valor = reader.GetDecimal(
                    reader.GetOrdinal("valor")),

                tipo_pagamento = reader.IsDBNull(reader.GetOrdinal("tipo_pagamento"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("tipo_pagamento")),

                observacao = reader.IsDBNull(reader.GetOrdinal("observacao"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("observacao")),

                origem_tipo = reader.IsDBNull(reader.GetOrdinal("origem_tipo"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("origem_tipo"))
            });
        }

        response.Success = true;
        response.Data = contas;
        response.Message = contas.Count == 0
            ? "Nenhuma conta encontrada."
            : "Contas encontradas com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar contas: {ex.Message}";
        return StatusCode(500, response);
    }
}

// [Authorize]
// [HttpGet("Get/Category/Active")]
// public async Task<IActionResult> FetchCategoriesActive()
// {
//     await using var conn = NovaConexao();
//     await conn.OpenAsync();

//     var response = new Response<CategoryActiveCountDTO>();
//     var empresaId = User.GetEmpresaId();

//     try
//     {
//         const string query = @"
//             SELECT
//                 COUNT(*) FILTER (WHERE tipo = 'Receita' AND ativo = true) AS total_receita,
//                 COUNT(*) FILTER (WHERE tipo = 'Despesa' AND ativo = true) AS total_despesa
//             FROM categoria_wallet
//             WHERE empresa_id = @empresa_id;
//         ";

//         await using var cmd = new NpgsqlCommand(query, conn);
//         cmd.Parameters.AddWithValue("@empresa_id", empresaId);

//         await using var reader = await cmd.ExecuteReaderAsync();
//         if (await reader.ReadAsync())
//         {
//             response.Data = new CategoryActiveCountDTO
//             {
//                 TotalReceita = reader.GetInt32(reader.GetOrdinal("total_receita")),
//                 TotalDespesa = reader.GetInt32(reader.GetOrdinal("total_despesa"))
//             };
//         }

//         response.Success = true;
//         response.Message = "Categorias ativas encontradas com sucesso.";
//         return Ok(response);
//     }
//     catch (Exception ex)
//     {
//         response.Success = false;
//         response.Message = $"Erro ao buscar categorias ativas: {ex.Message}";
//         return StatusCode(500, response);
//     }
// }

[Authorize]
[HttpGet("Get/Wallet/Cliente-Categoria")]
public async Task<IActionResult> FetchClienteECategoria(  [FromQuery] int id_cliente,  [FromQuery] int id_categoria)
{
    var response = new Response<WalletClienteCategoriaDto>();
    var empresaId = User.GetEmpresaId();

    try
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        const string sql = @"
        SELECT
            c.id   AS cliente_id,
            c.nome AS cliente_nome,
            ca.id  AS categoria_id,
            ca.nome AS categoria_nome
        FROM cliente c
        FULL JOIN categoria_wallet ca
            ON ca.id = @id_categoria
           AND ca.empresa_id = @empresa_id
        WHERE
            c.id = @id_cliente
            AND c.empresa_id = @empresa_id;
        ";

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.Add("@id_cliente", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = id_cliente;

        cmd.Parameters.Add("@id_categoria", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = id_categoria;

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = empresaId;

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            response.Success = false;
            response.Message = "Cliente ou categoria não encontrados.";
            return NotFound(response);
        }

        var result = new WalletClienteCategoriaDto();

        if (!reader.IsDBNull(reader.GetOrdinal("cliente_id")))
        {
            result.cliente = new WalletClienteDto
            {
                id = reader.GetInt32(reader.GetOrdinal("cliente_id")),
                nome = reader.GetString(reader.GetOrdinal("cliente_nome"))
            };
        }

        if (!reader.IsDBNull(reader.GetOrdinal("categoria_id")))
        {
            result.categoria = new WalletCategoriaDto
            {
                id = reader.GetInt32(reader.GetOrdinal("categoria_id")),
                nome = reader.GetString(reader.GetOrdinal("categoria_nome"))
            };
        }

        response.Success = true;
        response.Data = result;
        response.Message = "Cliente e categoria carregados com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar cliente e categoria: {ex.Message}";
        return StatusCode(500, response);
    }
}
[Authorize]
[HttpGet("Get/Wallet/Fornecedor-Categoria")]
public async Task<IActionResult> FetchFornecedorECategoria(  [FromQuery] int id_fornecedor,  [FromQuery] int id_categoria)
{
    var response = new Response<WalletFornecedorCategoriaDto>();
    var empresaId = User.GetEmpresaId();

    try
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        const string sql = @"
        SELECT
            f.id   AS fornecedor_id,
            f.nome AS fornecedor_nome,
            ca.id  AS categoria_id,
            ca.nome AS categoria_nome
        FROM fornecedores f
        FULL JOIN categoria_wallet ca
            ON ca.id = @id_categoria
           AND ca.empresa_id = @empresa_id
        WHERE
            f.id = @id_fornecedor
            AND f.empresa_id = @empresa_id;
        ";

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.Add("@id_fornecedor", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = id_fornecedor;

        cmd.Parameters.Add("@id_categoria", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = id_categoria;

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer)
            .Value = empresaId;

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            response.Success = false;
            response.Message = "Fornecedor ou categoria não encontrados.";
            return NotFound(response);
        }

        var result = new WalletFornecedorCategoriaDto();

        if (!reader.IsDBNull(reader.GetOrdinal("fornecedor_id")))
        {
            result.fornecedor = new WalletFornecedorDto
            {
                id = reader.GetInt32(reader.GetOrdinal("fornecedor_id")),
                nome = reader.GetString(reader.GetOrdinal("fornecedor_nome"))
            };
        }

        if (!reader.IsDBNull(reader.GetOrdinal("categoria_id")))
        {
            result.categoria = new WalletCategoriaDto
            {
                id = reader.GetInt32(reader.GetOrdinal("categoria_id")),
                nome = reader.GetString(reader.GetOrdinal("categoria_nome"))
            };
        }

        response.Success = true;
        response.Data = result;
        response.Message = "Fornecedor e categoria carregados com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar Fornecedor e categoria: {ex.Message}";
        return StatusCode(500, response);
    }
}

[Authorize]
[HttpGet("Get/Dashboard/Wallet")]
public async Task<IActionResult> FetchDashBoardWallet(  [FromQuery] string origem_tipo)
{
    var response = new Response<DashBoardWalletEntity>();
    var empresaId = User.GetEmpresaId();

    try
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        const string sql = @"
        SELECT
            Count( case when origem_tipo = @origem_tipo and status ='Pendente' then id else null end) as qtd_aberto,
            SUM( CASE WHEN origem_tipo = @origem_tipo AND status = 'Pendente' THEN valor_total ELSE 0 END ) AS total_aberto,
            Count( case when origem_tipo = @origem_tipo and status ='Pendente' AND data_vencimento < CURRENT_DATE then id else null end) as qtd_vencido,
            SUM( CASE WHEN origem_tipo = @origem_tipo AND status = 'Pendente' AND data_vencimento < CURRENT_DATE THEN valor_total ELSE 0 END ) AS total_vencido
        FROM wallet
        WHERE empresa_id = @empresa_id; 
        ";
       

        await using var cmd = new NpgsqlCommand(sql, conn);

        cmd.Parameters.AddWithValue("@origem_tipo", origem_tipo);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);

        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            response.Success = false;
            response.Message = "Dashboard não encontrado";
            return NotFound(response);
        }


         response.Data = new DashBoardWalletEntity
         {
            qtd_aberto= reader.GetInt32(reader.GetOrdinal("qtd_aberto")),
             total_aberto= reader.GetDecimal(reader.GetOrdinal("total_aberto")),
             qtd_vencido= reader.GetInt32(reader.GetOrdinal("qtd_vencido")),
             total_vencido= reader.GetDecimal(reader.GetOrdinal("total_vencido")),
         };

        response.Success = true;
        response.Message = "Dashboard carregado com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar Dashboard: {ex.Message}";
        return StatusCode(500, response);
    }
}

[Authorize]
[HttpGet("Get/Wallet/Resumo-Financeiro")]
public async Task<IActionResult> FetchResumoFinanceiro( [FromQuery] DateOnly data_inicio,  [FromQuery] DateOnly data_fim)
{
    var response = new Response<WalletResumoFinanceiroDto>();
    var empresaId = User.GetEmpresaId();

    try
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        const string query = @"
            SELECT
            COALESCE(
                SUM(
                    CASE
                        WHEN c.tipo = 'Receita'
                        AND w.status = 'Recebido'
                        AND w.data_cadastro >= @data_inicio
                        AND w.data_cadastro < (@data_fim + INTERVAL '1 day')
                        THEN w.valor_total
                        ELSE 0
                    END
                ), 0
            ) AS total_entrada,

            COALESCE(
                SUM(
                    CASE
                        WHEN c.tipo = 'Despesa'
                        AND w.status = 'Pago'
                        AND w.data_cadastro >= @data_inicio
                        AND w.data_cadastro < (@data_fim + INTERVAL '1 day')
                        THEN w.valor_total
                        ELSE 0
                    END
                ), 0
            ) AS total_saida
        FROM wallet w
        INNER JOIN categoria_wallet c
            ON c.id = w.categoria_id
        AND c.empresa_id = w.empresa_id
        WHERE w.empresa_id = @empresa_id;
        ";

        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer)
           .Value = empresaId;

        cmd.Parameters.Add("@data_inicio", NpgsqlTypes.NpgsqlDbType.Date)
           .Value = data_inicio;

        cmd.Parameters.Add("@data_fim", NpgsqlTypes.NpgsqlDbType.Date)
           .Value = data_fim;

        await using var reader = await cmd.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            response.Success = true;
            response.Data = new WalletResumoFinanceiroDto
            {
                total_entrada = reader.GetDecimal(reader.GetOrdinal("total_entrada")),
                total_saida = reader.GetDecimal(reader.GetOrdinal("total_saida"))
            };
            response.Message = "Resumo financeiro carregado com sucesso.";
        }
        else
        {
            response.Success = true;
            response.Data = new WalletResumoFinanceiroDto();
            response.Message = "Nenhum dado encontrado para o período.";
        }

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar resumo financeiro: {ex.Message}";
        return StatusCode(500, response);
    }
}

[Authorize]
[HttpGet("Get/Wallet/Resumo-Financeiro-Registros")]
public async Task<IActionResult> FetchResumoFinanceiroRegistros( [FromQuery] DateOnly data_inicio,  [FromQuery] DateOnly data_fim,[FromQuery] string? receita, [FromQuery] string? status, [FromQuery] string? search)
{
    var response = new Response<List<WalletResumoFinanceiroRegistroDTO>>();
    response.Data = new();
    var empresaId = User.GetEmpresaId();

    try
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        const string query = @"
          select 
           w.id, w.data_cadastro, w.descricao, c.nome, c.cor, w.origem_tipo, w.status, w.valor_total, w.data_vencimento
            from wallet w
            inner join categoria_wallet c on c.id = w.categoria_id
            where w.empresa_id = @empresa_id
            And w.data_cadastro >= @data_inicio
            And w.data_cadastro < (@data_fim + INTERVAL '1 day')
            And (
            @search is null
            OR w.descricao ILIKE '%' || @search || '%'
            )
            AND (
                @status IS NULL
                OR (
                    @status <> 'Vencido'
                    AND w.status = @status
                )
                OR (
                    @status = 'Vencido'
                    AND w.status = 'Pendente'
                    AND w.data_vencimento < CURRENT_DATE
                )
            )
            And (
            @receita is null
            OR c.tipo ILIKE '%' || @receita || '%'
            )
            order by w.data_cadastro asc
        ";

        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.Add("@empresa_id", NpgsqlTypes.NpgsqlDbType.Integer)
           .Value = empresaId;

        cmd.Parameters.Add("@data_inicio", NpgsqlTypes.NpgsqlDbType.Date)
           .Value = data_inicio;

        cmd.Parameters.Add("@data_fim", NpgsqlTypes.NpgsqlDbType.Date)
           .Value = data_fim;

        cmd.Parameters.Add("@receita", NpgsqlTypes.NpgsqlDbType.Text)
        .Value =(object?)receita ?? DBNull.Value;


        cmd.Parameters.Add("@search", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = (object?)search ?? DBNull.Value;


        cmd.Parameters.Add("@status", NpgsqlTypes.NpgsqlDbType.Text)
        .Value = (object?)status ?? DBNull.Value;


        await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                response.Data.Add(new WalletResumoFinanceiroRegistroDTO
                {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                data_vencimento = reader.GetFieldValue<DateOnly>(reader.GetOrdinal("data_vencimento")),
                descricao = reader.GetString(reader.GetOrdinal("descricao")),
                cor = reader.GetString(reader.GetOrdinal("cor")),
                nome = reader.GetString(reader.GetOrdinal("nome")),
                origem_tipo = reader.GetString(reader.GetOrdinal("origem_tipo")),
                status = reader.GetString(reader.GetOrdinal("status")),
                valor_total = reader.GetDecimal(reader.GetOrdinal("valor_total"))
                });
            }

        response.Success = true;
        response.Message = response.Data.Count == 0
        ? "Nenhum dado encontrado para o período."
        : "Resumo financeiro carregado com sucesso.";
        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao buscar resumo financeiro: {ex.Message}";
        return StatusCode(500, response);
    }
}



}


