using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

public interface IXmlBuilderService
{
    string BuildNFCeXml(NFeData data);
    string BuildNFeXml(NFeData data);
    string BuildStatusServicoXml(string uf, int ambiente);
    string BuildCancelamentoXml(string chaveAcesso, string protocolo, string justificativa, string cnpj, int ambiente);
    string BuildInutilizacaoXml(string cnpj, int ambiente, string uf, int serie, int numeroInicial, int numeroFinal, string justificativa, int modelo);
    string GenerateChaveAcesso(int cUF, DateTime dataEmissao, string cnpj, int modelo, int serie, int numero, int tpEmis, int cNF);
}

public class NFeData
{
    public int Modelo { get; set; }
    public int Serie { get; set; }
    public int Numero { get; set; }
    public int Ambiente { get; set; }
    public string UF { get; set; } = "";
    public string CNPJ { get; set; } = "";
    public string IE { get; set; } = "";
    public string RazaoSocial { get; set; } = "";
    public string Fantasia { get; set; } = "";
    public int RegimeTributario { get; set; }
    public string NaturezaOperacao { get; set; } = "VENDA";
    public string? CscId { get; set; }
    public string? CscToken { get; set; }

    // Emitente Endereço
    public string EmitLogradouro { get; set; } = "";
    public string EmitNumero { get; set; } = "";
    public string EmitBairro { get; set; } = "";
    public int EmitCMun { get; set; }
    public string EmitXMun { get; set; } = "";
    public string EmitCEP { get; set; } = "";
    public string EmitFone { get; set; } = "";

    // Destinatário (opcional para NFCe)
    public string? DestCpfCnpj { get; set; }
    public string? DestNome { get; set; }
    public string? DestUF { get; set; }
    public int DestIndIEDest { get; set; } = 9;

    // Itens
    public List<NFeItemData> Itens { get; set; } = new();

    // Pagamento
    public List<NFePagamento> Pagamentos { get; set; } = new();
}

public class NFeItemData
{
    public int NumeroItem { get; set; }
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string NCM { get; set; } = "";
    public string? CEST { get; set; }
    public string CFOP { get; set; } = "";
    public string Unidade { get; set; } = "UN";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public int Origem { get; set; }
    public string IcmsCst { get; set; } = "";
    public decimal? IcmsAliquota { get; set; }
    public decimal? IcmsReducaoBc { get; set; }
    public string PisCst { get; set; } = "07";
    public decimal? PisAliquota { get; set; }
    public string CofinsCst { get; set; } = "07";
    public decimal? CofinsAliquota { get; set; }
}

public class NFePagamento
{
    public string FormaPagamento { get; set; } = "01";
    public decimal Valor { get; set; }
}

public class XmlBuilderService : IXmlBuilderService
{
    private static readonly XNamespace NFeNs = "http://www.portalfiscal.inf.br/nfe";

    public string BuildStatusServicoXml(string uf, int ambiente)
    {
        var cUF = SefazEndpoints.GetCodigoUF(uf);
        var xml = new XElement(NFeNs + "consStatServ",
            new XAttribute("versao", "4.00"),
            new XAttribute(XNamespace.Xmlns + "xmlns", NFeNs.NamespaceName),
            new XElement(NFeNs + "tpAmb", ambiente),
            new XElement(NFeNs + "cUF", cUF),
            new XElement(NFeNs + "xServ", "STATUS")
        );

        return $@"<nfeDadosMsg xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4"">{xml}</nfeDadosMsg>";
    }

    public string BuildNFCeXml(NFeData data)
    {
        return BuildNFXml(data, 65);
    }

    public string BuildNFeXml(NFeData data)
    {
        return BuildNFXml(data, 55);
    }

