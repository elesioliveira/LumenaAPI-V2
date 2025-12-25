public class EstoqueItemDTO
{
    public int produto_id { get; set; }
    public int quantidade { get; set; }
    public decimal valor_unitario { get; set; }
    public decimal sub_total { get; set; }
}

public class MovimentarEstoqueDTO
{
    public string tipo { get; set; } // ENTRADA | SAIDA | AJUSTE_POS | AJUSTE_NEG | DEVOLUCAO
    public string? nota { get; set; }
    public int? fornecedor_id { get; set; }
    public DateTime? data_emissao { get; set; }
    public DateTime? data_ocorrencia { get; set; }
    public string? observacao { get; set; }
    public decimal? valor_total { get; set; } = 0;
    public string? motivo_saida { get; set; }
    public List<EstoqueItemDTO> itens { get; set; } = new();
}


public class StockFornecedor
{
    public int id { get; set; }
    public string nome { get; set; }
}

public class StockProuct
{
    public int id { get; set; }
    public string? nome { get; set; }
    public decimal valor_custo { get; set; }
    public bool fracionado { get; set; }

}