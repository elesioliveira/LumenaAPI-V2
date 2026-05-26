using System.Security.Cryptography.X509Certificates;

public interface ICertificateService
{
    X509Certificate2 LoadCertificate(byte[] pfxData, string password);
    (string razaoSocial, string cnpj, DateTime validadeInicio, DateTime validadeFim) GetCertificateInfo(X509Certificate2 cert);
    bool IsCertificateValid(X509Certificate2 cert);
}

public class CertificateService : ICertificateService
{
    public X509Certificate2 LoadCertificate(byte[] pfxData, string password)
    {
        return new X509Certificate2(pfxData, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
    }

    public (string razaoSocial, string cnpj, DateTime validadeInicio, DateTime validadeFim) GetCertificateInfo(X509Certificate2 cert)
    {
        var subject = cert.Subject;
        var cnpj = ExtractCnpjFromSubject(subject);
        var razaoSocial = ExtractCnFromSubject(subject);

        return (razaoSocial, cnpj, cert.NotBefore, cert.NotAfter);
    }

    public bool IsCertificateValid(X509Certificate2 cert)
    {
        return DateTime.Now >= cert.NotBefore && DateTime.Now <= cert.NotAfter;
    }

    private string ExtractCnpjFromSubject(string subject)
    {
        var parts = subject.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("serialNumber=", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("OID.2.16.76.1.3.3=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Split('=', 2)[1].Trim();
                var digits = new string(value.Where(char.IsDigit).ToArray());
                if (digits.Length == 14) return digits;
            }
        }

        var allDigits = new string(subject.Where(char.IsDigit).ToArray());
        for (int i = 0; i <= allDigits.Length - 14; i++)
        {
            var candidate = allDigits.Substring(i, 14);
            if (IsValidCnpj(candidate)) return candidate;
        }

        return "";
    }

    private string ExtractCnFromSubject(string subject)
    {
        var parts = subject.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(3).Trim();
            }
        }
        return "";
    }

    private bool IsValidCnpj(string cnpj)
    {
        if (cnpj.Length != 14 || cnpj.All(c => c == cnpj[0])) return false;

        int[] multiplier1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] multiplier2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };

        var tempCnpj = cnpj.Substring(0, 12);
        var sum = 0;
        for (int i = 0; i < 12; i++)
            sum += int.Parse(tempCnpj[i].ToString()) * multiplier1[i];

        var remainder = sum % 11;
        var digit = remainder < 2 ? 0 : 11 - remainder;
        tempCnpj += digit;

        sum = 0;
        for (int i = 0; i < 13; i++)
            sum += int.Parse(tempCnpj[i].ToString()) * multiplier2[i];

        remainder = sum % 11;
        digit = remainder < 2 ? 0 : 11 - remainder;
        tempCnpj += digit;

        return cnpj.EndsWith(tempCnpj.Substring(12));
    }
}
