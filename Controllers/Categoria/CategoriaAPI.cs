using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class CategoriaController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly CacheHelper _cacheHelper;

    public CategoriaController(IConfiguration config, CacheHelper cacheHelper)
    {
        _config = config;
        _cacheHelper = cacheHelper;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/Category")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryDTO dto)
    {
        await using var conn = NovaConexao(); // deve retornar NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            _cacheHelper.RemoveByEmpresa(User.GetEmpresaId());
            const string queryInsertFornecedor = @"insert into categoria (empresa_id,nome, descricao) values (@empresa_id,@nome, @descricao)";

            await using (var cmd = new NpgsqlCommand(queryInsertFornecedor, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@nome", dto.nome);
                cmd.Parameters.AddWithValue("@descricao", string.IsNullOrEmpty(dto.descricao) ? DBNull.Value : dto.descricao.Trim());



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
            response.Message = "Categoria cadastrado com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe uma categoria cadastrado com este nome.";
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
    [HttpPut("Put/Update/Category")]
    public async Task<IActionResult> UpdateCategory([FromBody] CategoryEntity dto)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();

        try
        {
            _cacheHelper.RemoveByEmpresa(User.GetEmpresaId());
            var fields = new List<string>();

            if (!string.IsNullOrWhiteSpace(dto.nome))
                fields.Add("nome = @nome");

            if (!string.IsNullOrWhiteSpace(dto.descricao))
                fields.Add("descricao = @descricao");


            fields.Add("ativo = @ativo");


            if (!fields.Any())
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Nenhum campo válido foi informado para atualização.";
                return BadRequest(response);
            }

            var queryUpdate = $@"
            UPDATE categoria
            SET {string.Join(", ", fields)}
            WHERE id = @id
              AND empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(queryUpdate, conn, transaction);

            if (!string.IsNullOrWhiteSpace(dto.nome))
                cmd.Parameters.AddWithValue("@nome", dto.nome.Trim());

            if (!string.IsNullOrWhiteSpace(dto.descricao))
                cmd.Parameters.AddWithValue("@descricao", dto.descricao.Trim());

            cmd.Parameters.AddWithValue("@ativo", dto.ativo);

            cmd.Parameters.AddWithValue("@id", dto.id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync();
                response.Success = false;
                response.Message = "Categoria não encontrado ou não pertence à empresa.";
                return NotFound(response);
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Categoria atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Categoria: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Category")]
    public async Task<IActionResult> FetchCategory([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<CategoryEntity>>();
        var fornecedores = new List<CategoryEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
SELECT
    c.id,
    c.empresa_id,
    c.nome,
    c.descricao,
    c.data_cadastro,
    c.ativo,
    COUNT(p.id) AS qtd
FROM categoria c
LEFT JOIN produto p
    ON p.categoria_id = c.id
WHERE c.empresa_id = @empresa_id

        ";


            if (!string.IsNullOrWhiteSpace(search))
            {
                query += " AND c.nome ILIKE '%' || @search || '%' ";
            }

            query += " GROUP BY c.id,c.empresa_id, c.nome, c.descricao, c.data_cadastro, c.ativo ORDER BY c.nome LIMIT 100;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrEmpty(search))
            {
                cmd.Parameters.AddWithValue("@search", search.Trim());
            }

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                fornecedores.Add(new CategoryEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    empresa_id = reader.GetInt32(reader.GetOrdinal("empresa_id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString(reader.GetOrdinal("descricao")).Trim(),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    qtd = reader.GetInt32("qtd")
                });
            }

            response.Success = true;
            response.Data = fornecedores;
            response.Message = fornecedores.Count == 0
                ? "Nenhuma categoria encontrado."
                : "Categorias encontradas com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar Categorias: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
