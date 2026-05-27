using Microsoft.AspNetCore.Authorization;
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
        // ---------- EMPRESA ----------
        const string empresaQuery = @"
            INSERT INTO empresa (razao_social, cnpj, email)
            VALUES (@razao_social, @cnpj, @email)
            RETURNING id;
        ";

        int empresaId;
        await using (var cmd = new NpgsqlCommand(empresaQuery, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@razao_social", dto.razao_social.Trim());
            cmd.Parameters.AddWithValue("@cnpj", dto.cnpj.Trim());
            cmd.Parameters.AddWithValue("@email", dto.email);

            empresaId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // ---------- USUÁRIO ADMIN ----------
        var (senhaHash, salt) = PasswordHasher.HashPassword(dto.senha);

        const string usuarioQuery = @"
            INSERT INTO usuario
            (empresa_id, nome, email, senha_hash, salt, perfil, ativo)
            VALUES
            (@empresa_id, @nome, @email, @senha_hash, @salt, 'Administrador', true)
            RETURNING id;
        ";

        int usuarioId;
        await using (var cmd = new NpgsqlCommand(usuarioQuery, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@nome", dto.nome_responsavel);
            cmd.Parameters.AddWithValue("@email", dto.email);
            cmd.Parameters.AddWithValue("@senha_hash", senhaHash);
            cmd.Parameters.AddWithValue("@salt", salt);

            usuarioId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        // ---------- CATEGORIAS ----------
        const string insertCategorias = @"
            INSERT INTO categoria_wallet (empresa_id, nome, tipo, descricao, cor)
            VALUES
            (@empresa_id, 'Vendas', 'Receita', 'Vendas feitas pelo PDV', '#00FF9C'),
            (@empresa_id, 'Fornecedores', 'Despesa', 'Entrada de nota pelo sistema', '#FF2E2E');
        ";

        await using (var cmd = new NpgsqlCommand(insertCategorias, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ---------- SEQUÊNCIA DE PEDIDOS ----------
        const string insertSequencia = @"
            INSERT INTO empresa_sequencia (empresa_id, ultimo_pedido)
            VALUES (@empresa_id, 0);
        ";

        await using (var cmd2 = new NpgsqlCommand(insertSequencia, conn, transaction))
        {
            cmd2.Parameters.AddWithValue("@empresa_id", empresaId);
            await cmd2.ExecuteNonQueryAsync();
        }

        // ---------- ROTAS (ADMIN = TODAS) ----------
        const string insertRotas = @"
            INSERT INTO rota_usuario (rota, id_usuario)
            SELECT r, @id_usuario
            FROM generate_series(0, 9) AS r;
        ";

        await using (var cmd = new NpgsqlCommand(insertRotas, conn, transaction))
        {
            cmd.Parameters.AddWithValue("@id_usuario", usuarioId);
            await cmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();

        response.Success = true;
        response.Message = "Empresa e usuário administrador cadastrados com sucesso.";
        return Ok(response);
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

    [Authorize]
    [HttpGet("Get/Company")]
    public async Task<IActionResult> FetchCompany()
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<EmpresaEntity>();
        EmpresaEntity empresa = new();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            select razao_social, cnpj, email, ie, im, telefone, celular, website, cep, logradouro, numero, complemento, bairro, cidade, uf, observacao, data_licenca, nome_fantasia, logo
            from empresa where id = @id
             ";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
              empresa.razao_social= reader.IsDBNull(reader.GetOrdinal("razao_social"))? null : reader.GetString(reader.GetOrdinal("razao_social"));
              empresa.cnpj= reader.IsDBNull(reader.GetOrdinal("cnpj"))? null : reader.GetString(reader.GetOrdinal("cnpj"));
              empresa.email= reader.IsDBNull(reader.GetOrdinal("email"))? null : reader.GetString(reader.GetOrdinal("email"));
              empresa.ie= reader.IsDBNull(reader.GetOrdinal("ie"))? null : reader.GetString(reader.GetOrdinal("ie"));
              empresa.im= reader.IsDBNull(reader.GetOrdinal("im"))? null : reader.GetString(reader.GetOrdinal("im"));
              empresa.telefone= reader.IsDBNull(reader.GetOrdinal("telefone"))? null : reader.GetString(reader.GetOrdinal("telefone"));
              empresa.celular= reader.IsDBNull(reader.GetOrdinal("celular"))? null : reader.GetString(reader.GetOrdinal("celular"));
              empresa.website= reader.IsDBNull(reader.GetOrdinal("website"))? null : reader.GetString(reader.GetOrdinal("website"));
              empresa.cep= reader.IsDBNull(reader.GetOrdinal("cep"))? null : reader.GetString(reader.GetOrdinal("cep"));
              empresa.logradouro= reader.IsDBNull(reader.GetOrdinal("logradouro"))? null : reader.GetString(reader.GetOrdinal("logradouro"));
              empresa.numero= reader.IsDBNull(reader.GetOrdinal("numero"))? null : reader.GetString(reader.GetOrdinal("numero"));
              empresa.complemento= reader.IsDBNull(reader.GetOrdinal("complemento"))? null : reader.GetString(reader.GetOrdinal("complemento"));
              empresa.bairro= reader.IsDBNull(reader.GetOrdinal("bairro"))? null : reader.GetString(reader.GetOrdinal("bairro"));
              empresa.cidade= reader.IsDBNull(reader.GetOrdinal("cidade"))? null : reader.GetString(reader.GetOrdinal("cidade"));
              empresa.uf= reader.IsDBNull(reader.GetOrdinal("uf"))? null : reader.GetString(reader.GetOrdinal("uf"));
              empresa.observacao= reader.IsDBNull(reader.GetOrdinal("observacao"))? null : reader.GetString(reader.GetOrdinal("observacao"));
              empresa.fantasia= reader.IsDBNull(reader.GetOrdinal("nome_fantasia"))? null : reader.GetString(reader.GetOrdinal("nome_fantasia"));
              empresa.logo= reader.IsDBNull(reader.GetOrdinal("logo"))? null : reader.GetString(reader.GetOrdinal("logo"));
              empresa.data_licenca= reader.IsDBNull(reader.GetOrdinal("data_licenca"))? null : reader.GetDateTime(reader.GetOrdinal("data_licenca"));
            }

            response.Success = true;
            response.Data = empresa;
            response.Message = "Empresa encontrada com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar Entregas: {ex.Message}";
            return StatusCode(500, response);
        }
    }

[Authorize]
[HttpPut("Update/Company")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> UpdateCompany([FromForm] EmpresaUpdateDTO dto)
{
    await using var conn = NovaConexao();
    await conn.OpenAsync();

    var response = new Response<bool>();
    var empresaId = User.GetEmpresaId();

    string? logoPath = null;

    try
    {
        // ===============================
        // Upload da logo (se existir)
        // ===============================
        if (dto.logo != null && dto.logo.Length > 0)
        {
            var folderPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "empresas",
                empresaId.ToString()
            );

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var extension = Path.GetExtension(dto.logo.FileName);
            var fileName = $"logo{extension}";
            var fullPath = Path.Combine(folderPath, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await dto.logo.CopyToAsync(stream);

            // Caminho salvo no banco
            logoPath = $"/uploads/empresas/{empresaId}/{fileName}";
        }

        // ===============================
        // SQL (logo só atualiza se existir)
        // ===============================
        var query = @"
        update empresa set
            razao_social   = @razao_social,
            nome_fantasia  = @nome_fantasia,
            cnpj           = @cnpj,
            email          = @email,
            ie             = @ie,
            im             = @im,
            telefone       = @telefone,
            celular        = @celular,
            website        = @website,
            cep            = @cep,
            logradouro     = @logradouro,
            numero         = @numero,
            complemento    = @complemento,
            bairro         = @bairro,
            cidade         = @cidade,
            uf             = @uf,
            observacao     = @observacao
            " + (logoPath != null ? ", logo = @logo" : "") + @"
        where id = @id
        ";

        await using var cmd = new NpgsqlCommand(query, conn);

        cmd.Parameters.AddWithValue("@id", empresaId);
        cmd.Parameters.AddWithValue("@razao_social", (object?)dto.razao_social ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@nome_fantasia", (object?)dto.nome_fantasia ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cnpj", (object?)dto.cnpj ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email", (object?)dto.email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ie", (object?)dto.ie ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@im", (object?)dto.im ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@telefone", (object?)dto.telefone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@celular", (object?)dto.celular ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@website", (object?)dto.website ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cep", (object?)dto.cep ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@logradouro", (object?)dto.logradouro ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@numero", (object?)dto.numero ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@complemento", (object?)dto.complemento ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@bairro", (object?)dto.bairro ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cidade", (object?)dto.cidade ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@uf", (object?)dto.uf ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);

        if (logoPath != null)
            cmd.Parameters.AddWithValue("@logo", logoPath);

        await cmd.ExecuteNonQueryAsync();

        response.Success = true;
        response.Data = true;
        response.Message = "Empresa atualizada com sucesso.";

        return Ok(response);
    }
    catch (Exception ex)
    {
        response.Success = false;
        response.Message = $"Erro ao atualizar empresa: {ex.Message}";
        return StatusCode(500, response);
    }
}

}
