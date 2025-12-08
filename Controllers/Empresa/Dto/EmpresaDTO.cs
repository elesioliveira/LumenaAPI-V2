public class LoginDto
{
    public string email { get; set; }
    public string Senha { get; set; }
}

public class EmpresaUsuarioCreateDTO
{
    // Dados da empresa
    public string razao_social { get; set; }
    public string nome_responsavel { get; set; }
    public string cnpj { get; set; }
    public string email { get; set; }
    public string senha { get; set; }
}
