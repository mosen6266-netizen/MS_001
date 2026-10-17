using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Mosen.GroupScheduler.Updater;

/// <summary>Downloads only an already verified manifest target. Never runs a downloaded file.</summary>
public static class ResumablePackageDownloader
{
    public static async Task<string> StageAsync(HttpClient http, VerifiedUpdate update, string stagingRoot, CancellationToken ct = default)
    {
        if (!Path.IsPathFullyQualified(stagingRoot)) throw new ArgumentException("更新目录必须是绝对路径。");
        Directory.CreateDirectory(stagingRoot);
        var root = Path.GetFullPath(stagingRoot);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("更新目录不能是符号链接。");
        var target = Path.Combine(root, update.Payload.FileName);
        var partial = target + ".part";
        if (File.Exists(target) && await IsValidAsync(target, update.Payload, ct)) return target;
        if (File.Exists(target)) File.Delete(target);
        if (File.Exists(partial) && (new FileInfo(partial).Attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("不允许通过符号链接续传更新文件。");
        long offset = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (offset > update.Payload.Size) { File.Delete(partial); offset = 0; }
        if (offset == update.Payload.Size && await IsValidAsync(partial, update.Payload, ct))
        {
            File.Move(partial, target, true);
            return target;
        }
        if (offset == update.Payload.Size) { File.Delete(partial); offset = 0; }
        using var request = new HttpRequestMessage(HttpMethod.Get, update.Payload.DownloadUrl);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.PartialContent))
            throw new IOException($"更新下载失败，HTTP {(int)response.StatusCode}。");
        if (response.StatusCode == HttpStatusCode.PartialContent &&
            (offset == 0 || response.Content.Headers.ContentRange?.From != offset ||
             response.Content.Headers.ContentRange?.To != update.Payload.Size - 1 ||
             response.Content.Headers.ContentRange?.Length != update.Payload.Size))
            throw new InvalidDataException("下载断点与文件长度不匹配。");
        bool append = offset > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!append) offset = 0; // 200 means server ignored Range: restart rather than append.
        if (response.Content.Headers.ContentLength is long claimed &&
            claimed != update.Payload.Size - offset)
            throw new InvalidDataException("下载响应长度与已签名清单不匹配。");
        try
        {
            await using var dest = new FileStream(partial, append ? FileMode.Append : FileMode.Create,
                FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous);
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            byte[] buffer = new byte[128 * 1024];
            long total = offset;
            int n;
            while ((n = await source.ReadAsync(buffer, ct)) > 0)
            {
                total += n;
                if (total > update.Payload.Size) throw new InvalidDataException("下载文件超过签名指定长度。");
                await dest.WriteAsync(buffer.AsMemory(0, n), ct);
            }
            await dest.FlushAsync(ct);
            if (total != update.Payload.Size) throw new IOException("更新包尚未完整下载，可以稍后断点续传。");
        }
        catch (InvalidDataException) { if (File.Exists(partial)) File.Delete(partial); throw; }
        if (!await IsValidAsync(partial, update.Payload, ct))
        {
            File.Delete(partial);
            throw new CryptographicException("安装包SHA256校验失败，拒绝执行。");
        }
        File.Move(partial, target, true);
        return target;
    }

    public static async Task<bool> IsValidAsync(string path, UpdatePayload payload, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return false;
        var info = new FileInfo(path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length != payload.Size) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var digest = await SHA256.HashDataAsync(stream, ct);
        return CryptographicOperations.FixedTimeEquals(digest, Convert.FromHexString(payload.Sha256));
    }
}
