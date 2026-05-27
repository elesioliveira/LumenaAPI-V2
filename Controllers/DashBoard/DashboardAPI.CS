using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class DashboardAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public DashboardAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpGet("Get/Dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var empresaId = User.GetEmpresaId();
        var response = new Response<DashboardData>();

        try
        {
            const string batchQuery = @"
                -- KPI CARDS
                SELECT 
                    COALESCE(SUM(V.total), 0) AS receita_total,
                    SUM(CASE WHEN V.STATUS NOT IN ('Pendente','Cancelada') THEN 1 ELSE 0 END) AS qtd_pedido,
                    COUNT(DISTINCT C.ID) AS qtd_cliente,
                    COUNT(DISTINCT P.ID) AS qtd_produto
                FROM VENDA V
                INNER JOIN CLIENTE C ON C.empresa_id = V.empresa_id
                INNER JOIN produto P ON P.empresa_id = V.empresa_id
                WHERE V.empresa_id = @empresa_id;

                -- GRÁFICO LINHA (vendas aprovadas por mês no ano corrente)
                SELECT 
                    EXTRACT(YEAR FROM V.data_cadastro)::int AS ano,
                    EXTRACT(MONTH FROM V.data_cadastro)::int AS mes,
                    SUM(V.TOTAL) AS receita
                FROM VENDA V
                WHERE V.data_cadastro >= DATE_TRUNC('year', CURRENT_DATE)
                  AND V.data_cadastro < CURRENT_DATE + INTERVAL '1 day'
                  AND V.status = 'Aprovado'
                  AND V.empresa_id = @empresa_id
                GROUP BY 
                    EXTRACT(YEAR FROM V.data_cadastro),
                    EXTRACT(MONTH FROM V.data_cadastro)
                ORDER BY 
                    EXTRACT(YEAR FROM V.data_cadastro),
                    EXTRACT(MONTH FROM V.data_cadastro);

                -- PEDIDOS RECENTES
                SELECT 
                    V.ID AS pedido,
                    TRIM(C.NOME) AS cliente,
                    TRIM(C.TELEFONE) AS contato,
                    TRIM(V.STATUS) AS status,
                    TO_CHAR(V.DATA_CADASTRO, 'DD/MM/YYYY HH24:MI') AS data_cadastro
                FROM VENDA V
                INNER JOIN CLIENTE C ON C.ID = V.CLIENTE_ID
                WHERE V.EMPRESA_ID = @empresa_id
                ORDER BY V.DATA_CADASTRO DESC
                LIMIT 15;

                -- TOP 10 PRODUTOS MAIS VENDIDOS
                SELECT 
                    SUM(IV.qtd) AS qtd_por_produto,
                    TRIM(P.NOME) AS produto
                FROM VENDA V
                INNER JOIN ITENS_VENDA IV ON IV.VENDA_ID = V.ID
                INNER JOIN PRODUTO P ON P.ID = IV.PRODUTO_ID
                WHERE V.EMPRESA_ID = @empresa_id
                  AND V.STATUS = 'Aprovado'
                GROUP BY P.id, P.NOME
                ORDER BY qtd_por_produto DESC
                LIMIT 10;

                -- DESPESAS POR MÊS (ano corrente, baseado na wallet)
                SELECT 
                    EXTRACT(YEAR FROM W.data_cadastro)::int AS ano,
                    EXTRACT(MONTH FROM W.data_cadastro)::int AS mes,
                    COALESCE(SUM(W.valor_total), 0) AS despesa
                FROM wallet W
                INNER JOIN categoria_wallet CW ON CW.id = W.categoria_id AND CW.empresa_id = W.empresa_id
                WHERE W.data_cadastro >= DATE_TRUNC('year', CURRENT_DATE)
                  AND W.data_cadastro < CURRENT_DATE + INTERVAL '1 day'
                  AND W.empresa_id = @empresa_id
                  AND CW.tipo = 'Despesa'
                  AND W.status = 'Pago'
                GROUP BY 
                    EXTRACT(YEAR FROM W.data_cadastro),
                    EXTRACT(MONTH FROM W.data_cadastro)
                ORDER BY 
                    EXTRACT(YEAR FROM W.data_cadastro),
                    EXTRACT(MONTH FROM W.data_cadastro);
            ";

            await using var cmd = new NpgsqlCommand(batchQuery, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            // 1) KPI Cards
            var kpi = new DashboardKpi();
            if (await reader.ReadAsync())
            {
                kpi.receita_total = reader.IsDBNull(reader.GetOrdinal("receita_total")) ? 0 : reader.GetDecimal(reader.GetOrdinal("receita_total"));
                kpi.qtd_pedido = reader.GetInt64(reader.GetOrdinal("qtd_pedido"));
                kpi.qtd_cliente = reader.GetInt64(reader.GetOrdinal("qtd_cliente"));
                kpi.qtd_produto = reader.GetInt64(reader.GetOrdinal("qtd_produto"));
            }

            // 2) Gráfico linha
            var grafico = new List<DashboardGraficoLinha>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                grafico.Add(new DashboardGraficoLinha
                {
                    ano = reader.GetInt32(reader.GetOrdinal("ano")),
                    mes = reader.GetInt32(reader.GetOrdinal("mes")),
                    receita = reader.GetInt64(reader.GetOrdinal("receita"))
                });
            }

            // 3) Pedidos recentes
            var pedidos = new List<DashboardPedidoRecente>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                pedidos.Add(new DashboardPedidoRecente
                {
                    pedido = reader.GetInt32(reader.GetOrdinal("pedido")),
                    cliente = reader.IsDBNull(reader.GetOrdinal("cliente")) ? null : reader.GetString(reader.GetOrdinal("cliente")),
                    contato = reader.IsDBNull(reader.GetOrdinal("contato")) ? null : reader.GetString(reader.GetOrdinal("contato")),
                    status = reader.IsDBNull(reader.GetOrdinal("status")) ? null : reader.GetString(reader.GetOrdinal("status")),
                    data_cadastro = reader.IsDBNull(reader.GetOrdinal("data_cadastro")) ? null : reader.GetString(reader.GetOrdinal("data_cadastro"))
                });
            }

            // 4) Top 10 produtos
            var topProdutos = new List<DashboardTopProduto>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                topProdutos.Add(new DashboardTopProduto
                {
                    qtd_por_produto = reader.GetInt64(reader.GetOrdinal("qtd_por_produto")),
                    produto = reader.IsDBNull(reader.GetOrdinal("produto")) ? null : reader.GetString(reader.GetOrdinal("produto"))
                });
            }

            // 5) Despesas por mês
            var despesas = new List<DashboardDespesaMensal>();
            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                despesas.Add(new DashboardDespesaMensal
                {
                    ano = reader.GetInt32(reader.GetOrdinal("ano")),
                    mes = reader.GetInt32(reader.GetOrdinal("mes")),
                    despesa = reader.GetDecimal(reader.GetOrdinal("despesa"))
                });
            }

            var data = new DashboardData
            {
                kpi = kpi,
                grafico_linha = grafico,
                pedidos_recentes = pedidos,
                top_produtos = topProdutos,
                despesas = despesas
            };

            response.Success = true;
            response.Data = data;
            response.Message = "Dashboard carregado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao carregar dashboard: {ex.Message}";
            return StatusCode(500, response);
        }
    }

}

public class DashboardData
{
    public DashboardKpi kpi { get; set; } = new();
    public List<DashboardGraficoLinha> grafico_linha { get; set; } = new();
    public List<DashboardPedidoRecente> pedidos_recentes { get; set; } = new();
    public List<DashboardTopProduto> top_produtos { get; set; } = new();
    public List<DashboardDespesaMensal> despesas { get; set; } = new();
}

public class DashboardKpi
{
    public decimal receita_total { get; set; }
    public long qtd_pedido { get; set; }
    public long qtd_cliente { get; set; }
    public long qtd_produto { get; set; }
}

public class DashboardGraficoLinha
{
    public int ano { get; set; }
    public int mes { get; set; }
    public long receita { get; set; }
}

public class DashboardPedidoRecente
{
    public int pedido { get; set; }
    public string? cliente { get; set; }
    public string? contato { get; set; }
    public string? status { get; set; }
    public string? data_cadastro { get; set; }
}

public class DashboardTopProduto
{
    public long qtd_por_produto { get; set; }
    public string? produto { get; set; }
}

public class DashboardDespesaMensal
{
    public int ano { get; set; }
    public int mes { get; set; }
    public decimal despesa { get; set; }
}
