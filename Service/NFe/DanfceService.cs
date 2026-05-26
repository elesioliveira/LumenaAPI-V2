using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public interface IDanfceService
{
    byte[] GeneratePdf(DanfceData data);
}

public class DanfceData
{
    public string ChaveAcesso { get; set; } = "";
    public string Protocolo { get; set; } = "";
    public string Numero { get; set; } = "";
    public string Serie { get; set; } = "";
    public DateTime DataEmissao { get; set; }
    public string RazaoSocial { get; set; } = "";
    public string CNPJ { get; set; } = "";
    public string Endereco { get; set; } = "";
    public string? DestNome { get; set; }
    public string? DestCpfCnpj { get; set; }
    public decimal ValorTotal { get; set; }
    public string FormaPagamento { get; set; } = "";
    public byte[]? QrCodeImage { get; set; }
    public string UrlConsulta { get; set; } = "";
    public List<DanfceItemData> Itens { get; set; } = new();
    public bool IsHomologacao { get; set; }
}

public class DanfceItemData
{
    public string Codigo { get; set; } = "";
    public string Descricao { get; set; } = "";
    public decimal Quantidade { get; set; }
    public string Unidade { get; set; } = "";
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}

public class DanfceService : IDanfceService
{
    public byte[] GeneratePdf(DanfceData data)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(80, PageSizes.A4.Height, Unit.Millimetre);
                page.MarginHorizontal(3, Unit.Millimetre);
                page.MarginVertical(5, Unit.Millimetre);

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text("DANFE NFC-e").Bold().FontSize(10);
                    col.Item().AlignCenter().Text("Documento Auxiliar da Nota Fiscal de Consumidor Eletrônica").FontSize(6);

                    if (data.IsHomologacao)
                    {
                        col.Item().AlignCenter().PaddingVertical(2, Unit.Millimetre)
                            .Text("EMITIDA EM AMBIENTE DE HOMOLOGAÇÃO - SEM VALOR FISCAL").FontSize(6).Bold();
                    }

                    col.Item().PaddingVertical(2, Unit.Millimetre).LineHorizontal(0.5f);

                    col.Item().Text(data.RazaoSocial).Bold().FontSize(8);
                    col.Item().Text($"CNPJ: {FormatCnpj(data.CNPJ)}").FontSize(7);
                    col.Item().Text(data.Endereco).FontSize(7);

                    col.Item().PaddingVertical(2, Unit.Millimetre).LineHorizontal(0.5f);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(1.5f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Cód").FontSize(6).Bold();
                            header.Cell().Text("Descrição").FontSize(6).Bold();
                            header.Cell().Text("Qtd").FontSize(6).Bold();
                            header.Cell().Text("V.Unit").FontSize(6).Bold();
                            header.Cell().Text("V.Total").FontSize(6).Bold();
                        });

                        foreach (var item in data.Itens)
                        {
                            table.Cell().Text(item.Codigo).FontSize(6);
                            table.Cell().Text(item.Descricao).FontSize(6);
                            table.Cell().Text(item.Quantidade.ToString("F2")).FontSize(6);
                            table.Cell().Text(item.ValorUnitario.ToString("F2")).FontSize(6);
                            table.Cell().Text(item.ValorTotal.ToString("F2")).FontSize(6);
                        }
                    });

                    col.Item().PaddingVertical(2, Unit.Millimetre).LineHorizontal(0.5f);

                    col.Item().AlignRight().Text($"TOTAL: R$ {data.ValorTotal:F2}").Bold().FontSize(10);
                    col.Item().Text($"Forma de Pagamento: {data.FormaPagamento}").FontSize(7);

                    col.Item().PaddingVertical(2, Unit.Millimetre).LineHorizontal(0.5f);

                    if (!string.IsNullOrEmpty(data.DestNome))
                        col.Item().Text($"Consumidor: {data.DestNome}").FontSize(7);
                    if (!string.IsNullOrEmpty(data.DestCpfCnpj))
                        col.Item().Text($"CPF/CNPJ: {data.DestCpfCnpj}").FontSize(7);

                    col.Item().PaddingVertical(2, Unit.Millimetre).LineHorizontal(0.5f);

                    col.Item().AlignCenter().Text($"NFC-e nº {data.Numero} Série {data.Serie}").FontSize(7);
                    col.Item().AlignCenter().Text($"Data: {data.DataEmissao:dd/MM/yyyy HH:mm:ss}").FontSize(7);
                    col.Item().AlignCenter().Text($"Protocolo: {data.Protocolo}").FontSize(7);

                    col.Item().PaddingVertical(2, Unit.Millimetre).LineHorizontal(0.5f);

                    if (data.QrCodeImage != null && data.QrCodeImage.Length > 0)
                    {
                        col.Item().AlignCenter().Width(40, Unit.Millimetre).Height(40, Unit.Millimetre)
                            .Image(data.QrCodeImage);
                    }

                    col.Item().PaddingTop(2, Unit.Millimetre).AlignCenter()
                        .Text("Consulte pela chave de acesso em:").FontSize(6);
                    col.Item().AlignCenter().Text(data.UrlConsulta).FontSize(5);
                    col.Item().PaddingTop(1, Unit.Millimetre).AlignCenter()
                        .Text(FormatChaveAcesso(data.ChaveAcesso)).FontSize(5);
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
