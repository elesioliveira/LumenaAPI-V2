public sealed class GrupoClienteEntity
{
    public int id { get; set; }
    public int empresa_id { get; set; }
    public DateTime data_cadastro { get; set; }
    public bool ativo { get; set; }
    public string nome { get; set; } = string.Empty;
    public string? descricao { get; set; }
    public decimal desconto { get; set; }
    public int? clients { get; set; }
}
