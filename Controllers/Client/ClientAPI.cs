using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;

[ApiController]
[Route("API/V1")]
public class ClientAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public ClientAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Create/Client")]
    public async Task<IActionResult> CreateClient([FromBody] ClientDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();
        var empresaId = User.GetEmpresaId();
        int clientId;

        try
        {
            const string sqlCliente = @"
            INSERT INTO cliente
            (empresa_id, grupo_id, tipo, nome, documento, email, telefone, observacao, ativo)
            VALUES
            (@empresa_id, @grupo_id, @tipo, @nome, @documento, @email, @telefone, @observacao, @ativo)
            RETURNING id;
        ";

            await using (var cmd = new NpgsqlCommand(sqlCliente, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@grupo_id", (object?)dto.grupo_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@documento", (object?)dto.documento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@email", (object?)dto.email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@telefone", (object?)dto.telefone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ativo", dto.ativo);

                clientId = (int)(await cmd.ExecuteScalarAsync()
                    ?? throw new Exception("Falha ao obter ID do cliente."));
            }

            const string sqlEndereco = @"
            INSERT INTO endereco
            (cliente_id, empresa_id, rua, numero, bairro, cidade, uf, complemento, cep, cod_uf)
            VALUES
            (@cliente_id, @empresa_id, @rua, @numero, @bairro, @cidade, @uf, @complemento, @cep,@cod_uf);
        ";

            if (!string.IsNullOrWhiteSpace(dto.rua))
            {
                await using var cmdEndereco = new NpgsqlCommand(sqlEndereco, conn, transaction);
                cmdEndereco.Parameters.AddWithValue("@cliente_id", clientId);
                cmdEndereco.Parameters.AddWithValue("@empresa_id", empresaId);
                cmdEndereco.Parameters.AddWithValue("@rua", (object?)dto.rua ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@numero", (object?)dto.numero ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@bairro", (object?)dto.bairro ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@cidade", (object?)dto.cidade ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@uf", (object?)dto.uf ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@complemento", (object?)dto.complemento ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@cep", (object?)dto.cep ?? DBNull.Value);
                cmdEndereco.Parameters.AddWithValue("@cod_uf", (object?)dto.cod_uf ?? DBNull.Value);

                await cmdEndereco.ExecuteNonQueryAsync();


            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Cliente cadastrado com sucesso.";
            return Ok(response);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = "Já existe um cliente cadastrado com este documento ou e-mail.";
            return BadRequest(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao cadastrar cliente: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/Client")]
    public async Task<IActionResult> UpdateClient([FromBody] ClientDetailsEntity dto)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();
        try
        {
            const string sql = @"
            UPDATE cliente SET
                grupo_id = @grupo_id,
                tipo = @tipo,
                nome = @nome,
                documento = @documento,
                email = @email,
                telefone = @telefone,
                observacao = @observacao,
                ativo = @ativo
            WHERE id = @id and empresa_id=@empresa_id;
        ";
            const string sqlDelete = @"delete from endereco where cliente_id = @cliente_id";

            const string sqlEndereco = @"
            INSERT INTO endereco
            (cliente_id, empresa_id, rua, numero, bairro, cidade, uf, complemento, cep, cod_uf)
            VALUES
            (@cliente_id, @empresa_id, @rua, @numero, @bairro, @cidade, @uf, @complemento, @cep,@cod_uf);
        ";

            await using (var cmd = new NpgsqlCommand(sql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", dto.id);
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@grupo_id", (object?)dto.grupo_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@documento", (object?)dto.documento ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@email", (object?)dto.email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@telefone", (object?)dto.telefone ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ativo", dto.ativo);
                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    response.Success = false;
                    response.Message = "Cliente não encontrado ou não pertence à empresa.";
                    return NotFound(response);
                }
            }

            await using var cmdDeleteEndereco = new NpgsqlCommand(sqlDelete, conn, transaction);
            cmdDeleteEndereco.Parameters.AddWithValue("@cliente_id", dto.id);
            await cmdDeleteEndereco.ExecuteNonQueryAsync();

            await using var cmdEndereco = new NpgsqlCommand(sqlEndereco, conn, transaction);
            cmdEndereco.Parameters.AddWithValue("@cliente_id", dto.id);
            cmdEndereco.Parameters.AddWithValue("@empresa_id", empresaId);
            cmdEndereco.Parameters.AddWithValue("@rua", (object?)dto.rua ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@numero", (object?)dto.numero ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@bairro", (object?)dto.bairro ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@cidade", (object?)dto.cidade ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@uf", (object?)dto.uf ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@complemento", (object?)dto.complemento ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@cep", (object?)dto.cep ?? DBNull.Value);
            cmdEndereco.Parameters.AddWithValue("@cod_uf", (object?)dto.cod_uf ?? DBNull.Value);
            await cmdEndereco.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            response.Success = true;
            response.Message = "Cliente atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Cliente: {ex.Message}";
            return StatusCode(500, response);
        }

    }
    [Authorize]
    [HttpPut("Put/Status/Client")]
    public async Task<IActionResult> StatusUpdateClient([FromBody] ClientStatusDTO dto)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<string>();
        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();
        try
        {
            const string sql = @"
            UPDATE cliente SET
                ativo = @ativo
            WHERE id = @id and empresa_id=@empresa_id;
        ";
            await using (var cmd = new NpgsqlCommand(sql, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", dto.id);
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@ativo", dto.status);
                var rowsAffected = await cmd.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    await transaction.RollbackAsync();
                    response.Success = false;
                    response.Message = "Cliente não encontrado ou não pertence à empresa.";
                    return NotFound(response);
                }
            }
            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Cliente atualizado com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            response.Success = false;
            response.Message = $"Erro ao atualizar Cliente: {ex.Message}";
            return StatusCode(500, response);
        }

    }

    [Authorize]
    [HttpGet("Get/Client")]
    public async Task<IActionResult> FetchClient([FromQuery] string? search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<ClientEntity>>();
        var clients = new List<ClientEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            string sql = @"
            SELECT 
                c.id,
                c.data_cadastro,
                c.ativo,
                c.grupo_id,
                c.tipo,
                c.nome,
                c.documento,
                c.email,
                c.telefone,
                c.observacao,
                (e.cidade || ' - ' || e.uf) AS local,
                g.nome AS grupo
            FROM cliente c
            LEFT JOIN endereco e 
                ON e.cliente_id = c.id 
               AND e.empresa_id = c.empresa_id
            LEFT JOIN grupo_cliente g 
                ON g.id = c.grupo_id 
               AND g.empresa_id = c.empresa_id
            WHERE c.empresa_id = @empresa_id
        ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @"
                AND (
                    c.nome ILIKE '%' || @search || '%'
                    OR c.documento ILIKE '%' || @search || '%'
                    OR c.email ILIKE '%' || @search || '%'
                )
            ";
            }

            sql += @" ORDER BY c.ativo desc, c.nome ASC  LIMIT 100; ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clients.Add(new ClientEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                    grupo_id = reader.IsDBNull(reader.GetOrdinal("grupo_id")) ? null : reader.GetInt32("grupo_id"),
                    tipo = reader.IsDBNull(reader.GetOrdinal("tipo")) ? null : reader.GetInt32(reader.GetOrdinal("tipo")),
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader["nome"] as string,
                    documento = reader.IsDBNull(reader.GetOrdinal("documento")) ? null : reader["documento"] as string,
                    email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader["email"] as string,
                    telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader["telefone"] as string,
                    observacao = reader.IsDBNull(reader.GetOrdinal("observacao")) ? null : reader["observacao"] as string,
                    grupo = reader.IsDBNull(reader.GetOrdinal("grupo")) ? null : reader["grupo"] as string,
                    local = reader.IsDBNull(reader.GetOrdinal("local")) ? null : reader["local"] as string
                });
            }

            response.Success = true;
            response.Data = clients;

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar clientes: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/Client/ById/{clientId}")]
    public async Task<IActionResult> FetchClient(int clientId)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<ClientDetailsEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string sql = @"
            SELECT 
                c.id,
                c.data_cadastro,
                c.ativo,
                c.grupo_id,
                c.tipo,
                c.nome,
                c.documento,
                c.email,
                c.telefone,
                c.observacao,
                e.cidade,
                e.uf,
                e.bairro,
                e.rua,
                e.cep,
                e.numero,
                e.complemento,
                e.cod_uf
            FROM cliente c
            LEFT JOIN endereco e 
                ON e.cliente_id = c.id 
               AND e.empresa_id = c.empresa_id
            WHERE c.empresa_id = @empresa_id
              AND c.id = @cliente_id;
        ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@cliente_id", clientId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Cliente não encontrado.";
                return NotFound(response);
            }

            var client = new ClientDetailsEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
                grupo_id = reader["grupo_id"] as int?,
                tipo = reader.IsDBNull(reader.GetOrdinal("tipo")) ? null : reader.GetInt32(reader.GetOrdinal("tipo")),
                nome = reader["nome"] as string,
                documento = reader["documento"] as string,
                email = reader["email"] as string,
                telefone = reader["telefone"] as string,
                observacao = reader["observacao"] as string,
                cidade = reader["cidade"] as string,
                uf = reader["uf"] as string,
                bairro = reader["bairro"] as string,
                rua = reader["rua"] as string,
                numero = reader["numero"] as string,
                complemento = reader["complemento"] as string,
                cep = reader["cep"] as string,
                cod_uf = reader.IsDBNull(reader.GetOrdinal("cod_uf")) ? null : reader.GetInt32(reader.GetOrdinal("cod_uf"))
            };

            response.Success = true;
            response.Data = client;

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar cliente: {ex.Message}";
            return StatusCode(500, response);
        }
    }


}
