using QRCoder;
using SteamKit2.Authentication;
using SteamFriendsCLI.Constants;
using System.Diagnostics;

namespace SteamFriendsCLI.Services;

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

    public static Process? ShowQRCodeInNewTerminal(QrAuthSession authSession)
    {
        try
        {
            // Generate QR code
            using var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(authSession.ChallengeURL, QRCodeGenerator.ECCLevel.L);
            using var qrCode = new AsciiQRCode(qrCodeData);
            var qrCodeAsAsciiArt = qrCode.GetGraphic(1, drawQuietZones: false);

            // Create a temporary script file to display the QR code
            var tempScriptPath = Path.Combine(Path.GetTempPath(), $"steam_qr_{Guid.NewGuid():N}.sh");
            var qrDisplayText = $@"#!/bin/bash
clear
echo -e ""\033[1;36mSteam Authentication Required\033[0m""
echo -e ""\033[1;36m============================\033[0m""
echo """"
echo -e ""\033[1;33mScan this QR code with the Steam Mobile App:\033[0m""
echo """"
cat << 'EOF'
{qrCodeAsAsciiArt}
EOF
echo """"
echo -e ""\033[1;32mAlternatively, visit this URL in your browser:\033[0m""
echo -e ""\033[4;34m{authSession.ChallengeURL}\033[0m""
echo """"
echo -e ""\033[1;31mThis window will close automatically after authentication.\033[0m""
echo -e ""\033[0;37mYou can also close it manually by pressing Ctrl+C or closing the window.\033[0m""
echo """"

# Clean up the script file when done
trap 'rm -f ""{tempScriptPath}""' EXIT

# Wait for user input or until killed
echo -e ""\033[1;35mPress Enter to close this window manually, or wait for auto-close...\033[0m""
read
";

            File.WriteAllText(tempScriptPath, qrDisplayText);
            
            // Make the script executable
            var chmodProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            chmodProcess.Start();
            chmodProcess.WaitForExit();

            // Launch a new terminal window to display the QR code
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "gnome-terminal",
                    Arguments = $"--title=\"Steam Authentication\" --geometry=80x30 -- bash \"{tempScriptPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = false
                }
            };

            // Try different terminal emulators if gnome-terminal is not available
            if (!IsCommandAvailable("gnome-terminal"))
            {
                if (IsCommandAvailable("konsole"))
                {
                    process.StartInfo.FileName = "konsole";
                    process.StartInfo.Arguments = $"--title \"Steam Authentication\" -e bash \"{tempScriptPath}\"";
                }
                else if (IsCommandAvailable("xterm"))
                {
                    process.StartInfo.FileName = "xterm";
                    process.StartInfo.Arguments = $"-title \"Steam Authentication\" -geometry 80x30 -e bash \"{tempScriptPath}\"";
                }
                else
                {
                    // Fallback to console if no terminal emulator is available
                    Console.WriteLine("No suitable terminal emulator found. Displaying QR code in current terminal:");
                    DrawQRCode(authSession);
                    File.Delete(tempScriptPath);
                    return null;
                }
            }

            process.Start();

            // Schedule cleanup of the temp file when the process exits
            Task.Run(async () =>
            {
                try
                {
                    // Wait for process to exit or timeout after 15 minutes
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                catch
                {
                    // If waiting fails, wait a bit and clean up anyway
                    await Task.Delay(TimeSpan.FromMinutes(15));
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempScriptPath))
                            File.Delete(tempScriptPath);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
            });

            return process;
        }
        catch (Exception ex)
        {
            // Fallback to console output if terminal launch fails
            Console.WriteLine($"Failed to launch QR code terminal: {ex.Message}");
            Console.WriteLine("Displaying QR code in current terminal:");
            DrawQRCode(authSession);
            return null;
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
