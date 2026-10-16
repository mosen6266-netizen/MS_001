import {forwardRef,useCallback,useEffect,useImperativeHandle,useReducer,useRef,useState} from "react";
import {ActionButton,ConfirmDialog,StatusPill,TextField} from "./DesignSystem";
import {isBridgeResponse,isAccountPage,isScriptSnapshot,isScriptSummaries,makeAccountRequest,makeScriptRequest,
 type ScriptAction,type ScriptSnapshot,type ScriptSummary,type AccountPage,type UserPreferences} from "../../../../src/contracts/ipc";
import {editorReducer,newEditorState,normalizedLabel,keyboardAction,type ScriptDoc,type EditorState} from "../script-editor/state";

type WorkspaceHandle={flush:()=>Promise<boolean>};
function useLocalScriptHost(){
  const seq=useRef(0);
  const request=useCallback((action:ScriptAction,payload:Record<string,unknown>):Promise<unknown>=>{
    const host=window.chrome?.webview;
    if(!host)return Promise.reject(new Error("离线浏览器预览不能保存到 Windows 本地数据库"));
    const id="script-"+(++seq.current);
    return new Promise((resolve,reject)=>{
      let done=false;
      const finish=()=>{if(done)return false;done=true;clearTimeout(timer);host.removeEventListener("message",listener);return true;};
      const listener=({data}:{data:unknown})=>{
        if(!isBridgeResponse(data)||data.requestId!==id||!finish())return;
        if(data.ok)resolve(data.payload);
        else reject(new Error(typeof data.payload.message==="string"?data.payload.message:"本地剧本保存失败"));
      };
      host.addEventListener("message",listener);
      const timer=setTimeout(()=>{if(finish())reject(new Error("写入超时：先确认是否已保存，不要重复创建剧本"));},10000);
      try{host.postMessage(JSON.stringify(makeScriptRequest(action,id,payload)));}
      catch(e){if(finish())reject(e);}
    });
  },[]);
  return request;
}
async function readAliases():Promise<string[]>{
  const webview=window.chrome?.webview;
  if(!webview)return [];
  let sequence=0;const names=new Set<string>();
  for(let page=1;page<=6;page++){
    const id="script-alias-"+(++sequence);
    const data=await new Promise<unknown>((resolve,reject)=>{
      const listener=({data}:{data:unknown})=>{
        if(!isBridgeResponse(data)||data.requestId!==id)return;
        clearTimeout(timer);webview.removeEventListener("message",listener);
        if(data.ok)resolve(data.payload);else reject(new Error("账号备注目录读取失败"));
      };
      const timer=setTimeout(()=>{webview.removeEventListener("message",listener);reject(new Error("账号备注读取超时"));},8000);
      webview.addEventListener("message",listener);
      webview.postMessage(JSON.stringify(makeAccountRequest("accounts.list",id,
        {query:"",group:"",page,pageSize:50,archived:false})));
    });
    if(!isAccountPage(data))throw new Error("账号备注格式异常");
    for(const item of (data as AccountPage).items){
      const label=normalizedLabel(item.alias);
      if(label)names.add(label);
    }
    if(page*50>=data.total)break;
  }
  return [...names].sort((a,b)=>a.localeCompare(b,"zh-CN"));
}
const pageSize=20;
const ScriptWorkspace=forwardRef<WorkspaceHandle,{
  script:ScriptSnapshot;aliases:string[];save:(id:string,revision:number,doc:ScriptDoc)=>Promise<ScriptSnapshot>;
}>(({script,aliases,save},ref)=>{
  const [editor,dispatch]=useReducer(editorReducer,script.document,newEditorState);
  const [actor,setActor]=useState("");
  const [roleFilter,setRoleFilter]=useState("");
  const [messageFilter,setMessageFilter]=useState("");
  const [page,setPage]=useState(1);
  const [saveInfo,setSaveInfo]=useState("草稿已加载");
  const stateRef=useRef<EditorState>(editor);
  const storedRef=useRef(JSON.stringify(script.document));
  const revRef=useRef(script.revision);
  const savingRef=useRef(false);
  const savePromiseRef=useRef<Promise<boolean>|null>(null);
  const dragRef=useRef<string|null>(null);
  stateRef.current=editor;
  const flush=useCallback(async():Promise<boolean>=>{
    if(savePromiseRef.current)return savePromiseRef.current.then(async done=>{
      if(!done)return false;
      if(JSON.stringify(stateRef.current.present)!==storedRef.current)return flush();
      return true;
    });
    if(JSON.stringify(stateRef.current.present)===storedRef.current)return true;
    savingRef.current=true;setSaveInfo("正在保存…");
    const body=stateRef.current.present;
    const pending=save(script.scriptId,revRef.current,body).then(reply=>{
      revRef.current=reply.revision;storedRef.current=JSON.stringify(body);
      setSaveInfo(JSON.stringify(stateRef.current.present)===storedRef.current?"已自动保存":"还有修改待保存");
      return true;
    }).catch(e=>{
      setSaveInfo(e instanceof Error?"保存失败："+e.message:"保存失败，请检查数据库");
      return false;
    }).finally(()=>{savingRef.current=false;savePromiseRef.current=null;});
    savePromiseRef.current=pending;
    return pending;
  },[save,script.scriptId]);
  useImperativeHandle(ref,()=>({flush}),[flush]);
  useEffect(()=>{
    if(JSON.stringify(editor.present)===storedRef.current)return;
    setSaveInfo("有未保存修改");
    const timer=setTimeout(()=>{void flush();},700);
    return()=>clearTimeout(timer);
  },[editor.present,flush]);
  useEffect(()=>{
    const listener=(event:BeforeUnloadEvent)=>{
      if(JSON.stringify(stateRef.current.present)!==storedRef.current){
        event.preventDefault();
      }
    };
    window.addEventListener("beforeunload",listener);
    return()=>window.removeEventListener("beforeunload",listener);
  },[]);
  const keys=editor.present.roles.map(r=>r.roleKey);
  const aliasesAvailable=[...new Set([...aliases,...editor.present.roles.map(r=>r.roleLabel)])]
    .filter(x=>x.toLocaleLowerCase().includes(roleFilter.toLocaleLowerCase()));
  const canAdd=actor.length>0&&aliasesAvailable.includes(actor);
  const ensureRole=(label:string)=>{
    const normalized=normalizedLabel(label);
    const existing=editor.present.roles.find(r=>normalizedLabel(r.roleLabel)===normalized);
    if(existing)return existing.roleKey;
    if(!normalized||!aliases.includes(normalized))return null;
    const key=crypto.randomUUID();
    dispatch({type:"makeRole",roleKey:key,roleLabel:normalized});
    return key;
  };
  const add=()=>{
    const key=ensureRole(actor);
    if(key)dispatch({type:"add",roleKey:key,stepKey:crypto.randomUUID()});
  };
  const assign=(stepKey:string,label:string)=>{
    const key=ensureRole(label);
    if(key)dispatch({type:"choose",stepKey,roleKey:key});
  };
  const all=editor.present.steps.filter(x=>x.body.toLocaleLowerCase().includes(messageFilter.toLocaleLowerCase()));
  const visible=all.slice((page-1)*pageSize,page*pageSize);
  const pages=Math.max(1,Math.ceil(all.length/pageSize));
  const toggle=(id:string)=>dispatch({type:"select",stepKey:id,multiple:true});
  return <div className="script-workspace">
    <div className="script-toolbar">
      <TextField label="剧本名称" value={editor.present.title} onChange={v=>dispatch({type:"title",value:v.slice(0,128)})}/>
      <div className="script-save"><StatusPill tone={saveInfo.includes("失败")?"danger":undefined}>{saveInfo}</StatusPill>
        <ActionButton tone="success" onClick={()=>void flush()}>保存草稿</ActionButton></div>
    </div>
    <div className="script-toolbar">
      <TextField label="搜索本机账号备注（仅用于角色选择）" value={roleFilter} onChange={setRoleFilter}
        placeholder="输入部分备注筛选"/>
      <div className="ui-field"><label htmlFor="script-actor">发言账号（选择备注）</label>
        <select id="script-actor" aria-label="发言账号（选择备注）" value={actor} onChange={e=>setActor(e.target.value)}>
          <option value="">选择本机账号备注</option>
          {aliasesAvailable.map(label=><option key={label} value={label}>{label}</option>)}
        </select></div>
      <ActionButton tone="success" disabled={!canAdd||editor.present.steps.length>=1000} onClick={add}>添加消息</ActionButton>
    </div>
    <p className="ui-preview-caption">角色只存备注与角色键，不保存本机账号ID。这里展示本机已登记备注，是否真正授权及所在群权限会在启动前重新检查。</p>
    <div className="script-actions">
      <TextField label="搜索消息内容" value={messageFilter} onChange={v=>{setMessageFilter(v);setPage(1);}} placeholder="支持中文、英文"/>
      <ActionButton onClick={()=>dispatch({type:"undo"})} disabled={!editor.past.length}>撤销</ActionButton>
      <ActionButton onClick={()=>dispatch({type:"redo"})} disabled={!editor.future.length}>重做</ActionButton>
      <ActionButton disabled={!editor.selected.length||editor.present.steps.length+editor.selected.length>1000}
        onClick={()=>dispatch({type:"copy",newStepKeys:editor.present.steps.filter(x=>editor.selected.includes(x.stepKey)).map(()=>crypto.randomUUID())})}>复制选中</ActionButton>
      <ActionButton tone="danger" disabled={!editor.selected.length} onClick={()=>dispatch({type:"remove"})}>删除选中</ActionButton>
    </div>
    <div className="script-scroll" role="list" aria-label="剧本消息卡片列表"
      onKeyDown={e=>{
        const node=e.target;
        const isEditable=node instanceof HTMLElement&&(["INPUT","TEXTAREA","SELECT"].includes(node.tagName)||node.isContentEditable);
        const action=keyboardAction(e,isEditable);
        if(action){e.preventDefault();dispatch({type:action});}
      }}>
      {visible.length===0?<div className="ui-empty">暂无消息，先选择发言账号备注，再添加一条。</div>:null}
      {visible.map((step,index)=>{
        const role=editor.present.roles.find(r=>r.roleKey===step.roleKey);
        return <article className={"script-card"+(editor.selected.includes(step.stepKey)?" is-selected":"")}
          role="listitem" key={step.stepKey} draggable
          onDragStart={e=>{dragRef.current=step.stepKey;e.dataTransfer.effectAllowed="move";}}
          onDragOver={e=>e.preventDefault()}
          onDrop={e=>{e.preventDefault();if(dragRef.current)dispatch({type:"reorder",from:dragRef.current,to:step.stepKey});dragRef.current=null;}}
          onDragEnd={()=>{dragRef.current=null;}}>
          <div className="script-card-top">
            <label><input type="checkbox" checked={editor.selected.includes(step.stepKey)}
              onChange={()=>toggle(step.stepKey)}/> 选择</label>
            <span>第 {(page-1)*pageSize+index+1} 条 <span aria-hidden>↕</span> 可拖拽</span>
            <div className="ui-action-row">
              <ActionButton onClick={()=>dispatch({type:"move",id:step.stepKey,direction:-1})}>上移</ActionButton>
              <ActionButton onClick={()=>dispatch({type:"move",id:step.stepKey,direction:1})}>下移</ActionButton>
            </div>
          </div>
          <div className="ui-field">
            <label htmlFor={"speaker-"+step.stepKey}>发言账号备注</label>
            <select id={"speaker-"+step.stepKey} aria-label="发言账号备注" value={role?.roleLabel??""}
              onChange={e=>assign(step.stepKey,e.target.value)}>
              {[...new Set([...aliases,...editor.present.roles.map(x=>x.roleLabel)])].map(x=><option key={x} value={x}>{x}{!aliases.includes(x)?"（本机缺少）":""}</option>)}
            </select>
          </div>
          <label className="script-text-label" htmlFor={"body-"+step.stepKey}>消息内容</label>
          <textarea id={"body-"+step.stepKey} rows={3} maxLength={8192} value={step.body}
            onChange={e=>dispatch({type:"body",stepKey:step.stepKey,body:e.target.value})}
            placeholder="输入文字、换行、emoji 或英文内容"/>
          <div className="script-foot">
            <small>发送前等待 {step.preSendDelayMs/1000}s → 正在输入 {step.typingDurationMs/1000}s → 发送 → 发送后等待 {step.postSendDelayMs/1000}s</small>
            <small>{step.body.length} 字符</small>
          </div>
        </article>;
      })}
    </div>
    <div className="ui-action-row">
      <ActionButton disabled={page<=1} onClick={()=>setPage(v=>Math.max(1,v-1))}>上一页</ActionButton>
      <span className="ui-pagination-label">{page}/{pages} · {all.length} 条</span>
      <ActionButton disabled={page>=pages} onClick={()=>setPage(v=>v+1)}>下一页</ActionButton>
    </div>
    <p className="ui-preview-caption">当前仅支持文字草稿编辑；三段时间批量编辑和图片素材将在第14步实现。拖拽仅改变草稿顺序，不会发送 Telegram 消息。</p>
    <span className="sr-only">{keys.length} 个本地逻辑角色</span>
  </div>;
});
ScriptWorkspace.displayName="ScriptWorkspace";

