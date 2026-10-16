import {useCallback,useEffect,useMemo,useState} from "react";
import {ActionButton,StatusPill} from "./DesignSystem";
import {isGroupPage,isMemberList,isScriptSnapshot,isTopicPage,
  type ScriptAction,type GroupAction,type ScriptSnapshot,type GroupRecord,type MemberRecord,type TopicRecord} from "../../../../src/contracts/ipc";

type Request=(action:ScriptAction|GroupAction,payload:Record<string,unknown>)=>Promise<unknown>;
type Version={version:number;sourceRevision:number;sha256:string};
type PreviewLine={ordinal:number;stepKey:string;roleKey:string;roleLabel:string;body:string;imageName:string|null;
  waitingMs:number;typingMs:number;postSendMs:number;earliestSendFromStepStartMs:number};
type Preview={version:number;sha256:string;entries:PreviewLine[];minimumDurationWithoutNetworkMs:number};
type Issue={chatId:string;topicId:string|null;code:string;message:string;roleLabel:string|null};
type Target={chatId:string;topicId?:string};
const norm=(v:string)=>v.trim().normalize("NFC");
const safeVersion=(v:unknown):v is Version=>typeof v==="object"&&v!==null&&
  "version" in v&&Number.isInteger(v.version)&&"sourceRevision" in v&&Number.isInteger(v.sourceRevision)&&
  "sha256" in v&&typeof v.sha256==="string";
const validPreview=(v:unknown):v is Preview=>typeof v==="object"&&v!==null&&
  "entries" in v&&Array.isArray(v.entries)&&"version" in v&&Number.isInteger(v.version)&&
  "sha256" in v&&typeof v.sha256==="string";
