using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

public interface ISefazClientService
{
    Task<string> SendAsync(string url, string xmlBody, X509Certificate2 certificate, string soapAction);
}

public class SefazClientService : ISefazClientService
{
    public async Task<string> SendAsync(string url, string xmlBody, X509Certificate2 certificate, string soapAction)
    {
        var soapEnvelope = BuildSoapEnvelope(xmlBody);

        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(certificate);
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(60);

        var content = new StringContent(soapEnvelope, Encoding.UTF8, "application/soap+xml");
        content.Headers.ContentType!.Parameters.Add(
            new System.Net.Http.Headers.NameValueHeaderValue("action", $"\"{soapAction}\"")
        );

        var response = await client.PostAsync(url, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        return ExtractResponseBody(responseBody);
    }

    private string BuildSoapEnvelope(string xmlBody)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
  <soap12:Header/>
  <soap12:Body>
    {xmlBody}
  </soap12:Body>
</soap12:Envelope>";
    }

    private string ExtractResponseBody(string soapResponse)
    {
        try
        {
            var doc = XDocument.Parse(soapResponse);
            XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
            var body = doc.Descendants(soap + "Body").FirstOrDefault();
            if (body != null && body.HasElements)
            {
                return body.Elements().First().ToString();
            }
        }
        catch
        {
            // If parsing fails, return raw response
        }
        return soapResponse;
    }
}
