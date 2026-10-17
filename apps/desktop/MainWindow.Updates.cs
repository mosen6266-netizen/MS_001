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
using System.Windows.Controls;
using System.Windows.Media;
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
    private bool ConfirmSignedUpdate(string version, long bytes)
    {
        var dialog=new Window { Owner=this, Width=495, Height=230, MinWidth=420,
            Title="检查更新", WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ResizeMode=ResizeMode.NoResize, Background=new SolidColorBrush(Color.FromRgb(23,27,33)) };
        var root=new StackPanel { Margin=new Thickness(24) };
        root.Children.Add(new TextBlock { Text=$"发现已验证签名的新版本 {version}",
            FontSize=17, FontWeight=FontWeights.SemiBold, Foreground=Brushes.White });
        root.Children.Add(new TextBlock { Text=$"安装包约 {bytes/1024/1024} MB。安装前程序会关闭并保留旧程序文件。账号、备注、剧本不会被删除。",
            TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,17,0,17),
            FontSize=13, Foreground=new SolidColorBrush(Color.FromRgb(195,205,215)) });
        var buttons=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Left };
        var confirm=new Button { Content="确认下载并安装", MinWidth=135, Padding=new Thickness(12,8,12,8),
            Foreground=Brushes.White, Background=new SolidColorBrush(Color.FromRgb(28,132,88)) };
        var cancel=new Button { Content="取消", MinWidth=88, Padding=new Thickness(12,8,12,8),
            Foreground=Brushes.White, Background=new SolidColorBrush(Color.FromRgb(171,58,58)),
            Margin=new Thickness(12,0,0,0), IsCancel=true };
        confirm.Click+=(_,_)=>dialog.DialogResult=true;
        cancel.Click+=(_,_)=>dialog.DialogResult=false;
        buttons.Children.Add(confirm);buttons.Children.Add(cancel);
        root.Children.Add(buttons); dialog.Content=root;
        return dialog.ShowDialog()==true;
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
            if(!ConfirmSignedUpdate(checkedManifest.Payload.Version,checkedManifest.Payload.Size))return;
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
        }catch(Exception){
            if(!_closing)MessageBox.Show("更新检查或下载失败：签名不可信、网络故障或文件无效。原版本没有改变。请检查网络或使用正式签名安装包。","更新未完成",MessageBoxButton.OK,MessageBoxImage.Warning);
        }finally{if(!_closing)CheckUpdateButton.IsEnabled=true;}
    }
}
