public static class SefazEndpoints
{
    public static (string autorizacao, string retAutorizacao, string consultaProtocolo, string statusServico, string inutilizacao, string recepcaoEvento) GetEndpoints(string uf, int ambiente, int modelo)
    {
        var isHomologacao = ambiente == 2;
        var isNFCe = modelo == 65;

        if (isNFCe)
            return GetNFCeEndpoints(uf, isHomologacao);

        return GetNFeEndpoints(uf, isHomologacao);
    }

    private static (string, string, string, string, string, string) GetNFeEndpoints(string uf, bool homologacao)
    {
        var svrs = homologacao
            ? ("https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeRetAutorizacao4/NFeRetAutorizacao4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeConsulta4/NFeConsulta4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeStatusServico4/NFeStatusServico4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/nfeinutilizacao4/NFeInutilizacao4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento4/RecepcaoEvento4.asmx")
            : ("https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/NfeRetAutorizacao4/NFeRetAutorizacao4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/NfeConsulta4/NFeConsulta4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/NfeStatusServico4/NFeStatusServico4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/nfeinutilizacao4/NFeInutilizacao4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/recepcaoevento4/RecepcaoEvento4.asmx");

        return uf.ToUpper() switch
        {
            "SP" => homologacao
                ? ("https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx",
                   "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferetautorizacao4.asmx",
                   "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx",
                   "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx",
                   "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeinutilizacao4.asmx",
                   "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx")
                : ("https://nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx",
                   "https://nfe.fazenda.sp.gov.br/ws/nferetautorizacao4.asmx",
                   "https://nfe.fazenda.sp.gov.br/ws/nfeconsultaprotocolo4.asmx",
                   "https://nfe.fazenda.sp.gov.br/ws/nfestatusservico4.asmx",
                   "https://nfe.fazenda.sp.gov.br/ws/nfeinutilizacao4.asmx",
                   "https://nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx"),

            "MG" => homologacao
                ? ("https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4",
                   "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeRetAutorizacao4",
                   "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeConsultaProtocolo4",
                   "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeStatusServico4",
                   "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeInutilizacao4",
                   "https://hnfe.fazenda.mg.gov.br/nfe2/services/RecepcaoEvento4")
                : ("https://nfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4",
                   "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeRetAutorizacao4",
                   "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeConsultaProtocolo4",
                   "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeStatusServico4",
                   "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeInutilizacao4",
                   "https://nfe.fazenda.mg.gov.br/nfe2/services/RecepcaoEvento4"),

            "RS" => homologacao
                ? ("https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx",
                   "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeRetAutorizacao/NFeRetAutorizacao4.asmx",
                   "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeConsulta/NFeConsulta4.asmx",
                   "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeStatusServico/NFeStatusServico4.asmx",
                   "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeInutilizacao/NFeInutilizacao4.asmx",
                   "https://nfe-homologacao.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx")
                : ("https://nfe.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx",
                   "https://nfe.sefazrs.rs.gov.br/ws/NfeRetAutorizacao/NFeRetAutorizacao4.asmx",
                   "https://nfe.sefazrs.rs.gov.br/ws/NfeConsulta/NFeConsulta4.asmx",
                   "https://nfe.sefazrs.rs.gov.br/ws/NfeStatusServico/NFeStatusServico4.asmx",
                   "https://nfe.sefazrs.rs.gov.br/ws/NfeInutilizacao/NFeInutilizacao4.asmx",
                   "https://nfe.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx"),

            "PR" => homologacao
                ? ("https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeAutorizacao4?wsdl",
                   "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeRetAutorizacao4?wsdl",
                   "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeConsultaProtocolo4?wsdl",
                   "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeStatusServico4?wsdl",
                   "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeInutilizacao4?wsdl",
                   "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeRecepcaoEvento4?wsdl")
                : ("https://nfe.sefa.pr.gov.br/nfe/NFeAutorizacao4?wsdl",
                   "https://nfe.sefa.pr.gov.br/nfe/NFeRetAutorizacao4?wsdl",
                   "https://nfe.sefa.pr.gov.br/nfe/NFeConsultaProtocolo4?wsdl",
                   "https://nfe.sefa.pr.gov.br/nfe/NFeStatusServico4?wsdl",
                   "https://nfe.sefa.pr.gov.br/nfe/NFeInutilizacao4?wsdl",
                   "https://nfe.sefa.pr.gov.br/nfe/NFeRecepcaoEvento4?wsdl"),

            // Demais estados usam SVRS
            _ => svrs
        };
    }

