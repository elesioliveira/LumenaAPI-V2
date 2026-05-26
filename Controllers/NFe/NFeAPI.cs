using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography.X509Certificates;

[ApiController]
[Route("API/V1")]
public class NFeAPI : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ICertificateService _certService;
    private readonly IXmlBuilderService _xmlBuilder;
    private readonly IXmlSignerService _xmlSigner;
    private readonly ISefazClientService _sefazClient;
    private readonly IDanfeService _danfeService;
    private readonly IDanfceService _danfceService;
    private readonly IQrCodeService _qrCodeService;

    public NFeAPI(
        IConfiguration config,
        ICertificateService certService,
        IXmlBuilderService xmlBuilder,
        IXmlSignerService xmlSigner,
        ISefazClientService sefazClient,
        IDanfeService danfeService,
        IDanfceService danfceService,
        IQrCodeService qrCodeService)
    {
        _config = config;
        _certService = certService;
        _xmlBuilder = xmlBuilder;
        _xmlSigner = xmlSigner;
        _sefazClient = sefazClient;
        _danfeService = danfeService;
        _danfceService = danfceService;
        _qrCodeService = qrCodeService;
    }

    private NpgsqlConnection NovaConexao()
        => new(_config.GetConnectionString("DefaultConnection"));

    [Authorize]
    [HttpPost("Post/Upload/Certificado")]
    public async Task<IActionResult> UploadCertificado([FromBody] UploadCertificadoDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<CertificadoDigitalEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var pfxBytes = Convert.FromBase64String(dto.pfx_base64);
            var cert = _certService.LoadCertificate(pfxBytes, dto.senha);
            var info = _certService.GetCertificateInfo(cert);

            await using var transaction = await conn.BeginTransactionAsync();

            const string upsert = @"
                INSERT INTO certificado_digital (empresa_id, pfx_data, senha_hash, razao_social, cnpj, validade_inicio, validade_fim)
                VALUES (@empresa_id, @pfx_data, @senha_hash, @razao_social, @cnpj, @validade_inicio, @validade_fim)
                ON CONFLICT (empresa_id) DO UPDATE SET
                    pfx_data = @pfx_data,
                    senha_hash = @senha_hash,
                    razao_social = @razao_social,
                    cnpj = @cnpj,
                    validade_inicio = @validade_inicio,
                    validade_fim = @validade_fim,
                    data_upload = NOW()
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(upsert, conn, transaction);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            cmd.Parameters.AddWithValue("@pfx_data", pfxBytes);
            cmd.Parameters.AddWithValue("@senha_hash", dto.senha);
            cmd.Parameters.AddWithValue("@razao_social", info.razaoSocial);
            cmd.Parameters.AddWithValue("@cnpj", info.cnpj);
            cmd.Parameters.AddWithValue("@validade_inicio", info.validadeInicio);
            cmd.Parameters.AddWithValue("@validade_fim", info.validadeFim);

            await cmd.ExecuteScalarAsync();
            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Certificado digital carregado com sucesso.";
            response.Data = new CertificadoDigitalEntity
            {
                razao_social = info.razaoSocial,
                cnpj = info.cnpj,
                validade_inicio = info.validadeInicio,
                validade_fim = info.validadeFim,
                valido = _certService.IsCertificateValid(cert)
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao carregar certificado: {ex.Message}";
            return BadRequest(response);
        }
    }

    [Authorize]
    [HttpGet("Get/Certificado")]
    public async Task<IActionResult> GetCertificado()
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<CertificadoDigitalEntity>();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"
                SELECT id, razao_social, cnpj, validade_inicio, validade_fim, data_upload
                FROM certificado_digital WHERE empresa_id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Nenhum certificado cadastrado.";
                return NotFound(response);
            }

            var validadeFim = reader.GetDateTime(reader.GetOrdinal("validade_fim"));
            response.Success = true;
            response.Data = new CertificadoDigitalEntity
            {
                id = reader.GetInt32(reader.GetOrdinal("id")),
                razao_social = reader.IsDBNull(reader.GetOrdinal("razao_social")) ? null : reader.GetString(reader.GetOrdinal("razao_social")),
                cnpj = reader.IsDBNull(reader.GetOrdinal("cnpj")) ? null : reader.GetString(reader.GetOrdinal("cnpj")),
                validade_inicio = reader.GetDateTime(reader.GetOrdinal("validade_inicio")),
                validade_fim = validadeFim,
                data_upload = reader.GetDateTime(reader.GetOrdinal("data_upload")),
                valido = DateTime.Now <= validadeFim
            };
            response.Message = "Certificado encontrado.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar certificado: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPut("Put/Update/ConfigFiscal")]
    public async Task<IActionResult> UpdateConfigFiscal([FromBody] ConfigFiscalEmpresaDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<string>();
        var empresaId = User.GetEmpresaId();

        try
        {
            await using var transaction = await conn.BeginTransactionAsync();
            const string query = @"
                UPDATE empresa SET
                    uf = COALESCE(@uf, uf),
                    regime_tributario = COALESCE(@regime_tributario, regime_tributario),
                    csc_id = COALESCE(@csc_id, csc_id),
                    csc_token = COALESCE(@csc_token, csc_token),
                    nfe_ambiente = COALESCE(@nfe_ambiente, nfe_ambiente)
                WHERE id = @empresa_id;";

            await using var cmd = new NpgsqlCommand(query, conn, transaction);
            cmd.Parameters.AddWithValue("@uf", string.IsNullOrEmpty(dto.uf) ? DBNull.Value : dto.uf.Trim().ToUpper());
            cmd.Parameters.AddWithValue("@regime_tributario", dto.regime_tributario.HasValue ? dto.regime_tributario.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@csc_id", string.IsNullOrEmpty(dto.csc_id) ? DBNull.Value : dto.csc_id.Trim());
            cmd.Parameters.AddWithValue("@csc_token", string.IsNullOrEmpty(dto.csc_token) ? DBNull.Value : dto.csc_token.Trim());
            cmd.Parameters.AddWithValue("@nfe_ambiente", dto.nfe_ambiente.HasValue ? dto.nfe_ambiente.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);

            await cmd.ExecuteNonQueryAsync();
            await transaction.CommitAsync();

            response.Success = true;
            response.Message = "Configuração fiscal atualizada com sucesso.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao atualizar configuração: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/NFe/StatusSefaz")]
    public async Task<IActionResult> StatusSefaz()
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<object>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var (uf, ambiente, cert) = await LoadEmpresaFiscalData(conn, empresaId);

            var xml = _xmlBuilder.BuildStatusServicoXml(uf, ambiente);
            var endpoints = SefazEndpoints.GetEndpoints(uf, ambiente, 55);

            var result = await _sefazClient.SendAsync(
                endpoints.statusServico, xml, cert,
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4/nfeStatusServicoNF"
            );

            response.Success = true;
            response.Message = "Consulta realizada.";
            response.Data = new { xml_retorno = result };
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao consultar status SEFAZ: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPost("Post/Emitir/NFe")]
    public async Task<IActionResult> EmitirNFe([FromBody] EmitirNFeDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<object>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var (uf, ambiente, cert) = await LoadEmpresaFiscalData(conn, empresaId);
            var empresaData = await LoadEmpresaData(conn, empresaId);

            await using var transaction = await conn.BeginTransactionAsync();

            var numero = await GetProximoNumero(conn, transaction, empresaId, dto.modelo, 1);

            var nfeData = new NFeData
            {
                Modelo = dto.modelo,
                Serie = 1,
                Numero = numero,
                Ambiente = ambiente,
                UF = uf,
                CNPJ = empresaData.cnpj,
                IE = empresaData.ie,
                RazaoSocial = empresaData.razao_social,
                Fantasia = empresaData.fantasia,
                RegimeTributario = empresaData.regime_tributario,
                NaturezaOperacao = dto.natureza_operacao ?? "VENDA",
                CscId = empresaData.csc_id,
                CscToken = empresaData.csc_token,
                EmitLogradouro = empresaData.logradouro,
                EmitNumero = empresaData.numero_end,
                EmitBairro = empresaData.bairro,
                EmitCMun = empresaData.cmun,
                EmitXMun = empresaData.xmun,
                EmitCEP = empresaData.cep,
                EmitFone = empresaData.fone,
                DestCpfCnpj = dto.dest_cpf_cnpj,
                DestNome = dto.dest_nome,
                Itens = dto.itens.Select((item, idx) => new NFeItemData
                {
                    NumeroItem = idx + 1,
                    Codigo = item.codigo ?? item.produto_id.ToString(),
                    Descricao = item.descricao ?? "",
                    NCM = item.ncm ?? "00000000",
                    CEST = item.cest,
                    CFOP = item.cfop ?? "5102",
                    Unidade = item.unidade ?? "UN",
                    Quantidade = item.quantidade,
                    ValorUnitario = item.valor_unitario,
                    ValorTotal = item.valor_total,
                    Origem = item.origem,
                    IcmsCst = item.icms_cst ?? "102",
                    IcmsAliquota = item.icms_aliquota,
                    IcmsReducaoBc = item.icms_reducao_bc,
                    PisCst = item.pis_cst ?? "07",
                    PisAliquota = item.pis_aliquota,
                    CofinsCst = item.cofins_cst ?? "07",
                    CofinsAliquota = item.cofins_aliquota,
                }).ToList(),
                Pagamentos = dto.pagamentos.Select(p => new NFePagamento
                {
                    FormaPagamento = p.forma_pagamento,
                    Valor = p.valor
                }).ToList()
            };

            var xml = dto.modelo == 65
                ? _xmlBuilder.BuildNFCeXml(nfeData)
                : _xmlBuilder.BuildNFeXml(nfeData);

            var signedXml = _xmlSigner.SignXml(xml, cert, "infNFe");

            var loteXml = BuildLoteXml(signedXml, ambiente);
            var endpoints = SefazEndpoints.GetEndpoints(uf, ambiente, dto.modelo);
            var sefazResponse = await _sefazClient.SendAsync(
                endpoints.autorizacao, loteXml, cert,
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4/nfeAutorizacaoLote"
            );

            var (statusCode, motivo, protocolo, chaveAcesso) = ParseSefazResponse(sefazResponse);

            var status = statusCode == "100" ? "autorizada" : "rejeitada";

            const string insertNota = @"
                INSERT INTO nota_fiscal (empresa_id, modelo, serie, numero, chave_acesso, natureza_operacao, data_emissao, data_autorizacao, dest_cpf_cnpj, dest_nome, valor_produtos, valor_total, status, protocolo_autorizacao, xml_envio, xml_retorno, xml_proc, motivo_rejeicao, ambiente, venda_id)
                VALUES (@empresa_id, @modelo, @serie, @numero, @chave_acesso, @natureza_operacao, NOW(), @data_autorizacao, @dest_cpf_cnpj, @dest_nome, @valor_produtos, @valor_total, @status, @protocolo, @xml_envio, @xml_retorno, @xml_proc, @motivo_rejeicao, @ambiente, @venda_id)
                RETURNING id;";

            var valorTotal = dto.itens.Sum(i => i.valor_total);
            await using var cmdInsert = new NpgsqlCommand(insertNota, conn, transaction);
            cmdInsert.Parameters.AddWithValue("@empresa_id", empresaId);
            cmdInsert.Parameters.AddWithValue("@modelo", dto.modelo);
            cmdInsert.Parameters.AddWithValue("@serie", 1);
            cmdInsert.Parameters.AddWithValue("@numero", numero);
            cmdInsert.Parameters.AddWithValue("@chave_acesso", chaveAcesso ?? (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@natureza_operacao", dto.natureza_operacao ?? "VENDA");
            cmdInsert.Parameters.AddWithValue("@data_autorizacao", status == "autorizada" ? DateTime.Now : DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@dest_cpf_cnpj", dto.dest_cpf_cnpj ?? (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@dest_nome", dto.dest_nome ?? (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@valor_produtos", valorTotal);
            cmdInsert.Parameters.AddWithValue("@valor_total", valorTotal);
            cmdInsert.Parameters.AddWithValue("@status", status);
            cmdInsert.Parameters.AddWithValue("@protocolo", protocolo ?? (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@xml_envio", signedXml);
            cmdInsert.Parameters.AddWithValue("@xml_retorno", sefazResponse);
            cmdInsert.Parameters.AddWithValue("@xml_proc", status == "autorizada" ? signedXml : (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@motivo_rejeicao", status != "autorizada" ? motivo : (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@ambiente", ambiente);
            cmdInsert.Parameters.AddWithValue("@venda_id", dto.venda_id.HasValue ? dto.venda_id.Value : DBNull.Value);

            var notaId = Convert.ToInt32(await cmdInsert.ExecuteScalarAsync());

            foreach (var item in dto.itens)
            {
                const string insertItem = @"
                    INSERT INTO nota_fiscal_item (nota_fiscal_id, numero_item, produto_id, codigo, descricao, ncm, cest, cfop, unidade, quantidade, valor_unitario, valor_total, icms_origem, icms_cst, icms_aliquota, pis_cst, pis_aliquota, cofins_cst, cofins_aliquota)
                    VALUES (@nota_fiscal_id, @numero_item, @produto_id, @codigo, @descricao, @ncm, @cest, @cfop, @unidade, @quantidade, @valor_unitario, @valor_total, @icms_origem, @icms_cst, @icms_aliquota, @pis_cst, @pis_aliquota, @cofins_cst, @cofins_aliquota);";

                await using var cmdItem = new NpgsqlCommand(insertItem, conn, transaction);
                cmdItem.Parameters.AddWithValue("@nota_fiscal_id", notaId);
                cmdItem.Parameters.AddWithValue("@numero_item", dto.itens.IndexOf(item) + 1);
                cmdItem.Parameters.AddWithValue("@produto_id", item.produto_id);
                cmdItem.Parameters.AddWithValue("@codigo", item.codigo ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@descricao", item.descricao ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@ncm", item.ncm ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@cest", item.cest ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@cfop", item.cfop ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@unidade", item.unidade ?? "UN");
                cmdItem.Parameters.AddWithValue("@quantidade", item.quantidade);
                cmdItem.Parameters.AddWithValue("@valor_unitario", item.valor_unitario);
                cmdItem.Parameters.AddWithValue("@valor_total", item.valor_total);
                cmdItem.Parameters.AddWithValue("@icms_origem", item.origem);
                cmdItem.Parameters.AddWithValue("@icms_cst", item.icms_cst ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@icms_aliquota", item.icms_aliquota ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@pis_cst", item.pis_cst ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@pis_aliquota", item.pis_aliquota ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@cofins_cst", item.cofins_cst ?? (object)DBNull.Value);
                cmdItem.Parameters.AddWithValue("@cofins_aliquota", item.cofins_aliquota ?? (object)DBNull.Value);
                await cmdItem.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            response.Success = status == "autorizada";
            response.Message = status == "autorizada"
                ? $"NFe autorizada. Protocolo: {protocolo}"
                : $"NFe rejeitada: {motivo}";
            response.Data = new { id = notaId, status, protocolo, chave_acesso = chaveAcesso, numero };
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao emitir NFe: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPost("Post/Cancelar/NFe")]
    public async Task<IActionResult> CancelarNFe([FromBody] CancelarNFeDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<object>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var (uf, ambiente, cert) = await LoadEmpresaFiscalData(conn, empresaId);

            const string queryNota = @"SELECT chave_acesso, protocolo_autorizacao, modelo FROM nota_fiscal WHERE id = @id AND empresa_id = @empresa_id AND status = 'autorizada';";
            await using var cmdNota = new NpgsqlCommand(queryNota, conn);
            cmdNota.Parameters.AddWithValue("@id", dto.nota_fiscal_id);
            cmdNota.Parameters.AddWithValue("@empresa_id", empresaId);
            await using var reader = await cmdNota.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Nota fiscal não encontrada ou não pode ser cancelada.";
                return BadRequest(response);
            }

            var chave = reader.GetString(0);
            var protocolo = reader.GetString(1);
            var modelo = reader.GetInt32(2);
            await reader.CloseAsync();

            const string queryCnpj = "SELECT cnpj FROM empresa WHERE id = @empresa_id;";
            await using var cmdCnpj = new NpgsqlCommand(queryCnpj, conn);
            cmdCnpj.Parameters.AddWithValue("@empresa_id", empresaId);
            var cnpj = (string)(await cmdCnpj.ExecuteScalarAsync())!;

            var xml = _xmlBuilder.BuildCancelamentoXml(chave, protocolo, dto.justificativa, cnpj, ambiente);
            var signedXml = _xmlSigner.SignXml(xml, cert, "infEvento");

            var envioXml = $@"<envEvento versao=""1.00"" xmlns=""http://www.portalfiscal.inf.br/nfe"">{signedXml}</envEvento>";
            var loteEvento = $@"<nfeDadosMsg xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4"">{envioXml}</nfeDadosMsg>";

            var endpoints = SefazEndpoints.GetEndpoints(uf, ambiente, modelo);
            var sefazResponse = await _sefazClient.SendAsync(
                endpoints.recepcaoEvento, loteEvento, cert,
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4/nfeRecepcaoEvento"
            );

            var cancelado = sefazResponse.Contains("<cStat>135</cStat>") || sefazResponse.Contains("<cStat>155</cStat>");

            if (cancelado)
            {
                const string updateStatus = "UPDATE nota_fiscal SET status = 'cancelada' WHERE id = @id AND empresa_id = @empresa_id;";
                await using var cmdUpdate = new NpgsqlCommand(updateStatus, conn);
                cmdUpdate.Parameters.AddWithValue("@id", dto.nota_fiscal_id);
                cmdUpdate.Parameters.AddWithValue("@empresa_id", empresaId);
                await cmdUpdate.ExecuteNonQueryAsync();
            }

            response.Success = cancelado;
            response.Message = cancelado ? "Nota fiscal cancelada com sucesso." : "Falha ao cancelar nota fiscal.";
            response.Data = new { xml_retorno = sefazResponse };
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao cancelar NFe: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/NFe/Historico")]
    public async Task<IActionResult> GetHistorico([FromQuery] string? status, [FromQuery] int? modelo)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<List<NotaFiscalEntity>>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var query = @"SELECT id, modelo, serie, numero, chave_acesso, natureza_operacao, data_emissao, data_autorizacao, dest_cpf_cnpj, dest_nome, dest_uf, valor_produtos, valor_total, status, protocolo_autorizacao, motivo_rejeicao, ambiente, venda_id, data_cadastro
                FROM nota_fiscal WHERE empresa_id = @empresa_id";

            if (!string.IsNullOrEmpty(status)) query += " AND status = @status";
            if (modelo.HasValue) query += " AND modelo = @modelo";
            query += " ORDER BY data_emissao DESC LIMIT 100;";

            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@status", status);
            if (modelo.HasValue) cmd.Parameters.AddWithValue("@modelo", modelo.Value);

            var notas = new List<NotaFiscalEntity>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                notas.Add(new NotaFiscalEntity
                {
                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    modelo = reader.GetInt32(reader.GetOrdinal("modelo")),
                    serie = reader.GetInt32(reader.GetOrdinal("serie")),
                    numero = reader.GetInt32(reader.GetOrdinal("numero")),
                    chave_acesso = reader.IsDBNull(reader.GetOrdinal("chave_acesso")) ? null : reader.GetString(reader.GetOrdinal("chave_acesso")),
                    natureza_operacao = reader.IsDBNull(reader.GetOrdinal("natureza_operacao")) ? null : reader.GetString(reader.GetOrdinal("natureza_operacao")),
                    data_emissao = reader.IsDBNull(reader.GetOrdinal("data_emissao")) ? null : reader.GetDateTime(reader.GetOrdinal("data_emissao")),
                    data_autorizacao = reader.IsDBNull(reader.GetOrdinal("data_autorizacao")) ? null : reader.GetDateTime(reader.GetOrdinal("data_autorizacao")),
                    dest_cpf_cnpj = reader.IsDBNull(reader.GetOrdinal("dest_cpf_cnpj")) ? null : reader.GetString(reader.GetOrdinal("dest_cpf_cnpj")),
                    dest_nome = reader.IsDBNull(reader.GetOrdinal("dest_nome")) ? null : reader.GetString(reader.GetOrdinal("dest_nome")),
                    valor_produtos = reader.IsDBNull(reader.GetOrdinal("valor_produtos")) ? null : reader.GetDecimal(reader.GetOrdinal("valor_produtos")),
                    valor_total = reader.IsDBNull(reader.GetOrdinal("valor_total")) ? null : reader.GetDecimal(reader.GetOrdinal("valor_total")),
                    status = reader.GetString(reader.GetOrdinal("status")),
                    protocolo_autorizacao = reader.IsDBNull(reader.GetOrdinal("protocolo_autorizacao")) ? null : reader.GetString(reader.GetOrdinal("protocolo_autorizacao")),
                    motivo_rejeicao = reader.IsDBNull(reader.GetOrdinal("motivo_rejeicao")) ? null : reader.GetString(reader.GetOrdinal("motivo_rejeicao")),
                    ambiente = reader.GetInt32(reader.GetOrdinal("ambiente")),
                    venda_id = reader.IsDBNull(reader.GetOrdinal("venda_id")) ? null : reader.GetInt32(reader.GetOrdinal("venda_id")),
                    data_cadastro = reader.GetDateTime(reader.GetOrdinal("data_cadastro")),
                });
            }

            response.Success = true;
            response.Data = notas;
            response.Message = notas.Count == 0 ? "Nenhuma nota fiscal encontrada." : "Notas fiscais encontradas.";
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao buscar histórico: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpPost("Post/Inutilizar/NFe")]
    public async Task<IActionResult> InutilizarNFe([FromBody] InutilizarNFeDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<object>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var (uf, ambiente, cert) = await LoadEmpresaFiscalData(conn, empresaId);

            const string queryCnpj = "SELECT cnpj FROM empresa WHERE id = @empresa_id;";
            await using var cmdCnpj = new NpgsqlCommand(queryCnpj, conn);
            cmdCnpj.Parameters.AddWithValue("@empresa_id", empresaId);
            var cnpj = (string)(await cmdCnpj.ExecuteScalarAsync())!;

            var xml = _xmlBuilder.BuildInutilizacaoXml(cnpj, ambiente, uf, dto.serie, dto.numero_inicial, dto.numero_final, dto.justificativa, dto.modelo);
            var signedXml = _xmlSigner.SignXml(xml, cert, "infInut");

            var envioXml = $@"<nfeDadosMsg xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeInutilizacao4"">{signedXml}</nfeDadosMsg>";

            var endpoints = SefazEndpoints.GetEndpoints(uf, ambiente, dto.modelo);
            var sefazResponse = await _sefazClient.SendAsync(
                endpoints.inutilizacao, envioXml, cert,
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeInutilizacao4/nfeInutilizacaoNF"
            );

            var sucesso = sefazResponse.Contains("<cStat>102</cStat>");
            response.Success = sucesso;
            response.Message = sucesso ? "Numeração inutilizada com sucesso." : "Falha ao inutilizar numeração.";
            response.Data = new { xml_retorno = sefazResponse };
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao inutilizar: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    [Authorize]
    [HttpGet("Get/NFe/Pdf/{id}")]
    public async Task<IActionResult> GetNFePdf(int id)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string queryNota = @"
                SELECT nf.modelo, nf.serie, nf.numero, nf.chave_acesso, nf.protocolo_autorizacao, nf.data_emissao, nf.natureza_operacao, nf.dest_nome, nf.dest_cpf_cnpj, nf.valor_total, nf.ambiente,
                       e.razao_social, e.fantasia, e.cnpj, e.ie, e.uf
                FROM nota_fiscal nf
                JOIN empresa e ON e.id = nf.empresa_id
                WHERE nf.id = @id AND nf.empresa_id = @empresa_id;";

            await using var cmdNota = new NpgsqlCommand(queryNota, conn);
            cmdNota.Parameters.AddWithValue("@id", id);
            cmdNota.Parameters.AddWithValue("@empresa_id", empresaId);
            await using var reader = await cmdNota.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new Response<string> { Success = false, Message = "Nota não encontrada." });

            var modelo = reader.GetInt32(reader.GetOrdinal("modelo"));
            var serie = reader.GetInt32(reader.GetOrdinal("serie")).ToString();
            var numero = reader.GetInt32(reader.GetOrdinal("numero")).ToString();
            var chave = reader.IsDBNull(reader.GetOrdinal("chave_acesso")) ? "" : reader.GetString(reader.GetOrdinal("chave_acesso"));
            var protocolo = reader.IsDBNull(reader.GetOrdinal("protocolo_autorizacao")) ? "" : reader.GetString(reader.GetOrdinal("protocolo_autorizacao"));
            var dataEmissao = reader.IsDBNull(reader.GetOrdinal("data_emissao")) ? DateTime.Now : reader.GetDateTime(reader.GetOrdinal("data_emissao"));
            var naturezaOp = reader.IsDBNull(reader.GetOrdinal("natureza_operacao")) ? "VENDA" : reader.GetString(reader.GetOrdinal("natureza_operacao"));
            var destNome = reader.IsDBNull(reader.GetOrdinal("dest_nome")) ? null : reader.GetString(reader.GetOrdinal("dest_nome"));
            var destCpfCnpj = reader.IsDBNull(reader.GetOrdinal("dest_cpf_cnpj")) ? null : reader.GetString(reader.GetOrdinal("dest_cpf_cnpj"));
            var valorTotal = reader.IsDBNull(reader.GetOrdinal("valor_total")) ? 0m : reader.GetDecimal(reader.GetOrdinal("valor_total"));
            var ambiente = reader.GetInt32(reader.GetOrdinal("ambiente"));
            var razaoSocial = reader.IsDBNull(reader.GetOrdinal("razao_social")) ? "" : reader.GetString(reader.GetOrdinal("razao_social"));
            var fantasia = reader.IsDBNull(reader.GetOrdinal("fantasia")) ? "" : reader.GetString(reader.GetOrdinal("fantasia"));
            var cnpj = reader.IsDBNull(reader.GetOrdinal("cnpj")) ? "" : reader.GetString(reader.GetOrdinal("cnpj"));
            var ie = reader.IsDBNull(reader.GetOrdinal("ie")) ? "" : reader.GetString(reader.GetOrdinal("ie"));
            var uf = reader.IsDBNull(reader.GetOrdinal("uf")) ? "" : reader.GetString(reader.GetOrdinal("uf"));
            await reader.CloseAsync();

            const string queryItens = @"SELECT codigo, descricao, ncm, cfop, unidade, quantidade, valor_unitario, valor_total, icms_base_calculo, icms_valor, icms_aliquota FROM nota_fiscal_item WHERE nota_fiscal_id = @nota_id ORDER BY numero_item;";
            await using var cmdItens = new NpgsqlCommand(queryItens, conn);
            cmdItens.Parameters.AddWithValue("@nota_id", id);
            await using var readerItens = await cmdItens.ExecuteReaderAsync();

            byte[] pdf;
            if (modelo == 65)
            {
                var itens = new List<DanfceItemData>();
                while (await readerItens.ReadAsync())
                {
                    itens.Add(new DanfceItemData
                    {
                        Codigo = readerItens.IsDBNull(0) ? "" : readerItens.GetString(0),
                        Descricao = readerItens.IsDBNull(1) ? "" : readerItens.GetString(1),
                        Quantidade = readerItens.IsDBNull(5) ? 0 : readerItens.GetDecimal(5),
                        Unidade = readerItens.IsDBNull(4) ? "UN" : readerItens.GetString(4),
                        ValorUnitario = readerItens.IsDBNull(6) ? 0 : readerItens.GetDecimal(6),
                        ValorTotal = readerItens.IsDBNull(7) ? 0 : readerItens.GetDecimal(7),
                    });
                }

                var qrCodeImage = _qrCodeService.GenerateQrCode(chave);
                pdf = _danfceService.GeneratePdf(new DanfceData
                {
                    ChaveAcesso = chave,
                    Protocolo = protocolo,
                    Numero = numero,
                    Serie = serie,
                    DataEmissao = dataEmissao,
                    RazaoSocial = razaoSocial,
                    CNPJ = cnpj,
                    Endereco = $"{uf}",
                    DestNome = destNome,
                    DestCpfCnpj = destCpfCnpj,
                    ValorTotal = valorTotal,
                    FormaPagamento = "Dinheiro",
                    QrCodeImage = qrCodeImage,
                    Itens = itens,
                    IsHomologacao = ambiente == 2
                });
            }
            else
            {
                var itens = new List<DanfeItemData>();
                while (await readerItens.ReadAsync())
                {
                    itens.Add(new DanfeItemData
                    {
                        Codigo = readerItens.IsDBNull(0) ? "" : readerItens.GetString(0),
                        Descricao = readerItens.IsDBNull(1) ? "" : readerItens.GetString(1),
                        NCM = readerItens.IsDBNull(2) ? "" : readerItens.GetString(2),
                        CFOP = readerItens.IsDBNull(3) ? "" : readerItens.GetString(3),
                        Unidade = readerItens.IsDBNull(4) ? "UN" : readerItens.GetString(4),
                        Quantidade = readerItens.IsDBNull(5) ? 0 : readerItens.GetDecimal(5),
                        ValorUnitario = readerItens.IsDBNull(6) ? 0 : readerItens.GetDecimal(6),
                        ValorTotal = readerItens.IsDBNull(7) ? 0 : readerItens.GetDecimal(7),
                        IcmsBase = readerItens.IsDBNull(8) ? 0 : readerItens.GetDecimal(8),
                        IcmsValor = readerItens.IsDBNull(9) ? 0 : readerItens.GetDecimal(9),
                        IcmsAliquota = readerItens.IsDBNull(10) ? 0 : readerItens.GetDecimal(10),
                    });
                }

                pdf = _danfeService.GeneratePdf(new DanfeData
                {
                    ChaveAcesso = chave,
                    Protocolo = protocolo,
                    Numero = numero,
                    Serie = serie,
                    DataEmissao = dataEmissao,
                    NaturezaOperacao = naturezaOp,
                    RazaoSocial = razaoSocial,
                    Fantasia = fantasia,
                    CNPJ = cnpj,
                    IE = ie,
                    UF = uf,
                    DestNome = destNome,
                    DestCpfCnpj = destCpfCnpj,
                    ValorProdutos = valorTotal,
                    ValorTotal = valorTotal,
                    ValorIcms = itens.Sum(i => i.IcmsValor),
                    BaseCalculo = itens.Sum(i => i.IcmsBase),
                    Itens = itens,
                    IsHomologacao = ambiente == 2
                });
            }

            return File(pdf, "application/pdf", $"danfe_{numero}.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new Response<string> { Success = false, Message = $"Erro ao gerar PDF: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpGet("Get/NFe/Xml/{id}")]
    public async Task<IActionResult> GetNFeXml(int id)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var empresaId = User.GetEmpresaId();

        try
        {
            const string query = @"SELECT xml_proc, xml_envio, numero FROM nota_fiscal WHERE id = @id AND empresa_id = @empresa_id;";
            await using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@empresa_id", empresaId);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return NotFound(new Response<string> { Success = false, Message = "Nota não encontrada." });

            var xmlProc = reader.IsDBNull(0) ? null : reader.GetString(0);
            var xmlEnvio = reader.IsDBNull(1) ? null : reader.GetString(1);
            var numero = reader.GetInt32(2);

            var xml = xmlProc ?? xmlEnvio ?? "";
            var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
            return File(bytes, "application/xml", $"nfe_{numero}.xml");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new Response<string> { Success = false, Message = $"Erro ao buscar XML: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpPost("Post/CartaCorrecao/NFe")]
    public async Task<IActionResult> CartaCorrecao([FromBody] CartaCorrecaoDTO dto)
    {
        await using var conn = NovaConexao();
        await conn.OpenAsync();
        var response = new Response<object>();
        var empresaId = User.GetEmpresaId();

        try
        {
            var (uf, ambiente, cert) = await LoadEmpresaFiscalData(conn, empresaId);

            const string queryNota = @"SELECT chave_acesso, modelo FROM nota_fiscal WHERE id = @id AND empresa_id = @empresa_id AND status = 'autorizada';";
            await using var cmdNota = new NpgsqlCommand(queryNota, conn);
            cmdNota.Parameters.AddWithValue("@id", dto.nota_fiscal_id);
            cmdNota.Parameters.AddWithValue("@empresa_id", empresaId);
            await using var reader = await cmdNota.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                response.Success = false;
                response.Message = "Nota fiscal não encontrada ou não pode receber carta de correção.";
                return BadRequest(response);
            }

            var chave = reader.GetString(0);
            var modelo = reader.GetInt32(1);
            await reader.CloseAsync();

            const string queryCnpj = "SELECT cnpj FROM empresa WHERE id = @empresa_id;";
            await using var cmdCnpj = new NpgsqlCommand(queryCnpj, conn);
            cmdCnpj.Parameters.AddWithValue("@empresa_id", empresaId);
            var cnpj = (string)(await cmdCnpj.ExecuteScalarAsync())!;

            var xml = BuildCartaCorrecaoXml(chave, dto.correcao, cnpj, ambiente, dto.sequencia);
            var signedXml = _xmlSigner.SignXml(xml, cert, "infEvento");

            var envioXml = $@"<envEvento versao=""1.00"" xmlns=""http://www.portalfiscal.inf.br/nfe"">{signedXml}</envEvento>";
            var loteEvento = $@"<nfeDadosMsg xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4"">{envioXml}</nfeDadosMsg>";

            var endpoints = SefazEndpoints.GetEndpoints(uf, ambiente, modelo);
            var sefazResponse = await _sefazClient.SendAsync(
                endpoints.recepcaoEvento, loteEvento, cert,
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeRecepcaoEvento4/nfeRecepcaoEvento"
            );

            var sucesso = sefazResponse.Contains("<cStat>135</cStat>") || sefazResponse.Contains("<cStat>155</cStat>");
            response.Success = sucesso;
            response.Message = sucesso ? "Carta de correção registrada com sucesso." : "Falha ao registrar carta de correção.";
            response.Data = new { xml_retorno = sefazResponse };
            return Ok(response);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Message = $"Erro ao emitir carta de correção: {ex.Message}";
            return StatusCode(500, response);
        }
    }

    private string BuildCartaCorrecaoXml(string chaveAcesso, string correcao, string cnpj, int ambiente, int sequencia)
    {
        var dataEvento = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
        var idEvento = $"ID110110{chaveAcesso}{sequencia.ToString().PadLeft(2, '0')}";
        var ns = "http://www.portalfiscal.inf.br/nfe";

        var xml = $@"<evento versao=""1.00"" xmlns=""{ns}""><infEvento Id=""{idEvento}""><cOrgao>{chaveAcesso.Substring(0, 2)}</cOrgao><tpAmb>{ambiente}</tpAmb><CNPJ>{cnpj}</CNPJ><chNFe>{chaveAcesso}</chNFe><dhEvento>{dataEvento}</dhEvento><tpEvento>110110</tpEvento><nSeqEvento>{sequencia}</nSeqEvento><verEvento>1.00</verEvento><detEvento versao=""1.00""><descEvento>Carta de Correcao</descEvento><xCorrecao>{correcao}</xCorrecao><xCondUso>A Carta de Correcao e disciplinada pelo paragrafo 1o-A do art. 7o do Convenio S/N, de 15 de dezembro de 1970 e pode ser utilizada para regularizacao de erro ocorrido na emissao de documento fiscal, desde que o erro nao esteja relacionado com: I - as variaveis que determinam o valor do imposto tais como: base de calculo, aliquota, diferenca de preco, quantidade, valor da operacao ou da prestacao; II - a correcao de dados cadastrais que implique mudanca do remetente ou do destinatario; III - a data de emissao ou de saida.</xCondUso></detEvento></infEvento></evento>";
        return xml;
    }

    #region Helpers

    private async Task<(string uf, int ambiente, X509Certificate2 cert)> LoadEmpresaFiscalData(NpgsqlConnection conn, int empresaId)
    {
        const string query = @"SELECT e.uf, e.nfe_ambiente, c.pfx_data, c.senha_hash
            FROM empresa e
            LEFT JOIN certificado_digital c ON c.empresa_id = e.id
            WHERE e.id = @empresa_id;";

        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new Exception("Empresa não encontrada.");

        var uf = reader.IsDBNull(reader.GetOrdinal("uf")) ? "SP" : reader.GetString(reader.GetOrdinal("uf"));
        var ambiente = reader.IsDBNull(reader.GetOrdinal("nfe_ambiente")) ? 2 : reader.GetInt32(reader.GetOrdinal("nfe_ambiente"));

        if (reader.IsDBNull(reader.GetOrdinal("pfx_data")))
            throw new Exception("Certificado digital não cadastrado. Faça o upload primeiro.");

        var pfxData = (byte[])reader["pfx_data"];
        var senha = reader.GetString(reader.GetOrdinal("senha_hash"));
        await reader.CloseAsync();

        var cert = _certService.LoadCertificate(pfxData, senha);
        if (!_certService.IsCertificateValid(cert))
            throw new Exception("Certificado digital expirado.");

        return (uf, ambiente, cert);
    }

    private async Task<EmpresaFiscalInfo> LoadEmpresaData(NpgsqlConnection conn, int empresaId)
    {
        const string query = @"SELECT cnpj, razao_social, fantasia, ie, im, uf, regime_tributario, csc_id, csc_token FROM empresa WHERE id = @empresa_id;";
        await using var cmd = new NpgsqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new Exception("Empresa não encontrada.");

        return new EmpresaFiscalInfo
        {
            cnpj = reader.IsDBNull(reader.GetOrdinal("cnpj")) ? "" : reader.GetString(reader.GetOrdinal("cnpj")),
            razao_social = reader.IsDBNull(reader.GetOrdinal("razao_social")) ? "" : reader.GetString(reader.GetOrdinal("razao_social")),
            fantasia = reader.IsDBNull(reader.GetOrdinal("fantasia")) ? "" : reader.GetString(reader.GetOrdinal("fantasia")),
            ie = reader.IsDBNull(reader.GetOrdinal("ie")) ? "" : reader.GetString(reader.GetOrdinal("ie")),
            uf = reader.IsDBNull(reader.GetOrdinal("uf")) ? "SP" : reader.GetString(reader.GetOrdinal("uf")),
            regime_tributario = reader.IsDBNull(reader.GetOrdinal("regime_tributario")) ? 1 : reader.GetInt32(reader.GetOrdinal("regime_tributario")),
            csc_id = reader.IsDBNull(reader.GetOrdinal("csc_id")) ? null : reader.GetString(reader.GetOrdinal("csc_id")),
            csc_token = reader.IsDBNull(reader.GetOrdinal("csc_token")) ? null : reader.GetString(reader.GetOrdinal("csc_token")),
        };
    }

    private async Task<int> GetProximoNumero(NpgsqlConnection conn, NpgsqlTransaction transaction, int empresaId, int modelo, int serie)
    {
        const string query = @"
            INSERT INTO empresa_nfe_sequencia (empresa_id, modelo, serie, ultimo_numero)
            VALUES (@empresa_id, @modelo, @serie, 1)
            ON CONFLICT (empresa_id, modelo, serie) DO UPDATE SET ultimo_numero = empresa_nfe_sequencia.ultimo_numero + 1
            RETURNING ultimo_numero;";

        await using var cmd = new NpgsqlCommand(query, conn, transaction);
        cmd.Parameters.AddWithValue("@empresa_id", empresaId);
        cmd.Parameters.AddWithValue("@modelo", modelo);
        cmd.Parameters.AddWithValue("@serie", serie);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private (string statusCode, string motivo, string? protocolo, string? chaveAcesso) ParseSefazResponse(string xml)
    {
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(xml);
            var ns = "http://www.portalfiscal.inf.br/nfe";

            var cStat = doc.Descendants(System.Xml.Linq.XName.Get("cStat", ns)).LastOrDefault()?.Value ?? "999";
            var xMotivo = doc.Descendants(System.Xml.Linq.XName.Get("xMotivo", ns)).LastOrDefault()?.Value ?? "Erro desconhecido";
            var nProt = doc.Descendants(System.Xml.Linq.XName.Get("nProt", ns)).FirstOrDefault()?.Value;
            var chNFe = doc.Descendants(System.Xml.Linq.XName.Get("chNFe", ns)).FirstOrDefault()?.Value;

            return (cStat, xMotivo, nProt, chNFe);
        }
        catch
        {
            return ("999", "Erro ao parsear resposta da SEFAZ", null, null);
        }
    }

    private string BuildLoteXml(string nfeXml, int ambiente)
    {
        var idLote = DateTime.Now.ToString("yyMMddHHmmss");
        return $@"<nfeDadosMsg xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4""><enviNFe versao=""4.00"" xmlns=""http://www.portalfiscal.inf.br/nfe""><idLote>{idLote}</idLote><indSinc>1</indSinc>{nfeXml}</enviNFe></nfeDadosMsg>";
    }

    #endregion
}

public class EmpresaFiscalInfo
{
    public string cnpj { get; set; } = "";
    public string razao_social { get; set; } = "";
    public string fantasia { get; set; } = "";
    public string ie { get; set; } = "";
    public string uf { get; set; } = "";
    public int regime_tributario { get; set; }
    public string? csc_id { get; set; }
    public string? csc_token { get; set; }
    public string logradouro { get; set; } = "";
    public string numero_end { get; set; } = "";
    public string bairro { get; set; } = "";
    public int cmun { get; set; }
    public string xmun { get; set; } = "";
    public string cep { get; set; } = "";
    public string fone { get; set; } = "";
}
