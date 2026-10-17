using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mosen.GroupScheduler.Updater;

/// <summary>
/// Developer-controlled, immutable release descriptor. The signature is over the exact
/// UTF-8 JSON payload bytes (not the outer envelope's serialized representation).
/// No user-supplied public key is treated as a production trust anchor.
/// </summary>
public sealed record UpdatePayload(string Product, string Version, string MinimumVersion,
    string FileName, string DownloadUrl, long Size, string Sha256);

public sealed record VerifiedUpdate(UpdatePayload Payload, byte[] SignedPayload);

public static class SignedUpdateManifest
{
    public const string ProductId = "MOSEN_TELEGRAM_SCHEDULER";
    private const long MaxInstallerBytes = 768L * 1024 * 1024;
    private static readonly Regex VersionPattern = new(@"^[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}$", RegexOptions.CultureInvariant);

    public static VerifiedUpdate Verify(string json, IReadOnlyDictionary<string, byte[]> trustedSpki, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > 16384)
            throw new InvalidDataException("更新清单为空或过大。");
        using var outer = JsonDocument.Parse(json);
        var root = outer.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.GetProperty("schema").GetInt32() != 1)
            throw new InvalidDataException("更新清单版本不受支持。");
        string keyId = root.GetProperty("keyId").GetString() ?? "";
        if (!trustedSpki.TryGetValue(keyId, out var pub) || pub.Length == 0)
            throw new CryptographicException("缺少可信的发布签名公钥，更新已禁用。");
        byte[] payload = Convert.FromBase64String(root.GetProperty("payload").GetString() ?? "");
        byte[] signature = Convert.FromBase64String(root.GetProperty("signature").GetString() ?? "");
        if (payload.Length == 0 || payload.Length > 4096 || signature.Length != 64)
            throw new InvalidDataException("更新清单签名或载荷长度不合法。");
        using var key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(pub, out int read);
        if (read != pub.Length || key.KeySize != 256 ||
            !key.VerifyData(payload, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new CryptographicException("更新清单数字签名校验失败。");
        using var doc = JsonDocument.Parse(payload);
        var p = doc.RootElement;
        var update = new UpdatePayload(
            p.GetProperty("product").GetString() ?? "",
            p.GetProperty("version").GetString() ?? "",
            p.GetProperty("minimumVersion").GetString() ?? "",
            p.GetProperty("fileName").GetString() ?? "",
            p.GetProperty("downloadUrl").GetString() ?? "",
            p.GetProperty("size").GetInt64(),
            p.GetProperty("sha256").GetString() ?? "");
        if (update.Product != ProductId) throw new InvalidDataException("更新产品标识不匹配。");
        var target = ParseVersion(update.Version);
        var minimum = ParseVersion(update.MinimumVersion);
        var current = ParseVersion(currentVersion);
        if (target <= current) throw new InvalidOperationException("当前已是相同或更新版本。");
        if (current < minimum) throw new InvalidOperationException("当前版本过旧，需要使用官方完整安装包。");
        if (minimum > target) throw new InvalidDataException("最低版本字段非法。");
        if (update.Size < 100 * 1024 * 1024L || update.Size > MaxInstallerBytes)
            throw new InvalidDataException("安装包长度不符合离线安装包边界。");
        if (!Regex.IsMatch(update.Sha256, @"\A[a-fA-F0-9]{64}\z", RegexOptions.CultureInvariant))
            throw new InvalidDataException("SHA256格式非法。");
        var expectedFileName = $"MOSEN-Telegram-Scheduler-Setup-{update.Version}.exe";
        if (!StringComparer.Ordinal.Equals(update.FileName, expectedFileName))
            throw new InvalidDataException("文件名与目标版本不匹配。");
        var expectedUrl = $"https://github.com/mosen6266-netizen/MS_001/releases/download/v{update.Version}/{expectedFileName}";
        if (!StringComparer.Ordinal.Equals(update.DownloadUrl, expectedUrl) ||
            !Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("更新下载地址不在已锁定的GitHub Releases范围。");
        return new VerifiedUpdate(update, payload);
    }

    public static Version ParseVersion(string value)
    {
        if (!VersionPattern.IsMatch(value) || !Version.TryParse(value, out var parsed))
            throw new InvalidDataException("更新版本号格式非法。");
        return parsed;
    }
}
