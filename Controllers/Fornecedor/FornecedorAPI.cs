using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;

[ApiController]
[Route("API/V1")]
public class FornecedorController : ControllerBase
{
    private readonly IConfiguration _config;

    public FornecedorController(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [HttpPost("Post/Create/Fornecedor")]
    public async Task<IActionResult> CadastrarFornecedor([FromBody] FornecedorDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        try
        {
            const string queryInsertFornecedor = @"
            INSERT INTO fornecedores
            (
                empresa_id,
                nome,
                contato,
                cnpj,
                cidade,
                bairro,
                endereco,
                email
            )
            VALUES
            (
                @empresa_id,
                @nome,
                @contato,
                @cnpj,
                @cidade,
                @bairro,
                @endereco,
                @email
            );";

            await using (var cmd = new NpgsqlCommand(queryInsertFornecedor, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", dto.empresa_id);
                cmd.Parameters.AddWithValue("@nome", dto.razao_social);
                cmd.Parameters.AddWithValue("@contato", dto.telefone);
                cmd.Parameters.AddWithValue("@cnpj", dto.cnpj);
                cmd.Parameters.AddWithValue("@cidade", dto.cidade);
                cmd.Parameters.AddWithValue("@bairro", dto.bairro);
                cmd.Parameters.AddWithValue("@endereco", dto.endereco);
                cmd.Parameters.AddWithValue("@email", dto.email);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    response.Success = false;
                    response.Message = "Nenhum registro foi inserido.";
                    return BadRequest(response);
                }
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Fornecedor cadastrado com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe um fornecedor cadastrado com este CNPJ.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar fornecedor: {ex.Message}";
            return StatusCode(500, response);
        }
    }


    [HttpGet("Get/Fornecedores")]
    public async Task<IActionResult> BuscarFornecedores([FromQuery] int empresaId)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<FornecedorEntity>>();
        var fornecedores = new List<FornecedorEntity>();

        try
        {
            const string query = @"
            SELECT 
                id,
                empresa_id,
                nome,
                contato,
                cnpj,
                cidade,
                bairro,
                endereco,
                ativo,
                email
            FROM fornecedores
            WHERE empresa_id = 10
              AND ativo = TRUE
            ORDER BY nome
			LIMIT 100;
        ";

            await using (var cmd = new NpgsqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);

                await using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    fornecedores.Add(new FornecedorEntity
                    {
                        id = reader.GetInt32(reader.GetOrdinal("id")),
                        empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                        razao_social = reader.GetString(reader.GetOrdinal("nome")),
                        telefone = reader.IsDBNull(reader.GetOrdinal("contato"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("contato")),
                        cnpj = reader.GetString(reader.GetOrdinal("cnpj")),
                        cidade = reader.IsDBNull(reader.GetOrdinal("cidade"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("cidade")),
                        bairro = reader.IsDBNull(reader.GetOrdinal("bairro"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("bairro")),
                        endereco = reader.IsDBNull(reader.GetOrdinal("endereco"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("endereco")),
                        email = reader.IsDBNull(reader.GetOrdinal("email"))
                            ? null
                            : reader.GetString(reader.GetOrdinal("email")),
                        ativo = reader.GetBoolean(reader.GetOrdinal("ativo"))
                    });
                }
            }

            response.Success = true;
            response.Data = fornecedores;
            response.Message = fornecedores.Count == 0
                ? "Nenhum fornecedor ativo encontrado."
                : "Fornecedores encontrados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar fornecedores: {ex.Message}";
            return StatusCode(500, response);
        }
    }



}