    private static (string, string, string, string, string, string) GetNFCeEndpoints(string uf, bool homologacao)
    {
        return uf.ToUpper() switch
        {
            "SP" => homologacao
                ? ("https://homologacao.nfce.fazenda.sp.gov.br/ws/NFeAutorizacao4.asmx",
                   "https://homologacao.nfce.fazenda.sp.gov.br/ws/NFeRetAutorizacao4.asmx",
                   "https://homologacao.nfce.fazenda.sp.gov.br/ws/NFeConsultaProtocolo4.asmx",
                   "https://homologacao.nfce.fazenda.sp.gov.br/ws/NFeStatusServico4.asmx",
                   "https://homologacao.nfce.fazenda.sp.gov.br/ws/NFeInutilizacao4.asmx",
                   "https://homologacao.nfce.fazenda.sp.gov.br/ws/NFeRecepcaoEvento4.asmx")
                : ("https://nfce.fazenda.sp.gov.br/ws/NFeAutorizacao4.asmx",
                   "https://nfce.fazenda.sp.gov.br/ws/NFeRetAutorizacao4.asmx",
                   "https://nfce.fazenda.sp.gov.br/ws/NFeConsultaProtocolo4.asmx",
                   "https://nfce.fazenda.sp.gov.br/ws/NFeStatusServico4.asmx",
                   "https://nfce.fazenda.sp.gov.br/ws/NFeInutilizacao4.asmx",
                   "https://nfce.fazenda.sp.gov.br/ws/NFeRecepcaoEvento4.asmx"),

            "RS" => homologacao
                ? ("https://nfce-homologacao.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx",
                   "https://nfce-homologacao.sefazrs.rs.gov.br/ws/NfeRetAutorizacao/NFeRetAutorizacao4.asmx",
                   "https://nfce-homologacao.sefazrs.rs.gov.br/ws/NfeConsulta/NFeConsulta4.asmx",
                   "https://nfce-homologacao.sefazrs.rs.gov.br/ws/NfeStatusServico/NFeStatusServico4.asmx",
                   "https://nfce-homologacao.sefazrs.rs.gov.br/ws/NfeInutilizacao/NFeInutilizacao4.asmx",
                   "https://nfce-homologacao.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx")
                : ("https://nfce.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx",
                   "https://nfce.sefazrs.rs.gov.br/ws/NfeRetAutorizacao/NFeRetAutorizacao4.asmx",
                   "https://nfce.sefazrs.rs.gov.br/ws/NfeConsulta/NFeConsulta4.asmx",
                   "https://nfce.sefazrs.rs.gov.br/ws/NfeStatusServico/NFeStatusServico4.asmx",
                   "https://nfce.sefazrs.rs.gov.br/ws/NfeInutilizacao/NFeInutilizacao4.asmx",
                   "https://nfce.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx"),

            // SVRS NFCe para demais estados
            _ => homologacao
                ? ("https://nfce-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx",
                   "https://nfce-homologacao.svrs.rs.gov.br/ws/NfeRetAutorizacao4/NFeRetAutorizacao4.asmx",
                   "https://nfce-homologacao.svrs.rs.gov.br/ws/NfeConsulta4/NFeConsulta4.asmx",
                   "https://nfce-homologacao.svrs.rs.gov.br/ws/NfeStatusServico4/NFeStatusServico4.asmx",
                   "https://nfce-homologacao.svrs.rs.gov.br/ws/NfeInutilizacao4/NFeInutilizacao4.asmx",
                   "https://nfce-homologacao.svrs.rs.gov.br/ws/recepcaoevento4/RecepcaoEvento4.asmx")
                : ("https://nfce.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx",
                   "https://nfce.svrs.rs.gov.br/ws/NfeRetAutorizacao4/NFeRetAutorizacao4.asmx",
                   "https://nfce.svrs.rs.gov.br/ws/NfeConsulta4/NFeConsulta4.asmx",
                   "https://nfce.svrs.rs.gov.br/ws/NfeStatusServico4/NFeStatusServico4.asmx",
                   "https://nfce.svrs.rs.gov.br/ws/NfeInutilizacao4/NFeInutilizacao4.asmx",
                   "https://nfce.svrs.rs.gov.br/ws/recepcaoevento4/RecepcaoEvento4.asmx")
        };
    }

