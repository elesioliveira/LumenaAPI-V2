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

public class EmpresaUpdateDTO
{
    public string? razao_social { get; set; }
    public string? nome_fantasia { get; set; }
    public string? cnpj { get; set; }
    public string? email { get; set; }
    public string? ie { get; set; }
    public string? im { get; set; }
    public string? telefone { get; set; }
    public string? celular { get; set; }
    public string? website { get; set; }
    public string? cep { get; set; }
    public string? logradouro { get; set; }
    public string? numero { get; set; }
    public string? complemento { get; set; }
    public string? bairro { get; set; }
    public string? cidade { get; set; }
    public string? uf { get; set; }
    public string? observacao { get; set; }

    public IFormFile? logo { get; set; }
}
