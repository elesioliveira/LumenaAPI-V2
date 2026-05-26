using QRCoder;

public interface IQrCodeService
{
    byte[] GenerateQrCode(string content, int pixelsPerModule = 5);
}

public class QrCodeService : IQrCodeService
{
    public byte[] GenerateQrCode(string content, int pixelsPerModule = 5)
    {
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new BitmapByteQRCode(qrCodeData);
        return qrCode.GetGraphic(pixelsPerModule);
    }
}
