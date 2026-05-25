public class CfopEntity
{
    public long id { get; set; }
    public string? codigo { get; set; }
    public string? descricao { get; set; }
    public string? natureza_operacao { get; set; }
    public string? tipo_operacao { get; set; }
    public bool ativo { get; set; }
    public DateTime data_cadastro { get; set; }
}
