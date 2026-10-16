import {useEffect,useState} from "react";
import {ActionButton,SelectField,StatusPill,ToastViewport,type ToastInfo} from "./DesignSystem";
import {useActionLock} from "../hooks/useActionLock";
import type {PreferencesSnapshot,UserPreferences} from "../../../../src/contracts/ipc";

export function PreferencesPanel({snapshot,mode,save,reload}:{
  snapshot:PreferencesSnapshot;mode:"loading"|"ready"|"preview"|"error";
  save:(preferences:UserPreferences)=>Promise<PreferencesSnapshot>;
  reload:()=>void;
}) {
  const [draft,setDraft]=useState(snapshot.preferences);
  const [toast,setToast]=useState<ToastInfo|null>(null);
  const {busy,run}=useActionLock();
  useEffect(()=>setDraft(snapshot.preferences),[snapshot]);
  const changed=JSON.stringify(draft)!==JSON.stringify(snapshot.preferences);
  const choose=<T extends keyof UserPreferences>(key:T,value:UserPreferences[T])=>setDraft(previous=>({...previous,[key]:value}));
  const handleSave=async()=>{
    if(!changed||mode!=="ready"||busy)return;
    try{
      const result=await run(()=>save(draft));
      if(!result)return;
      setToast({id:Date.now(),tone:"success",message:"设置已保存到本地数据库。"});
    }catch(e){
      setToast({id:Date.now(),tone:"danger",message:e instanceof Error?e.message:"保存失败"});
    }
  };
  return <div className="ui-demo-section">
    <div className="ui-demo-box">
      <h2 id="panel-title">全局设置</h2>
      <p className="ui-preview-caption">设置保存于本机 SQLite 数据库，与账号登录会话分开。修改不会触发页面刷新。</p>
      {mode==="preview"?<StatusPill tone="warning">浏览器预览：不能保存到本机</StatusPill>:
        mode==="loading"?<StatusPill>正在读取本地设置…</StatusPill>:
        mode==="error"?<StatusPill tone="danger">读取失败，请检查本地数据库</StatusPill>:
        <StatusPill tone="success">本地设置已连接 · 版本 {snapshot.revision}</StatusPill>}
      <div className="ui-form-row" style={{marginTop:16}}>
        <SelectField label="列表密度" value={draft.density} disabled={busy||mode==="loading"}
          onChange={v=>choose("density",v as UserPreferences["density"])}
          options={[{value:"comfortable",label:"舒适（默认）"},{value:"compact",label:"紧凑"}]}
          hint="用于后续账号、群组与历史记录页面布局。"/>
        <SelectField label="搜索防抖时间" value={String(draft.searchDebounceMs)} disabled={busy||mode==="loading"}
          onChange={v=>choose("searchDebounceMs",Number(v))}
          options={[{value:"150",label:"150 毫秒"},{value:"200",label:"200 毫秒"},{value:"250",label:"250 毫秒"}]}
          hint="停止输入一小段时间后才筛选，不逐字重复刷新列表。"/>
        <SelectField label="每页显示条数" value={String(draft.pageSize)} disabled={busy||mode==="loading"}
          onChange={v=>choose("pageSize",Number(v) as 10|20|50)}
          options={[{value:"10",label:"10 条"},{value:"20",label:"20 条"},{value:"50",label:"50 条"}]}
          hint="大列表限制 DOM 行数，避免一次渲染 300 项。"/>
        <div className="ui-field">
          <label htmlFor="setting-desktop-notices">桌面通知偏好</label>
          <div className="ui-toggle-row">
            <input id="setting-desktop-notices" type="checkbox" disabled={busy||mode==="loading"}
              checked={draft.desktopNotifications} onChange={e=>choose("desktopNotifications",e.target.checked)}/>
            <span>启用通知偏好（Windows 实际通知将在后续步骤接入）</span>
          </div>
        </div>
      </div>
      <div className="ui-action-row" style={{marginTop:20}}>
        <ActionButton tone="success" busy={busy} disabled={!changed||mode!=="ready"} onClick={handleSave}>保存设置</ActionButton>
        <ActionButton tone="quiet" disabled={busy||mode==="loading"} onClick={()=>setDraft(snapshot.preferences)}>撤销更改</ActionButton>
        <ActionButton tone="ghost" disabled={busy||mode==="preview"} onClick={reload}>重新读取</ActionButton>
      </div>
      <p className="ui-preview-caption" style={{marginTop:14}}>若出现保存结果不确定或版本冲突，请先重新读取，不会自动重复写入。</p>
    </div>
    <ToastViewport toast={toast} dismiss={()=>setToast(null)}/>
  </div>;
}
