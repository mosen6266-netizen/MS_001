import {useCallback,useEffect,useRef,useState} from "react";
import {ActionButton,StatusPill,TextField} from "./DesignSystem";
import {makeGroupRequest,isBridgeResponse,isGroupPage,isTopicPage,isMemberList,
  type GroupPage,type TopicPage,type MemberRecord,type GroupAction,type UserPreferences} from "../../../../src/contracts/ipc";
import {useDebounced} from "../hooks/useDebounced";

type ViewStatus="loading"|"ready"|"error"|"preview";
const Empty:GroupPage={items:[],total:0,page:1,pageSize:20};
export function GroupCatalogPanel({preferences}:{preferences:UserPreferences}){
  const [query,setQuery]=useState("");
  const [page,setPage]=useState(1);
  const [groups,setGroups]=useState<GroupPage>(Empty);
  const [selected,setSelected]=useState<string|null>(null);
  const [topics,setTopics]=useState<TopicPage>({items:[],total:0,page:1,pageSize:20});
  const [topicPage,setTopicPage]=useState(1);
  const [members,setMembers]=useState<MemberRecord[]>([]);
  const [view,setView]=useState<ViewStatus>("loading");
  const [detailError,setDetailError]=useState(false);
  const [revision,setRevision]=useState(0);
  const serial=useRef(0);
  const debounce=useDebounced(query,preferences.searchDebounceMs);
  const request=useCallback((kind:GroupAction,payload:Record<string,unknown>):Promise<unknown>=>{
    const host=window.chrome?.webview;
    if(!host)return Promise.reject(new Error("桌面宿主尚未接入"));
    const id="grp-"+(++serial.current);
    return new Promise((resolve,reject)=>{
      const onData=({data}:{data:unknown})=>{
        if(!isBridgeResponse(data)||data.requestId!==id)return;
        clearTimeout(timeout);host.removeEventListener("message",onData);
        if(data.ok)resolve(data.payload);
        else reject(new Error("群组资料查询失败"));
      };
      const timeout=setTimeout(()=>{host.removeEventListener("message",onData);reject(new Error("本地群组读取超时"));},10000);
      host.addEventListener("message",onData);
      try{host.postMessage(JSON.stringify(makeGroupRequest(kind,id,payload)));}
      catch{clearTimeout(timeout);host.removeEventListener("message",onData);reject(new Error("群组查询无法执行"));}
    });
  },[]);
  useEffect(()=>{
    if(!window.chrome?.webview){setView("preview");return;}
    let live=true;setView("loading");
    request("groups.list",{query:debounce,page,pageSize:preferences.pageSize}).then(v=>{
      if(!live)return;
      if(!isGroupPage(v))throw new Error("不安全的群组目录响应");
      setGroups(v);setView("ready");
      if(selected && !v.items.some(g=>g.chatId===selected)){setSelected(null);setTopics({items:[],total:0,page:1,pageSize:preferences.pageSize});setMembers([]);}
    }).catch(()=>{if(live)setView("error");});
    return()=>{live=false;};
  },[debounce,page,preferences.pageSize,revision,request,selected]);
  useEffect(()=>{
    if(!selected)return;
    let live=true;setDetailError(false);
    Promise.all([
      request("groups.topics",{chatId:selected,page:topicPage,pageSize:preferences.pageSize}),
      request("groups.members",{chatId:selected})
    ]).then(([topicResult,memberResult])=>{
      if(!live)return;
      if(!isTopicPage(topicResult)||typeof memberResult!=="object"||memberResult===null||
        !("items" in memberResult)||!isMemberList(memberResult.items))
        throw new Error("目录资料格式异常");
      setTopics(topicResult);setMembers(memberResult.items);
    }).catch(()=>{if(live)setDetailError(true);});
    return()=>{live=false;};
  },[selected,topicPage,preferences.pageSize,revision,request]);
  const chosen=groups.items.find(g=>g.chatId===selected);
  const totalPages=Math.max(1,Math.ceil(groups.total/preferences.pageSize));
  const topicPages=Math.max(1,Math.ceil(topics.total/preferences.pageSize));
  return <div className="ui-demo-section">
    <section className="ui-demo-box" aria-labelledby="panel-title">
      <h2 id="panel-title">Telegram 群组与话题目录</h2>
      <StatusPill tone="warning">仅显示官方来源 · 真实群组同步尚未接入</StatusPill>
      <p className="ui-preview-caption" style={{marginTop:12}}>
        群组以 chat_id 识别，改名不会丢失绑定。当前客户端未连接官方 TDLib 原生库，不会出现模拟成真实群组的内容。
      </p>
      <div className="ui-form-row">
        <TextField label="搜索群组名称" value={query} onChange={s=>{setQuery(s);setPage(1);setSelected(null);}} placeholder="输入群组名称"/>
        <div className="ui-action-row" style={{alignItems:"flex-end"}}><ActionButton onClick={()=>setRevision(v=>v+1)}>刷新本地目录</ActionButton></div>
      </div>
      <div className="ui-list-summary">已同步 {groups.total} 个真实来源群组 · 第 {groups.page}/{totalPages} 页</div>
      {view==="preview"?<p className="ui-preview-caption">浏览器预览模式：没有 Windows 本地群组目录。</p>:
        view==="error"?<StatusPill tone="danger">本地目录读取失败</StatusPill>:null}
      <ul className={"ui-bounded-list "+(preferences.density==="compact"?"ui-bounded-list--compact":"")} aria-label="群组目录">
        {groups.items.map(g=><li key={g.chatId} className="ui-account-row">
          <div><strong>{g.title}</strong><small>{g.kind==="supergroup"?"超级群组":"普通群组"}{g.isForum?" · 论坛话题":""} · ID {g.chatId}</small></div>
          <ActionButton disabled={view!=="ready"} tone={selected===g.chatId?"success":"quiet"}
            onClick={()=>{setSelected(g.chatId);setTopicPage(1);}}>查看话题与权限</ActionButton>
        </li>)}
        {!groups.items.length?<li className="ui-empty">
          {view==="loading"?"正在读取群组…":"尚无来自已验证 Telegram 账号的群组资料。"}
        </li>:null}
      </ul>
      <div className="ui-action-row">
        <ActionButton disabled={page<=1} onClick={()=>{setPage(v=>Math.max(1,v-1));setSelected(null);}}>上一页</ActionButton>
        <span className="ui-pagination-label">{groups.page} / {totalPages}</span>
        <ActionButton disabled={page>=totalPages} onClick={()=>{setPage(v=>v+1);setSelected(null);}}>下一页</ActionButton>
      </div>
    </section>
    {chosen?<section className="ui-demo-box" aria-label="群组话题和成员权限">
      <h3>{chosen.title} — 话题与账号权限</h3>
      {detailError?<StatusPill tone="danger">部分资料加载失败，请刷新目录</StatusPill>:null}
      <h4>话题列表</h4>
      {topics.items.length===0?<p className="ui-preview-caption">该群尚无已同步话题。</p>:
        <ul className="ui-bounded-list" aria-label="话题列表">{topics.items.map(t=><li key={t.topicId}>
          <span>{t.title} · #{t.topicId}</span>
          <small>{t.isClosed?"已关闭":t.isHidden?"已隐藏":"未标记关闭"} · 启动前仍需检查权限</small>
        </li>)}</ul>}
      <div className="ui-action-row">
        <ActionButton disabled={topicPage<=1} onClick={()=>setTopicPage(v=>v-1)}>上一页话题</ActionButton>
        <span className="ui-pagination-label">{topicPage} / {topicPages}</span>
        <ActionButton disabled={topicPage>=topicPages} onClick={()=>setTopicPage(v=>v+1)}>下一页话题</ActionButton>
      </div>
      <h4 style={{marginTop:18}}>本地账号成员资格</h4>
      {members.length===0?<p className="ui-preview-caption">没有经过官方身份验证的账号权限资料。</p>:
        <ul className="ui-bounded-list" aria-label="账号成员权限">{members.map(m=><li key={m.accountId}>
          <span>{m.alias} · {m.status}</span>
          <small>{m.canSendText?"文字权限已记录":"文字不可发"} · {m.canSendPhotos?"图片权限已记录":"图片不可发"} · 慢速模式 {m.slowModeDelaySeconds} 秒</small>
        </li>)}</ul>}
      <p className="ui-preview-caption">此处是上次权限快照，不作为可立即发送的证明。启动任务必须重新核验账号、群组、话题、慢速模式和官方平台限制。</p>
    </section>:null}
  </div>;
}
