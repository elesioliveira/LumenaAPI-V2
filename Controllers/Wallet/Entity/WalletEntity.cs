
public class WalletEntity
{
    public int id {get;set;}
    public DateTime data_cadastro {get;set;}
    public DateOnly vencimento {get;set;}
    public string? descricao {get;set;}
    public string? cliente {get;set;}
    public string? fornecedor {get;set;}
    public int ? cliente_id {get;set;}
    public int ? fornecedor_id {get;set;}
    public int categoria_id {get;set;}
    public string? categoria {get;set;}
    public string? status {get;set;}
    public decimal valor {get;set;}
    public string?  tipo_pagamento {get;set;}
    public string?  observacao {get;set;}
    public string?  origem_tipo {get;set;}
}

public class DashBoardWalletEntity
{
    public int qtd_aberto {get;set;}
    public decimal total_aberto {get;set;}
    public decimal qtd_vencido {get;set;}
    public decimal total_vencido {get;set;}
}

public class WalletFornecedorCategoriaDto
{
    public WalletFornecedorDto fornecedor {get;set;}
    public WalletCategoriaDto categoria {get;set;}
    
}
public class WalletClienteCategoriaDto
{
    public WalletClienteDto cliente {get;set;}
    public WalletCategoriaDto categoria {get;set;}
    
}

public class WalletClienteDto
{
    public int id {get;set;}
    public string nome {get;set;}
}
public class WalletFornecedorDto
{
    public int id {get;set;}
    public string nome {get;set;}
}
public class WalletCategoriaDto
{
    public int id {get;set;}
    public string nome {get;set;}
}


public class CategoriasAtivasWallet
{
    public int despesa_ativa {get;set;}
    public int receita_ativa {get;set;}
}

public class WalletResumoFinanceiroDto
{
    public decimal total_entrada { get; set; }
    public decimal total_saida { get; set; }
    public decimal saldo => total_entrada - total_saida;
}

public class WalletResumoFinanceiroRegistroDTO
{
    public int id {get;set;}
    public DateTime? data_cadastro {get;set;}
    public DateOnly? data_vencimento {get;set;}
    public string ?descricao {get;set;}
    public string ?cor {get;set;}
    public string ?nome {get;set;}
    public string ?origem_tipo {get;set;}
    public string ?status {get;set;}
    public decimal? valor_total {get;set;}

}