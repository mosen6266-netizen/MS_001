import {useCallback,useEffect,useRef,useState} from "react";
import {ActionButton,ConfirmDialog,StatusPill} from "./DesignSystem";
import {IPC_PROTOCOL,isBridgeResponse,isBridgeRequest} from "../../../../src/contracts/ipc";

type ApiState="checking"|"configured"|"missing"|"error"|"preview";
let unique=0;

export function TelegramApiConfigurationPanel(){
  const [status,setStatus]=useState<ApiState>("checking");
  const [apiId,setApiId]=useState("");
  const [apiHash,setApiHash]=useState("");
  const [busy,setBusy]=useState(false);
  const [remove,setRemove]=useState(false);
  const [notice,setNotice]=useState("");
  const inFlight=useRef(new Set<()=>void>());

  const invoke=useCallback((type:"telegram.api.status"|"telegram.api.save"|"telegram.api.clear", payload?:{apiId:number;apiHash:string})=>{
    const host=window.chrome?.webview;
    if(!host)return Promise.reject(new Error("请在 Windows 客户端打开此设置。"));
    const requestId="tg-api-"+(++unique);
    const packet=payload?{protocol:IPC_PROTOCOL,requestId,type,payload}:{protocol:IPC_PROTOCOL,requestId,type};
    if(!isBridgeRequest(packet))return Promise.reject(new Error("配置格式无效。"));
    return new Promise<Record<string,unknown>>((resolve,reject)=>{
      let done=false;
      const finish=()=>{if(done)return false;done=true;clearTimeout(timer);host.removeEventListener("message",onResponse);inFlight.current.delete(cancel);return true;};
      const cancel=()=>{if(finish())reject(new Error("请求已取消"));};
      const onResponse=({data}:{data:unknown})=>{
        if(!isBridgeResponse(data)||data.requestId!==requestId||!finish())return;
        if(data.ok)resolve(data.payload);
        else reject(new Error(typeof data.payload.message==="string"?data.payload.message:"本机配置失败"));
      };
      const timer=setTimeout(()=>{if(finish())reject(new Error("本机响应超时，请先检查配置状态。"));},15000);
      inFlight.current.add(cancel);
      host.addEventListener("message",onResponse);
      try{host.postMessage(JSON.stringify(packet));}catch{cancel();}
    });
  },[]);

  useEffect(()=>{
    if(!window.chrome?.webview){setStatus("preview");return;}
    let active=true;
    invoke("telegram.api.status").then(data=>{
      if(active)setStatus(data.configured===true?"configured":"missing");
    }).catch(()=>{if(active)setStatus("error");});
    return()=>{active=false;for(const cancel of inFlight.current)cancel();};
  },[invoke]);

  const valid=/^[1-9]\d{0,9}$/.test(apiId)&&Number(apiId)<=2147483647&&/^[0-9a-fA-F]{32}$/.test(apiHash);
  const save=async()=>{
    if(!valid||busy)return;
    setBusy(true);setNotice("");
    try{
      const result=await invoke("telegram.api.save",{apiId:Number(apiId),apiHash});
      if(result.configured!==true)throw new Error("无法确认本地保存结果。");
      setStatus("configured");setApiHash("");setApiId("");
      setNotice("已加密保存到当前 Windows 用户。");
    }catch(e){setNotice(e instanceof Error?e.message:"保存失败");}
    finally{setBusy(false);}
  };
  const clear=async()=>{
    if(busy)return;
    setBusy(true);setNotice("");
    try{
      await invoke("telegram.api.clear");setStatus("missing");
      setNotice("本机应用配置已清除。");setRemove(false);setApiHash("");setApiId("");
    }catch(e){setNotice(e instanceof Error?e.message:"清除失败");}
    finally{setBusy(false);}
  };

  return <section className="ui-demo-box" aria-label="本机 Telegram API 配置" style={{marginTop:16}}>
    <h2>Telegram 应用连接配置</h2>
    <p className="ui-preview-caption">
      使用你本人在 my.telegram.org 创建的 API ID 和 API Hash，仅存于当前 Windows 用户的加密配置。
      这不是 Telegram 账号登录，也不会启用消息发送。
    </p>
    {status==="configured"?<StatusPill tone="success">API 参数已加密保存</StatusPill>:
     status==="checking"?<StatusPill>检查本机配置中…</StatusPill>:
     status==="preview"?<StatusPill tone="warning">网页预览不可保存</StatusPill>:
     status==="error"?<StatusPill tone="danger">本机配置读取失败</StatusPill>:
     <StatusPill tone="warning">尚未设置 API 参数</StatusPill>}
    <div className="ui-form-row" style={{marginTop:16}}>
      <div className="ui-field">
        <label htmlFor="telegram-api-id">Telegram API ID</label>
        <input id="telegram-api-id" type="text" inputMode="numeric" autoComplete="off"
          value={apiId} maxLength={10} onChange={e=>setApiId(e.target.value)}
          placeholder="填写数字 ID" disabled={busy||status==="preview"}/>
      </div>
      <div className="ui-field">
        <label htmlFor="telegram-api-hash">Telegram API Hash</label>
        <input id="telegram-api-hash" type="password" autoComplete="new-password"
          spellCheck={false} maxLength={32} value={apiHash}
          onChange={e=>setApiHash(e.target.value)} placeholder="32位十六进制"
          disabled={busy||status==="preview"}/>
      </div>
    </div>
    <div className="ui-action-row" style={{marginTop:15}}>
      <ActionButton tone="success" disabled={!valid||busy||status==="preview"} busy={busy} onClick={save}>加密保存</ActionButton>
      <ActionButton tone="danger" disabled={busy||status!=="configured"} onClick={()=>setRemove(true)}>清除本机配置</ActionButton>
    </div>
    {notice?<p role="status" className="ui-preview-caption">{notice}</p>:null}
    <ConfirmDialog open={remove} title="清除 Telegram API 配置"
      description="只清除本机加密保存的应用 API 参数，不操作Telegram账号或其它应用数据。"
      confirmLabel="确认清除" onConfirm={clear} onCancel={()=>setRemove(false)} busy={busy}/>
  </section>;
}
