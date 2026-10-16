import {useEffect,useMemo,useState} from "react";
import {isBridgeResponse,isRuntimeDashboardSnapshot,makeRequest,
    type RuntimeDashboardSnapshot,type RuntimeDashboardRow} from "../../../../src/contracts/ipc";
import {selectRuntimePage,displayRemaining,statusLabel,issueLabel,
    type RuntimeFilter} from "../runtime-dashboard/model";
import "../runtime-dashboard.css";

const PAGE_SIZE=10;
type FetchState="loading"|"preview"|"ready"|"error";
export function RuntimeDashboardPanel(){
  const [snapshot,setSnapshot]=useState<RuntimeDashboardSnapshot|null>(null);
  const [state,setState]=useState<FetchState>("loading");
  const [now,setNow]=useState(()=>Date.now());
  const [search,setSearch]=useState("");
  const [filter,setFilter]=useState<RuntimeFilter>("all");
  const [page,setPage]=useState(1);
  const [note,setNote]=useState("");
  useEffect(()=>{
    const host=window.chrome?.webview;
    if(!host){setState("preview");return;}
    let running=true,seconds=0,sequence=0,latest=0;
    const handler=({data}:{data:unknown})=>{
      if(!running||!isBridgeResponse(data)||data.type!=="runtime.snapshot.result"||
         !data.requestId.startsWith("runtime-"))return;
      const number=Number(data.requestId.slice("runtime-".length));
      if(!Number.isSafeInteger(number)||number<latest)return;
      latest=number;
      if(data.ok&&isRuntimeDashboardSnapshot(data.payload)){
        setSnapshot(data.payload);setState("ready");setNote("");
      }else{
        setState("error");setNote("本地快照读取失败；不会自动恢复或执行任务。");
      }
    };
    host.addEventListener("message",handler);
    const refresh=()=>{
      const requestId="runtime-"+(++sequence);
      try{host.postMessage(JSON.stringify(makeRequest("runtime.snapshot",requestId)));}
      catch{setState("error");setNote("本地界面连接失败。");}
    };
    refresh();
    // ONE shared clock for countdown + a five-second read refresh, not one per row.
    const timer=window.setInterval(()=>{
      if(!running)return;
      setNow(Date.now());
      if(++seconds%5===0)refresh();
    },1000);
    return()=>{running=false;window.clearInterval(timer);host.removeEventListener("message",handler);};
  },[]);
  const chosen=useMemo(()=>selectRuntimePage(snapshot?.items??[],filter,search,page,PAGE_SIZE),
    [snapshot,filter,search,page]);
  const totals=snapshot?.summary;
  const updateFilter=(v:RuntimeFilter)=>{setFilter(v);setPage(1);};
  const updateSearch=(v:string)=>{setSearch(v.slice(0,80));setPage(1);};
  const stateClass=(j:RuntimeDashboardRow)=>j.issueCode||["Review","Failed"].includes(j.state)
    ?"is-warning":j.state==="Completed"?"is-complete":j.state==="Running"?"is-running":"";
  return <section className="runtime-center" aria-label="任务运行中心（只读）">
    <div className="runtime-intro">
      <div><h2 id="panel-title">运行中心 <small>只读状态</small></h2>
        <p>数据来自本机已持久化的模拟任务。当前不支持真实 Telegram 发送，也不会自动启动或恢复群任务。</p></div>
      <span role="status" className={"runtime-service "+(state==="error"?"runtime-error":"")}>
        {state==="ready"?"本地快照已同步":state==="loading"?"正在读取本地状态":
          state==="preview"?"浏览器预览 · 无本地任务":"快照不可用"}
      </span>
    </div>
    {note?<div className="runtime-alert" role="alert">{note}</div>:null}
    <div className="runtime-stats" aria-label="任务状态统计">
      <article><span>任务总数</span><strong>{totals?.total??0}</strong></article>
      <article><span>运行中（模拟）</span><strong>{totals?.running??0}</strong></article>
      <article><span>等待人工复核</span><strong className={totals?.review?"runtime-count-warning":""}>{totals?.review??0}</strong></article>
      <article><span>异常任务</span><strong className={totals?.failed?"runtime-count-warning":""}>{totals?.failed??0}</strong></article>
      <article><span>已完成</span><strong>{totals?.completed??0}</strong></article>
    </div>
    {(totals?.uncertainMessages||totals?.corruptRecords)?<div className="runtime-alert" role="alert">
      待复核的投递记录：{totals.uncertainMessages} · 隔离的损坏记录：{totals.corruptRecords}。
      请先核对历史记录；此页面不会重发或修改任何消息。
    </div>:null}
    <div className="runtime-controls">
      <label>搜索群ID或任务ID<input aria-label="搜索任务" placeholder="输入群 ID 或任务 ID" value={search}
        onChange={e=>updateSearch(e.target.value)} maxLength={80}/></label>
      <label>状态筛选<select aria-label="任务状态筛选" value={filter}
        onChange={e=>updateFilter(e.target.value as RuntimeFilter)}>
        <option value="all">全部</option><option value="active">运行中 / 等待 / 暂停</option>
        <option value="review">需复核</option><option value="failed">失败</option>
        <option value="completed">已完成</option><option value="stopped">已停止</option>
      </select></label>
      <p>每页最多 {PAGE_SIZE} 个任务 · 单一倒计时时钟 · 每5秒只读刷新</p>
    </div>
    {chosen.items.length===0?<div className="runtime-empty" role="status">
      {state==="preview"?"浏览器预览不显示虚构任务；在 Windows 客户端内读取本地记录。":
        state==="loading"?"正在读取保存的任务记录…":state==="error"?"当前无法读取任务记录。":
        snapshot?.items.length===0?"目前没有已保存的模拟群任务。真正运行仍受实时授权与平台权限限制。":
        "没有符合筛选条件的任务。"}
    </div>:<div className="runtime-cards" role="list" aria-label="当前页任务">
      {chosen.items.map(item=><article key={item.runId} role="listitem"
        className={"runtime-card "+stateClass(item)}>
        <div className="runtime-card-head"><div>
          <strong>群 {item.chatId}{item.topicId?" · 话题 "+item.topicId:""}</strong>
          <small>任务 {item.runId.slice(0,8)}… · 保存修订 {item.revision}</small>
        </div><span className="runtime-status">{statusLabel(item.state)}</span></div>
        <div className="runtime-progress-label"><span>已完成 {item.completedSteps} / {item.totalSteps} 条</span>
          <strong>{item.completionPercent}%</strong></div>
        <div className="runtime-bar" role="progressbar" aria-label="任务完成率"
          aria-valuemin={0} aria-valuemax={100} aria-valuenow={item.completionPercent}>
          <div style={{width:item.completionPercent+"%"}}/></div>
        <div className="runtime-card-bottom">
          <span>下一条：{item.state==="Completed"?"全部完成":"第 "+item.nextStepNumber+" 条"}</span>
          <span>阶段：{item.phase==="WaitingBefore"?"发送前等待":item.phase==="Typing"?"正在输入":
            item.phase==="AwaitingSend"?"等待发送许可":item.phase==="AwaitingReceipt"?"等待回执":
            item.phase==="WaitingAfter"?"回执后等待":"已停止推进"}</span>
          <strong>剩余：{displayRemaining(item,now)}</strong>
        </div>
        {item.issueCode?<div className="runtime-issue" role="alert">{issueLabel(item.issueCode)} · 需要人工核对</div>:null}
      </article>)}</div>}
    <div className="runtime-pagination" aria-label="任务翻页">
      <button type="button" disabled={chosen.page<=1} onClick={()=>setPage(p=>Math.max(1,p-1))}>上一页</button>
      <span>第 {chosen.page} / {chosen.pages} 页 · {chosen.total} 项</span>
      <button type="button" disabled={chosen.page>=chosen.pages} onClick={()=>setPage(p=>Math.min(chosen.pages,p+1))}>下一页</button>
    </div>
  </section>;
}
