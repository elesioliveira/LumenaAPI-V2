using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public interface IDanfeService
{
    byte[] GeneratePdf(DanfeData data);
}

public class DanfeData
{
    public string ChaveAcesso { get; set; } = "";
    public string Protocolo { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public string NaturezaOperacao { get; set; } = "";
    public string RazaoSocial { get; set; } = "";
    public string Fantasia { get; set; } = "";
    public string CNPJ { get; set; } = "";
    public string IE { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string Municipio { get; set; } = "";
    public string UF { get; set; } = "";
    public string Fone { get; set; } = "";

    public string? DestNome { get; set; }
    public string? DestCpfCnpj { get; set; }
    public string? DestEndereco { get; set; }
    public string? DestMunicipio { get; set; }
    public string? DestUF { get; set; }

    public decimal ValorProdutos { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorIcms { get; set; }
    public decimal BaseCalculo { get; set; }

    public List<DanfeItemData> Itens { get; set; } = new();
    public bool IsHomologacao { get; set; }
}

public class DanfeItemData
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string NCM { get; set; } = "";
    public string CFOP { get; set; } = "";
    public string Unidade { get; set; } = "";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal IcmsBase { get; set; }
    public decimal IcmsValor { get; set; }
    public decimal IcmsAliquota { get; set; }
}

public class DanfeService : IDanfeService
{
    public byte[] GeneratePdf(DanfeData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(10, Unit.Millimetre);
                page.MarginVertical(10, Unit.Millimetre);

                page.Content().Column(col =>
                {
                    // Header
                    col.Item().Border(1).Padding(3, Unit.Millimetre).Row(row =>
                    {
                        row.RelativeItem(3).Column(c =>
                        {
                            c.Item().Text(data.RazaoSocial).Bold().FontSize(11);
                            if (!string.IsNullOrEmpty(data.Fantasia))
                                c.Item().Text(data.Fantasia).FontSize(9);
                            c.Item().Text(data.Endereco).FontSize(8);
                            c.Item().Text($"{data.Municipio} - {data.UF} | Fone: {data.Fone}").FontSize(8);
                        });

                        row.RelativeItem(2).Border(1).Padding(2, Unit.Millimetre).Column(c =>
                        {
                            c.Item().AlignCenter().Text("DANFE").Bold().FontSize(12);
                            c.Item().AlignCenter().Text("Documento Auxiliar da Nota Fiscal Eletrônica").FontSize(7);
                            c.Item().AlignCenter().Text("0 - ENTRADA  1 - SAÍDA").FontSize(7);
                            c.Item().AlignCenter().Text("1").Bold().FontSize(10);
                            c.Item().AlignCenter().Text($"Nº {data.Numero}").FontSize(9);
                            c.Item().AlignCenter().Text($"Série {data.Serie}").FontSize(9);
                        });

                        row.RelativeItem(3).Padding(2, Unit.Millimetre).Column(c =>
                        {
                            c.Item().Text("CHAVE DE ACESSO").FontSize(7);
                            c.Item().Text(FormatChaveAcesso(data.ChaveAcesso)).FontSize(8).Bold();
                            c.Item().PaddingTop(3, Unit.Millimetre).Text($"Protocolo: {data.Protocolo}").FontSize(7);
                            c.Item().Text($"Data: {data.DataEmissao:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                        });
                    });

                    if (data.IsHomologacao)
                    {
                        col.Item().PaddingVertical(2, Unit.Millimetre).AlignCenter()
                            .Text("SEM VALOR FISCAL - EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO").Bold().FontSize(9);
                    }

                    // Natureza da Operação
                    col.Item().Border(1).Padding(2, Unit.Millimetre).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("NATUREZA DA OPERAÇÃO").FontSize(6);
                            c.Item().Text(data.NaturezaOperacao).FontSize(9);
                        });
                    });

                    // Emitente
                    col.Item().Border(1).Padding(2, Unit.Millimetre).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("CNPJ").FontSize(6);
                            c.Item().Text(FormatCnpj(data.CNPJ)).FontSize(9);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("INSCRIÇÃO ESTADUAL").FontSize(6);
                            c.Item().Text(data.IE).FontSize(9);
                        });
                    });

                    // Destinatário
                    col.Item().Border(1).Padding(2, Unit.Millimetre).Column(c =>
                    {
                        c.Item().Text("DESTINATÁRIO / REMETENTE").Bold().FontSize(7);
                        c.Item().Row(row =>
                        {
                            row.RelativeItem(3).Column(dc =>
                            {
                                dc.Item().Text("NOME / RAZÃO SOCIAL").FontSize(6);
                                dc.Item().Text(data.DestNome ?? "-").FontSize(9);
                            });
                            row.RelativeItem(2).Column(dc =>
                            {
                                dc.Item().Text("CPF/CNPJ").FontSize(6);
                                dc.Item().Text(data.DestCpfCnpj ?? "-").FontSize(9);
                            });
                        });
                    });

                    // Items table
                    col.Item().PaddingTop(3, Unit.Millimetre).Border(1).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(0.8f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("Código").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("Descrição").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("NCM").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("CFOP").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("Un").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("Qtd").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("V.Unit").FontSize(6).Bold();
                            header.Cell().BorderBottom(1).Padding(1, Unit.Millimetre).Text("V.Total").FontSize(6).Bold();
                        });

                        foreach (var item in data.Itens)
                        {
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.Codigo).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.Descricao).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.NCM).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.CFOP).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.Unidade).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.Quantidade.ToString("F2")).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.ValorUnitario.ToString("F2")).FontSize(7);
                            table.Cell().Padding(1, Unit.Millimetre).Text(item.ValorTotal.ToString("F2")).FontSize(7);
                        }
                    });

                    // Totals
                    col.Item().Border(1).Padding(2, Unit.Millimetre).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("BASE CÁLCULO ICMS").FontSize(6);
                            c.Item().Text(data.BaseCalculo.ToString("F2")).FontSize(9);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("VALOR ICMS").FontSize(6);
                            c.Item().Text(data.ValorIcms.ToString("F2")).FontSize(9);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("VALOR PRODUTOS").FontSize(6);
                            c.Item().Text(data.ValorProdutos.ToString("F2")).FontSize(9);
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("VALOR TOTAL DA NF").FontSize(6);
                            c.Item().Text(data.ValorTotal.ToString("F2")).Bold().FontSize(9);
                        });
                    });
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private string FormatCnpj(string cnpj)
    {
        if (cnpj.Length != 14) return cnpj;
        return $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}";
    }

    private string FormatChaveAcesso(string chave)
    {
        if (chave.Length != 44) return chave;
        var parts = new List<string>();
        for (int i = 0; i < 44; i += 4)
            parts.Add(chave.Substring(i, Math.Min(4, 44 - i)));
        return string.Join(" ", parts);
    }
}
