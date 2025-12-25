public class StockEntradaEtity
{
    public int movimentacao_id { get; set; }
    public string nota { get; set; }
    public string tipo { get; set; }
    public string fornecedor { get; set; }
    public DateTime data_emissao { get; set; }
    public DateTime data_ocorrencia { get; set; }
    public decimal valor_total { get; set; }
    public long total_itens { get; set; }
}

public class StockEntradaDetalheEntity
{
    public int movimentacao_id { get; set; }
    public string nota { get; set; }
    public string tipo { get; set; }
    public decimal valor_total { get; set; }
    public DateTime data_emissao { get; set; }
    public string? observacao { get; set; }
    public string fornecedor { get; set; }
    public List<StockEntradaItemEntity> itens { get; set; } = new();
}

public class StockEntradaItemEntity
{
    public string produto { get; set; }
    public int quantidade { get; set; }
    public decimal valor_unitario { get; set; }
    public decimal sub_total { get; set; }
}
