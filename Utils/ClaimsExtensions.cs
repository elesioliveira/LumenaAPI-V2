using System.Security.Claims;

public static class ClaimsExtensions
{
    public static int GetEmpresaId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirstValue("empresaId"));

    public static int GetUserId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirstValue("userId"));
}
