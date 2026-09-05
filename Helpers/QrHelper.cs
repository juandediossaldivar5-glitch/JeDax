using QRCoder;

namespace JeDax.Helpers;

public static class QrHelper
{
    public static string ToBase64Png(string text, int pixelsPerModule = 8)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        var bytes = qr.GetGraphic(pixelsPerModule);
        return Convert.ToBase64String(bytes);
    }
}
