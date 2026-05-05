using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace LeitorEPUB.Helpers;
public static class FileHelper
{
    public static string ComputeHash(string filePath)
    {
        using var md5 = MD5.Create();
        var bytes = Encoding.UTF8.GetBytes(filePath);
        var hash = md5.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", "").ToLower();
    }
    public static string GetAppDataPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LeitorEPUB"
        );
        Directory.CreateDirectory(path);
        return path;
    }
}