    public static int GetCodigoUF(string uf)
    {
        return uf.ToUpper() switch
        {
            "RO" => 11, "AC" => 12, "AM" => 13, "RR" => 14, "PA" => 15,
            "AP" => 16, "TO" => 17, "MA" => 21, "PI" => 22, "CE" => 23,
            "RN" => 24, "PB" => 25, "PE" => 26, "AL" => 27, "SE" => 28,
            "BA" => 29, "MG" => 31, "ES" => 32, "RJ" => 33, "SP" => 35,
            "PR" => 41, "SC" => 42, "RS" => 43, "MS" => 50, "MT" => 51,
            "GO" => 52, "DF" => 53,
            _ => 0
        };
    }

    public static (string autorizacao, string retAutorizacao, string consultaProtocolo, string statusServico, string inutilizacao, string recepcaoEvento) GetSvcEndpoints(string tipoSvc, bool homologacao)
    {
        if (tipoSvc == "SVC-AN")
        {
            return homologacao
                ? ("https://hom.svc.fazenda.gov.br/NFeAutorizacao4/NFeAutorizacao4.asmx",
                   "https://hom.svc.fazenda.gov.br/NFeRetAutorizacao4/NFeRetAutorizacao4.asmx",
                   "https://hom.svc.fazenda.gov.br/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx",
                   "https://hom.svc.fazenda.gov.br/NFeStatusServico4/NFeStatusServico4.asmx",
                   "https://hom.svc.fazenda.gov.br/NFeInutilizacao4/NFeInutilizacao4.asmx",
                   "https://hom.svc.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx")
                : ("https://www.svc.fazenda.gov.br/NFeAutorizacao4/NFeAutorizacao4.asmx",
                   "https://www.svc.fazenda.gov.br/NFeRetAutorizacao4/NFeRetAutorizacao4.asmx",
                   "https://www.svc.fazenda.gov.br/NFeConsultaProtocolo4/NFeConsultaProtocolo4.asmx",
                   "https://www.svc.fazenda.gov.br/NFeStatusServico4/NFeStatusServico4.asmx",
                   "https://www.svc.fazenda.gov.br/NFeInutilizacao4/NFeInutilizacao4.asmx",
                   "https://www.svc.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx");
        }

        // SVC-RS
        return homologacao
            ? ("https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeRetAutorizacao4/NFeRetAutorizacao4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeConsulta4/NFeConsulta4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeStatusServico4/NFeStatusServico4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/nfeinutilizacao4/NFeInutilizacao4.asmx",
               "https://nfe-homologacao.svrs.rs.gov.br/ws/recepcaoevento4/RecepcaoEvento4.asmx")
            : ("https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao4/NFeAutorizacao4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/NfeRetAutorizacao4/NFeRetAutorizacao4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/NfeConsulta4/NFeConsulta4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/NfeStatusServico4/NFeStatusServico4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/nfeinutilizacao4/NFeInutilizacao4.asmx",
               "https://nfe.svrs.rs.gov.br/ws/recepcaoevento4/RecepcaoEvento4.asmx");
    }
}
