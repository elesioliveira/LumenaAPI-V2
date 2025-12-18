public class CanalVendaEntity
{
    public long id { get; set; }
    public long? empresa_id { get; set; }
    public string nome { get; set; }
    public string? tipo { get; set; }
    public bool ativo { get; set; }
    public DateTime? data_cadastro { get; set; }
}