import { useEffect, useRef, useState } from "react";
import { ActionButton, StatusPill } from "./DesignSystem";
import { isBridgeResponse, makeRequest } from "../../../../src/contracts/ipc";
type DiagnosticsState="preview"|"loading"|"ready"|"error";
type Report={schemaVersion:number;appState:string;uptimeSeconds:number;events:number;warnings:number;
  failures:number;workingSetMiB:number;currentThreads:number;openHandles:number;windowsHandlesAvailable:boolean};
function isReport(value:unknown):value is Report {
  if(typeof value!=="object"||value===null)return false;
  const v=value as Record<string,unknown>;
  return v.schemaVersion===1 && ["initializing","ready","degraded","closing"].includes(String(v.appState)) &&
    ["uptimeSeconds","events","warnings","failures","workingSetMiB","currentThreads","openHandles"]
      .every(key=>Number.isSafeInteger(v[key])&&(v[key] as number)>=0) &&
    typeof v.windowsHandlesAvailable==="boolean";
}
export function DiagnosticsPanel() {
  const [state,setState]=useState<DiagnosticsState>("preview");
  const [report,setReport]=useState<Report|null>(null);
  const sequence=useRef(0);
  const request=()=>{
    if(!window.chrome?.webview){setState("preview");return;}
    setState("loading");
    const id="diag-"+(++sequence.current);
    window.chrome.webview.postMessage(JSON.stringify(makeRequest("diagnostics.get",id)));
  };
  useEffect(()=>{
    const host=window.chrome?.webview;
    if(!host){setState("preview");return;}
    let timeout:ReturnType<typeof setTimeout>|null=null;
    const handler=({data}:{data:unknown})=>{
      if(!isBridgeResponse(data)||data.type!=="diagnostics.get.result")return;
      if(data.ok&&isReport(data.payload)){setReport(data.payload);setState("ready");}
      else setState("error");
      if(timeout!==null)clearTimeout(timeout);
    };
    host.addEventListener("message",handler);
    setState("loading");
    host.postMessage(JSON.stringify(makeRequest("diagnostics.get","diag-0")));
    timeout=setTimeout(()=>setState(s=>s==="loading"?"error":s),10000);
    return ()=>{if(timeout!==null)clearTimeout(timeout);host.removeEventListener("message",handler);};
  },[]);
  return <section className="ui-demo-box" aria-label="本地运行诊断">
    <h2>本地运行诊断</h2>
    <p className="ui-preview-caption">仅显示运行时间、资源计数及事件数量。没有手机号、账号详情、聊天内容或异常堆栈。</p>
    {state==="preview"?<StatusPill tone="warning">浏览器预览 · 无桌面诊断数据</StatusPill>:
      state==="loading"?<StatusPill>正在读取诊断…</StatusPill>:
      state==="error"?<StatusPill tone="danger">诊断暂不可用</StatusPill>:
      <StatusPill tone="success">诊断服务正常 · {report?.appState}</StatusPill>}
    {report?<dl className="ui-health-grid">
      <div><dt>运行秒数</dt><dd>{report.uptimeSeconds}</dd></div>
      <div><dt>内存（MiB）</dt><dd>{report.workingSetMiB}</dd></div>
      <div><dt>线程数</dt><dd>{report.currentThreads}</dd></div>
      <div><dt>安全事件数</dt><dd>{report.events}</dd></div>
      <div><dt>警告事件</dt><dd>{report.warnings}</dd></div>
      <div><dt>错误事件</dt><dd>{report.failures}</dd></div>
    </dl>:null}
    <div className="ui-action-row" style={{marginTop:14}}>
      <ActionButton disabled={state==="loading"||state==="preview"} onClick={request}>刷新诊断</ActionButton>
    </div>
  </section>;
}
