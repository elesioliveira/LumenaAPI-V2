
public class UserEntityV2
{
    public int id { get; set; }
    public string? nome { get; set; }
    public string? email { get; set; }
    public string? perfil { get; set; }
    public string? status { get; set; }
    public DateTime ultimo_acesso { get; set; }
    public List<int> rotas {get;set;} = new();
}

public class DashBoardUserDTO
{
    public long qtd_usuario {get;set;}
    public long qtd_ativo {get;set;}
    public long qtd_inativo {get;set;}
    public long qtd_administrador {get;set;}
}