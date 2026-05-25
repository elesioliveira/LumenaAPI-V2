public class ProdutoFiscalDTO
{
    public long produto_id { get; set; }
    public long cfop_id { get; set; }

    public string? icms_cst { get; set; }
    public decimal? icms_aliquota { get; set; }
    public decimal? icms_reducao_bc { get; set; }

    public string? pis_cst { get; set; }
    public decimal? pis_aliquota { get; set; }

    public string? cofins_cst { get; set; }
    public decimal? cofins_aliquota { get; set; }

    public string? ipi_cst { get; set; }
    public decimal? ipi_aliquota { get; set; }
}