    private string BuildNFXml(NFeData data, int modelo)
    {
        var cUF = SefazEndpoints.GetCodigoUF(data.UF);
        var dataEmissao = DateTime.Now;
        var cNF = new Random().Next(10000000, 99999999);
        var tpEmis = 1;

        var chave = GenerateChaveAcesso(cUF, dataEmissao, data.CNPJ, modelo, data.Serie, data.Numero, tpEmis, cNF);
        var digitoVerificador = CalculateDigitoVerificador(chave.Substring(0, 43));

        var ide = new XElement(NFeNs + "ide",
            new XElement(NFeNs + "cUF", cUF),
            new XElement(NFeNs + "cNF", cNF.ToString("D8")),
            new XElement(NFeNs + "natOp", data.NaturezaOperacao),
            new XElement(NFeNs + "mod", modelo),
            new XElement(NFeNs + "serie", data.Serie),
            new XElement(NFeNs + "nNF", data.Numero),
            new XElement(NFeNs + "dhEmi", dataEmissao.ToString("yyyy-MM-ddTHH:mm:sszzz")),
            new XElement(NFeNs + "tpNF", "1"),
            new XElement(NFeNs + "idDest", modelo == 65 ? "1" : "1"),
            new XElement(NFeNs + "cMunFG", data.EmitCMun),
            new XElement(NFeNs + "tpImp", modelo == 65 ? "4" : "1"),
            new XElement(NFeNs + "tpEmis", tpEmis),
            new XElement(NFeNs + "cDV", digitoVerificador),
            new XElement(NFeNs + "tpAmb", data.Ambiente),
            new XElement(NFeNs + "finNFe", "1"),
            new XElement(NFeNs + "indFinal", modelo == 65 ? "1" : "0"),
            new XElement(NFeNs + "indPres", modelo == 65 ? "1" : "0"),
            new XElement(NFeNs + "procEmi", "0"),
            new XElement(NFeNs + "verProc", "LumenaERP 1.0")
        );

        var emit = new XElement(NFeNs + "emit",
            new XElement(NFeNs + "CNPJ", data.CNPJ),
            new XElement(NFeNs + "xNome", data.Ambiente == 2 ? "NF-E EMITIDA EM AMBIENTE DE HOMOLOGACAO - SEM VALOR FISCAL" : data.RazaoSocial),
            new XElement(NFeNs + "xFant", data.Fantasia),
            new XElement(NFeNs + "enderEmit",
                new XElement(NFeNs + "xLgr", data.EmitLogradouro),
                new XElement(NFeNs + "nro", data.EmitNumero),
                new XElement(NFeNs + "xBairro", data.EmitBairro),
                new XElement(NFeNs + "cMun", data.EmitCMun),
                new XElement(NFeNs + "xMun", data.EmitXMun),
                new XElement(NFeNs + "UF", data.UF),
                new XElement(NFeNs + "CEP", data.EmitCEP),
                new XElement(NFeNs + "cPais", "1058"),
                new XElement(NFeNs + "xPais", "BRASIL"),
                new XElement(NFeNs + "fone", data.EmitFone)
            ),
            new XElement(NFeNs + "IE", data.IE),
            new XElement(NFeNs + "CRT", data.RegimeTributario)
        );

        XElement? dest = null;
        if (!string.IsNullOrEmpty(data.DestCpfCnpj))
        {
            dest = new XElement(NFeNs + "dest",
                data.DestCpfCnpj.Length == 11
                    ? new XElement(NFeNs + "CPF", data.DestCpfCnpj)
                    : new XElement(NFeNs + "CNPJ", data.DestCpfCnpj),
                new XElement(NFeNs + "xNome", data.DestNome ?? "CONSUMIDOR NAO IDENTIFICADO"),
                new XElement(NFeNs + "indIEDest", data.DestIndIEDest)
            );
        }
        else if (modelo == 55)
        {
            dest = new XElement(NFeNs + "dest",
                new XElement(NFeNs + "xNome", "CONSUMIDOR NAO IDENTIFICADO"),
                new XElement(NFeNs + "indIEDest", "9")
            );
        }

        var detElements = new List<XElement>();
        decimal totalProdutos = 0;
        decimal totalIcms = 0;
        decimal totalPis = 0;
        decimal totalCofins = 0;

        foreach (var item in data.Itens)
        {
            totalProdutos += item.ValorTotal;
            var icmsValor = (item.IcmsAliquota ?? 0) > 0 ? item.ValorTotal * (item.IcmsAliquota!.Value / 100m) : 0;
            var pisValor = (item.PisAliquota ?? 0) > 0 ? item.ValorTotal * (item.PisAliquota!.Value / 100m) : 0;
            var cofinsValor = (item.CofinsAliquota ?? 0) > 0 ? item.ValorTotal * (item.CofinsAliquota!.Value / 100m) : 0;
            totalIcms += icmsValor;
            totalPis += pisValor;
            totalCofins += cofinsValor;

            var icmsGroup = BuildIcmsGroup(item, icmsValor, data.RegimeTributario);

            var det = new XElement(NFeNs + "det",
                new XAttribute("nItem", item.NumeroItem),
                new XElement(NFeNs + "prod",
                    new XElement(NFeNs + "cProd", item.Codigo),
                    new XElement(NFeNs + "cEAN", "SEM GTIN"),
                    new XElement(NFeNs + "xProd", item.Descricao),
                    new XElement(NFeNs + "NCM", item.NCM),
                    item.CEST != null ? new XElement(NFeNs + "CEST", item.CEST) : null!,
                    new XElement(NFeNs + "CFOP", item.CFOP),
                    new XElement(NFeNs + "uCom", item.Unidade),
                    new XElement(NFeNs + "qCom", item.Quantidade.ToString("F4")),
                    new XElement(NFeNs + "vUnCom", item.ValorUnitario.ToString("F10")),
                    new XElement(NFeNs + "vProd", item.ValorTotal.ToString("F2")),
                    new XElement(NFeNs + "cEANTrib", "SEM GTIN"),
                    new XElement(NFeNs + "uTrib", item.Unidade),
                    new XElement(NFeNs + "qTrib", item.Quantidade.ToString("F4")),
                    new XElement(NFeNs + "vUnTrib", item.ValorUnitario.ToString("F10")),
                    new XElement(NFeNs + "indTot", "1")
                ),
                new XElement(NFeNs + "imposto",
                    icmsGroup,
                    new XElement(NFeNs + "PIS",
                        new XElement(NFeNs + "PISOutr",
                            new XElement(NFeNs + "CST", item.PisCst),
                            new XElement(NFeNs + "vBC", pisValor > 0 ? item.ValorTotal.ToString("F2") : "0.00"),
                            new XElement(NFeNs + "pPIS", (item.PisAliquota ?? 0).ToString("F4")),
                            new XElement(NFeNs + "vPIS", pisValor.ToString("F2"))
                        )
                    ),
                    new XElement(NFeNs + "COFINS",
                        new XElement(NFeNs + "COFINSOutr",
                            new XElement(NFeNs + "CST", item.CofinsCst),
                            new XElement(NFeNs + "vBC", cofinsValor > 0 ? item.ValorTotal.ToString("F2") : "0.00"),
                            new XElement(NFeNs + "pCOFINS", (item.CofinsAliquota ?? 0).ToString("F4")),
                            new XElement(NFeNs + "vCOFINS", cofinsValor.ToString("F2"))
                        )
                    )
                )
            );
            detElements.Add(det);
        }

        var total = new XElement(NFeNs + "total",
            new XElement(NFeNs + "ICMSTot",
                new XElement(NFeNs + "vBC", totalIcms > 0 ? totalProdutos.ToString("F2") : "0.00"),
                new XElement(NFeNs + "vICMS", totalIcms.ToString("F2")),
                new XElement(NFeNs + "vICMSDeson", "0.00"),
                new XElement(NFeNs + "vFCPUFDest", "0.00"),
                new XElement(NFeNs + "vICMSUFDest", "0.00"),
                new XElement(NFeNs + "vICMSUFRemet", "0.00"),
                new XElement(NFeNs + "vFCP", "0.00"),
                new XElement(NFeNs + "vBCST", "0.00"),
                new XElement(NFeNs + "vST", "0.00"),
                new XElement(NFeNs + "vFCPST", "0.00"),
                new XElement(NFeNs + "vFCPSTRet", "0.00"),
                new XElement(NFeNs + "vProd", totalProdutos.ToString("F2")),
                new XElement(NFeNs + "vFrete", "0.00"),
                new XElement(NFeNs + "vSeg", "0.00"),
                new XElement(NFeNs + "vDesc", "0.00"),
                new XElement(NFeNs + "vII", "0.00"),
                new XElement(NFeNs + "vIPI", "0.00"),
                new XElement(NFeNs + "vIPIDevol", "0.00"),
                new XElement(NFeNs + "vPIS", totalPis.ToString("F2")),
                new XElement(NFeNs + "vCOFINS", totalCofins.ToString("F2")),
                new XElement(NFeNs + "vOutro", "0.00"),
                new XElement(NFeNs + "vNF", totalProdutos.ToString("F2"))
            )
        );

        var transp = new XElement(NFeNs + "transp",
            new XElement(NFeNs + "modFrete", modelo == 65 ? "9" : "9")
        );

        var pagElements = data.Pagamentos.Select(p =>
            new XElement(NFeNs + "detPag",
                new XElement(NFeNs + "tPag", p.FormaPagamento),
                new XElement(NFeNs + "vPag", p.Valor.ToString("F2"))
            )
        );

        var pag = new XElement(NFeNs + "pag", pagElements);

        var infNFe = new XElement(NFeNs + "infNFe",
            new XAttribute("versao", "4.00"),
            new XAttribute("Id", $"NFe{chave}"),
            ide, emit
        );
        if (dest != null) infNFe.Add(dest);
        foreach (var det in detElements) infNFe.Add(det);
        infNFe.Add(total);
        infNFe.Add(transp);
        infNFe.Add(pag);

        if (modelo == 65)
        {
            var infNFeSupl = BuildInfNFeSuplNFCe(chave, data);
            var nfe = new XElement(NFeNs + "NFe",
                new XAttribute(XNamespace.Xmlns + "xmlns", NFeNs.NamespaceName),
                infNFe,
                infNFeSupl
            );
            return nfe.ToString(SaveOptions.DisableFormatting);
        }
        else
        {
            var nfe = new XElement(NFeNs + "NFe",
                new XAttribute(XNamespace.Xmlns + "xmlns", NFeNs.NamespaceName),
                infNFe
            );
            return nfe.ToString(SaveOptions.DisableFormatting);
        }
    }

