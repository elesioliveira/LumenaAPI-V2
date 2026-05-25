using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

[ApiController]
[Route("API/V1")]
public class AnaliseAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public AnaliseAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpGet("Get/Analise")]
    public async Task<IActionResult> GetAnalise(
        [FromQuery] DateTime? data_inicio,
        [FromQuery] DateTime? data_fim)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var empresaId = User.GetEmpresaId();
        var response = new Response<AnaliseData>();

        var inicio = data_inicio ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var fim = data_fim ?? inicio.AddMonths(1);

        try
        {
            const string batchQuery = @"
                -- 1) MÉTRICA TICKET MÉDIO
                WITH vendas_mes_atual AS (
                    SELECT 
                        AVG(V.TOTAL) AS ticket_medio,
                        SUM(V.TOTAL) AS total
                    FROM VENDA V
                    WHERE V.EMPRESA_ID = @empresa_id
                    AND V.STATUS = 'Aprovado'
                    AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                    AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                ),
                vendas_mes_anterior AS (
                    SELECT 
                        AVG(V.TOTAL) AS ticket_medio,
                        SUM(V.TOTAL) AS total
                    FROM VENDA V
                    WHERE V.EMPRESA_ID = @empresa_id
                    AND V.STATUS = 'Aprovado'
                    AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE) - INTERVAL '1 month'
                    AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE)
                )
                SELECT 
                    A.ticket_medio AS ticket_mes_atual,
                    B.ticket_medio AS ticket_mes_anterior,
                    CASE 
                        WHEN B.ticket_medio IS NULL OR B.ticket_medio = 0 THEN 0
                        ELSE ROUND(
                            ((A.ticket_medio - B.ticket_medio) / B.ticket_medio) * 100, 2
                        )
                    END AS percentual_variacao
                FROM vendas_mes_atual A
                CROSS JOIN vendas_mes_anterior B;

                -- 2) MARGEM DE LUCRO
                WITH receitas AS (
                    SELECT 
                        COALESCE(SUM(V.TOTAL), 0) AS total_receita
                    FROM VENDA V
                    WHERE V.STATUS = 'Aprovado'
                    AND V.EMPRESA_ID = @empresa_id
                    AND (
                        (V.DATA_CADASTRO >= @data_inicio AND V.DATA_CADASTRO < @data_fim)
                        OR
                        (V.DATA_ATUALIZACAO >= @data_inicio AND V.DATA_ATUALIZACAO < @data_fim)
                    )
                ),
                despesas AS (
                    SELECT 
                        COALESCE(SUM(W.VALOR_TOTAL), 0) AS total_despesa
                    FROM WALLET W
                    WHERE W.STATUS = 'Pago'
                    AND W.EMPRESA_ID = @empresa_id
                    AND (
                        (W.DATA_CADASTRO >= @data_inicio AND W.DATA_CADASTRO < @data_fim)
                        OR
                        (W.DATA_ATUALIZACAO >= @data_inicio AND W.DATA_ATUALIZACAO < @data_fim)
                    )
                )
                SELECT 
                    R.total_receita,
                    D.total_despesa,
                    (R.total_receita - D.total_despesa) AS lucro_liquido,
                    CASE 
                        WHEN R.total_receita = 0 THEN 0
                        ELSE ROUND(
                            ((R.total_receita - D.total_despesa) / R.total_receita) * 100, 2
                        )
                    END AS margem_lucro_percentual
                FROM receitas R
                CROSS JOIN despesas D;

                -- 3) MÉTRICA INADIMPLÊNCIA
                WITH inadimplencia_mes_atual AS (
                    SELECT 
                        COUNT(W.ID) AS total_contas,
                        SUM(CASE WHEN W.STATUS = 'Pendente' THEN 1 ELSE 0 END) AS contas_pendentes,
                        ROUND(
                            SUM(CASE WHEN W.STATUS = 'Pendente' THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(W.ID), 0), 2
                        ) AS porcentagem
                    FROM WALLET W
                    WHERE W.EMPRESA_ID = @empresa_id
                    AND W.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                    AND W.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                ),
                inadimplencia_mes_anterior AS (
                    SELECT 
                        COUNT(W.ID) AS total_contas,
                        SUM(CASE WHEN W.STATUS = 'Pendente' THEN 1 ELSE 0 END) AS contas_pendentes,
                        ROUND(
                            SUM(CASE WHEN W.STATUS = 'Pendente' THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(W.ID), 0), 2
                        ) AS porcentagem
                    FROM WALLET W
                    WHERE W.EMPRESA_ID = @empresa_id
                    AND W.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE) - INTERVAL '1 month'
                    AND W.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE)
                )
                SELECT 
                    A.porcentagem AS porcentagem_mes_atual,
                    B.porcentagem AS porcentagem_mes_anterior,
                    CASE 
                        WHEN B.porcentagem IS NULL OR B.porcentagem = 0 THEN 0
                        ELSE ROUND(
                            ((A.porcentagem - B.porcentagem) / B.porcentagem) * 100, 2
                        )
                    END AS percentual_variacao
                FROM inadimplencia_mes_atual A
                CROSS JOIN inadimplencia_mes_anterior B;

                -- 4) VENDAS POR CANAL
                WITH vendas_canal AS (
                    SELECT 
                        COUNT(V.ID) AS qtd_por_canal,
                        TRIM(CV.NOME) AS nome
                    FROM VENDA V
                    INNER JOIN CANAL_DE_VENDA CV ON CV.ID = V.CANAL_ID
                    WHERE CV.EMPRESA_ID = @empresa_id
                    AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                    AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                    AND TRIM(V.STATUS) = 'Aprovado'
                    GROUP BY CV.ID, CV.NOME
                )
                SELECT 
                    nome,
                    qtd_por_canal,
                    ROUND(qtd_por_canal * 100.0 / SUM(qtd_por_canal) OVER (), 2) AS percentual
                FROM vendas_canal
                ORDER BY qtd_por_canal DESC;

                -- 5) VENDAS POR CATEGORIA
                WITH vendas_categoria AS (
                    SELECT 
                        COUNT(V.ID) AS qtd_por_categoria,
                        TRIM(C.NOME) AS nome
                    FROM VENDA V
                    INNER JOIN ITENS_VENDA I ON I.VENDA_ID = V.ID
                    INNER JOIN PRODUTO P ON P.ID = I.PRODUTO_ID
                    INNER JOIN CATEGORIA C ON C.ID = P.CATEGORIA_ID
                    WHERE C.EMPRESA_ID = @empresa_id
                    AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                    AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                    AND TRIM(V.STATUS) = 'Aprovado'
                    GROUP BY C.ID, C.NOME
                )
                SELECT 
                    nome,
                    qtd_por_categoria,
                    ROUND(qtd_por_categoria * 100.0 / SUM(qtd_por_categoria) OVER (), 2) AS percentual
                FROM vendas_categoria
                ORDER BY qtd_por_categoria DESC;

                -- 6) ANÁLISE DE MARGEM POR PRODUTO
                SELECT 
                    P.ID,
                    TRIM(P.NOME) AS produto,
                    P.PRECO_CUSTO,
                    P.PRECO_VENDA,
                    ROUND(((P.PRECO_VENDA - P.PRECO_CUSTO) / NULLIF(P.PRECO_VENDA, 0)) * 100, 2) AS margem,
                    SUM(IV.QTD) AS qtd_venda_por_produto,
                    ROUND(SUM(IV.QTD) * (P.PRECO_VENDA - P.PRECO_CUSTO), 2) AS lucro_total
                FROM VENDA V
                INNER JOIN ITENS_VENDA IV ON IV.VENDA_ID = V.ID
                INNER JOIN PRODUTO P ON P.ID = IV.PRODUTO_ID
                WHERE V.EMPRESA_ID = @empresa_id
                AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                AND TRIM(V.STATUS) = 'Aprovado'
                GROUP BY P.ID, P.NOME, P.PRECO_CUSTO, P.PRECO_VENDA
                ORDER BY qtd_venda_por_produto DESC
                LIMIT 10;

                -- 7) TOP CLIENTES
                SELECT 
                    C.ID,
                    TRIM(C.NOME) AS cliente,
                    COUNT(V.ID) AS qtd_por_cliente,
                    ROUND(SUM(V.TOTAL), 2) AS total_por_cliente
                FROM VENDA V
                INNER JOIN CLIENTE C ON C.ID = V.CLIENTE_ID
                WHERE V.EMPRESA_ID = @empresa_id
                AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                AND TRIM(V.STATUS) = 'Aprovado'
                GROUP BY C.ID, C.NOME
                ORDER BY total_por_cliente DESC
                LIMIT 10;

                -- 8) EVOLUÇÃO MENSAL COMPARATIVA (ano atual vs anterior)
                SELECT 
                    EXTRACT(MONTH FROM V.DATA_CADASTRO)::int AS mes,
                    SUM(CASE WHEN EXTRACT(YEAR FROM V.DATA_CADASTRO) = EXTRACT(YEAR FROM CURRENT_DATE) THEN V.TOTAL ELSE 0 END) AS receita_ano_atual,
                    SUM(CASE WHEN EXTRACT(YEAR FROM V.DATA_CADASTRO) = EXTRACT(YEAR FROM CURRENT_DATE) - 1 THEN V.TOTAL ELSE 0 END) AS receita_ano_anterior
                FROM VENDA V
                WHERE V.EMPRESA_ID = @empresa_id
                AND V.STATUS = 'Aprovado'
                AND V.DATA_CADASTRO >= DATE_TRUNC('year', CURRENT_DATE) - INTERVAL '1 year'
                AND V.DATA_CADASTRO < DATE_TRUNC('year', CURRENT_DATE) + INTERVAL '1 year'
                GROUP BY EXTRACT(MONTH FROM V.DATA_CADASTRO)
                ORDER BY mes;

                -- 9) RESUMO VENDAS (qtd e total do mês atual)
                SELECT 
                    COUNT(V.ID) AS qtd,
                    COALESCE(SUM(V.TOTAL), 0) AS total
                FROM VENDA V
                WHERE V.EMPRESA_ID = @empresa_id
                AND V.DATA_CADASTRO >= DATE_TRUNC('month', CURRENT_DATE)
                AND V.DATA_CADASTRO < DATE_TRUNC('month', CURRENT_DATE) + INTERVAL '1 month'
                AND TRIM(V.STATUS) = 'Aprovado';
            ";

            await using var cmd = new NpgsqlCommand(batchQuery, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@data_inicio", inicio);
            cmd.Parameters.AddWithValue("@data_fim", fim);

            await using var reader = await cmd.ExecuteReaderAsync();

            // 1) Ticket Médio
            var ticket = new AnaliseTicketMedio();
            if (await reader.ReadAsync())
            {
                ticket.ticket_mes_atual = reader.IsDBNull(reader.GetOrdinal("ticket_mes_atual")) ? 0 : reader.GetDecimal(reader.GetOrdinal("ticket_mes_atual"));
                ticket.ticket_mes_anterior = reader.IsDBNull(reader.GetOrdinal("ticket_mes_anterior")) ? 0 : reader.GetDecimal(reader.GetOrdinal("ticket_mes_anterior"));
                ticket.percentual_variacao = reader.IsDBNull(reader.GetOrdinal("percentual_variacao")) ? 0 : reader.GetDecimal(reader.GetOrdinal("percentual_variacao"));
            }

            // 2) Margem de Lucro
            var margem = new AnaliseMargemLucro();
            await reader.NextResultAsync();
            if (await reader.ReadAsync())
            {
                margem.total_receita = reader.IsDBNull(reader.GetOrdinal("total_receita")) ? 0 : reader.GetDecimal(reader.GetOrdinal("total_receita"));
                margem.total_despesa = reader.IsDBNull(reader.GetOrdinal("total_despesa")) ? 0 : reader.GetDecimal(reader.GetOrdinal("total_despesa"));
                margem.lucro_liquido = reader.IsDBNull(reader.GetOrdinal("lucro_liquido")) ? 0 : reader.GetDecimal(reader.GetOrdinal("lucro_liquido"));
                margem.margem_lucro_percentual = reader.IsDBNull(reader.GetOrdinal("margem_lucro_percentual")) ? 0 : reader.GetDecimal(reader.GetOrdinal("margem_lucro_percentual"));
            }

            // 3) Inadimplência
            var inadimplencia = new AnaliseInadimplencia();
            await reader.NextResultAsync();
            if (await reader.ReadAsync())
            {
                inadimplencia.porcentagem_mes_atual = reader.IsDBNull(reader.GetOrdinal("porcentagem_mes_atual")) ? 0 : reader.GetDecimal(reader.GetOrdinal("porcentagem_mes_atual"));
                inadimplencia.porcentagem_mes_anterior = reader.IsDBNull(reader.GetOrdinal("porcentagem_mes_anterior")) ? 0 : reader.GetDecimal(reader.GetOrdinal("porcentagem_mes_anterior"));
                inadimplencia.percentual_variacao = reader.IsDBNull(reader.GetOrdinal("percentual_variacao")) ? 0 : reader.GetDecimal(reader.GetOrdinal("percentual_variacao"));
            }

            // 4) Vendas por Canal
            var canais = new List<AnaliseVendaCanal>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                canais.Add(new AnaliseVendaCanal
                {
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader.GetString(reader.GetOrdinal("nome")),
                    qtd_por_canal = reader.GetInt64(reader.GetOrdinal("qtd_por_canal")),
                    percentual = reader.IsDBNull(reader.GetOrdinal("percentual")) ? 0 : reader.GetDecimal(reader.GetOrdinal("percentual"))
                });
            }

            // 5) Vendas por Categoria
            var categorias = new List<AnaliseVendaCategoria>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                categorias.Add(new AnaliseVendaCategoria
                {
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader.GetString(reader.GetOrdinal("nome")),
                    qtd_por_categoria = reader.GetInt64(reader.GetOrdinal("qtd_por_categoria")),
                    percentual = reader.IsDBNull(reader.GetOrdinal("percentual")) ? 0 : reader.GetDecimal(reader.GetOrdinal("percentual"))
                });
            }

            // 6) Margem por Produto
            var margemProdutos = new List<AnaliseMargemProduto>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                margemProdutos.Add(new AnaliseMargemProduto
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    produto = reader.IsDBNull(reader.GetOrdinal("produto")) ? null : reader.GetString(reader.GetOrdinal("produto")),
                    preco_custo = reader.IsDBNull(reader.GetOrdinal("preco_custo")) ? 0 : reader.GetDecimal(reader.GetOrdinal("preco_custo")),
                    preco_venda = reader.IsDBNull(reader.GetOrdinal("preco_venda")) ? 0 : reader.GetDecimal(reader.GetOrdinal("preco_venda")),
                    margem = reader.IsDBNull(reader.GetOrdinal("margem")) ? 0 : reader.GetDecimal(reader.GetOrdinal("margem")),
                    qtd_venda_por_produto = reader.GetInt64(reader.GetOrdinal("qtd_venda_por_produto")),
                    lucro_total = reader.IsDBNull(reader.GetOrdinal("lucro_total")) ? 0 : reader.GetDecimal(reader.GetOrdinal("lucro_total"))
                });
            }

            // 7) Top Clientes
            var topClientes = new List<AnaliseTopCliente>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                topClientes.Add(new AnaliseTopCliente
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    cliente = reader.IsDBNull(reader.GetOrdinal("cliente")) ? null : reader.GetString(reader.GetOrdinal("cliente")),
                    qtd_por_cliente = reader.GetInt64(reader.GetOrdinal("qtd_por_cliente")),
                    total_por_cliente = reader.IsDBNull(reader.GetOrdinal("total_por_cliente")) ? 0 : reader.GetDecimal(reader.GetOrdinal("total_por_cliente"))
                });
            }

            // 8) Evolução Mensal Comparativa
            var evolucao = new List<AnaliseEvolucaoMensal>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                evolucao.Add(new AnaliseEvolucaoMensal
                {
                    mes = reader.GetInt32(reader.GetOrdinal("mes")),
                    receita_ano_atual = reader.IsDBNull(reader.GetOrdinal("receita_ano_atual")) ? 0 : reader.GetDecimal(reader.GetOrdinal("receita_ano_atual")),
                    receita_ano_anterior = reader.IsDBNull(reader.GetOrdinal("receita_ano_anterior")) ? 0 : reader.GetDecimal(reader.GetOrdinal("receita_ano_anterior"))
                });
            }

            // 9) Resumo Vendas
            var resumoVendas = new AnaliseResumoVendas();
            await reader.NextResultAsync();
            if (await reader.ReadAsync())
            {
                resumoVendas.qtd = reader.GetInt64(reader.GetOrdinal("qtd"));
                resumoVendas.total = reader.IsDBNull(reader.GetOrdinal("total")) ? 0 : reader.GetDecimal(reader.GetOrdinal("total"));
            }

            var data = new AnaliseData
            {
                ticket_medio = ticket,
                margem_lucro = margem,
                inadimplencia = inadimplencia,
                vendas_por_canal = canais,
                vendas_por_categoria = categorias,
                margem_por_produto = margemProdutos,
                top_clientes = topClientes,
                evolucao_mensal = evolucao,
                resumo_vendas = resumoVendas
            };

            response.Success = true;
            response.Data = data;
            response.Message = "Análise carregada com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao carregar análise: {ex.Message}";
            return StatusCode(500, response);
        }
    }
}

