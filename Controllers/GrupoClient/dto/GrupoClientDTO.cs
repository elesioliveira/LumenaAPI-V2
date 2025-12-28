public sealed class GrupoClienteDTO
{
    public string nome { get; set; } = string.Empty;
    public string? descricao { get; set; }
    public decimal? desconto { get; set; } = 0;
    public bool ativo { get; set; } = true;
}
