
public class UserDTO
{
    public string nome {get;set;}
    public string email {get;set;}
    public string senha {get;set;}
    public bool ativo {get;set;} =true;
    public string perfil {get;set;}
}
public class UserUpdateDTO
{
    public string nome { get; set; } = string.Empty;
    public string email { get; set; } = string.Empty;
    public bool ativo { get; set; }
    public string perfil { get; set; } = string.Empty;
    public string? senha { get; set; } // opcional
}


