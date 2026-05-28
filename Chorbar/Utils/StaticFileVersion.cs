using System.Security.Cryptography;

namespace Chorbar.Utils;

public sealed class StaticFileVersion
{
    public string Hash { get; }

    public StaticFileVersion(IWebHostEnvironment env)
    {
        var assetFiles = Directory
            .EnumerateFiles(env.WebRootPath, "*.*", SearchOption.AllDirectories)
            .Where(f =>
                f.EndsWith(".css", StringComparison.Ordinal)
                || f.EndsWith(".js", StringComparison.Ordinal)
            )
            .OrderBy(f => f);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in assetFiles)
        {
            var bytes = File.ReadAllBytes(file);
            hasher.AppendData(bytes);
        }

        Hash = Convert.ToHexString(hasher.GetHashAndReset())[..12].ToLowerInvariant();
    }
}
