import { useEffect, useState } from "react";
import { isBridgeResponse, makeRequest } from "../../../src/contracts/ipc";

type Page = "overview" | "accounts" | "groups" | "scripts" | "running" | "settings";
const pages: { id: Page; label: string; desc: string }[] = [
 { id: "overview", label: "工作台", desc: "项目运行状态概览" },
 { id: "accounts", label: "账号管理", desc: "此模块将在第 09–12 步接入授权账号" },
 { id: "groups", label: "群组与话题", desc: "此模块将在第 12 步接入群组列表" },
 { id: "scripts", label: "剧本编辑", desc: "此模块将在第 13–16 步实现" },
 { id: "running", label: "运行中心", desc: "此模块将在第 17–23 步实现" },
 { id: "settings", label: "设置", desc: "本地设置与诊断仍在开发中" }
];

declare global {
 interface Window {
  chrome?: { webview?: { postMessage(message: string): void; addEventListener(type: "message", cb: (event: { data: unknown }) => void): void; } };
 }
}

export function App() {
 const [active, setActive] = useState<Page>("overview");
 const [bridgeStatus, setBridgeStatus] = useState("待连接本地桌面宿主");
 const current = pages.find(page => page.id === active) ?? pages[0];
 useEffect(() => {
  const webview = window.chrome?.webview;
  if (!webview) { setBridgeStatus("浏览器预览（无桌面宿主）"); return; }
  webview.addEventListener("message", ({ data }) => {
   if (!isBridgeResponse(data) || data.requestId !== "startup-check") return;
   setBridgeStatus(data.ok ? "本地宿主连接正常" : "本地宿主响应异常");
  });
  webview.postMessage(JSON.stringify(makeRequest("system.ping", "startup-check")));
 }, []);
 return <div className="layout">
  <aside className="sidebar" aria-label="主导航">
   <div className="brand">MOSEN <span>GROUP SCHEDULER</span></div>
   <nav>{pages.map(page => <button type="button" key={page.id} className={"nav-item " + (active === page.id ? "selected" : "")} onClick={() => setActive(page.id)} aria-current={active === page.id ? "page" : undefined}>{page.label}</button>)}</nav>
   <div className="sidebar-footer">V0.1 工程骨架<br/>离线技术原型 · 尚未连接 Telegram</div>
  </aside>
  <main className="workspace">
   <header className="toolbar"><div><h1>{current.label}</h1><p>{current.desc}</p></div><span className="status">{bridgeStatus}</span></header>
   {active === "overview" ? <section className="grid">
     <div className="stat"><h2>账号目录</h2><strong>未接入</strong><small>不会读取任何 Telegram 会话</small></div>
     <div className="stat"><h2>群任务</h2><strong>未运行</strong><small>未启用发送功能</small></div>
     <div className="stat"><h2>技术状态</h2><strong>Step 03</strong><small>Windows 宿主 / WebView2 / React UI</small></div>
     <div className="notice"><h2>工程基础阶段</h2><p>这是一套可编译的桌面 UI 基础工程。账户登录、剧本、卡密和任务调度将在后续阶段逐步开发并经过自动化测试，未实现的功能不会显示为可运行。</p></div>
    </section> : <section className="placeholder"><h2>{current.label}</h2><p>{current.desc}</p><span>开发步骤尚未完成；目前禁止实际发送消息。</span></section>}
  </main>
 </div>;
}
