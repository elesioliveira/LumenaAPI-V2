public class FormSaleEntity
{
    public int Id { get; set; }
    public string RazaoSocial { get; set; } = string.Empty;

    public List<SimpleOptionDto> Canais { get; set; } = new();
    public List<SimpleOptionDto> Entrega { get; set; } = new();
}


public class ClientSalesEntity
{
    public int id { get; set; }
    public string? nome { get; set; }
    public int desconto { get; set; }
}


public class ProductSalesEntity
{
    public int id { get; set; }
    public string nome { get; set; }
    public decimal preco_venda { get; set; }
    public bool fracionado { get; set; }
}