    private XElement BuildIcmsGroup(NFeItemData item, decimal icmsValor, int regimeTributario)
    {
        if (regimeTributario == 1)
        {
            return new XElement(NFeNs + "ICMS",
                new XElement(NFeNs + "ICMSSN102",
                    new XElement(NFeNs + "orig", item.Origem),
                    new XElement(NFeNs + "CSOSN", item.IcmsCst)
                )
            );
        }

        return new XElement(NFeNs + "ICMS",
            new XElement(NFeNs + "ICMS00",
                new XElement(NFeNs + "orig", item.Origem),
                new XElement(NFeNs + "CST", item.IcmsCst),
                new XElement(NFeNs + "modBC", "3"),
                new XElement(NFeNs + "vBC", item.ValorTotal.ToString("F2")),
                new XElement(NFeNs + "pICMS", (item.IcmsAliquota ?? 0).ToString("F2")),
                new XElement(NFeNs + "vICMS", icmsValor.ToString("F2"))
            )
        );
    }

    private XElement BuildInfNFeSuplNFCe(string chave, NFeData data)
    {
        var urlConsulta = GetUrlConsultaNFCe(data.UF, data.Ambiente);
        var qrCodeUrl = GenerateQrCodeUrl(chave, data);

        return new XElement(NFeNs + "infNFeSupl",
            new XElement(NFeNs + "qrCode", new XCData(qrCodeUrl)),
            new XElement(NFeNs + "urlChave", urlConsulta)
        );
    }

