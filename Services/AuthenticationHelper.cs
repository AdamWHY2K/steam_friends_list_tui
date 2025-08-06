using QRCoder;
using SteamFriendsTUI.Constants;
using SteamKit2.Authentication;

namespace SteamFriendsTUI.Services;

public static class AuthenticationHelper
{
    public static void DrawQRCode(QrAuthSession authSession)
    {
        Console.WriteLine($"Challenge URL: {authSession.ChallengeURL}");
        Console.WriteLine();

        // Encode the link as a QR code
        using var qrGenerator = new QRCodeGenerator();
        var qrCodeData = qrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
        using var qrCode = new AsciiQRCode(qrCodeData);
        var qrCodeAsAsciiArt = qrCode.GetGraphic(1, drawQuietZones: false);

        Console.WriteLine(AppConstants.Messages.UseQrCode);
        Console.WriteLine(qrCodeAsAsciiArt);
    }
}
