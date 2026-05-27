using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Npgsql;

public class ValidarLicencaFilter : IAsyncActionFilter
{
    private readonly IConfiguration _config;

    public ValidarLicencaFilter(IConfiguration config)
    {
        _config = config;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var hasAuthorize = endpoint?.Metadata.GetMetadata<AuthorizeAttribute>() != null;

        if (!hasAuthorize || context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var empresaId = context.HttpContext.User.GetEmpresaId();
        var usuarioId = context.HttpContext.User.GetUserId();

        const string sql = @"
            SELECT E.data_licenca
            FROM empresa E
            INNER JOIN usuario U ON U.empresa_id = E.id
            WHERE U.id = @id_user AND E.id = @empresa_id;
        ";

        await using var conn = new NpgsqlConnection(_config.GetConnectionString("DefaultConnection"));
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id_user", usuarioId);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);

        var result = await cmd.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
        {
            context.Result = new NotFoundObjectResult(new Response<string>
            {
                Success = false,
                Message = "Empresa não encontrada."
            });
            return;
        }

        var dataLicenca = Convert.ToDateTime(result);
        if (dataLicenca < DateTime.Now.Date)
        {
            context.Result = new ObjectResult(new Response<string>
            {
                Success = false,
                Message = "Licença expirada. Entre em contato com o suporte."
            })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }
}
