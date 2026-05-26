using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

public interface IXmlSignerService
{
    string SignXml(string xml, X509Certificate2 certificate, string tagToSign);
}

public class XmlSignerService : IXmlSignerService
{
    public string SignXml(string xml, X509Certificate2 certificate, string tagToSign)
    {
        var doc = new XmlDocument();
        doc.PreserveWhitespace = true;
        doc.LoadXml(xml);

        var nodeToSign = doc.GetElementsByTagName(tagToSign)[0] as XmlElement
            ?? throw new Exception($"Tag '{tagToSign}' não encontrada no XML.");

        var id = nodeToSign.GetAttribute("Id");
        if (string.IsNullOrEmpty(id))
            throw new Exception($"Atributo 'Id' não encontrado na tag '{tagToSign}'.");

        var signedXml = new SignedXml(doc);
        signedXml.SigningKey = certificate.GetRSAPrivateKey();

        var reference = new Reference($"#{id}");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigC14NTransform());
        reference.DigestMethod = "http://www.w3.org/2000/09/xmldsig#sha1";

        signedXml.AddReference(reference);
        signedXml.SignedInfo.CanonicalizationMethod = "http://www.w3.org/TR/2001/REC-xml-c14n-20010315";
        signedXml.SignedInfo.SignatureMethod = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";

        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(certificate));
        signedXml.KeyInfo = keyInfo;

        signedXml.ComputeSignature();

        var signatureNode = doc.ImportNode(signedXml.GetXml(), true);
        var nfeNode = doc.GetElementsByTagName("NFe")[0]
            ?? doc.GetElementsByTagName("inutNFe")[0]
            ?? doc.GetElementsByTagName("evento")[0]
            ?? doc.DocumentElement;
        nfeNode!.AppendChild(signatureNode);

        return doc.OuterXml;
    }
}
