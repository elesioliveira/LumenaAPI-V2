public class ProductDTO
{
    public string? nome { get; set; }
    public string? descricao { get; set; }
    public bool ativo { get; set; }
    public string? un { get; set; }
    public string? eanCode { get; set; }
    public long marca_id { get; set; }
    public long fornecedor_id { get; set; }
    public long categoria_id { get; set; }
    public decimal? preco_custo { get; set; }
    public decimal? preco_venda { get; set; }
    public decimal? estoque_minimo { get; set; }

}