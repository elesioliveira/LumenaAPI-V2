using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class FornecedorController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly CacheHelper _cacheHelper;

    public FornecedorController(IConfiguration config, CacheHelper cacheHelper)
    {
        _config = config;
        _cacheHelper = cacheHelper;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/Fornecedor")]
    public async Task<IActionResult> CadastrarFornecedor([FromBody] FornecedorDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

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
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome);
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

    [Authorize]
    [HttpPut("Put/Update/Fornecedor")]
    public async Task<IActionResult> UpdateFornecedor([FromBody] FornecedorEntity fornecedor)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();

        try
        {
            var fields = new List<string>();

            if (!string.IsNullOrWhiteSpace(fornecedor.nome))
                fields.Add("nome = @nome");

            if (!string.IsNullOrWhiteSpace(fornecedor.cnpj))
                fields.Add("cnpj = @cnpj");

            if (!string.IsNullOrWhiteSpace(fornecedor.email))
                fields.Add("email = @email");

            if (!string.IsNullOrWhiteSpace(fornecedor.cidade))
                fields.Add("cidade = @cidade");

            if (!string.IsNullOrWhiteSpace(fornecedor.telefone))
                fields.Add("contato = @contato");

            if (!string.IsNullOrWhiteSpace(fornecedor.bairro))
                fields.Add("bairro = @bairro");


            fields.Add("ativo = @ativo");

            if (!string.IsNullOrWhiteSpace(fornecedor.endereco))
                fields.Add("endereco = @endereco");

            if (!fields.Any())
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Nenhum campo válido foi informado para atualização.";
                return BadRequest(response);
            }

            var queryUpdate = $@"
            UPDATE fornecedores
            SET {string.Join(", ", fields)}
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(queryUpdate, conn, transaction);

            if (!string.IsNullOrWhiteSpace(fornecedor.nome))
                cmd.Parameters.AddWithValue("@nome", fornecedor.nome.Trim());

            if (!string.IsNullOrWhiteSpace(fornecedor.cnpj))
                cmd.Parameters.AddWithValue("@cnpj", fornecedor.cnpj.Trim());

            if (!string.IsNullOrWhiteSpace(fornecedor.email))
                cmd.Parameters.AddWithValue("@email", fornecedor.email.Trim());

            if (!string.IsNullOrWhiteSpace(fornecedor.cidade))
                cmd.Parameters.AddWithValue("@cidade", fornecedor.cidade.Trim());

            if (!string.IsNullOrWhiteSpace(fornecedor.telefone))
                cmd.Parameters.AddWithValue("@contato", fornecedor.telefone.Trim());

            if (!string.IsNullOrWhiteSpace(fornecedor.bairro))
                cmd.Parameters.AddWithValue("@bairro", fornecedor.bairro.Trim());

            if (!string.IsNullOrWhiteSpace(fornecedor.endereco))
                cmd.Parameters.AddWithValue("@endereco", fornecedor.endereco.Trim());

            cmd.Parameters.AddWithValue("@ativo", fornecedor.ativo);

            cmd.Parameters.AddWithValue("@id", fornecedor.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Fornecedor não encontrado ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Fornecedor atualizado com sucesso.";
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
            response.Message = $"Erro ao atualizar fornecedor: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Fornecedores")]
    public async Task<IActionResult> BuscarFornecedores([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<FornecedorEntity>>();
        var fornecedores = new List<FornecedorEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
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
            WHERE empresa_id = @empresa_id
        ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                query += " AND nome ILIKE '%' || @search || '%' ";
            }

            query += " ORDER BY nome LIMIT 100;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(search))
            {
                cmd.Parameters.AddWithValue("@search", search.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                fornecedores.Add(new FornecedorEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")),
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

            response.Success = true;
            response.Data = fornecedores;
            response.Message = fornecedores.Count == 0
                ? "Nenhum fornecedor encontrado."
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
