
public class CategoryWalletDTO
{
    public string nome {get;set;}
    public string tipo {get;set;}
    public string? descricao {get;set;}
    public string cor {get;set;}
    public bool ?ativo {get;set;}
}

public class CategoryActiveCountDTO
{
    public int TotalReceita { get; set; }
    public int TotalDespesa { get; set; }
}
