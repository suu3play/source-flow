using System.Security.Cryptography;
using System.Text;

namespace SourceFlow.Services.FileOperations;

public static class FileHashService
{
    public static async Task<string> ComputeHashAsync(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = await Task.Run(() => md5.ComputeHash(stream));
        return Convert.ToHexString(hashBytes);
    }
    
    public static async Task<string> ComputeHashAsync(byte[] data)
    {
        using var md5 = MD5.Create();
        var hashBytes = await Task.Run(() => md5.ComputeHash(data));
        return Convert.ToHexString(hashBytes);
    }
    
    public static async Task<bool> CompareFileHashesAsync(string filePath1, string filePath2)
    {
        if (!File.Exists(filePath1) || !File.Exists(filePath2))
        {
            return false;
        }
        
        var hash1 = await ComputeHashAsync(filePath1);
        var hash2 = await ComputeHashAsync(filePath2);
        
        return hash1.Equals(hash2, StringComparison.OrdinalIgnoreCase);
    }
}