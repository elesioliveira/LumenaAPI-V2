
public class ProductEntity
{
    public long id { get; set; }
    public DateTime data_cadastro { get; set; }
    public string? nome { get; set; }
    public string? descricao { get; set; }
    public bool ativo { get; set; }
    public string? un { get; set; }
    public string? sku { get; set; }
    public string? eanCode { get; set; }
    public string? marca { get; set; }
    public long? marca_id { get; set; }
    public string? fornecedor { get; set; }
    public long? fornecedor_id { get; set; }
    public long? categoria_id { get; set; }
    public string? categoria { get; set; }
    public decimal? preco_custo { get; set; }
    public decimal? preco_venda { get; set; }
    public decimal? estoque_minimo { get; set; }

}

public class LookupItem
{
    public int id { get; set; }
    public string nome { get; set; }
}


public class FormProduct
{
    public List<LookupItem> categorias { get; set; } = new();
    public List<LookupItem> fornecedores { get; set; } = new();
    public List<LookupItem> marcas { get; set; } = new();
}
