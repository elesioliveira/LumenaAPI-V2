public class UploadCertificadoDTO
{
    public string pfx_base64 { get; set; } = "";
    public string senha { get; set; } = "";
}

public class EmitirNFeDTO
{
    public int modelo { get; set; } = 65;
    public string? natureza_operacao { get; set; }
    public string? dest_cpf_cnpj { get; set; }
    public string? dest_nome { get; set; }
    public int? venda_id { get; set; }
    public List<EmitirNFeItemDTO> itens { get; set; } = new();
    public List<EmitirNFePagamentoDTO> pagamentos { get; set; } = new();
}

public class EmitirNFeItemDTO
{
    public int produto_id { get; set; }
    public string? codigo { get; set; }
    public string? descricao { get; set; }
    public string? ncm { get; set; }
    public string? cest { get; set; }
    public string? cfop { get; set; }
    public string? unidade { get; set; }
    public decimal quantidade { get; set; }
    public decimal valor_unitario { get; set; }
    public decimal valor_total { get; set; }
    public int origem { get; set; }
    public string? icms_cst { get; set; }
    public decimal? icms_aliquota { get; set; }
    public decimal? icms_reducao_bc { get; set; }
    public string? pis_cst { get; set; }
    public decimal? pis_aliquota { get; set; }
    public string? cofins_cst { get; set; }
    public decimal? cofins_aliquota { get; set; }
}

public class EmitirNFePagamentoDTO
{
    public string forma_pagamento { get; set; } = "01";
    public decimal valor { get; set; }
}

public class CancelarNFeDTO
{
    public long nota_fiscal_id { get; set; }
    public string justificativa { get; set; } = "";
}

public class InutilizarNFeDTO
{
    public int modelo { get; set; } = 55;
    public int serie { get; set; } = 1;
    public int numero_inicial { get; set; }
    public int numero_final { get; set; }
    public string justificativa { get; set; } = "";
}

public class ConfigFiscalEmpresaDTO
{
    public string? uf { get; set; }
    public int? regime_tributario { get; set; }
    public string? csc_id { get; set; }
    public string? csc_token { get; set; }
    public int? nfe_ambiente { get; set; }
}

public class CartaCorrecaoDTO
{
    public long nota_fiscal_id { get; set; }
    public string correcao { get; set; } = "";
    public int sequencia { get; set; } = 1;
}
