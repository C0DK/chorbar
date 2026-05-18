using System.Security.Cryptography;

namespace Chorbar.Utils;

public sealed class StaticFileVersion
{
    public string Hash { get; }

    public StaticFileVersion(IWebHostEnvironment env)
    {
        var cssPath = Path.Combine(env.WebRootPath, "static", "style.css");
        using var stream = File.OpenRead(cssPath);
        var hashBytes = SHA256.HashData(stream);
        Hash = Convert.ToHexString(hashBytes)[..12].ToLowerInvariant();
    }
}
