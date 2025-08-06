using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using SteamFriendsCLI.Models;
using SteamFriendsCLI.Constants;

namespace SteamFriendsCLI.Services;

public class TokenStorage
{
    private static readonly string TokenFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "sfcli",
        "auth_tokens.json"
    );

    public static void SaveAuthTokens(string accountName, string refreshToken)
    {
        try
        {
            var tokenData = new AuthTokenData
            {
                AccountName = accountName,
                RefreshToken = refreshToken,
                SavedAt = DateTime.UtcNow
            };

            // Ensure directory exists with secure permissions
            var directory = Path.GetDirectoryName(TokenFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                var dirInfo = Directory.CreateDirectory(directory);
                SetSecureDirectoryPermissions(dirInfo);
            }

            var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            // Encrypt the JSON data before writing
            var encryptedData = EncryptData(json);
            File.WriteAllBytes(TokenFilePath, encryptedData);
            
            // Set secure file permissions (owner read/write only)
            SetSecureFilePermissions(TokenFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to save authentication tokens: {ex.Message}");
        }
    }

    public static AuthTokenData? LoadAuthTokens()
    {
        try
        {
            if (!File.Exists(TokenFilePath))
            {
                return null;
            }

            // Read and decrypt the data
            var encryptedData = File.ReadAllBytes(TokenFilePath);
            var json = DecryptData(encryptedData);
            var tokenData = JsonSerializer.Deserialize<AuthTokenData>(json);

            // Check if tokens are too old (older than configured expiration period)
            if (tokenData != null && DateTime.UtcNow - tokenData.SavedAt > TimeSpan.FromDays(AppConstants.Token.TokenExpirationDays))
            {
                DeleteAuthTokens();
                return null;
            }

            return tokenData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load authentication tokens: {ex.Message}");
            // If we can't decrypt the tokens (e.g., corrupted file), delete them
            DeleteAuthTokens();
            return null;
        }
    }

    public static void DeleteAuthTokens()
    {
        try
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to delete authentication tokens: {ex.Message}");
        }
    }

    public static bool HasValidTokens()
    {
        var tokens = LoadAuthTokens();
        return tokens != null && !string.IsNullOrEmpty(tokens.RefreshToken);
    }

    public static string GetTokenStatusMessage()
    {
        var tokens = LoadAuthTokens();
        
        if (tokens == null)
        {
            return "No saved authentication tokens found.";
        }

        if (string.IsNullOrEmpty(tokens.RefreshToken))
        {
            return "Invalid authentication tokens found.";
        }

        var daysSinceCreated = (DateTime.UtcNow - tokens.SavedAt).TotalDays;
        if (daysSinceCreated > AppConstants.Token.TokenExpirationDays)
        {
            return $"Authentication tokens expired ({daysSinceCreated:F0} days old).";
        }

        return $"Valid authentication tokens found for '{tokens.AccountName}' ({daysSinceCreated:F0} days old).";
    }

    private static byte[] EncryptData(string plainText)
    {
        // Use machine-specific entropy for encryption key derivation
        var entropy = GetMachineEntropy();
        var salt = GenerateRandomSalt();
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);

        // Use AES encryption with machine-specific key
        using var aes = Aes.Create();
        aes.Key = DeriveKeyFromEntropy(entropy, salt);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var encryptedData = encryptor.TransformFinalBlock(plainTextBytes, 0, plainTextBytes.Length);

        // Prepend salt, then IV, then encrypted data
        var result = new byte[salt.Length + aes.IV.Length + encryptedData.Length];
        Array.Copy(salt, 0, result, 0, salt.Length);
        Array.Copy(aes.IV, 0, result, salt.Length, aes.IV.Length);
        Array.Copy(encryptedData, 0, result, salt.Length + aes.IV.Length, encryptedData.Length);

        return result;
    }

    private static string DecryptData(byte[] encryptedData)
    {
        if (encryptedData.Length < 32) // Salt (16 bytes) + AES IV (16 bytes) minimum
            throw new ArgumentException("Invalid encrypted data");

        var entropy = GetMachineEntropy();

        // Extract salt from the beginning of the encrypted data
        var salt = new byte[16];
        Array.Copy(encryptedData, 0, salt, 0, 16);

        using var aes = Aes.Create();
        aes.Key = DeriveKeyFromEntropy(entropy, salt);

        // Extract IV from after the salt
        var iv = new byte[16];
        Array.Copy(encryptedData, 16, iv, 0, 16);
        aes.IV = iv;

        // Extract the actual encrypted data
        var cipherText = new byte[encryptedData.Length - 32]; // 16 (salt) + 16 (IV)
        Array.Copy(encryptedData, 32, cipherText, 0, cipherText.Length);

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    private static byte[] GetMachineEntropy()
    {
        // Create machine-specific entropy using multiple sources
        var entropy = new List<byte>();

        // Add machine name
        entropy.AddRange(Encoding.UTF8.GetBytes(Environment.MachineName));

        // Add user name
        entropy.AddRange(Encoding.UTF8.GetBytes(Environment.UserName));

        // Add OS version
        entropy.AddRange(Encoding.UTF8.GetBytes(Environment.OSVersion.ToString()));

        // Add processor count
        entropy.AddRange(BitConverter.GetBytes(Environment.ProcessorCount));

        return entropy.ToArray();
    }

    private static byte[] DeriveKeyFromEntropy(byte[] entropy, byte[] salt)
    {
        // Use PBKDF2 to derive a 32-byte key from entropy and a random salt
        using var pbkdf2 = new Rfc2898DeriveBytes(entropy, salt, 10000, HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32); // 256-bit key for AES
    }

    private static byte[] GenerateRandomSalt(int size = 16)
    {
        var salt = new byte[size];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }
    private static void SetSecureFilePermissions(string filePath)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Set permissions to 600 (owner read/write only) on Unix-like systems
                var fileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                File.SetUnixFileMode(filePath, fileMode);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, set file to be accessible only by the current user
                var fileInfo = new FileInfo(filePath);
                var security = fileInfo.GetAccessControl();
                security.SetAccessRuleProtection(true, false); // Remove inheritance
                
                // Add full control for current user only
                var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent();
                if (currentUser.User != null)
                {
                    security.SetAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                        currentUser.User,
                        System.Security.AccessControl.FileSystemRights.FullControl,
                        System.Security.AccessControl.AccessControlType.Allow));
                }
                
                fileInfo.SetAccessControl(security);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not set secure file permissions: {ex.Message}");
        }
    }

    private static void SetSecureDirectoryPermissions(DirectoryInfo directory)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || 
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Set permissions to 700 (owner read/write/execute only) on Unix-like systems
                var dirMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
                File.SetUnixFileMode(directory.FullName, dirMode);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // On Windows, set directory to be accessible only by the current user
                var security = directory.GetAccessControl();
                security.SetAccessRuleProtection(true, false); // Remove inheritance
                
                // Add full control for current user only
                var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent();
                if (currentUser.User != null)
                {
                    security.SetAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                        currentUser.User,
                        System.Security.AccessControl.FileSystemRights.FullControl,
                        System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                        System.Security.AccessControl.PropagationFlags.None,
                        System.Security.AccessControl.AccessControlType.Allow));
                }
                
                directory.SetAccessControl(security);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not set secure directory permissions: {ex.Message}");
        }
    }
}