export function ScriptEditorPanel({preferences}:{preferences:UserPreferences}){
  const request=useLocalScriptHost();
  const workspaceRef=useRef<WorkspaceHandle>(null);
  const [all,setAll]=useState<ScriptSummary[]>([]);
  const [script,setScript]=useState<ScriptSnapshot|null>(null);
  const [aliases,setAliases]=useState<string[]>([]);
  const [title,setTitle]=useState("新剧本");
  const [query,setQuery]=useState("");
  const [archived,setArchived]=useState(false);
  const [message,setMessage]=useState("正在读取本地剧本…");
  const [busy,setBusy]=useState(false);
  const [confirm,setConfirm]=useState<ScriptSummary|null>(null);
  const selectedRef=useRef<ScriptSnapshot|null>(null);
  selectedRef.current=script;
  const list=useCallback(async(search=query,isArchived=archived)=>{
    try{
      const answer=await request("scripts.list",{query:search,archived:isArchived});
      if(typeof answer!=="object"||answer===null||!("items" in answer)||!isScriptSummaries(answer.items))
        throw new Error("本地剧本响应格式错误");
      setAll(answer.items);
      setMessage(answer.items.length?"草稿已从本地数据库读取":"尚无剧本，点击新建开始");
    }catch(e){setMessage(e instanceof Error?e.message:"读取本地草稿失败");}
  },[request,query,archived]);
  useEffect(()=>{if(!window.chrome?.webview){setMessage("仅展示离线界面：Windows 本地剧本保存尚不可用");return;}
    void list();void readAliases().then(setAliases).catch(()=>setMessage("本机备注读取失败；请检查账号注册表"));
  },[list]);
  const activate=async(id:string)=>{
    if(busy)return;
    if(script?.scriptId===id)return;
    if(workspaceRef.current && !(await workspaceRef.current.flush())){
      setMessage("上一个剧本未保存成功，已阻止切换，以免丢失修改。");return;
    }
    setBusy(true);
    try{
      const answer=await request("scripts.get",{id});
      if(!isScriptSnapshot(answer))throw new Error("剧本数据格式错误");
      setScript(answer);setMessage("已打开剧本："+answer.document.title);
    }catch(e){setMessage(e instanceof Error?e.message:"打开失败");}
    finally{setBusy(false);}
  };
  const create=async()=>{
    if(busy||!window.chrome?.webview)return;
    if(workspaceRef.current&&!(await workspaceRef.current.flush())){setMessage("先保存当前剧本");return;}
    setBusy(true);
    try{
      const result=await request("scripts.create",{title:title.trim().slice(0,128)});
      if(!isScriptSnapshot(result))throw new Error("创建结果格式错误");
      setScript(result);await list();
    }catch(e){setMessage(e instanceof Error?e.message:"创建失败");}
    finally{setBusy(false);}
  };
  const save=useCallback(async(id:string,revision:number,doc:ScriptDoc)=>{
    const result=await request("scripts.save",{id,expectedRevision:revision,document:doc});
    if(!isScriptSnapshot(result))throw new Error("保存结果无效");
    return result;
  },[request]);
  const duplicate=async()=>{
    if(!script||busy)return;
    if(workspaceRef.current&&!(await workspaceRef.current.flush())){setMessage("先保存当前剧本");return;}
    setBusy(true);
    try{
      const r=await request("scripts.duplicate",{id:script.scriptId});
      if(!isScriptSnapshot(r))throw new Error("复制结果无效");
      setScript(r);await list();
    }catch(e){setMessage(e instanceof Error?e.message:"复制失败");}finally{setBusy(false);}
  };
  const archive=async()=>{
    if(!confirm)return;
    if(script?.scriptId===confirm.scriptId&&workspaceRef.current&&!(await workspaceRef.current.flush())){
      setMessage("归档前保存失败，已阻止归档");return;
    }
    setBusy(true);
    try{
      // The current revision may have advanced after a draft save; re-read it before an archive.
      const fresh=await request("scripts.get",{id:confirm.scriptId});
      if(!isScriptSnapshot(fresh))throw new Error("剧本读取失败");
      await request("scripts.archive",{id:confirm.scriptId,expectedRevision:fresh.revision,confirmation:"归档剧本"});
      if(script?.scriptId===confirm.scriptId)setScript(null);
      setConfirm(null);await list();setMessage("已归档，内容保留，可恢复");
    }catch(e){setMessage(e instanceof Error?e.message:"归档失败");}finally{setBusy(false);}
  };
  const restore=async(item:ScriptSummary)=>{
    setBusy(true);
    try{
      await request("scripts.restore",{id:item.scriptId,expectedRevision:item.revision});
      await list();setMessage("剧本已恢复为未发布草稿");
    }catch(e){setMessage(e instanceof Error?e.message:"恢复失败");}finally{setBusy(false);}
  };
  const filtered=all.filter(x=>x.title.toLocaleLowerCase().includes(query.toLocaleLowerCase()));
  return <div className="script-editor-layout" aria-label="可视化剧本编辑器">
    <aside className="script-directory">
      <h2 id="panel-title">我的剧本</h2>
      <StatusPill tone="warning">仅本地草稿 · 不会发送消息</StatusPill>
      <div className="script-create">
        <TextField label="新建剧本名称" value={title} onChange={setTitle}/>
        <ActionButton tone="success" onClick={()=>void create()} disabled={busy||!title.trim()}>新建剧本</ActionButton>
      </div>
      <TextField label="搜索剧本" value={query} onChange={setQuery}/>
      <div className="ui-action-row">
        <ActionButton tone={!archived?"success":"quiet"} onClick={()=>{setArchived(false);void list(query,false);}}>草稿</ActionButton>
        <ActionButton tone={archived?"success":"quiet"} onClick={()=>{setArchived(true);void list(query,true);}}>已归档</ActionButton>
      </div>
      <ul className="script-directory-list">
        {filtered.map(item=><li key={item.scriptId}>
          <button type="button" className={"script-item"+(script?.scriptId===item.scriptId?" is-active":"")}
            onClick={()=>void activate(item.scriptId)}>
            <strong>{item.title}</strong><small>{item.stepCount} 条消息 · 草稿 v{item.revision}</small>
          </button>
          {archived?<ActionButton tone="success" onClick={()=>void restore(item)} disabled={busy}>恢复</ActionButton>:
            <ActionButton tone="danger" onClick={()=>setConfirm(item)} disabled={busy}>归档</ActionButton>}
        </li>)}
      </ul>
      <small>{message}</small>
    </aside>
    <div className="script-editor-main">
      {script&&!script.archived?
        <>
          <div className="script-main-top">
            <h2>剧本消息编辑</h2>
            <ActionButton disabled={busy} onClick={()=>void duplicate()}>复制整个剧本</ActionButton>
          </div>
          <ScriptWorkspace key={script.scriptId} ref={workspaceRef} script={script} aliases={aliases} save={save}/>
        </>:<div className="ui-demo-box">
          <h2>创建或选择剧本</h2><p>在左侧新建剧本，然后从本机账号备注下拉选择发言角色、添加消息和调整顺序。</p>
          <p>浏览器预览不连接真实账号。剧本始终是草稿，发布与运行在后续步骤实现。</p>
          <small>每条消息时间顺序：发送前等待 → 正在输入 → 发送 → 发送后等待</small>
        </div>}
    </div>
    <ConfirmDialog open={confirm!==null} title="确认归档剧本"
      description={confirm?"将“"+confirm.title+"”移入已归档列表，内容不会永久删除。若关联运行任务则会被拒绝。":""}
      confirmLabel="确认归档" onConfirm={()=>void archive()}
      onCancel={()=>setConfirm(null)} busy={busy}/>
    <small className="sr-only">{preferences.pageSize} 条账号索引分页设置</small>
  </div>;
}
