import {useCallback,useEffect,useRef,useState} from "react";
import {ActionButton,ConfirmDialog,StatusPill,TextField,ToastViewport,type ToastInfo} from "./DesignSystem";
import {makeAccountRequest,isBridgeResponse,isAccountPage,type AccountItem,type AccountPage,type AccountAction,type UserPreferences} from "../../../../src/contracts/ipc";
import {useDebounced} from "../hooks/useDebounced";
import {useActionLock} from "../hooks/useActionLock";

export function AccountRegistryPanel({preferences}:{preferences:UserPreferences}){
  const [query,setQuery]=useState("");
  const [group,setGroup]=useState("");
  const [page,setPage]=useState(1);
  const [result,setResult]=useState<AccountPage>({items:[],total:0,page:1,pageSize:preferences.pageSize});
  const [state,setState]=useState<"loading"|"ready"|"error">("loading");
  const [alias,setAlias]=useState("");
  const [groupTag,setGroupTag]=useState("");
  const [editing,setEditing]=useState<AccountItem|null>(null);
  const [archive,setArchive]=useState<AccountItem|null>(null);
  const [typedAlias,setTypedAlias]=useState("");
  const [toast,setToast]=useState<ToastInfo|null>(null);
  const [serial,setSerial]=useState(0);
  const {busy,run}=useActionLock();
  const requestCounter=useRef(0);
  const inFlight=useRef(new Set<()=>void>());
  const debounce=useDebounced(query,preferences.searchDebounceMs);
  const request=useCallback((type:AccountAction,payload:Record<string,unknown>):Promise<unknown>=>{
    const host=window.chrome?.webview;
    if(!host)return Promise.reject(new Error("此页面只能在 Windows 桌面程序中保存账号资料。"));
    const id="acc-"+(++requestCounter.current);
    return new Promise((resolve,reject)=>{
      let completed=false;
      let timeout:ReturnType<typeof setTimeout>|undefined;
      const finish=()=>{
        if(completed)return false;
        completed=true;host.removeEventListener("message",listener);
        if(timeout)clearTimeout(timeout);
        inFlight.current.delete(cancel);
        return true;
      };
      const cancel=()=>{if(finish())reject(new Error("请求已取消"));};
      const listener=({data}:{data:unknown})=>{
        if(!isBridgeResponse(data)||data.requestId!==id||!finish())return;
        if(data.ok)resolve(data.payload);
        else reject(new Error(typeof data.payload.message==="string"?data.payload.message:"本地操作被拒绝"));
      };
      inFlight.current.add(cancel);host.addEventListener("message",listener);
      timeout=setTimeout(()=>{if(finish())reject(new Error("本地操作响应超时，先刷新确认状态，不要重复操作。"));},10000);
      try{host.postMessage(JSON.stringify(makeAccountRequest(type,id,payload)));}
      catch{if(finish())reject(new Error("无法与 Windows 本地服务通信。"));}
    });
  },[]);
  useEffect(()=>()=>{
    // Cleanup all subscriptions on navigation/unmount; no ghost page listeners.
    for(const cancel of [...inFlight.current])cancel();
  },[]);
  useEffect(()=>{
    let active=true;
    setState("loading");
    request("accounts.list",{query:debounce,group,page,pageSize:preferences.pageSize})
      .then(data=>{
        if(!active)return;
        if(!isAccountPage(data))throw new Error("本地账号列表响应格式异常");
        setResult(data);setState("ready");
      }).catch(()=>{if(active)setState("error");});
    return ()=>{active=false;};
  },[request,debounce,group,page,preferences.pageSize,serial]);
  const announce=(message:string,tone:"success"|"danger"="success")=>
    setToast({id:Date.now(),message,tone});
  const refresh=()=>setSerial(x=>x+1);
  const write=async(type:AccountAction,payload:Record<string,unknown>,message:string):Promise<boolean>=>{
    try{
      const output=await run(()=>request(type,payload));
      if(!output)return false;
      announce(message);refresh();
      return true;
    }catch(e){announce(e instanceof Error?e.message:"本地账号操作失败","danger");return false;}
  };
  const save=async()=>{
    if(!alias.trim()||busy)return;
    const saved=editing ?
      await write("accounts.edit",{id:editing.id,expectedRevision:editing.revision,alias,groupTag},"账号备注与分组已保存") :
      await write("accounts.create",{alias,groupTag},"已登记账号槽位（尚未登录 Telegram）");
    if(saved){setEditing(null);setAlias("");setGroupTag("");}
  };
  const startEdit=(item:AccountItem)=>{
    if(!item.canAttachSession){announce("旧版账号ID需要迁移后才能编辑或启动会话。","danger");return;}
    setEditing(item);setAlias(item.alias);setGroupTag(item.groupTag);
  };
  const confirmArchive=async()=>{
    if(!archive||typedAlias!==archive.alias){announce("请输入完全一致的账号备注才能确认归档。","danger");return;}
    const target=archive;
    const ok=await write("accounts.archive",{id:target.id,expectedRevision:target.revision,typedAlias},"账号已归档；会话密钥和文件未被删除");
    if(ok){setArchive(null);setTypedAlias("");}
  };
  const pages=Math.max(1,Math.ceil(result.total/preferences.pageSize));
  return <div className="ui-demo-section">
    <section className="ui-demo-box" aria-labelledby="panel-title">
      <h2 id="panel-title">本地账号注册表</h2>
      <StatusPill tone="warning">仅登记元数据 · 不代表真实登录或在线</StatusPill>
      <p className="ui-preview-caption" style={{marginTop:10}}>
        最多登记300个账号。登录与会话恢复需要官方 TDLib 原生库验收后才能使用；备注和分组数据已保存于本机 SQLite。
      </p>
      <div className="ui-form-row">
        <TextField label="搜索账号备注" value={query} onChange={x=>{setQuery(x);setPage(1);}} placeholder="查找账号"/>
        <TextField label="按分组筛选（留空表示全部）" value={group} onChange={x=>{setGroup(x);setPage(1);}}/>
      </div>
      <div className="ui-list-summary">本地已登记 {result.total} 条（筛选结果） · 目标上限 300 · 当前第 {result.page}/{pages} 页</div>
      {state==="error"?<StatusPill tone="danger">账号读取失败，请检查本地数据库</StatusPill>:null}
      <ul className={"ui-bounded-list "+(preferences.density==="compact"?"ui-bounded-list--compact":"")} aria-label="本地账号目录">
        {result.items.map(item=><li key={item.id} className="ui-account-row">
          <div><strong>{item.alias}</strong><small>{item.groupTag||"未分组"} · {item.displayName||"未绑定Telegram身份"} · 未连接</small></div>
          <div className="ui-action-row">
            <ActionButton disabled={busy||!item.canAttachSession} onClick={()=>startEdit(item)}>编辑备注</ActionButton>
            <ActionButton tone="danger" disabled={busy||!item.canAttachSession} onClick={()=>{setArchive(item);setTypedAlias("");}}>归档</ActionButton>
          </div>
        </li>)}
        {!result.items.length&&state==="ready"?<li className="ui-empty">暂无匹配账号</li>:null}
        {state==="loading"?<li className="ui-empty">正在读取账号…</li>:null}
      </ul>
      <div className="ui-action-row">
        <ActionButton disabled={page<=1} onClick={()=>setPage(p=>Math.max(1,p-1))}>上一页</ActionButton>
        <span className="ui-pagination-label">{page} / {pages}</span>
        <ActionButton disabled={page>=pages} onClick={()=>setPage(p=>p+1)}>下一页</ActionButton>
        <ActionButton tone="ghost" onClick={refresh}>刷新列表</ActionButton>
      </div>
    </section>
    <section className="ui-demo-box" aria-label="账号备注和分组编辑">
      <h3>{editing?"编辑现有账号备注":"登记新的本地账号槽位"}</h3>
      <div className="ui-form-row">
        <TextField label="账号备注" value={alias} onChange={setAlias} placeholder="例如：客服A"/>
        <TextField label="分组名称" value={groupTag} onChange={setGroupTag} placeholder="例如：工作账号"/>
      </div>
      <div className="ui-action-row" style={{marginTop:16}}>
        <ActionButton tone="success" busy={busy} disabled={!alias.trim()||alias.length>64||groupTag.length>64}
          onClick={save}>{editing?"保存备注":"登记账号"}</ActionButton>
        {editing?<ActionButton onClick={()=>{setEditing(null);setAlias("");setGroupTag("");}}>取消编辑</ActionButton>:null}
      </div>
      <p className="ui-preview-caption" style={{marginTop:12}}>不收集手机号、验证码、密码或会话密钥。真实 Telegram 账号的重复身份检查必须在后续官方授权之后完成。</p>
    </section>
    <ConfirmDialog open={archive!==null} title="确认归档本地账号" description={archive?
      "将账号“"+archive.alias+"”从目录归档；须先解除任务关联。密钥及会话文件不会永久删除。":""}
      confirmDisabled={!archive || typedAlias!==archive.alias || busy}
      onConfirm={confirmArchive} onCancel={()=>{setArchive(null);setTypedAlias("");}}>
      <div style={{marginTop:14}}>
        <TextField label={"请输入备注“"+(archive?.alias??"")+"”以确认"} value={typedAlias} onChange={setTypedAlias}
          hint="必须与账号备注完全一致才能归档。"/>
      </div>
    </ConfirmDialog>
    <ToastViewport toast={toast} dismiss={()=>setToast(null)}/>
  </div>;
}