    private string GenerateQrCodeUrl(string chave, NFeData data)
    {
        var baseUrl = GetUrlQrCodeNFCe(data.UF, data.Ambiente);
        var parametros = $"{chave}|{data.Ambiente}|{data.CscId}";

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(parametros + data.CscToken));
        var hashHex = BitConverter.ToString(hash).Replace("-", "").ToLower();

        return $"{baseUrl}?p={chave}|{data.Ambiente}|{data.CscId}|{hashHex}";
    }

    private string GetUrlQrCodeNFCe(string uf, int ambiente)
    {
        var isHomolog = ambiente == 2;
        return uf.ToUpper() switch
        {
            "SP" => isHomolog ? "https://homologacao.nfce.fazenda.sp.gov.br/NFCeConsultaPublica/Paginas/ConsultaQRCode.aspx" : "https://www.nfce.fazenda.sp.gov.br/NFCeConsultaPublica/Paginas/ConsultaQRCode.aspx",
            "RS" => isHomolog ? "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx" : "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx",
            _ => isHomolog ? "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx" : "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx",
        };
    }

    private string GetUrlConsultaNFCe(string uf, int ambiente)
    {
        var isHomolog = ambiente == 2;
        return uf.ToUpper() switch
        {
            "SP" => isHomolog ? "https://homologacao.nfce.fazenda.sp.gov.br/consulta" : "https://www.nfce.fazenda.sp.gov.br/consulta",
            "RS" => isHomolog ? "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx" : "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx",
            _ => isHomolog ? "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx" : "https://www.sefaz.rs.gov.br/NFCE/NFCE-COM.aspx",
        };
    }

    public string GenerateChaveAcesso(int cUF, DateTime dataEmissao, string cnpj, int modelo, int serie, int numero, int tpEmis, int cNF)
    {
        var chave = $"{cUF:D2}{dataEmissao:yyMM}{cnpj}{modelo:D2}{serie:D3}{numero:D9}{tpEmis}{cNF:D8}";
        var dv = CalculateDigitoVerificador(chave);
        return chave + dv;
    }

    private string CalculateDigitoVerificador(string chave)
    {
        int[] weights = { 2, 3, 4, 5, 6, 7, 8, 9 };
        int sum = 0;
        int weightIndex = 0;

        for (int i = chave.Length - 1; i >= 0; i--)
        {
            sum += int.Parse(chave[i].ToString()) * weights[weightIndex];
            weightIndex = (weightIndex + 1) % 8;
        }

        int remainder = sum % 11;
        int digit = remainder < 2 ? 0 : 11 - remainder;
        return digit.ToString();
    }

    public string BuildCancelamentoXml(string chaveAcesso, string protocolo, string justificativa, string cnpj, int ambiente)
    {
        var dataEvento = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
        var sequencia = "1";
        var idEvento = $"ID110111{chaveAcesso}{sequencia.PadLeft(2, '0')}";

        var evento = new XElement(NFeNs + "evento",
            new XAttribute("versao", "1.00"),
            new XAttribute(XNamespace.Xmlns + "xmlns", NFeNs.NamespaceName),
            new XElement(NFeNs + "infEvento",
                new XAttribute("Id", idEvento),
                new XElement(NFeNs + "cOrgao", chaveAcesso.Substring(0, 2)),
                new XElement(NFeNs + "tpAmb", ambiente),
                new XElement(NFeNs + "CNPJ", cnpj),
                new XElement(NFeNs + "chNFe", chaveAcesso),
                new XElement(NFeNs + "dhEvento", dataEvento),
                new XElement(NFeNs + "tpEvento", "110111"),
                new XElement(NFeNs + "nSeqEvento", sequencia),
                new XElement(NFeNs + "verEvento", "1.00"),
                new XElement(NFeNs + "detEvento",
                    new XAttribute("versao", "1.00"),
                    new XElement(NFeNs + "descEvento", "Cancelamento"),
                    new XElement(NFeNs + "nProt", protocolo),
                    new XElement(NFeNs + "xJust", justificativa)
                )
            )
        );

        return evento.ToString(SaveOptions.DisableFormatting);
    }

    public string BuildInutilizacaoXml(string cnpj, int ambiente, string uf, int serie, int numeroInicial, int numeroFinal, string justificativa, int modelo)
    {
        var cUF = SefazEndpoints.GetCodigoUF(uf);
        var ano = DateTime.Now.ToString("yy");
        var id = $"ID{cUF:D2}{ano}{cnpj}{modelo:D2}{serie:D3}{numeroInicial:D9}{numeroFinal:D9}";

        var inutNFe = new XElement(NFeNs + "inutNFe",
            new XAttribute("versao", "4.00"),
            new XAttribute(XNamespace.Xmlns + "xmlns", NFeNs.NamespaceName),
            new XElement(NFeNs + "infInut",
                new XAttribute("Id", id),
                new XElement(NFeNs + "tpAmb", ambiente),
                new XElement(NFeNs + "xServ", "INUTILIZAR"),
                new XElement(NFeNs + "cUF", cUF),
                new XElement(NFeNs + "ano", ano),
                new XElement(NFeNs + "CNPJ", cnpj),
                new XElement(NFeNs + "mod", modelo),
                new XElement(NFeNs + "serie", serie),
                new XElement(NFeNs + "nNFIni", numeroInicial),
                new XElement(NFeNs + "nNFFin", numeroFinal),
                new XElement(NFeNs + "xJust", justificativa)
            )
        );

        return inutNFe.ToString(SaveOptions.DisableFormatting);
    }
}
