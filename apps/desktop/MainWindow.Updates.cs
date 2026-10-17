using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Mosen.GroupScheduler.Updater;

namespace Mosen.GroupScheduler.Desktop;

public partial class MainWindow
{
    private const string SignedReleaseFeed = "https://github.com/mosen6266-netizen/MS_001/releases/latest/download/update.json";
    private const string KeyId = "release-v1";
    private static string ReadPinnedResource(string name)
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("发行者的信任配置未包含在程序中。");
        using var reader=new StreamReader(stream,Encoding.UTF8);
        return reader.ReadToEnd().Trim();
    }
    private static (byte[] key, string publisher) LoadPublisherTrust()
    {
        string publicKey=ReadPinnedResource("Mosen.ReleasePublicKey");
        string thumbprint=ReadPinnedResource("Mosen.PublisherThumbprint");
        if(publicKey=="UNCONFIGURED" || thumbprint=="UNCONFIGURED" ||
           !Regex.IsMatch(thumbprint,@"\A[a-fA-F0-9]{40}\z"))
            throw new InvalidOperationException("尚未配置正式发布者签名信任证书，开发版本已禁用自动更新。");
        byte[] spki=Convert.FromBase64String(publicKey);
        using var issuer=ECDsa.Create();
        issuer.ImportSubjectPublicKeyInfo(spki,out int read);
        if(read!=spki.Length || issuer.KeySize!=256)
            throw new CryptographicException("发行者公钥格式无效。");
        return (spki,thumbprint);
    }
    private async void OnCheckUpdates(object sender, RoutedEventArgs evt)
    {
        if(_closing)return;
        CheckUpdateButton.IsEnabled=false;
        try{
            var (pub,thumbprint)=LoadPublisherTrust();
            using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(25) };
            using var response=await client.GetAsync(SignedReleaseFeed,HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength is long bytes && bytes>16384)
                throw new InvalidDataException("发布清单过大。");
            await using var remote=await response.Content.ReadAsStreamAsync();
            using var reader=new StreamReader(remote,Encoding.UTF8);
            char[] buf=new char[16385];
            int count=0, read;
            while(count<buf.Length && (read=await reader.ReadAsync(buf.AsMemory(count,buf.Length-count)))>0)
                count+=read;
            if(count>16384)throw new InvalidDataException("发布清单过大。");
            var v=Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0,27,0);
            string current=$"{v.Major}.{v.Minor}.{v.Build}";
            var checkedManifest=SignedUpdateManifest.Verify(new string(buf,0,count),
                new System.Collections.Generic.Dictionary<string,byte[]> { [KeyId]=pub },current);
            if(_closing)return;
            var answer=MessageBox.Show(
                $"发现已签名版本 {checkedManifest.Payload.Version}。需要下载约 {checkedManifest.Payload.Size/1024/1024} MB 离线安装包。\n\n更新前必须关闭程序。旧程序文件将备份，账号、备注及剧本保留。是否下载并安装？",
                "检查更新",MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No);
            if(answer!=MessageBoxResult.Yes)return;
            string stage=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MOSEN","TelegramSchedulerUpdates");
            string installer=await ResumablePackageDownloader.StageAsync(client,checkedManifest,stage);
            if(_closing)return;
            string script=Path.Combine(AppContext.BaseDirectory,"tools","Apply-Update.ps1");
            if(!File.Exists(script))throw new FileNotFoundException("更新辅助程序不存在。");
            var launch=new ProcessStartInfo {
                FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32","WindowsPowerShell","v1.0","powershell.exe"),
                UseShellExecute=false,CreateNoWindow=false,WorkingDirectory=stage
            };
            launch.ArgumentList.Add("-NoProfile");
            launch.ArgumentList.Add("-NonInteractive");
            launch.ArgumentList.Add("-ExecutionPolicy");launch.ArgumentList.Add("Bypass");
            launch.ArgumentList.Add("-File");launch.ArgumentList.Add(script);
            launch.ArgumentList.Add("-Installer");launch.ArgumentList.Add(installer);
            launch.ArgumentList.Add("-ExpectedSHA256");launch.ArgumentList.Add(checkedManifest.Payload.Sha256);
            launch.ArgumentList.Add("-PublisherThumbprint");launch.ArgumentList.Add(thumbprint);
            launch.ArgumentList.Add("-ParentProcessId");launch.ArgumentList.Add(Environment.ProcessId.ToString());
            Process.Start(launch) ?? throw new IOException("无法启动更新辅助程序。");
            Application.Current.Shutdown();
        }catch(InvalidOperationException ex){
            if(!_closing)MessageBox.Show(ex.Message,"检查更新",MessageBoxButton.OK,MessageBoxImage.Information);
        }catch(Exception ex) when (ex is HttpRequestException or IOException or CryptographicException or FormatException or UnauthorizedAccessException or System.Text.Json.JsonException or System.ArgumentException){
            if(!_closing)MessageBox.Show("更新检查或下载失败：签名不可信、网络故障或文件无效。原版本没有改变。请检查网络或使用正式签名安装包。","更新未完成",MessageBoxButton.OK,MessageBoxImage.Warning);
        }finally{if(!_closing)CheckUpdateButton.IsEnabled=true;}
    }
}
