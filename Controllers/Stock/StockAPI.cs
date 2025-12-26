using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("API/V1")]
public class StockAPI : ControllerBase
{
    private const string CacheKey = "PRODUCT_FORM_";
    private readonly IConfiguration _config;
    private readonly IGtinProdutoService _service;
    private readonly IGtinTokenService _tokenService;
    private readonly IMemoryCache _cache;
    public StockAPI(IConfiguration config, IMemoryCache cache)
    {
        _config = config;
        _cache = cache;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));


    [Authorize]
    [HttpPost("Post/MovimentarEstoque")]
    public async Task<IActionResult> MovimentarEstoque([FromBody] MovimentarEstoqueDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();
        var usuarioId = User.GetUserId();

        var response = new Response<string>();

        try
        {
            // 1️⃣ Inserir cabeçalho da movimentação
            const string insertMov = @"
            INSERT INTO estoque_movimentacao
            (empresa_id, nota, fornecedor_id, data_emissao, data_ocorrencia, observacao, usuario_id, tipo,valor_total, motivo_saida)
            VALUES
            (@empresa_id, @nota, @fornecedor_id, @data_emissao, @data_ocorrencia, @observacao, @usuario_id, @tipo,@valor_total, @motivo_saida)
            RETURNING id;
        ";

            int movimentacaoId;

            await using (var cmd = new NpgsqlCommand(insertMov, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nota", (object?)dto.nota ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@fornecedor_id", (object?)dto.fornecedor_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@data_emissao", (object?)dto.data_emissao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@data_ocorrencia", (object?)dto.data_ocorrencia ?? DateTime.Now);
                cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@motivo_saida", (object?)dto.motivo_saida ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@valor_total", (object?)dto.valor_total ?? 0);
                cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
                cmd.Parameters.AddWithValue("@tipo", dto.tipo);

                movimentacaoId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // 2️⃣ Processar cada item
            foreach (var item in dto.itens)
            {
                // 2.1 🔒 Lock do saldo do produto
                const string lockSaldo = @"
                SELECT saldo
                FROM saldo_estoque
                WHERE empresa_id = @empresa_id
                AND produto_id = @produto_id
                FOR UPDATE;
            ";

                int saldoAtual;

                await using (var cmdLock = new NpgsqlCommand(lockSaldo, conn, transaction))
                {
                    cmdLock.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdLock.Parameters.AddWithValue("@produto_id", item.produto_id);

                    var result = await cmdLock.ExecuteScalarAsync();
                    saldoAtual = result == null ? 0 : Convert.ToInt32(result);
                }

                // 2.2 Validação de saída
                if ((dto.tipo == "SAIDA" || dto.tipo == "AJUSTE_NEG") &&
                    saldoAtual < item.quantidade)
                {
                    throw new Exception($"Estoque insuficiente para o produto {item.produto_id}");
                }

                // 2.3 Inserir item da movimentação
                const string insertItem = @"
                INSERT INTO controle_estoque
                (movimentacao_id, produto_id, quantidade, valor_unitario, sub_total)
                VALUES
                (@movimentacao_id, @produto_id, @quantidade, @valor_unitario, @sub_total);
            ";

                await using (var cmdItem = new NpgsqlCommand(insertItem, conn, transaction))
                {
                    cmdItem.Parameters.AddWithValue("@movimentacao_id", movimentacaoId);
                    cmdItem.Parameters.AddWithValue("@produto_id", item.produto_id);
                    cmdItem.Parameters.AddWithValue("@quantidade", item.quantidade);
                    cmdItem.Parameters.AddWithValue("@valor_unitario", item.valor_unitario);
                    cmdItem.Parameters.AddWithValue("@sub_total", item.sub_total);
                    await cmdItem.ExecuteNonQueryAsync();
                }

                // 2.4 Atualizar saldo
                var sinal = dto.tipo is "ENTRADA" or "AJUSTE_POS" or "DEVOLUCAO" ? 1 : -1;

                const string updateSaldo = @"
                INSERT INTO saldo_estoque (empresa_id, produto_id, saldo)
                VALUES (@empresa_id, @produto_id, @saldo)
                ON CONFLICT (empresa_id, produto_id)
                DO UPDATE SET saldo = saldo_estoque.saldo + @saldo;
            ";

                await using (var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction))
                {
                    cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdSaldo.Parameters.AddWithValue("@produto_id", item.produto_id);
                    cmdSaldo.Parameters.AddWithValue("@saldo", sinal * item.quantidade);
                    await cmdSaldo.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Movimentação de estoque realizada com sucesso.";
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
    [HttpGet("Get/Stock/Fornecedores")]
    public async Task<IActionResult> BuscarFornecedores()
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<StockFornecedor>>();
        var fornecedores = new List<StockFornecedor>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            SELECT 
                id,
                nome
            FROM fornecedores
            WHERE empresa_id = @empresa_id and ativo = true
        ";


            query += " ORDER BY nome";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);


            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                fornecedores.Add(new StockFornecedor
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim()
                });
            }

            response.Success = true;
            response.Data = fornecedores;
            response.Message = fornecedores.Count == 0
                ? "Nenhum fornecedor encontrado."
                : "Fornecedores encontrados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar fornecedores: {ex.Message}";
            return StatusCode(500, response);
        }
    }


    [Authorize]
    [HttpGet("Get/Stock/Product")]
    public async Task<IActionResult> FetchMark([FromQuery] string? search)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<StockProuct>>();
        var products = new List<StockProuct>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            SELECT 
                p.id,
                p.nome,
                p.preco_custo,
                p.fracionado
            FROM produto p
            WHERE p.empresa_id = @empresa_id
        ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                query += @"
                AND (
                    p.codigo_barras = @search
                    OR p.nome ILIKE '%' || @search || '%'
                )
                ORDER BY 
                    CASE 
                        WHEN p.codigo_barras = @search THEN 0
                        ELSE 1
                    END,
                    p.nome
                LIMIT 100;
            ";
            }
            else
            {
                query += " ORDER BY p.nome LIMIT 100;";
            }

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search.Trim());

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                products.Add(new StockProuct
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    valor_custo = reader.GetDecimal(reader.GetOrdinal("preco_custo")),
                    fracionado = reader.GetBoolean(reader.GetOrdinal("fracionado"))
                });
            }

            response.Success = true;
            response.Data = products;
            response.Message = products.Count == 0
                ? "Nenhum produto encontrado."
                : "Produtos encontrados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar produtos: {ex.Message}";
            return StatusCode(500, response);
        }
    }



    [Authorize]
    [HttpGet("Get/Stock")]
    public async Task<IActionResult> FetchStockEntrada([FromQuery] string? numNota, [FromQuery] string? tipo)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<StockEntradaEtity>>();
        var entradas = new List<StockEntradaEtity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            SELECT 
                em.id AS movimentacao_id,
                em.nota,
                em.tipo,
                f.nome AS fornecedor,
                em.data_emissao,
                em.data_ocorrencia,
                em.valor_total,
                em.motivo_saida,
                COUNT(ce.id) AS total_itens
            FROM estoque_movimentacao em
            JOIN controle_estoque ce 
                ON ce.movimentacao_id = em.id
            LEFT JOIN fornecedores f 
                ON f.id = em.fornecedor_id
            WHERE 
              
        ";
            if (!string.IsNullOrEmpty(tipo))
            {
                query += @" em.tipo = @tipo";
            }
            if (!string.IsNullOrEmpty(tipo))
            {
                query += @" AND em.empresa_id = @empresa_id";
            }
            else
            {
                query += @" em.empresa_id = @empresa_id";
            }



            if (!string.IsNullOrWhiteSpace(numNota))
            {
                query += " AND em.nota ILIKE '%' || @search || '%' ";
            }

            query += @"
            GROUP BY 
                em.id,
                em.tipo,
                em.nota,
                f.nome,
                em.data_emissao,
                em.data_ocorrencia,
                em.valor_total
            ORDER BY em.data_ocorrencia DESC
            LIMIT 100;
        ";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(tipo))
            {
                cmd.Parameters.AddWithValue("@tipo", tipo.Trim());
            }

            if (!string.IsNullOrWhiteSpace(numNota))
            {
                cmd.Parameters.AddWithValue("@search", numNota.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                entradas.Add(new StockEntradaEtity
                {
                    movimentacao_id = reader.GetInt32(reader.GetOrdinal("movimentacao_id")),
                    tipo = reader.GetString(reader.GetOrdinal("tipo")).Trim(),
                    nota = reader.IsDBNull(reader.GetOrdinal("nota"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("nota")).Trim(),
                    motivo_saida = reader.IsDBNull(reader.GetOrdinal("motivo_saida"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("motivo_saida")).Trim(),
                    fornecedor = reader.IsDBNull(reader.GetOrdinal("fornecedor"))
                        ? string.Empty
                        : reader.GetString(reader.GetOrdinal("fornecedor")).Trim(),
                    data_emissao = reader.GetDateTime(reader.GetOrdinal("data_emissao")),
                    data_ocorrencia = reader.GetDateTime(reader.GetOrdinal("data_ocorrencia")),
                    valor_total = reader.GetDecimal(reader.GetOrdinal("valor_total")),
                    total_itens = reader.GetInt64(reader.GetOrdinal("total_itens"))
                });
            }

            response.Success = true;
            response.Data = entradas;
            response.Message = entradas.Count == 0
                ? "Nenhuma movimentação de entrada encontrada."
                : "Movimentações de entrada encontradas com sucesso.";

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
    [HttpGet("Get/Stock/Detalhe")]
    public async Task<IActionResult> FetchStockEntradaDetalhe([FromQuery] int movimentacao_id)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<StockEntradaDetalheEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            SELECT
                e.id AS movimentacao_id,
                e.nota,
                e.tipo,
                e.valor_total,
                e.data_emissao,
                e.observacao,
                f.nome AS fornecedor,
                COALESCE(
                    (
                        SELECT jsonb_agg(
                            jsonb_build_object(
                                'produto', p.nome,
                                'quantidade', c.quantidade,
                                'valor_unitario', c.valor_unitario,
                                'sub_total', c.sub_total
                            )
                        )
                        FROM controle_estoque c
                        INNER JOIN produto p ON p.id = c.produto_id
                        WHERE c.movimentacao_id = e.id
                    ),
                    '[]'::jsonb
                ) AS itens
            FROM estoque_movimentacao e
            INNER JOIN fornecedores f ON f.id = e.fornecedor_id
            WHERE e.id = @movimentacao_id
              AND e.empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@movimentacao_id", movimentacao_id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Movimentação não encontrada.";
                return NotFound(response);
            }

            var detalhe = new StockEntradaDetalheEntity
            {
                movimentacao_id = reader.GetInt32(reader.GetOrdinal("movimentacao_id")),
                nota = reader.IsDBNull(reader.GetOrdinal("nota"))
                    ? string.Empty
                    : reader.GetString(reader.GetOrdinal("nota")).Trim(),
                tipo = reader.GetString(reader.GetOrdinal("tipo")).Trim(),
                data_emissao = reader.GetDateTime(reader.GetOrdinal("data_emissao")),
                valor_total = reader.GetDecimal(reader.GetOrdinal("valor_total")),
                observacao = reader.IsDBNull(reader.GetOrdinal("observacao"))
                    ? null
                    : reader.GetString(reader.GetOrdinal("observacao")).Trim(),
                fornecedor = reader.GetString(reader.GetOrdinal("fornecedor")),
                itens = System.Text.Json.JsonSerializer.Deserialize<List<StockEntradaItemEntity>>(
                    reader.GetString(reader.GetOrdinal("itens"))
                )!
            };

            response.Success = true;
            response.Data = detalhe;
            response.Message = "Detalhes da entrada carregados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar detalhes da entrada: {ex.Message}";
            return StatusCode(500, response);
        }
    }
    [Authorize]
    [HttpDelete("Delete/MovimentacaoEstoque/{movimentacaoId:int}")]
    public async Task<IActionResult> ExcluirMovimentacaoEstoque(int movimentacaoId)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();
        var response = new Response<string>();

        try
        {
            // 1️⃣ Buscar tipo da movimentação
            const string sqlMov = @"
            SELECT tipo
            FROM estoque_movimentacao
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            string tipo;

            await using (var cmd = new NpgsqlCommand(sqlMov, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", movimentacaoId);
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);

                var result = await cmd.ExecuteScalarAsync();
                if (result == null)
                    return NotFound(new Response<string>
                    {
                        Success = false,
                        Message = "Movimentação não encontrada."
                    });

                tipo = result.ToString()!;
            }

            // 2️⃣ Buscar itens da movimentação
            const string sqlItens = @"
            SELECT produto_id, quantidade
            FROM controle_estoque
            WHERE movimentacao_id = @movimentacao_id;
        ";

            var itens = new List<(int produtoId, int quantidade)>();

            await using (var cmd = new NpgsqlCommand(sqlItens, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@movimentacao_id", movimentacaoId);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    itens.Add((
                        reader.GetInt32(0),
                        reader.GetInt32(1)
                    ));
                }
            }

            if (itens.Count == 0)
                throw new Exception("Movimentação não possui itens.");

            // 3️⃣ Definir sinal inverso
            // Se foi ENTRADA → remover do saldo
            // Se foi SAIDA → devolver ao saldo
            var sinalReverso = tipo is "ENTRADA" or "AJUSTE_POS" or "DEVOLUCAO" ? -1 : 1;

            foreach (var item in itens)
            {
                // 3.1 🔒 Lock do saldo
                const string lockSaldo = @"
                SELECT saldo
                FROM saldo_estoque
                WHERE empresa_id = @empresa_id
                  AND produto_id = @produto_id
                FOR UPDATE;
            ";

                int saldoAtual;

                await using (var cmdLock = new NpgsqlCommand(lockSaldo, conn, transaction))
                {
                    cmdLock.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdLock.Parameters.AddWithValue("@produto_id", item.produtoId);

                    var result = await cmdLock.ExecuteScalarAsync();
                    saldoAtual = result == null ? 0 : Convert.ToInt32(result);
                }

                // 3.2 Validação de rollback
                if (saldoAtual + (sinalReverso * item.quantidade) < 0)
                {
                    throw new Exception(
                        $"Rollback inválido. Produto {item.produtoId} ficaria com saldo negativo."
                    );
                }

                // 3.3 Atualizar saldo
                const string updateSaldo = @"
                UPDATE saldo_estoque
                SET saldo = saldo + @quantidade
                WHERE empresa_id = @empresa_id
                  AND produto_id = @produto_id;
            ";

                await using (var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction))
                {
                    cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdSaldo.Parameters.AddWithValue("@produto_id", item.produtoId);
                    cmdSaldo.Parameters.AddWithValue("@quantidade", sinalReverso * item.quantidade);
                    await cmdSaldo.ExecuteNonQueryAsync();
                }
            }

            // 4️⃣ Excluir itens
            const string deleteItens = @"
            DELETE FROM controle_estoque
            WHERE movimentacao_id = @movimentacao_id;
        ";

            await using (var cmd = new NpgsqlCommand(deleteItens, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@movimentacao_id", movimentacaoId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 5️⃣ Excluir movimentação
            const string deleteMov = @"
            DELETE FROM estoque_movimentacao
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using (var cmd = new NpgsqlCommand(deleteMov, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", movimentacaoId);
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Movimentação excluída e estoque revertido com sucesso.";
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



}


