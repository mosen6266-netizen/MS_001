using Mosen.GroupScheduler.Updater;

if(args.Length!=3)throw new ArgumentException("Usage: <version> <package> <update.json>");
string version=args[0];
SignedUpdateManifest.ParseVersion(version);
string package=Path.GetFullPath(args[1]);
string manifest=Path.GetFullPath(args[2]);
string pinned=File.ReadAllText("updater/trust/release-v1.spki.base64").Trim();
if(pinned=="UNCONFIGURED")throw new InvalidOperationException("Release signing trust root has not been enrolled");
var key=Convert.FromBase64String(pinned);
var update=SignedUpdateManifest.Verify(
    await File.ReadAllTextAsync(manifest),new Dictionary<string,byte[]> {["release-v1"]=key},"0.27.0");
if(update.Payload.Version!=version || Path.GetFileName(package)!=update.Payload.FileName)
    throw new InvalidDataException("Mismatch between signed release manifest and signed installer filename/version");
if(!await ResumablePackageDownloader.IsValidAsync(package,update.Payload))
    throw new InvalidDataException("Signed installer size or whole-file SHA256 does not match the signed feed");
Console.WriteLine($"Verified P-256 signed update.json and installer digest for v{version}");
