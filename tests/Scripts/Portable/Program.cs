using System.IO.Compression;
using System.Text;
using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Portable;
using Mosen.GroupScheduler.Scripts.Media;
int tests=0;
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);tests++;Console.WriteLine("PASS "+name);}
void Reject(byte[] payload,string label)
{
    try { PortableScriptPackage.Decode(payload);throw new Exception("UNSAFE ACCEPTED "+label); }
    catch(InvalidDataException){Check(true,label);}
}
byte[] Zip(params (string Name,byte[] Data)[] items)
{
    using var ms=new MemoryStream();
    using(var zip=new ZipArchive(ms,ZipArchiveMode.Create,true))
        foreach(var (name,data) in items){using var s=zip.CreateEntry(name).Open();s.Write(data);}
    return ms.ToArray();
}
var keyA=Guid.NewGuid().ToString("D");var keyB=Guid.NewGuid().ToString("D");var keyC=Guid.NewGuid().ToString("D");
var doc=new EditorDocument("三人测试 👋",
  new(){new(keyA,"张三"),new(keyB,"李四"),new(keyC,"王五")},
  new(){new(Guid.NewGuid().ToString("D"),keyA,"hi",30000,5000,12000),
        new(Guid.NewGuid().ToString("D"),keyB,"第二句",0,0,0),
        new(Guid.NewGuid().ToString("D"),keyC,"第三句",1500,3000,0)});
var baseDir=Path.Combine(Path.GetTempPath(),"mosen-portable-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(baseDir);
try
{
    var store=new MediaAssetStore(Path.Combine(baseDir,"device-A"));
    var bytes=PortableScriptPackage.Export(doc,store);
    var loaded=PortableScriptPackage.Decode(bytes);
    Check(loaded.Document.Title==doc.Title && loaded.Document.Steps.Count==3,"A exports B imports 3-role script");
    Check(loaded.Document.Steps[0].PreSendDelayMs==30000 && loaded.Document.Steps[0].TypingDurationMs==5000 &&
          loaded.Document.Steps[0].PostSendDelayMs==12000,"exact sequential timing fields roundtrip");
    Check(loaded.Document.Steps[2].PreSendDelayMs==1500,"legacy millisecond timing survives");
    var local=new[]{new LocalRemarkCandidate("B1","张三"),new LocalRemarkCandidate("B2","李四"),
                    new LocalRemarkCandidate("B3","王五"),new LocalRemarkCandidate("Extra","额外账号")};
    var result=RoleRemarkResolver.Resolve(loaded.Document,local);
    Check(result.Count==3&&result.All(r=>r.Status=="unique"),"extra accounts on B do not prevent matching");
    Check(RoleRemarkResolver.Resolve(doc,local.Take(2)).Count(x=>x.Status=="missing")==1,"all missing remarks explicitly identified");
    var duplicate=RoleRemarkResolver.Resolve(doc,local.Concat(new[]{new LocalRemarkCandidate("Duplicate","张三")}));
    Check(duplicate[0].Status=="ambiguous"&&duplicate[0].CandidateIds.Count==2,"duplicate name never auto-picked");
    var scoped=RoleRemarkResolver.Resolve(doc,local.Concat(new[]{new LocalRemarkCandidate("Duplicate","张三")}),
        new HashSet<string>{"B1","B2","B3"});
    Check(scoped.All(r=>r.Status=="unique"),"target group eligibility narrows duplicate ambiguity");
    Check(RoleRemarkResolver.Normalize(" e\u0301 ")=="é","NFC plus trim exact matching");
    var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
    var m=store.ImportBase64("测试.png","image/png",Convert.ToBase64String(png));
    var withImage=doc with {Steps=new(){doc.Steps[0] with {Media=m},doc.Steps[1] with {Media=m}}};
    var mediaZip=PortableScriptPackage.Export(withImage,store);
    var imported=PortableScriptPackage.Decode(mediaZip);
    Check(imported.Assets.Count==1,"shared image exported once");
    var bStore=new MediaAssetStore(Path.Combine(baseDir,"device-B"));
    PortableScriptPackage.InstallMedia(imported,bStore);
    Check(bStore.PreviewDataUrl(m).Contains("base64,"),"image restored and hash validated on another device");
    Check(imported.Document.Steps[1].Media?.AssetId==m.AssetId,"reused media references retained");
    var json=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
         new{format=PortableScriptPackage.Format,version=1,document=doc},EditorDocumentRules.WireOptions);
    Reject(Zip(("manifest.json",json),("../evil.txt",new byte[4])),"reject path traversal");
    Reject(Zip(("manifest.json",json),("manifest.json",json)),"reject duplicate manifest");
    Reject(Zip(("manifest.json",json),("media/garbage.png",new byte[100])),"reject undeclared image");
    Reject(Zip(("manifest.json",json),("extra.json",new byte[3])),"reject unexpected extra files");
    Reject(Zip(("manifest.json",Encoding.UTF8.GetBytes("{\"format\":\"other\",\"version\":1,\"document\":{}}"))),"reject foreign format");
    var extra=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
         new{format=PortableScriptPackage.Format,version=1,document=doc,accountId="secret"},EditorDocumentRules.WireOptions);
    Reject(Zip(("manifest.json",extra)),"reject embedded identity fields");
    using(var stream=new MemoryStream(mediaZip))
    using(var zip=new ZipArchive(stream,ZipArchiveMode.Read))
       Check(zip.Entries.Count==2,"manifest and one content-addressed image only");
}
finally {try{Directory.Delete(baseDir,true);}catch(IOException){}}
Console.WriteLine($"SUMMARY: {tests} portable script tests; no Telegram messages or sessions.");
