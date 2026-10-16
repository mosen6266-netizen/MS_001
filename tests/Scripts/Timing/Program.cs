using Mosen.GroupScheduler.Scripts.Editor;
using Mosen.GroupScheduler.Scripts.Timing;
using Mosen.GroupScheduler.Scripts.Media;
int tests=0;
void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);Console.WriteLine("PASS "+text);tests++;}
var p=TimingPreviewCalculator.Create(new TimingProfile(30000,5000,12000),36000);
Check(p.ReadyToSendAtMs==35000,"30 second pre-wait + 5 second typing = send after 35 seconds");
Check(p.Phases[0].BeginMs==0&&p.Phases[0].EndMs==30000,"pure waiting ends after 30s");
Check(p.Phases[1].BeginMs==30000&&p.Phases[1].EndMs==35000,"typing starts only after waiting");
Check(p.Phases[2].BeginMs==35000,"ready to send at 35s no earlier");
Check(p.Phases[3].BeginMs==36000&&p.Phases[3].EndMs==48000,"post-send waits after receipt not request start");
Check(TimingPreviewCalculator.Create(new TimingProfile(0,0,0)).ReadyToSendAtMs==0,"zero+zero immediate draft preview");
Check(TimingPreviewCalculator.Create(new TimingProfile()).ReadyToSendAtMs==10000,"5/5/0 default timeline");
bool tooEarly=false;try{TimingPreviewCalculator.Create(new(30000,5000,0),30000);}catch(ArgumentOutOfRangeException){tooEarly=true;}
Check(tooEarly,"cannot acknowledge before send gate");
bool badTime=false;try{TimingPreviewCalculator.Create(new(-1000,5000,0));}catch(ArgumentException){badTime=true;}
Check(badTime,"negative timing rejected");
Check(TimingPreviewCalculator.Create(new(999,5000,0)).ReadyToSendAtMs==5999,
  "legacy millisecond timings remain valid and preserved without rounding");
var temp=Path.Combine(Path.GetTempPath(),"ms001-images-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
try
{
 var store=new MediaAssetStore(temp);
 // 1 x 1 transparent PNG: test fixture, not downloaded from a remote URL.
 var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScL/nwAAAABJRU5ErkJggg==");
 var b64=Convert.ToBase64String(png);
 var refA=store.ImportBase64("../图片.png","image/png",b64);
 Check(refA.Width==1&&refA.Height==1&&refA.MimeType=="image/png","valid png dimensions read from header");
 Check(refA.AssetId.Length==64&&refA.FileName.IndexOf('/')<0,"sha256 ID and sanitized portable display name");
 Check(Directory.GetFiles(temp).Length==1,"local asset written once using content hash");
 var refB=store.ImportBase64("another-name.png","image/png",b64);
 Check(refA.AssetId==refB.AssetId&&Directory.GetFiles(temp).Length==1,"identical images deduplicated");
 Check(store.PreviewDataUrl(refA).StartsWith("data:image/png;base64,"),"preview returned as bounded data URL without file path");
 bool spoof=false;try{store.ImportBase64("photo.jpeg","image/jpeg",b64);}catch(ArgumentException){spoof=true;}
 Check(spoof,"mismatched declared mime rejected");
 bool malicious=false;try{store.ImportBase64("evil.png","image/png",Convert.ToBase64String(new byte[120]));}
 catch(ArgumentException){malicious=true;}
 Check(malicious,"non-image content rejected");
 bool huge=false;try{store.ImportBase64("too-big.png","image/png",Convert.ToBase64String(new byte[5242881]));}
 catch(ArgumentException){huge=true;}
 Check(huge,"image >5MiB rejected before disk allocation");
 var path=Directory.GetFiles(temp).Single();File.WriteAllBytes(path,new byte[50]);
 bool corrupt=false;try{store.PreviewDataUrl(refA);}catch(IOException){corrupt=true;}
 Check(corrupt,"tampered hash file fails preview checksum");
 Check(Directory.GetFiles(temp).Length==1,"preview failure never deletes media or another script reference");
}
finally{try{Directory.Delete(temp,true);}catch(IOException){}}
Console.WriteLine($"SUMMARY: {tests} virtual timing and local asset assertions passed. No Telegram actions sent.");
