using System.Security.Cryptography;

namespace Chorbar.Utils;

public sealed class StaticFileVersion
{
    public string Hash { get; }

    public StaticFileVersion(IWebHostEnvironment env)
    {
        var cssFiles = Directory
            .EnumerateFiles(env.WebRootPath, "*.css", SearchOption.AllDirectories)
            .OrderBy(f => f);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in cssFiles)
        {
            var bytes = File.ReadAllBytes(file);
            hasher.AppendData(bytes);
        }

        Hash = Convert.ToHexString(hasher.GetHashAndReset())[..12].ToLowerInvariant();
    }
}
