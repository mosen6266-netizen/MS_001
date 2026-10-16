import {useEffect,useRef,useState} from "react";
import type {MediaReference} from "../../../../src/contracts/ipc";
import {isMediaReference} from "../../../../src/contracts/ipc";
type MediaRequest=(action:"media.import"|"media.preview",payload:Record<string,unknown>)=>Promise<unknown>;
const MAX_BYTES=5*1024*1024;
function formatMime(file:File):"image/png"|"image/jpeg"|"image/webp"|null {
  if(["image/png","image/jpeg","image/webp"].includes(file.type))
    return file.type as "image/png"|"image/jpeg"|"image/webp";
  const ext=file.name.toLowerCase();
  if(ext.endsWith(".png"))return "image/png";
  if(ext.endsWith(".jpg")||ext.endsWith(".jpeg"))return "image/jpeg";
  if(ext.endsWith(".webp"))return "image/webp";
  return null;
}
function asBase64(file:File):Promise<string>{
  return new Promise((resolve,reject)=>{
    const reader=new FileReader();
    reader.onerror=()=>reject(new Error("无法读取本地图片"));
    reader.onload=()=>{
      const raw=reader.result;
      if(typeof raw!=="string"||!raw.includes(",")){reject(new Error("图片内容无法识别"));return;}
      resolve(raw.slice(raw.indexOf(",")+1));
    };
    reader.readAsDataURL(file);
  });
}
export function MediaAttachment({reference,request,onChange}:{
 reference?:MediaReference|null;request:MediaRequest;onChange:(asset:MediaReference|null)=>void
}){
  const fileInput=useRef<HTMLInputElement>(null);
  const [preview,setPreview]=useState<string|null>(null);
  const [error,setError]=useState("");
  const [busy,setBusy]=useState(false);
  useEffect(()=>{
    if(!reference){setPreview(null);return;}
    let active=true;setError("");setPreview(null);
    void request("media.preview",{reference}).then(data=>{
      if(!active)return;
      if(typeof data!=="object"||!data||!("dataUrl" in data)||
         typeof data.dataUrl!=="string"||
         !/^data:image\/(png|jpeg|webp);base64,[a-zA-Z0-9+/=]+$/.test(data.dataUrl)||
         data.dataUrl.length>6990600)throw new Error("本地图片预览返回格式无效");
      setPreview(data.dataUrl);
    }).catch(err=>{if(active)setError(err instanceof Error?err.message:"预览失败，请检查素材文件");});
    return ()=>{active=false;};
  },[reference?.assetId,reference?.mimeType,reference?.byteLength,request]);
  const load=async(file:File)=>{
    if(busy)return;
    if(file.size<32||file.size>MAX_BYTES){setError("只支持 5MB 以内的图片");return;}
    const mime=formatMime(file);
    if(!mime){setError("支持 PNG、JPEG、WebP 格式");return;}
    setBusy(true);setError("");
    try{
      const base64=await asBase64(file);
      const output=await request("media.import",{fileName:file.name.slice(0,128),mimeType:mime,base64});
      if(!isMediaReference(output))throw new Error("本地图片保存结果无效");
      onChange(output);
    }catch(err){setError(err instanceof Error?err.message:"图片导入失败");}
    finally{setBusy(false);}
  };
  return <div className="script-media">
    <div className="script-media-actions">
      <span>图片 / 图文消息（最多 5MB）</span>
      <button type="button" className="ui-button" disabled={busy||!window.chrome?.webview}
        onClick={()=>fileInput.current?.click()}>{reference?"替换图片":"添加图片"}</button>
      {reference?<button type="button" className="ui-button ui-button--danger" disabled={busy}
        onClick={()=>{onChange(null);setPreview(null);}}>移除引用</button>:null}
    </div>
    <input ref={fileInput} type="file" accept="image/png,image/jpeg,image/webp"
      aria-label="选择本地图片" className="sr-only" onChange={e=>{
        const file=e.currentTarget.files?.[0];if(file)void load(file);
        e.currentTarget.value="";
      }}/>
    <div className="script-media-drop" onDragOver={e=>{e.preventDefault();e.stopPropagation();}}
      onDrop={e=>{e.preventDefault();e.stopPropagation();const file=e.dataTransfer.files[0];if(file)void load(file);}}
      aria-label="将图片拖放到这里">
      {preview?<img src={preview} alt={"预览："+(reference?.fileName??"图片")}/>:
        <span>{reference?"图片已关联，预览加载中或暂不可用":"拖拽 PNG、JPEG、WebP 图片到此处，或点击添加图片"}</span>}
    </div>
    {reference?<small>{reference.fileName} · {reference.width}×{reference.height} · {Math.ceil(reference.byteLength/1024)} KB</small>:null}
    {error?<small role="alert" className="ui-field__error">{error}</small>:null}
  </div>;
}
