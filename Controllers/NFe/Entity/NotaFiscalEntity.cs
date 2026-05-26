public class NotaFiscalEntity
{
    public long id { get; set; }
    public int modelo { get; set; }
    public int serie { get; set; }
    public int numero { get; set; }
    public string? chave_acesso { get; set; }
    public string? natureza_operacao { get; set; }
    public DateTime? data_emissao { get; set; }
    public DateTime? data_autorizacao { get; set; }
    public string? dest_cpf_cnpj { get; set; }
    public string? dest_nome { get; set; }
    public string? dest_uf { get; set; }
    public decimal? valor_produtos { get; set; }
    public decimal? valor_total { get; set; }
    public string? status { get; set; }
    public string? protocolo_autorizacao { get; set; }
    public string? motivo_rejeicao { get; set; }
    public int ambiente { get; set; }
    public int? venda_id { get; set; }
    public DateTime data_cadastro { get; set; }
}

public class CertificadoDigitalEntity
{
    public long id { get; set; }
    public string? razao_social { get; set; }
    public string? cnpj { get; set; }
    public DateTime? validade_inicio { get; set; }
    public DateTime? validade_fim { get; set; }
    public DateTime data_upload { get; set; }
    public bool valido { get; set; }
}
