using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("API/V1")]
public class SaleAPI : ControllerBase
{
    private readonly IConfiguration _config;

    public SaleAPI(IConfiguration config)
    {
        _config = config;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    // [Authorize]
    // [HttpPost("Post/Create/Client")]
    // public async Task<IActionResult> CreateClient([FromBody] ClientDTO dto)
    // {
    //     await using var conn = NovaConexao();
    //     await conn.OpenAsync();

    //     var response = new Response<string>();
    //     await using var transaction = await conn.BeginTransactionAsync();
    //     var empresaId = User.GetEmpresaId();
    //     int clientId;

    //     try
    //     {
    //         const string sqlCliente = @"
    //         INSERT INTO cliente
    //         (empresa_id, grupo_id, tipo, nome, documento, email, telefone, observacao, ativo)
    //         VALUES
    //         (@empresa_id, @grupo_id, @tipo, @nome, @documento, @email, @telefone, @observacao, @ativo)
    //         RETURNING id;
    //     ";

    //         await using (var cmd = new NpgsqlCommand(sqlCliente, conn, transaction))
    //         {
    //             cmd.Parameters.AddWithValue("@empresa_id", empresaId);
    //             cmd.Parameters.AddWithValue("@grupo_id", (object?)dto.grupo_id ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@documento", (object?)dto.documento ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@email", (object?)dto.email ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@telefone", (object?)dto.telefone ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@ativo", dto.ativo);

    //             clientId = (int)(await cmd.ExecuteScalarAsync()
    //                 ?? throw new Exception("Falha ao obter ID do cliente."));
    //         }

    //         const string sqlEndereco = @"
    //         INSERT INTO endereco
    //         (cliente_id, empresa_id, rua, numero, bairro, cidade, uf, complemento, cep, cod_uf)
    //         VALUES
    //         (@cliente_id, @empresa_id, @rua, @numero, @bairro, @cidade, @uf, @complemento, @cep,@cod_uf);
    //     ";

    //         if (!string.IsNullOrWhiteSpace(dto.rua))
    //         {
    //             await using var cmdEndereco = new NpgsqlCommand(sqlEndereco, conn, transaction);
    //             cmdEndereco.Parameters.AddWithValue("@cliente_id", clientId);
    //             cmdEndereco.Parameters.AddWithValue("@empresa_id", empresaId);
    //             cmdEndereco.Parameters.AddWithValue("@rua", (object?)dto.rua ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@numero", (object?)dto.numero ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@bairro", (object?)dto.bairro ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@cidade", (object?)dto.cidade ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@uf", (object?)dto.uf ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@complemento", (object?)dto.complemento ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@cep", (object?)dto.cep ?? DBNull.Value);
    //             cmdEndereco.Parameters.AddWithValue("@cod_uf", (object?)dto.cod_uf ?? DBNull.Value);

    //             await cmdEndereco.ExecuteNonQueryAsync();


    //         }

    //         await transaction.CommitAsync();

    //         response.Success = true;
    //         response.Message = "Cliente cadastrado com sucesso.";
    //         return Ok(response);
    //     }
    //     catch (PostgresException ex) when (ex.SqlState == "23505")
    //     {
    //         await transaction.RollbackAsync();
    //         response.Success = false;
    //         response.Message = "Já existe um cliente cadastrado com este documento ou e-mail.";
    //         return BadRequest(response);
    //     }
    //     catch (Exception ex)
    //     {
    //         await transaction.RollbackAsync();
    //         response.Success = false;
    //         response.Message = $"Erro ao cadastrar cliente: {ex.Message}";
    //         return StatusCode(500, response);
    //     }
    // }

    // [Authorize]
    // [HttpPut("Put/Update/Client")]
    // public async Task<IActionResult> UpdateClient([FromBody] ClientDetailsEntity dto)
    // {
    //     await using var conn = NovaConexao(); // NpgsqlConnection
    //     await conn.OpenAsync();

    //     var response = new Response<string>();
    //     await using var transaction = await conn.BeginTransactionAsync();

    //     var empresaId = User.GetEmpresaId();
    //     try
    //     {
    //         const string sql = @"
    //         UPDATE cliente SET
    //             grupo_id = @grupo_id,
    //             tipo = @tipo,
    //             nome = @nome,
    //             documento = @documento,
    //             email = @email,
    //             telefone = @telefone,
    //             observacao = @observacao,
    //             ativo = @ativo
    //         WHERE id = @id and empresa_id=@empresa_id;
    //     ";
    //         const string sqlDelete = @"delete from endereco where cliente_id = @cliente_id";

    //         const string sqlEndereco = @"
    //         INSERT INTO endereco
    //         (cliente_id, empresa_id, rua, numero, bairro, cidade, uf, complemento, cep, cod_uf)
    //         VALUES
    //         (@cliente_id, @empresa_id, @rua, @numero, @bairro, @cidade, @uf, @complemento, @cep,@cod_uf);
    //     ";

    //         await using (var cmd = new NpgsqlCommand(sql, conn, transaction))
    //         {
    //             cmd.Parameters.AddWithValue("@id", dto.id);
    //             cmd.Parameters.AddWithValue("@empresa_id", empresaId);
    //             cmd.Parameters.AddWithValue("@grupo_id", (object?)dto.grupo_id ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@tipo", (object?)dto.tipo ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@nome", (object?)dto.nome ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@documento", (object?)dto.documento ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@email", (object?)dto.email ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@telefone", (object?)dto.telefone ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
    //             cmd.Parameters.AddWithValue("@ativo", dto.ativo);
    //             var rowsAffected = await cmd.ExecuteNonQueryAsync();

    //             if (rowsAffected == 0)
    //             {
    //                 await transaction.RollbackAsync();
    //                 response.Success = false;
    //                 response.Message = "Cliente não encontrado ou não pertence à empresa.";
    //                 return NotFound(response);
    //             }
    //         }

    //         await using var cmdDeleteEndereco = new NpgsqlCommand(sqlDelete, conn, transaction);
    //         cmdDeleteEndereco.Parameters.AddWithValue("@cliente_id", dto.id);
    //         await cmdDeleteEndereco.ExecuteNonQueryAsync();

    //         await using var cmdEndereco = new NpgsqlCommand(sqlEndereco, conn, transaction);
    //         cmdEndereco.Parameters.AddWithValue("@cliente_id", dto.id);
    //         cmdEndereco.Parameters.AddWithValue("@empresa_id", empresaId);
    //         cmdEndereco.Parameters.AddWithValue("@rua", (object?)dto.rua ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@numero", (object?)dto.numero ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@bairro", (object?)dto.bairro ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@cidade", (object?)dto.cidade ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@uf", (object?)dto.uf ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@complemento", (object?)dto.complemento ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@cep", (object?)dto.cep ?? DBNull.Value);
    //         cmdEndereco.Parameters.AddWithValue("@cod_uf", (object?)dto.cod_uf ?? DBNull.Value);
    //         await cmdEndereco.ExecuteNonQueryAsync();
    //         await transaction.CommitAsync();
    //         response.Success = true;
    //         response.Message = "Cliente atualizado com sucesso.";
    //         return Ok(response);
    //     }
    //     catch (Exception ex)
    //     {
    //         await transaction.RollbackAsync();
    //         response.Success = false;
    //         response.Message = $"Erro ao atualizar Cliente: {ex.Message}";
    //         return StatusCode(500, response);
    //     }

    // }
    // [Authorize]
    // [HttpPut("Put/Status/Client")]
    // public async Task<IActionResult> StatusUpdateClient([FromBody] ClientStatusDTO dto)
    // {
    //     await using var conn = NovaConexao(); // NpgsqlConnection
    //     await conn.OpenAsync();

    //     var response = new Response<string>();
    //     await using var transaction = await conn.BeginTransactionAsync();

    //     var empresaId = User.GetEmpresaId();
    //     try
    //     {
    //         const string sql = @"
    //         UPDATE cliente SET
    //             ativo = @ativo
    //         WHERE id = @id and empresa_id=@empresa_id;
    //     ";
    //         await using (var cmd = new NpgsqlCommand(sql, conn, transaction))
    //         {
    //             cmd.Parameters.AddWithValue("@id", dto.id);
    //             cmd.Parameters.AddWithValue("@empresa_id", empresaId);
    //             cmd.Parameters.AddWithValue("@ativo", dto.status);
    //             var rowsAffected = await cmd.ExecuteNonQueryAsync();

    //             if (rowsAffected == 0)
    //             {
    //                 await transaction.RollbackAsync();
    //                 response.Success = false;
    //                 response.Message = "Cliente não encontrado ou não pertence à empresa.";
    //                 return NotFound(response);
    //             }
    //         }
    //         await transaction.CommitAsync();

    //         response.Success = true;
    //         response.Message = "Cliente atualizado com sucesso.";
    //         return Ok(response);
    //     }
    //     catch (Exception ex)
    //     {
    //         await transaction.RollbackAsync();
    //         response.Success = false;
    //         response.Message = $"Erro ao atualizar Cliente: {ex.Message}";
    //         return StatusCode(500, response);
    //     }

    // }


    [Authorize]
    [HttpGet("Get/Sales/Product")]
    public async Task<IActionResult> FetchMark([FromQuery] string search)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<ProductSalesEntity>>();
        var products = new List<ProductSalesEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"
            SELECT 
                p.id,
                p.nome,
                p.preco_venda,
                p.fracionado
            FROM produto p
            WHERE p.empresa_id = @empresa_id
        ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                query += @"
                AND (
                    p.codigo_barras = @search
                    OR p.nome ILIKE '%' || @search || '%'
                )
                ORDER BY 
                    CASE 
                        WHEN p.codigo_barras = @search THEN 0
                        ELSE 1
                    END,
                    p.nome
                LIMIT 100;
            ";
            }
            else
            {
                query += " ORDER BY p.nome LIMIT 100;";
            }

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search.Trim());

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                products.Add(new ProductSalesEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.GetString(reader.GetOrdinal("nome")).Trim(),
                    preco_venda = reader.GetDecimal(reader.GetOrdinal("preco_venda")),
                    fracionado = reader.GetBoolean(reader.GetOrdinal("fracionado"))
                });
            }

            response.Success = true;
            response.Data = products;
            response.Message = products.Count == 0
                ? "Nenhum produto encontrado."
                : "Produtos encontrados com sucesso.";

            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar produtos: {ex.Message}";
            return StatusCode(500, response);
        }
    }


    [Authorize]
    [HttpGet("Get/Wallet/Categoria")]
    public async Task<IActionResult> FetchWalletCategoria([FromQuery] string search, [FromQuery] string tipo)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<WalletCategorySalesEntity>>();
        var clients = new List<WalletCategorySalesEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            string sql = @"
            select c.id, c.nome from categoria_wallet c
            where c.empresa_id= @empresa_id
            AND c.ativo = true ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @" AND (
                    nome ILIKE '%' || @search || '%'
                ) ";
            }
            if (!string.IsNullOrWhiteSpace(tipo))
            {
                sql += @" AND (
                    tipo ILIKE '%' || @tipo || '%'
                ) ";
            }

            sql += @" ORDER BY nome";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search);
            if (!string.IsNullOrWhiteSpace(tipo))
                cmd.Parameters.AddWithValue("@tipo", tipo);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clients.Add(new WalletCategorySalesEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader["nome"] as string,
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
    [HttpGet("Get/Sales/Client")]
    public async Task<IActionResult> FetchClient([FromQuery] string search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<ClientSalesEntity>>();
        var clients = new List<ClientSalesEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            string sql = @"
            SELECT 
                c.id,
                c.nome,
                g.desconto
            FROM cliente c
            LEFT JOIN grupo_cliente g on g.id = c.grupo_id
            WHERE c.empresa_id = @empresa_id AND c.ativo =true
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

            sql += @" ORDER BY c.nome";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                clients.Add(new ClientSalesEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader["nome"] as string,
                    desconto = reader.GetInt32(reader.GetOrdinal("desconto"))
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
    [HttpGet("Get/Sales/Fornecedores")]
    public async Task<IActionResult> FetchFornecedor([FromQuery] string search)
    {
        await using var conn = NovaConexao(); // NpgsqlConnection
        await conn.OpenAsync();

        var response = new Response<List<FornecedorWalletEntity>>();
        var fornecedores = new List<FornecedorWalletEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            string sql = @"
            SELECT 
                f.id,
                f.nome
            FROM fornecedores f
            WHERE f.empresa_id = @empresa_id AND f.ativo =true
        ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @"
                AND (
                    f.nome ILIKE '%' || @search || '%'
                    OR f.cnpj    ILIKE '%' || @search || '%'
                    OR f.email ILIKE '%' || @search || '%'
                )
            ";
            }

            sql += @" ORDER BY f.nome";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                fornecedores.Add(new FornecedorWalletEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader["nome"] as string,
                });
            }

            response.Success = true;
            response.Data = fornecedores;

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
    [HttpGet("Get/Form/Sale")]
    public async Task<IActionResult> FetchFromSale()
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<FormSaleEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string sql = @"SELECT 
            e.id,
            e.razao_social,
            COALESCE(
                (
                    SELECT jsonb_agg(
                        jsonb_build_object(
                            'id', c.id,
                            'nome', c.nome
                        )
                    )
                    FROM canal_de_venda c
                    WHERE c.empresa_id = e.id
                      AND c.ativo = true
                ),
                '[]'::jsonb
            ) AS canais,
            COALESCE(
                (
                    SELECT jsonb_agg(
                        jsonb_build_object(
                            'id', m.id,
                            'nome', m.nome,
                            'custo', m.custo_base
                        )
                    )
                    FROM metodo_entrega m
                    WHERE m.empresa_id = e.id
                      AND m.ativo = true
                ),
                '[]'::jsonb
            ) AS entrega
        FROM empresa e
        WHERE e.id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Empresa não encontrada.";
                return NotFound(response);
            }

            var entity = new FormSaleEntity
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                RazaoSocial = reader.GetString(reader.GetOrdinal("razao_social")),
                Canais = JsonSerializer.Deserialize<List<SimpleOptionDto>>(
                    reader.GetString(reader.GetOrdinal("canais"))
                ) ?? new(),
                Entrega = JsonSerializer.Deserialize<List<SimpleOptionDto>>(
                    reader.GetString(reader.GetOrdinal("entrega"))
                ) ?? new()
            };

            response.Success = true;
            response.Data = entity;
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar dados do formulário: {ex.Message}";
            return StatusCode(500, response);
        }
    }


    [Authorize]
    [HttpPost("Post/NewSale")]
    public async Task<IActionResult> NewSale([FromBody] NewSaleHeader dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();
        var usuarioId = User.GetUserId();

        var response = new Response<string>();

        try
        {
            // 1. Inserir cabeçalho da venda
            const string insertVenda = @"
            INSERT INTO venda
            (empresa_id, cliente_id, data_cadastro, data_pedido, usuario_id, canal_id, entrega_id,
             observacao, val_frete, desconto, total, status,ultimo_pedido, sub_total)
            VALUES
            (@empresa_id, @cliente_id, NOW(), @data_pedido, @usuario_id, @canal_id, @entrega_id,
             @observacao, @val_frete, @desconto, @total, @status,@ultimo_pedido,@sub_total)
            RETURNING id;
        ";
            // 1. Gerar número do pedido
            const string sqlNumeroPedido = @"
            UPDATE empresa_sequencia
            SET ultimo_pedido = ultimo_pedido + 1
            WHERE empresa_id = @empresa_id
            RETURNING ultimo_pedido;
        ";

            int ultimo_pedido;
            await using (var cmdSeq = new NpgsqlCommand(sqlNumeroPedido, conn, transaction))
            {
                cmdSeq.Parameters.AddWithValue("@empresa_id", empresaId);
                ultimo_pedido = Convert.ToInt32(await cmdSeq.ExecuteScalarAsync());
            }

            int vendaId;

            await using (var cmd = new NpgsqlCommand(insertVenda, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@empresa_id", empresaId);
                cmd.Parameters.AddWithValue("@cliente_id", dto.client_id);
                cmd.Parameters.AddWithValue("@data_pedido", (object?)dto.data_pedido ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
                cmd.Parameters.AddWithValue("@canal_id", (object?)dto.canal_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@entrega_id", (object?)dto.entrega_id ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@val_frete", dto.val_frete);
                cmd.Parameters.AddWithValue("@desconto", (object?)dto.desconto ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@total", dto.total);
                cmd.Parameters.AddWithValue("@status", dto.status);
                cmd.Parameters.AddWithValue("@ultimo_pedido", ultimo_pedido);
                cmd.Parameters.AddWithValue("@sub_total", dto.sub_total);

                vendaId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            // 2. Processar itens
            foreach (var item in dto.itens)
            {
                // 2.1 Lock do saldo do produto
                const string lockSaldo = @"
                SELECT saldo
                FROM saldo_estoque
                WHERE empresa_id = @empresa_id
                  AND produto_id = @produto_id
                FOR UPDATE;
            ";

                await using (var cmdLock = new NpgsqlCommand(lockSaldo, conn, transaction))
                {
                    cmdLock.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdLock.Parameters.AddWithValue("@produto_id", item.produto_id);

                    await cmdLock.ExecuteScalarAsync();
                    // lock intencional, sem validação de saldo
                }

                // 2.2 Inserir item da venda
                const string insertItem = @"
                INSERT INTO itens_venda
                (venda_id, produto_id, qtd, val_unitario, sub_total)
                VALUES
                (@venda_id, @produto_id, @qtd, @val_unitario, @sub_total);
            ";

                await using (var cmdItem = new NpgsqlCommand(insertItem, conn, transaction))
                {
                    cmdItem.Parameters.AddWithValue("@venda_id", vendaId);
                    cmdItem.Parameters.AddWithValue("@produto_id", item.produto_id);
                    cmdItem.Parameters.AddWithValue("@qtd", item.qtd);
                    cmdItem.Parameters.AddWithValue("@val_unitario", item.val_unitario);
                    cmdItem.Parameters.AddWithValue("@sub_total", item.sub_total);

                    await cmdItem.ExecuteNonQueryAsync();
                }

                // 2.3 Atualizar saldo
                const string updateSaldo = @"
                INSERT INTO saldo_estoque (empresa_id, produto_id, saldo)
                VALUES (@empresa_id, @produto_id, @saldo)
                ON CONFLICT (empresa_id, produto_id)
                DO UPDATE
                SET saldo = saldo_estoque.saldo + @saldo;
            ";

                await using (var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction))
                {
                    cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdSaldo.Parameters.AddWithValue("@produto_id", item.produto_id);
                    cmdSaldo.Parameters.AddWithValue("@saldo", -item.qtd);

                    await cmdSaldo.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Venda realizada com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            response.Success = false;
            response.Message = ex.Message;
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/CancelSale/{vendaId}")]
    public async Task<IActionResult> CancelSale(int vendaId)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();
        var response = new Response<string>();

        try
        {
            // 1. Buscar itens da venda
            const string selectItens = @"
            SELECT produto_id, qtd
            FROM itens_venda
            WHERE venda_id = @venda_id;
        ";

            var itens = new List<(int ProdutoId, decimal Qtd)>();

            await using (var cmdSelect = new NpgsqlCommand(selectItens, conn, transaction))
            {
                cmdSelect.Parameters.AddWithValue("@venda_id", vendaId);

                await using var reader = await cmdSelect.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    itens.Add((
                        reader.GetInt32(0),
                        reader.GetDecimal(1)
                    ));
                }
            }

            if (itens.Count == 0)
                throw new Exception("Venda não encontrada ou sem itens.");

            // 2. Estornar estoque
            const string updateSaldo = @"
            INSERT INTO saldo_estoque (empresa_id, produto_id, saldo)
            VALUES (@empresa_id, @produto_id, @saldo)
            ON CONFLICT (empresa_id, produto_id)
            DO UPDATE
            SET saldo = saldo_estoque.saldo + @saldo;
        ";

            foreach (var item in itens)
            {
                await using var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction);
                cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
                cmdSaldo.Parameters.AddWithValue("@produto_id", item.ProdutoId);
                cmdSaldo.Parameters.AddWithValue("@saldo", item.Qtd); // estorno

                await cmdSaldo.ExecuteNonQueryAsync();
            }

            // 3. Cancelar venda (cancelamento lógico)
            const string cancelVenda = @"
            UPDATE venda
            SET status = 'Cancelada'
            WHERE id = @venda_id
              AND empresa_id = @empresa_id
              AND status <> 'Cancelada';
        ";

            await using (var cmdCancel = new NpgsqlCommand(cancelVenda, conn, transaction))
            {
                cmdCancel.Parameters.AddWithValue("@venda_id", vendaId);
                cmdCancel.Parameters.AddWithValue("@empresa_id", empresaId);

                var rows = await cmdCancel.ExecuteNonQueryAsync();
                if (rows == 0)
                    throw new Exception("Venda não encontrada ou já Cancelada.");
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Venda Cancelada com sucesso e estoque estornado.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            response.Success = false;
            response.Message = ex.Message;
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/UpdateSale/{vendaId}")]
    public async Task<IActionResult> UpdateSale(int vendaId, [FromBody] NewSaleHeader dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        await using var transaction = await conn.BeginTransactionAsync();

        var empresaId = User.GetEmpresaId();
        var usuarioId = User.GetUserId();

        var response = new Response<string>();

        try
        {
            // 1. Validar se venda existe e não está Cancelada
            const string checkVenda = @"
            SELECT status
            FROM venda
            WHERE id = @venda_id
              AND empresa_id = @empresa_id
            FOR UPDATE;
        ";

            string status;

            await using (var cmdCheck = new NpgsqlCommand(checkVenda, conn, transaction))
            {
                cmdCheck.Parameters.AddWithValue("@venda_id", vendaId);
                cmdCheck.Parameters.AddWithValue("@empresa_id", empresaId);

                var result = await cmdCheck.ExecuteScalarAsync();
                if (result == null)
                    throw new Exception("Venda não encontrada.");

                status = result.ToString()!;
                if (status == "Cancelada")
                    throw new Exception("Não é possível editar uma venda Cancelada.");
            }

            // 2. Buscar itens atuais
            const string selectItens = @"
            SELECT produto_id, qtd
            FROM itens_venda
            WHERE venda_id = @venda_id;
        ";

            var itensAtuais = new Dictionary<int, decimal>();

            await using (var cmdSelect = new NpgsqlCommand(selectItens, conn, transaction))
            {
                cmdSelect.Parameters.AddWithValue("@venda_id", vendaId);

                await using var reader = await cmdSelect.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    itensAtuais[reader.GetInt32(0)] = reader.GetDecimal(1);
                }
            }

            // 3. Processar itens novos
            const string updateSaldo = @"
            INSERT INTO saldo_estoque (empresa_id, produto_id, saldo)
            VALUES (@empresa_id, @produto_id, @saldo)
            ON CONFLICT (empresa_id, produto_id)
            DO UPDATE
            SET saldo = saldo_estoque.saldo + @saldo;
        ";

            // 3.1 Itens novos ou alterados
            foreach (var item in dto.itens)
            {
                itensAtuais.TryGetValue(item.produto_id, out var qtdAntiga);
                var diferenca = item.qtd - qtdAntiga;

                if (diferenca != 0)
                {
                    await using var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction);
                    cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
                    cmdSaldo.Parameters.AddWithValue("@produto_id", item.produto_id);
                    cmdSaldo.Parameters.AddWithValue("@saldo", -diferenca);
                    // se aumentou → debita | se diminuiu → estorna

                    await cmdSaldo.ExecuteNonQueryAsync();
                }

                // upsert item
                const string upsertItem = @"
                INSERT INTO itens_venda (venda_id, produto_id, qtd, val_unitario, sub_total)
                VALUES (@venda_id, @produto_id, @qtd, @val_unitario, @sub_total)
                ON CONFLICT (venda_id, produto_id)
                DO UPDATE SET
                    qtd = EXCLUDED.qtd,
                    val_unitario = EXCLUDED.val_unitario,
                    sub_total = EXCLUDED.sub_total;
            ";

                await using var cmdItem = new NpgsqlCommand(upsertItem, conn, transaction);
                cmdItem.Parameters.AddWithValue("@venda_id", vendaId);
                cmdItem.Parameters.AddWithValue("@produto_id", item.produto_id);
                cmdItem.Parameters.AddWithValue("@qtd", item.qtd);
                cmdItem.Parameters.AddWithValue("@val_unitario", item.val_unitario);
                cmdItem.Parameters.AddWithValue("@sub_total", item.sub_total);

                await cmdItem.ExecuteNonQueryAsync();

                itensAtuais.Remove(item.produto_id);
            }

            // 3.2 Itens removidos
            foreach (var itemRemovido in itensAtuais)
            {
                await using var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction);
                cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
                cmdSaldo.Parameters.AddWithValue("@produto_id", itemRemovido.Key);
                cmdSaldo.Parameters.AddWithValue("@saldo", itemRemovido.Value); // estorno total

                await cmdSaldo.ExecuteNonQueryAsync();

                const string deleteItem = @"
                DELETE FROM itens_venda
                WHERE venda_id = @venda_id
                  AND produto_id = @produto_id;
            ";

                await using var cmdDelete = new NpgsqlCommand(deleteItem, conn, transaction);
                cmdDelete.Parameters.AddWithValue("@venda_id", vendaId);
                cmdDelete.Parameters.AddWithValue("@produto_id", itemRemovido.Key);

                await cmdDelete.ExecuteNonQueryAsync();
            }

            // 4. Atualizar cabeçalho da venda
            const string updateVenda = @"
            UPDATE venda
            SET cliente_id = @cliente_id,
                data_pedido = @data_pedido,
                canal_id = @canal_id,
                entrega_id = @entrega_id,
                observacao = @observacao,
                val_frete = @val_frete,
                desconto = @desconto,
                total = @total,
                status =@status,
                usuario_id = @usuario_id
            WHERE id = @venda_id
              AND empresa_id = @empresa_id;
        ";

            await using (var cmdVenda = new NpgsqlCommand(updateVenda, conn, transaction))
            {
                cmdVenda.Parameters.AddWithValue("@cliente_id", dto.client_id);
                cmdVenda.Parameters.AddWithValue("@data_pedido", (object?)dto.data_pedido ?? DBNull.Value);
                cmdVenda.Parameters.AddWithValue("@canal_id", (object?)dto.canal_id ?? DBNull.Value);
                cmdVenda.Parameters.AddWithValue("@entrega_id", (object?)dto.entrega_id ?? DBNull.Value);
                cmdVenda.Parameters.AddWithValue("@observacao", (object?)dto.observacao ?? DBNull.Value);
                cmdVenda.Parameters.AddWithValue("@val_frete", dto.val_frete);
                cmdVenda.Parameters.AddWithValue("@desconto", (object?)dto.desconto ?? DBNull.Value);
                cmdVenda.Parameters.AddWithValue("@total", dto.total);
                cmdVenda.Parameters.AddWithValue("@status", (object?)dto.status ?? DBNull.Value);
                cmdVenda.Parameters.AddWithValue("@usuario_id", usuarioId);
                cmdVenda.Parameters.AddWithValue("@venda_id", vendaId);
                cmdVenda.Parameters.AddWithValue("@empresa_id", empresaId);

                await cmdVenda.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Venda atualizada com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            response.Success = false;
            response.Message = ex.Message;
            return StatusCode(500, response);
        }
    }


    [Authorize]
    [HttpGet("Get/SaleDetails/{saleId:int}")]
    public async Task<IActionResult> FetchSaleDetails(int saleId)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<SalesDetailsEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string sql = @"
            SELECT 
                v.id,
                v.ultimo_pedido,
                v.cliente_id,
                c.nome AS cliente,
                v.data_pedido,
                v.canal_id,
                cv.nome AS canal,
                m.id as entrega_id,
                m.custo_base,
                m.nome AS entrega,
                g.nome AS grupo_cliente,
                v.val_frete,
                v.observacao,
                v.desconto,
                v.total,
                v.sub_total,
                v.status,
                COALESCE(
                    (
                        SELECT jsonb_agg(
                            jsonb_build_object(
                                'id', i.produto_id,
                                'produto', p.nome,
                                'qtd', i.qtd,
                                'val_un', i.val_unitario,
                                'sub_total', i.sub_total
                            )
                            ORDER BY p.nome
                        )
                        FROM itens_venda i
                        INNER JOIN produto p ON p.id = i.produto_id
                        WHERE i.venda_id = v.id
                    ),
                    '[]'::jsonb
                ) AS itens
            FROM venda v
            INNER JOIN cliente c ON c.id = v.cliente_id
            INNER JOIN canal_de_venda cv ON cv.id = v.canal_id
            INNER JOIN metodo_entrega m ON m.id = v.entrega_id
            LEFT JOIN grupo_cliente g ON g.id = c.grupo_id
            WHERE v.id = @sale_id
              AND v.empresa_id = @empresa_id;
        ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@sale_id", saleId);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Venda não encontrada.";
                return NotFound(response);
            }

            var sale = new SalesDetailsEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                cliente_id = reader.GetInt32(reader.GetOrdinal("cliente_id")),
                canal = reader.IsDBNull(reader.GetOrdinal("canal")) ? null : reader.GetString(reader.GetOrdinal("canal")).Trim(),
                cliente = reader.IsDBNull(reader.GetOrdinal("cliente")) ? null : reader.GetString(reader.GetOrdinal("cliente")).Trim(),
                canal_id = reader.GetInt32(reader.GetOrdinal("canal_id")),
                entrega_id = reader.GetInt32(reader.GetOrdinal("canal_id")),
                custo_base = reader.GetDecimal(reader.GetOrdinal("custo_base")),
                grupo_cliente = reader.IsDBNull(reader.GetOrdinal("grupo_cliente")) ? null : reader.GetString(reader.GetOrdinal("grupo_cliente")).Trim(),
                observacao = reader.IsDBNull(reader.GetOrdinal("observacao")) ? null : reader.GetString(reader.GetOrdinal("observacao")).Trim(),
                entrega = reader.IsDBNull(reader.GetOrdinal("entrega")) ? null : reader.GetString(reader.GetOrdinal("entrega")).Trim(),
                status = reader.IsDBNull(reader.GetOrdinal("status")) ? null : reader.GetString(reader.GetOrdinal("status")).Trim(),
                data_pedido = reader.GetDateTime(reader.GetOrdinal("data_pedido")),
                desconto = reader.GetInt32(reader.GetOrdinal("desconto")),
                ultimo_pedido = reader.GetInt32(reader.GetOrdinal("ultimo_pedido")),
                val_frete = reader.GetDecimal(reader.GetOrdinal("val_frete")),
                total = reader.GetDecimal(reader.GetOrdinal("total")),
                sub_total = reader.GetDecimal(reader.GetOrdinal("sub_total")),

                itens = System.Text.Json.JsonSerializer.Deserialize<List<ItensSale>>(
                    reader.GetFieldValue<string>(reader.GetOrdinal("itens"))
                ) ?? new List<ItensSale>()
            };

            response.Success = true;
            response.Data = sale;
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar detalhes da venda.{ex.Message}";
            return StatusCode(StatusCodes.Status500InternalServerError, response);
        }
    }


    [Authorize]
    [HttpGet("Get/Sale")]
    public async Task<IActionResult> FetchSale([FromQuery] string? search)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();

        var response = new Response<List<SalesEntity>>();
        var sales = new List<SalesEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var sql = @"
            SELECT 
                v.id,
                c.nome AS cliente,
                cv.nome AS canal,
                v.data_cadastro,
                v.data_pedido,
                COUNT(i.id) AS itens,
                v.total,
                v.status,
                v.ultimo_pedido
            FROM venda v
            INNER JOIN itens_venda i ON i.venda_id = v.id
            LEFT JOIN cliente c ON c.id = v.cliente_id
            LEFT JOIN canal_de_venda cv ON cv.id = v.canal_id
            WHERE v.empresa_id = @empresa_id
        ";

            if (!string.IsNullOrWhiteSpace(search))
            {
                sql += @"
                AND (
                    c.nome ILIKE '%' || @search || '%'
                    OR v.status ILIKE '%' || @search || '%'
                    OR v.ultimo_pedido::TEXT ILIKE '%' || @search || '%'
                )
            ";
            }

            sql += @"
            GROUP BY 
                v.id, c.nome, cv.nome, 
                v.data_cadastro, v.data_pedido, 
                v.total, v.status
            ORDER BY v.data_cadastro DESC, c.nome ASC
            LIMIT 100;
        ";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            if (!string.IsNullOrWhiteSpace(search))
                cmd.Parameters.AddWithValue("@search", search);

            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                sales.Add(new SalesEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    cliente = reader.GetString(reader.GetOrdinal("cliente")).Trim(),
                    canal = reader.GetString(reader.GetOrdinal("canal")).Trim(),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                    data_pedido = reader.GetDateTime(reader.GetOrdinal("data_pedido")),
                    itens = reader.GetInt32(reader.GetOrdinal("itens")),
                    total = reader.GetDecimal(reader.GetOrdinal("total")),
                    status = reader.GetString(reader.GetOrdinal("status")).Trim(),
                    ultimo_pedido = reader.GetInt32(reader.GetOrdinal("ultimo_pedido"))
                });
            }

            response.Success = true;
            response.Data = sales;
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar as vendas: {ex.Message}";
            return StatusCode(500, response);
        }
    }


    // [Authorize]
    // [HttpDelete("Delete/CancelSale/{vendaId}")]
    // public async Task<IActionResult> CancelSale(int vendaId)
    // {
    //     await using var conn = NovaConexao();
    //     await conn.OpenAsync();

    //     await using var transaction = await conn.BeginTransactionAsync();

    //     var empresaId = User.GetEmpresaId();
    //     var response = new Response<string>();

    //     try
    //     {
    //         // 1. Buscar itens da venda
    //         const string selectItens = @"
    //         SELECT produto_id, qtd
    //         FROM itens_venda
    //         WHERE venda_id = @venda_id;
    //     ";

    //         var itens = new List<(int ProdutoId, decimal Qtd)>();

    //         await using (var cmdSelect = new NpgsqlCommand(selectItens, conn, transaction))
    //         {
    //             cmdSelect.Parameters.AddWithValue("@venda_id", vendaId);

    //             await using var reader = await cmdSelect.ExecuteReaderAsync();
    //             while (await reader.ReadAsync())
    //             {
    //                 itens.Add((
    //                     reader.GetInt32(0),
    //                     reader.GetDecimal(1)
    //                 ));
    //             }
    //         }

    //         // 2. Estornar estoque
    //         const string updateSaldo = @"
    //         INSERT INTO saldo_estoque (empresa_id, produto_id, saldo)
    //         VALUES (@empresa_id, @produto_id, @saldo)
    //         ON CONFLICT (empresa_id, produto_id)
    //         DO UPDATE
    //         SET saldo = saldo_estoque.saldo + @saldo;
    //     ";

    //         foreach (var item in itens)
    //         {
    //             await using var cmdSaldo = new NpgsqlCommand(updateSaldo, conn, transaction);
    //             cmdSaldo.Parameters.AddWithValue("@empresa_id", empresaId);
    //             cmdSaldo.Parameters.AddWithValue("@produto_id", item.ProdutoId);
    //             cmdSaldo.Parameters.AddWithValue("@saldo", item.Qtd); // estorno → soma

    //             await cmdSaldo.ExecuteNonQueryAsync();
    //         }

    //         // 3. Excluir venda (itens serão removidos via CASCADE)
    //         const string deleteVenda = @"
    //         DELETE FROM venda
    //         WHERE id = @venda_id
    //           AND empresa_id = @empresa_id;
    //     ";

    //         await using (var cmdDelete = new NpgsqlCommand(deleteVenda, conn, transaction))
    //         {
    //             cmdDelete.Parameters.AddWithValue("@venda_id", vendaId);
    //             cmdDelete.Parameters.AddWithValue("@empresa_id", empresaId);

    //             var rows = await cmdDelete.ExecuteNonQueryAsync();
    //             if (rows == 0)
    //                 throw new Exception("Venda não encontrada ou já Cancelada.");
    //         }

    //         await transaction.CommitAsync();

    //         response.Success = true;
    //         response.Message = "Venda Cancelada e estoque estornado com sucesso.";
    //         return Ok(response);
    //     }
    //     catch (Exception ex)
    //     {
    //         await transaction.RollbackAsync();

    //         response.Success = false;
    //         response.Message = ex.Message;
    //         return StatusCode(500, response);
    //     }
    // }



    // [Authorize]
    // [HttpGet("Get/Client")]
    // public async Task<IActionResult> FetchClient([FromQuery] string? search)
    // {
    //     await using var conn = NovaConexao(); // NpgsqlConnection
    //     await conn.OpenAsync();

    //     var response = new Response<List<ClientEntity>>();
    //     var clients = new List<ClientEntity>();
    //     var empresaId = User.GetEmpresaId();

    //     try
    //     {
    //         string sql = @"
    //         SELECT 
    //             c.id,
    //             c.data_cadastro,
    //             c.ativo,
    //             c.grupo_id,
    //             c.tipo,
    //             c.nome,
    //             c.documento,
    //             c.email,
    //             c.telefone,
    //             c.observacao,
    //             (e.cidade || ' - ' || e.uf) AS local,
    //             g.nome AS grupo
    //         FROM cliente c
    //         LEFT JOIN endereco e 
    //             ON e.cliente_id = c.id 
    //            AND e.empresa_id = c.empresa_id
    //         LEFT JOIN grupo_cliente g 
    //             ON g.id = c.grupo_id 
    //            AND g.empresa_id = c.empresa_id
    //         WHERE c.empresa_id = @empresa_id
    //     ";

    //         if (!string.IsNullOrWhiteSpace(search))
    //         {
    //             sql += @"
    //             AND (
    //                 c.nome ILIKE '%' || @search || '%'
    //                 OR c.documento ILIKE '%' || @search || '%'
    //                 OR c.email ILIKE '%' || @search || '%'
    //             )
    //         ";
    //         }

    //         sql += @" ORDER BY c.ativo desc, c.nome ASC  LIMIT 100; ";

    //         await using var cmd = new NpgsqlCommand(sql, conn);
    //         cmd.Parameters.AddWithValue("@empresa_id", empresaId);

    //         if (!string.IsNullOrWhiteSpace(search))
    //             cmd.Parameters.AddWithValue("@search", search);

    //         await using var reader = await cmd.ExecuteReaderAsync();

    //         while (await reader.ReadAsync())
    //         {
    //             clients.Add(new ClientEntity
    //             {
    //                 id = reader.GetInt32(reader.GetOrdinal("id")),
    //                 data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
    //                 ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
    //                 grupo_id = reader.IsDBNull(reader.GetOrdinal("grupo_id")) ? null : reader.GetInt32("grupo_id"),
    //                 tipo = reader.IsDBNull(reader.GetOrdinal("tipo")) ? null : reader.GetInt32(reader.GetOrdinal("tipo")),
    //                 nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? null : reader["nome"] as string,
    //                 documento = reader.IsDBNull(reader.GetOrdinal("documento")) ? null : reader["documento"] as string,
    //                 email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader["email"] as string,
    //                 telefone = reader.IsDBNull(reader.GetOrdinal("telefone")) ? null : reader["telefone"] as string,
    //                 observacao = reader.IsDBNull(reader.GetOrdinal("observacao")) ? null : reader["observacao"] as string,
    //                 grupo = reader.IsDBNull(reader.GetOrdinal("grupo")) ? null : reader["grupo"] as string,
    //                 local = reader.IsDBNull(reader.GetOrdinal("local")) ? null : reader["local"] as string
    //             });
    //         }

    //         response.Success = true;
    //         response.Data = clients;

    //         return Ok(response);
    //     }
    //     catch (Exception ex)
    //     {
    //         response.Success = false;
    //         response.Message = $"Erro ao buscar clientes: {ex.Message}";
    //         return StatusCode(500, response);
    //     }
    // }

    // [Authorize]
    // [HttpGet("Get/Client/ById/{clientId}")]
    // public async Task<IActionResult> FetchClient(int clientId)
    // {
    //     await using var conn = NovaConexao();
    //     await conn.OpenAsync();

    //     var response = new Response<ClientDetailsEntity>();
    //     var empresaId = User.GetEmpresaId();

    //     try
    //     {
    //         const string sql = @"
    //         SELECT 
    //             c.id,
    //             c.data_cadastro,
    //             c.ativo,
    //             c.grupo_id,
    //             c.tipo,
    //             c.nome,
    //             c.documento,
    //             c.email,
    //             c.telefone,
    //             c.observacao,
    //             e.cidade,
    //             e.uf,
    //             e.bairro,
    //             e.rua,
    //             e.cep,
    //             e.numero,
    //             e.complemento,
    //             e.cod_uf
    //         FROM cliente c
    //         LEFT JOIN endereco e 
    //             ON e.cliente_id = c.id 
    //            AND e.empresa_id = c.empresa_id
    //         WHERE c.empresa_id = @empresa_id
    //           AND c.id = @cliente_id;
    //     ";

    //         await using var cmd = new NpgsqlCommand(sql, conn);
    //         cmd.Parameters.AddWithValue("@empresa_id", empresaId);
    //         cmd.Parameters.AddWithValue("@cliente_id", clientId);

    //         await using var reader = await cmd.ExecuteReaderAsync();

    //         if (!await reader.ReadAsync())
    //         {
    //             response.Success = false;
    //             response.Message = "Cliente não encontrado.";
    //             return NotFound(response);
    //         }

    //         var client = new ClientDetailsEntity
    //         {
    //             id = reader.GetInt32(reader.GetOrdinal("id")),
    //             data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
    //             ativo = reader.GetBoolean(reader.GetOrdinal("ativo")),
    //             grupo_id = reader["grupo_id"] as int?,
    //             tipo = reader.IsDBNull(reader.GetOrdinal("tipo")) ? null : reader.GetInt32(reader.GetOrdinal("tipo")),
    //             nome = reader["nome"] as string,
    //             documento = reader["documento"] as string,
    //             email = reader["email"] as string,
    //             telefone = reader["telefone"] as string,
    //             observacao = reader["observacao"] as string,
    //             cidade = reader["cidade"] as string,
    //             uf = reader["uf"] as string,
    //             bairro = reader["bairro"] as string,
    //             rua = reader["rua"] as string,
    //             numero = reader["numero"] as string,
    //             complemento = reader["complemento"] as string,
    //             cep = reader["cep"] as string,
    //             cod_uf = reader.IsDBNull(reader.GetOrdinal("cod_uf")) ? null : reader.GetInt32(reader.GetOrdinal("cod_uf"))
    //         };

    //         response.Success = true;
    //         response.Data = client;

    //         return Ok(response);
    //     }
    //     catch (Exception ex)
    //     {
    //         response.Success = false;
    //         response.Message = $"Erro ao buscar cliente: {ex.Message}";
    //         return StatusCode(500, response);
    //     }
    // }


}
