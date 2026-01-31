public class UsuarioEntity
{
    public int id { get; set; }
    public int empresaid { get; set; }
    public DateTime datacadastro { get; set; }
    public string? nome { get; set; }
    public string? email { get; set; }
    public bool ativo { get; set; }
    
    public List<int> rotas { get; set; } = new();
}
