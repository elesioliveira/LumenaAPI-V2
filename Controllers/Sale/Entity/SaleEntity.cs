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

public class NewSaleHeader
{
    public int client_id { get; set; }
    public DateTime data_pedido { get; set; }
    public int canal_id { get; set; }
    public int entrega_id { get; set; }
    public string? observacao { get; set; }
    public decimal val_frete { get; set; }
    public float? desconto { get; set; } = 0;
    public decimal total { get; set; }
    public decimal sub_total { get; set; }
    public string? status { get; set; } = "Pendente";
    public List<SaleItens> itens { get; set; } = new();
}

public class SaleItens
{
    public int produto_id { get; set; }
    public decimal qtd { get; set; }
    public decimal val_unitario { get; set; }
    public decimal sub_total { get; set; }
}

public class SalesEntity
{
    public int id { get; set; }
    public string cliente { get; set; }
    public string canal { get; set; }
    public DateTime data_cadastro { get; set; }
    public DateTime data_pedido { get; set; }
    public int itens { get; set; }
    public decimal total { get; set; }
    public string status { get; set; }
    public int ultimo_pedido { get; set; }
}

public class SalesDetailsEntity
{
    public int id { get; set; }
    public int cliente_id { get; set; }
    public int ultimo_pedido { get; set; }
    public string? cliente { get; set; }
    public DateTime data_pedido { get; set; }
    public string? canal { get; set; }
    public int canal_id { get; set; }
    public int entrega_id { get; set; }
    public decimal custo_base { get; set; }
    public string? entrega { get; set; }
    public string? status { get; set; }
    public string? grupo_cliente { get; set; }
    public decimal val_frete { get; set; }
    public decimal total { get; set; }
    public decimal sub_total { get; set; }
    public string? observacao { get; set; }
    public int desconto { get; set; }
    public List<ItensSale> itens { get; set; } = new();
}


public class ItensSale
{
    public int id { get; set; }
    public decimal qtd { get; set; }
    public decimal val_un { get; set; }
    public string produto { get; set; }
    public decimal sub_total { get; set; }
}