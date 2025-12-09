using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;

[ApiController]
[Route("API/V1")]
public class EmpresaController : ControllerBase
{
    private readonly IConfiguration _config;

    public EmpresaController(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));


    [HttpPost("Post/Empresa")]
    public async Task<IActionResult> CadastrarEmpresa([FromBody] EmpresaUsuarioCreateDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<string>();

        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            //  Inserir a empresa
            var empresaQuery = @"
                INSERT INTO empresa 
                (razao_social, cnpj, email)
                VALUES
                (@razao_social, @cnpj, @EmailEmpresa)
                RETURNING id;
            ";

            int empresaId;

            await using (var cmd = new NpgsqlCommand(empresaQuery, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@razao_social", dto.razao_social.Trim());
                cmd.Parameters.AddWithValue("@cnpj", dto.cnpj.Trim());
                cmd.Parameters.AddWithValue("@EmailEmpresa", dto.email);

                empresaId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // Gerar HASH + SALT da senha
            var (senhaHash, salt) = PasswordHasher.HashPassword(dto.senha);

            // Inserir o usuário
            var usuarioQuery = @"
                INSERT INTO usuario 
                (empresa_id, nome, email, senha_hash, salt)
                VALUES
                (@empresa_id, @nome, @email, @senha_hash, @salt);
            ";

            await using (var cmd = new NpgsqlCommand(usuarioQuery, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome_responsavel);
                cmd.Parameters.AddWithValue("@email", dto.email);
                cmd.Parameters.AddWithValue("@senha_hash", senhaHash);
                cmd.Parameters.AddWithValue("@salt", salt);

                await cmd.ExecuteNonQueryAsync();
            }

            // Se chegou até aqui, comita
            await transaction.CommitAsync();
            response.Success = true;
            response.Message = "Empresa e usuário cadastrados com sucesso.";
            return Ok(response); ;
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro: campo único duplicado → {ex.Detail}";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar empresa: {ex.Message}";
            return StatusCode(500, response);
        }
    }

}
