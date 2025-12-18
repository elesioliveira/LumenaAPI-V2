public class EntregaEntity
{
    public int id { get; set; }
    public string nome { get; set; }
    public string? prazo { get; set; }
    public decimal custo_base { get; set; }
    public bool ativo { get; set; }
    public DateTime? data_cadastro { get; set; }
}