const targetId=(t:Target)=>t.chatId+"|"+(t.topicId??"");
export function PublicationPanel({script,request,flush}:{
  script:ScriptSnapshot;request:Request;flush:()=>Promise<boolean>;
}){
  const [versions,setVersions]=useState<Version[]>([]);
  const [version,setVersion]=useState<number>(0);
  const [preview,setPreview]=useState<Preview|null>(null);
  const [groups,setGroups]=useState<GroupRecord[]>([]);
  const [targets,setTargets]=useState<Target[]>([]);
  const [topics,setTopics]=useState<Record<string,TopicRecord[]>>({});
  const [members,setMembers]=useState<Record<string,MemberRecord[]>>({});
  const [choices,setChoices]=useState<Record<string,string>>({});
  const [issues,setIssues]=useState<Issue[]>([]);
  const [ready,setReady]=useState(false);
  const [busy,setBusy]=useState(false);
  const [feedback,setFeedback]=useState("草稿与已发布版本隔离；发布和启动检查不会向 Telegram 发送任何消息。");
  const refresh=useCallback(async()=>{
    const reply=await request("scripts.versions",{id:script.scriptId});
    if(typeof reply!=="object"||reply===null||!("items" in reply)||!Array.isArray(reply.items)||
       !reply.items.every(safeVersion))throw new Error("发布版本列表无效");
    setVersions(reply.items);
    setVersion(old=>reply.items.some((v:Version)=>v.version===old)?old:(reply.items[0]?.version??0));
  },[request,script.scriptId]);
  useEffect(()=>{void refresh().catch(()=>setFeedback("发布版本读取失败。"));},[refresh]);
  useEffect(()=>{
    let active=true;
    const read=async()=>{
      const all:GroupRecord[]=[];
      for(let page=1;page<=6;page++){
        const r=await request("groups.list",{query:"",page,pageSize:50});
        if(!isGroupPage(r))throw new Error("群组列表读取失败");
        all.push(...r.items);
        if(all.length>=r.total)break;
      }
      if(active)setGroups(all);
    };
    void read().catch(()=>{if(active)setFeedback("群组目录尚不可用；启动检查需要官方同步后的有效群组。");});
    return()=>{active=false;};
  },[request]);
  useEffect(()=>{setPreview(null);setIssues([]);setReady(false);},[version,script.scriptId]);
  const publish=async()=>{
    if(busy||!await flush()){setFeedback("草稿保存失败，已阻止发布，避免发布旧内容。");return;}
    setBusy(true);
    try{
      const draft=await request("scripts.get",{id:script.scriptId});
      if(!isScriptSnapshot(draft)||draft.archived)throw new Error("草稿不存在或已经归档");
      const result=await request("scripts.publish",{id:script.scriptId,expectedRevision:draft.revision});
      if(typeof result!=="object"||result===null||!("version" in result)||!Number.isInteger(result.version))
        throw new Error("发布结果格式错误");
      await refresh();setVersion(Number(result.version));
      setFeedback("已冻结发布版本 v"+String(result.version)+"；继续编辑草稿不会修改该版本。");
    }catch(e){setFeedback(e instanceof Error?e.message:"发布失败");}
    finally{setBusy(false);}
  };
  const showPreview=async()=>{
    if(busy||!version)return;setBusy(true);
    try{
      const r=await request("scripts.preview",{id:script.scriptId,version});
      if(!validPreview(r))throw new Error("预览数据无效");
      setPreview(r);setFeedback("只读发布快照，最早发送时间未计入网络耗时或平台慢速模式。");
    }catch(e){setFeedback(e instanceof Error?e.message:"预览失败");}
    finally{setBusy(false);}
  };
  const loadTarget=async(group:GroupRecord,selected:boolean)=>{
    setIssues([]);setReady(false);
    if(!selected){setTargets(a=>a.filter(t=>t.chatId!==group.chatId));return;}
    if(targets.length>=50){setFeedback("最多选择50个目标群/话题。");return;}
    setTargets(a=>[...a,{chatId:group.chatId}]);
    try{
      const r=await request("groups.members",{chatId:group.chatId});
      if(isMemberList(r))setMembers(v=>({...v,[group.chatId]:r}));
      if(group.isForum){
        const list=await request("groups.topics",{chatId:group.chatId,page:1,pageSize:50});
        if(isTopicPage(list))setTopics(v=>({...v,[group.chatId]:list.items}));
      }
    }catch{setFeedback("群成员或话题暂时无法读取；预检会阻止未经验证的目标。");}
  };
  const check=async()=>{
    if(busy||!version||!targets.length)return;
    setBusy(true);setReady(false);setIssues([]);
    try{
      const selected=Object.entries(choices).filter(([,v])=>v).map(([key,accountId])=>{
        const [chatId,topicId,roleKey]=key.split("|");
        return {chatId,...(topicId?{topicId}:{}),roleKey,accountId};
      });
      const result=await request("scripts.preflight",{id:script.scriptId,version,targets,choices:selected});
      if(typeof result!=="object"||result===null||!("issues" in result)||!Array.isArray(result.issues)||
         !("ready" in result)||typeof result.ready!=="boolean")throw new Error("预检结果格式异常");
      const reported=result.issues.filter((x:unknown):x is Issue=>typeof x==="object"&&x!==null&&
        "message" in x&&typeof x.message==="string"&&"code" in x&&typeof x.code==="string"&&
        "chatId" in x&&typeof x.chatId==="string");
      setIssues(reported);setReady(result.ready);
      setFeedback(result.ready?
        "模拟预检通过；当前版本不提供真正启动任务功能。":
        "发现 "+reported.length+" 项问题；必须全部处理后才能进入后续运行步骤。");
    }catch(e){setFeedback(e instanceof Error?e.message:"启动预检失败");}
    finally{setBusy(false);}
  };
  const selectedGroups=useMemo(()=>targets.map(t=>groups.find(g=>g.chatId===t.chatId)).filter((x):x is GroupRecord=>Boolean(x)),[targets,groups]);
  return <section className="script-timing-panel" aria-label="剧本发布与启动预检">
    <h3>发布快照 · 预览 · 启动前检查</h3>
    <p className="ui-preview-caption">发布后锁定剧本版本；可继续编辑草稿，但不会修改已发布的内容。此界面不发送任何 Telegram 消息。</p>
    <div className="ui-action-row">
      <ActionButton tone="success" disabled={busy} onClick={()=>void publish()}>发布当前草稿</ActionButton>
      <label className="ui-field">选择已发布版本
        <select aria-label="选择已发布版本" value={version} onChange={e=>setVersion(Number(e.target.value))}>
          {versions.length===0?<option value={0}>尚未发布</option>:versions.map(v=>
            <option key={v.version} value={v.version}>v{v.version} · 草稿修订 {v.sourceRevision}</option>)}
        </select>
      </label>
      <ActionButton disabled={busy||!version} onClick={()=>void showPreview()}>预览版本</ActionButton>
    </div>
    {preview?<div className="script-scroll" role="region" aria-label="已发布剧本时间线预览">
      <p className="ui-preview-caption">仅计算等待、输入与发送后等待的理论最小时长：{(preview.minimumDurationWithoutNetworkMs/1000).toFixed(2)}秒。真实回执与慢速模式不包含在内。</p>
      {preview.entries.map(item=><article key={item.stepKey} className="script-card">
        <strong>#{item.ordinal} · {item.roleLabel} {item.imageName?"· 图片："+item.imageName:""}</strong>
        <p style={{whiteSpace:"pre-wrap",overflowWrap:"anywhere"}}>{item.body}</p>
        <small>发送前等待 {(item.waitingMs/1000)}秒 → 正在输入 {(item.typingMs/1000)}秒 → 发送 → 回执后等待 {(item.postSendMs/1000)}秒；本条最早第 {(item.earliestSendFromStepStartMs/1000)} 秒进入发送阶段</small>
      </article>)}
    </div>:null}
    <h3>选择预检群组（最多50个；不启动任务）</h3>
    <div className="ui-action-row">
      <span>已选 {targets.length} 个</span>
      <ActionButton disabled={busy||!version||targets.length===0} onClick={()=>void check()}>检查启动条件</ActionButton>
    </div>
    <div className="script-scroll" aria-label="可用群组" style={{maxHeight:220,overflowY:"auto"}}>
      {groups.length===0?<p className="ui-preview-caption">暂无经官方验证的群组；请在后续完成真实登录与群同步。</p>:null}
      {groups.map(g=><label key={g.chatId} style={{display:"block",padding:6}}>
        <input type="checkbox" checked={targets.some(t=>t.chatId===g.chatId)} disabled={busy}
          onChange={e=>void loadTarget(g,e.target.checked)}/> {g.title} {g.isForum?"（话题群）":""}
      </label>)}
    </div>
    {selectedGroups.map(g=>{
      const target=targets.find(t=>t.chatId===g.chatId)!;
      const options=topics[g.chatId]??[];
      const accountList=members[g.chatId]??[];
      const used=new Map((preview?.entries??[]).map(x=>[x.roleKey,x.roleLabel]));
      return <div className="script-timing-panel" key={g.chatId}>
        <strong>{g.title}</strong>
        {g.isForum?<label className="ui-field">选择话题
          <select aria-label={"群话题 "+g.title} value={target.topicId??""}
            onChange={e=>{setTargets(a=>a.map(t=>t.chatId===g.chatId?{chatId:g.chatId,...(e.target.value?{topicId:e.target.value}:{})}:t));setIssues([]);}}>
            <option value="">请选择话题</option>
            {options.filter(t=>!t.isClosed&&!t.isHidden).map(t=><option key={t.topicId} value={t.topicId}>{t.title}</option>)}
          </select>
        </label>:null}
        {[...used].map(([roleKey,label])=>{
          const matches=accountList.filter(a=>norm(a.alias)===norm(label));
          if(matches.length<2)return null;
          const key=targetId(target)+"|"+roleKey;
          return <label className="ui-field" key={key}>同名备注“{label}”的账号选择
            <select aria-label={"指定账号 "+label} value={choices[key]??""}
              onChange={e=>setChoices(x=>({...x,[key]:e.target.value}))}>
              <option value="">需人工选择</option>
              {matches.map(a=><option key={a.accountId} value={a.accountId}>{a.alias} · {a.accountId.slice(0,8)} · {a.status}</option>)}
            </select>
          </label>;
        })}
      </div>;
    })}
    {issues.length>0?<div role="status" aria-label="所有启动问题">
      <strong>预检发现 {issues.length} 个问题：</strong>
      <ul>{issues.map((issue,i)=><li key={i}>{groups.find(g=>g.chatId===issue.chatId)?.title??issue.chatId}
        {issue.topicId?" / "+issue.topicId:""} · {issue.message}</li>)}</ul>
    </div>:null}
    <StatusPill tone={ready?"success":"warning"}>{ready?"只读预检通过（未创建任务）":"不具备自动启动权限"}</StatusPill>
    <p className="ui-preview-caption">{feedback}</p>
  </section>;
}