public class AnaliseData
{
    public AnaliseTicketMedio ticket_medio { get; set; } = new();
    public AnaliseMargemLucro margem_lucro { get; set; } = new();
    public AnaliseInadimplencia inadimplencia { get; set; } = new();
    public List<AnaliseVendaCanal> vendas_por_canal { get; set; } = new();
    public List<AnaliseVendaCategoria> vendas_por_categoria { get; set; } = new();
    public List<AnaliseMargemProduto> margem_por_produto { get; set; } = new();
    public List<AnaliseTopCliente> top_clientes { get; set; } = new();
    public List<AnaliseEvolucaoMensal> evolucao_mensal { get; set; } = new();
    public AnaliseResumoVendas resumo_vendas { get; set; } = new();
}

public class AnaliseTicketMedio
{
    public decimal ticket_mes_atual { get; set; }
    public decimal ticket_mes_anterior { get; set; }
    public decimal percentual_variacao { get; set; }
}

public class AnaliseMargemLucro
{
    public decimal total_receita { get; set; }
    public decimal total_despesa { get; set; }
    public decimal lucro_liquido { get; set; }
    public decimal margem_lucro_percentual { get; set; }
}

public class AnaliseInadimplencia
{
    public decimal porcentagem_mes_atual { get; set; }
    public decimal porcentagem_mes_anterior { get; set; }
    public decimal percentual_variacao { get; set; }
}

public class AnaliseVendaCanal
{
    public string? nome { get; set; }
    public long qtd_por_canal { get; set; }
    public decimal percentual { get; set; }
}

public class AnaliseVendaCategoria
{
    public string? nome { get; set; }
    public long qtd_por_categoria { get; set; }
    public decimal percentual { get; set; }
}

public class AnaliseMargemProduto
{
    public int id { get; set; }
    public string? produto { get; set; }
    public decimal preco_custo { get; set; }
    public decimal preco_venda { get; set; }
    public decimal margem { get; set; }
    public long qtd_venda_por_produto { get; set; }
    public decimal lucro_total { get; set; }
}

public class AnaliseTopCliente
{
    public int id { get; set; }
    public string? cliente { get; set; }
    public long qtd_por_cliente { get; set; }
    public decimal total_por_cliente { get; set; }
}

public class AnaliseEvolucaoMensal
{
    public int mes { get; set; }
    public decimal receita_ano_atual { get; set; }
    public decimal receita_ano_anterior { get; set; }
}

public class AnaliseResumoVendas
{
    public long qtd { get; set; }
    public decimal total { get; set; }
}
