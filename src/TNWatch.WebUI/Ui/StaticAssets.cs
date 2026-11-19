using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TNWatch.WebUI.Ui;

/// <summary>
/// Cache busting for our own static files: <c>app.css</c> → <c>app.css?v=&lt;content hash&gt;</c>.
/// Without it, browsers keep using an old cached stylesheet/script after an update.
/// </summary>
public sealed class StaticAssets(IWebHostEnvironment environment)
{
    private readonly ConcurrentDictionary<string, string> _urls = new(StringComparer.Ordinal);

    /// <summary>Returns the path with a version query derived from the file content (computed once per file).</summary>
    public string Url(string path) => _urls.GetOrAdd(path, p =>
    {
        var file = environment.WebRootFileProvider.GetFileInfo(p);
        if (!file.Exists)
        {
            return p;
        }

        using var stream = file.CreateReadStream();
        var hash = Convert.ToHexString(SHA256.HashData(stream))[..12].ToLowerInvariant();
        return $"{p}?v={hash}";
    });
}
