public sealed class ClientEntity
{
    public int id { get; set; }
    public DateTime data_cadastro { get; set; }
    public bool ativo { get; set; }
    public int? grupo_id { get; set; }
    public int? tipo { get; set; }
    public string? nome { get; set; }
    public string? documento { get; set; }
    public string? email { get; set; }
    public string? telefone { get; set; }
    public string? observacao { get; set; }
    public string? grupo { get; set; }
    public string? local { get; set; }
}

public sealed class ClientDetailsEntity
{
    public int id { get; set; }
    public DateTime data_cadastro { get; set; }
    public bool ativo { get; set; }
    public int? grupo_id { get; set; }
    public int? tipo { get; set; }
    public string? nome { get; set; }
    public string? documento { get; set; }
    public string? email { get; set; }
    public string? telefone { get; set; }
    public string? observacao { get; set; }
    public string? cidade { get; set; }
    public string? uf { get; set; }
    public string? bairro { get; set; }
    public string? rua { get; set; }
    public string? numero { get; set; }
    public string? complemento { get; set; }
    public string? cep { get; set; }
    public int? cod_uf { get; set; }
}
