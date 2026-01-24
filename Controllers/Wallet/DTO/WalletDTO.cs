public class WalletReceberDTO
{
    public int categoria_id {get;set;}
    public int? cliente_id {get;set;}
    public int? fornecedor_id {get;set;}
    public decimal valor_total {get;set;}
    public DateOnly data_vencimento {get;set;}
    public string ?status {get;set;}
    public string ?origem_tipo {get;set;}
    public string ?observacao {get;set;}
    public string ?tipo_pagamento {get;set;}
    public string ?descricao {get;set;}
}

