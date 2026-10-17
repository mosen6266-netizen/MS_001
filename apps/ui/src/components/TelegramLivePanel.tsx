import {useCallback,useEffect,useRef,useState} from "react";
import {ActionButton,ConfirmDialog,StatusPill} from "./DesignSystem";
import {IPC_PROTOCOL,isBridgeRequest,isBridgeResponse,isTelegramLiveStatus,type TelegramLiveStatus} from "../../../../src/contracts/ipc";

type LiveType="telegram.auth.status"|"telegram.auth.start"|"telegram.auth.submit"|
  "telegram.auth.stop"|"telegram.chats.refresh"|"telegram.message.send";
const initial:TelegramLiveStatus={
 nativeReady:false,connected:false,phase:"Initializing",title:"尚未连接",
 instruction:"请先在设置中保存Telegram应用API参数。",qrPng:null,canStartQr:false,
 canSubmit:false,canCancel:false,groups:[],delivery:"尚未发送",issue:null,outboundPending:false
};
let sequence=0;

export function TelegramLivePanel({view="accounts"}:{view?:"accounts"|"groups"}){
 const [status,setStatus]=useState<TelegramLiveStatus>(initial);
 const [busy,setBusy]=useState(false);
 const [message,setMessage]=useState("");
 const [code,setCode]=useState("");
 const [chatId,setChatId]=useState("");
 const [draft,setDraft]=useState("");
 const [confirm,setConfirm]=useState(false);
 const pending=useRef(new Set<()=>void>());
 const checking=useRef(false);
 const invoke=useCallback((type:LiveType,payload?:Record<string,unknown>):Promise<Record<string,unknown>>=>{
  const host=window.chrome?.webview;
  if(!host)return Promise.reject(new Error("需要 Windows 客户端"));
  const requestId="live-tg-"+(++sequence);
  const request=payload?{protocol:IPC_PROTOCOL,requestId,type,payload}:{protocol:IPC_PROTOCOL,requestId,type};
  if(!isBridgeRequest(request))return Promise.reject(new Error("请求内容不符合安全规则"));
  return new Promise((resolve,reject)=>{
   let finished=false;
   const finish=()=>{
    if(finished)return false;
    finished=true;clearTimeout(timer);host.removeEventListener("message",handler);
    pending.current.delete(cancel);return true;
   };
   const cancel=()=>{if(finish())reject(new Error("请求已取消"));};
   const handler=({data}:{data:unknown})=>{
    if(!isBridgeResponse(data)||data.requestId!==requestId||!finish())return;
    if(data.ok)resolve(data.payload);
    else reject(new Error(typeof data.payload.message==="string"?data.payload.message:"Telegram操作失败"));
   };
   const timer=setTimeout(()=>{if(finish())reject(new Error("Telegram响应超时。请先核对当前状态，不要重复发送。"));},45000);
   pending.current.add(cancel);host.addEventListener("message",handler);
   try{host.postMessage(JSON.stringify(request));}catch{cancel();}
  });
 },[]);
 const reload=useCallback(async()=>{
   // A sluggish local host must not accumulate new polling requests every 2.5 seconds.
   if(checking.current)return;
   checking.current=true;
   try{
    const reply=await invoke("telegram.auth.status");
    if(isTelegramLiveStatus(reply)){
      setStatus(reply);
    }else{
      setMessage("Telegram 状态数据不完整，请重新打开页面；已保留原有界面。");
    }
   }catch{/* Keep last known safe status; never turn an error into a false login. */}
   finally{checking.current=false;}
 },[invoke]);
 useEffect(()=>{
  if(!window.chrome?.webview)return;
  let active=true; void reload();
  const tick=window.setInterval(()=>{if(active)void reload();},2500);
  return()=>{active=false;clearInterval(tick);for(const cancel of pending.current)cancel();};
 },[reload]);
 const action=async(type:LiveType,payload?:Record<string,unknown>)=>{
  if(busy)return;
  setBusy(true);setMessage("");
  try{
   const result=await invoke(type,payload);
   const next=("status" in result)?result.status:("phase" in result?result:null);
   if(next!==null){
     if(isTelegramLiveStatus(next))setStatus(next);
     else setMessage("Telegram 返回了不完整的状态，已忽略异常数据。");
   }
   if(typeof result.message==="string")setMessage(result.message);
   else if(type==="telegram.chats.refresh")setMessage("群组信息已从Telegram刷新。");
   else if(type==="telegram.auth.start")setMessage("已启动官方授权流程，请查看当前状态。");
   else if(type==="telegram.auth.stop")setMessage("已关闭Telegram连接。");
   await reload();
  }catch(err){setMessage(err instanceof Error?err.message:"操作没有确认");}
  finally{setBusy(false);setCode("");setConfirm(false);}
 };
 const authAction=status.phase==="WaitPhoneOrQr"?"phone":
  status.phase==="WaitCode"?"code":status.phase==="WaitEmailAddress"?"email":
  status.phase==="WaitEmailCode"?"emailCode":status.phase==="WaitPassword"?"password":null;
 const authorizedGroups=Array.isArray(status.groups)?status.groups.filter(g=>g&&g.canSend):[];
 const choice=authorizedGroups.find(g=>g.chatId===chatId);
 const canSend=status.connected&&!status.outboundPending&&!busy&&!!choice&&draft.trim().length>0&&draft.length<=2000;
 const doSend=()=>{void action("telegram.message.send",{chatId,text:draft}).then(()=>setDraft(""));};

 return <section className="ui-demo-box" aria-label="Telegram实际连接" style={{marginBottom:16}}>
  <h2>{view==="groups"?"Telegram 群组与手动消息":"Telegram 官方登录"}</h2>
  {!window.chrome?.webview?<StatusPill tone="warning">需要Windows客户端</StatusPill>:
    status.connected?<StatusPill tone="success">Telegram已授权</StatusPill>:
    status.nativeReady?<StatusPill tone="warning">正在等待Telegram认证</StatusPill>:
    <StatusPill tone="warning">尚未登录</StatusPill>}
  <div className="ui-auth-state" style={{marginTop:14}} role="status" aria-live="polite">
   <h3>{status.title}</h3><p>{status.instruction}</p>
   {status.issue?<p>连接状态：{status.issue}</p>:null}
  </div>
  <div className="ui-action-row" style={{marginTop:13}}>
    {!status.nativeReady?<ActionButton tone="success" busy={busy} onClick={()=>void action("telegram.auth.start")}>连接我的 Telegram</ActionButton>:null}
    {status.nativeReady?<ActionButton tone="danger" disabled={busy} onClick={()=>void action("telegram.auth.stop")}>关闭连接</ActionButton>:null}
  </div>
  {status.qrPng&&status.phase==="WaitQrConfirmation"?
    <div className="ui-auth-state" style={{marginTop:14}}>
     <p>用自己已登录的 Telegram 手机端：设置 → 设备 → 扫描二维码。</p>
     <img src={status.qrPng} alt="Telegram 官方授权二维码" width={240} height={240}
       style={{background:"#fff",padding:12,borderRadius:8,maxWidth:"100%"}}/>
    </div>:null}
  {status.canStartQr&&status.phase==="WaitPhoneOrQr"?
   <div className="ui-action-row" style={{marginTop:13}}>
    <ActionButton tone="success" disabled={busy} onClick={()=>void action("telegram.auth.submit",{action:"qr",input:""})}>使用手机扫码登录</ActionButton>
   </div>:null}
  {authAction?
    <div className="ui-action-row" style={{marginTop:15}}>
      <label className="ui-field" style={{flex:"1 1 220px"}}>
        <span>{authAction==="phone"?"本人手机号（含国家码）":
          authAction==="email"?"登录邮箱":
          authAction==="password"?"Telegram两步验证密码":"官方验证码"}</span>
        <input value={code} autoComplete="off" spellCheck={false}
          type={authAction==="password"||authAction==="code"||authAction==="emailCode"?"password":"text"}
          placeholder={authAction==="phone"?"+12345678900":"填写官方验证信息"}
          onChange={e=>setCode(e.target.value)} disabled={busy} maxLength={512}/>
      </label>
      <ActionButton tone="success" disabled={!code||busy}
        onClick={()=>void action("telegram.auth.submit",{action:authAction,input:code})}>提交验证</ActionButton>
    </div>:null}
  {status.connected?
    <div style={{marginTop:18}}>
      <div className="ui-action-row">
        <ActionButton tone="quiet" disabled={busy} onClick={()=>void action("telegram.chats.refresh")}>从Telegram刷新群组</ActionButton>
      </div>
      <p className="ui-preview-caption">仅列出已同步的群组及当前可发文字权限；频道和私聊不在这里发送。</p>
      <div className="ui-form-row" style={{marginTop:10}}>
        <label className="ui-field" style={{flex:"1 1 240px"}}>
          <span>选择要发送的群组</span>
          <select value={chatId} onChange={e=>setChatId(e.target.value)} disabled={busy}>
            <option value="">请选择</option>
            {authorizedGroups.map(g=><option key={g.chatId} value={g.chatId}>{g.title}</option>)}
          </select>
        </label>
      </div>
      <label className="ui-field" style={{display:"block",marginTop:10}}>
        <span>单次手动消息（最多2000字符）</span>
        <textarea value={draft} maxLength={2000} rows={3} disabled={busy}
          onChange={e=>setDraft(e.target.value)} placeholder="仅向有权发送的群组发布已征得同意的内容"/>
      </label>
      <div className="ui-action-row">
        <ActionButton tone="success" disabled={!canSend} onClick={()=>setConfirm(true)}>检查并手动发送一次</ActionButton>
      </div>
      <p role="status" className="ui-preview-caption">投递状态：{status.delivery}</p>
    </div>:null}
  {message?<p className="ui-preview-caption" role="status">{message}</p>:null}
  <ConfirmDialog open={confirm} title="确认向群组发送"
    description={"向 "+(choice?.title??"所选群组")+" 手动发送一条消息？该操作是真实Telegram发送，不会自动重发或群发。"}
    confirmLabel="确认发送" confirmDisabled={!canSend} busy={busy}
    onConfirm={doSend} onCancel={()=>setConfirm(false)}/>
 </section>;
}
