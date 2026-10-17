import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import brandIcon from "../../../assets/branding/icon.svg?url";
import { DesignShowcase } from "./components/DesignShowcase";
import { PreferencesPanel } from "./components/PreferencesPanel";
import { MockAccountIndex } from "./components/MockAccountIndex";
import { AccountRegistryPanel } from "./components/AccountRegistryPanel";
import { GroupCatalogPanel } from "./components/GroupCatalogPanel";
import { ScriptEditorPanel } from "./components/ScriptEditorPanel";
import { AuthStatusPreview } from "./components/AuthStatusPreview";
import { TelegramLivePanel } from "./components/TelegramLivePanel";
import { DiagnosticsPanel } from "./components/DiagnosticsPanel";
import { TelegramApiConfigurationPanel } from "./components/TelegramApiConfigurationPanel";
import { RuntimeDashboardPanel } from "./components/RuntimeDashboardPanel";
import { usePreferences } from "./hooks/usePreferences";
import { isBridgeResponse, makeRequest } from "../../../src/contracts/ipc";
import { initialShellState, SHELL_ROUTES, shortcutRoute, transitionShellRoute, type ShellRoute } from "../../../src/web/routes";
declare global {
  interface Window {
    chrome?: { webview?: {
      postMessage(message: string): void;
      addEventListener(type: "message", cb: (event: { data: unknown }) => void): void;
      removeEventListener(type: "message", cb: (event: { data: unknown }) => void): void;
    } };
  }
}
export function App() {
  const [shell, setShell] = useState(initialShellState);
  const [bridgeStatus, setBridgeStatus] = useState("等待本地界面连接");
  const viewportRef = useRef<HTMLElement | null>(null);
  const preferences = usePreferences();
  const active = SHELL_ROUTES.find(item => item.id === shell.active) ?? SHELL_ROUTES[0];
  const navigate = useCallback((target: ShellRoute) => {
    setShell(prev => transitionShellRoute(prev, target, viewportRef.current?.scrollTop ?? 0));
  }, []);
  useLayoutEffect(() => {
    if (viewportRef.current) viewportRef.current.scrollTop = shell.scroll[shell.active];
  }, [shell.active, shell.scroll]);
  useEffect(() => {
    const keyHandler = (event: KeyboardEvent) => {
      if (event.repeat) return;
      const route = shortcutRoute(event.altKey, event.ctrlKey, event.metaKey, event.key);
      if (!route) return;
      const node = event.target;
      if (node instanceof HTMLElement && (node.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(node.tagName))) return;
      event.preventDefault();
      navigate(route);
    };
    window.addEventListener("keydown", keyHandler);
    return () => window.removeEventListener("keydown", keyHandler);
  }, [navigate]);
  useEffect(() => {
    const webview = window.chrome?.webview;
    if (!webview) { setBridgeStatus("浏览器预览模式"); return; }
    let mounted = true;
    const handler = ({ data }: { data: unknown }) => {
      if (!mounted || !isBridgeResponse(data) || data.requestId !== "startup-check") return;
      setBridgeStatus(data.ok ? "桌面连接正常" : "桌面连接异常");
    };
    webview.addEventListener("message", handler);
    webview.postMessage(JSON.stringify(makeRequest("system.ping", "startup-check")));
    return () => { mounted = false; webview.removeEventListener("message", handler); };
  }, []);
  return <div className="shell">
    <aside className="shell-nav" aria-label="主导航">
      <div className="shell-brand"><span className="brand-mark" aria-hidden="true"><img alt="" src={brandIcon} /></span><div>MOSEN <small>GROUP SCHEDULER</small></div></div>
      <nav className="shell-navigation" aria-label="页面">
        {SHELL_ROUTES.map(page =>
          <button key={page.id} type="button" className={"nav-action" + (shell.active === page.id ? " is-active" : "")}
            aria-current={shell.active === page.id ? "page" : undefined} title={page.label + " · " + page.shortcut}
            onClick={() => navigate(page.id)}>
            <span className="nav-label">{page.label}</span>
            <span className="nav-shortcut" aria-hidden="true">{page.shortcut}</span>
          </button>)}
      </nav>
      <div className="shell-nav-foot"><span className="safe-indicator" aria-hidden="true" />Telegram 真实登录 · 未授权不发送</div>
    </aside>
    <main className="shell-main" ref={viewportRef} id="main-content" tabIndex={-1} aria-label="当前页面内容">
      <header className="shell-header">
        <div><h1>{active.label}</h1><p>{active.description}</p></div>
        <span className="shell-connection" role="status">{bridgeStatus}</span>
      </header>
      <section className="page-panel" aria-labelledby="panel-title">
        <div style={{display:shell.active==="scripts"?"block":"none"}}>
          <ScriptEditorPanel preferences={preferences.snapshot.preferences}/>
        </div>
        {shell.active==="scripts"?null:
        shell.active === "overview" ?
          <div className="overview-grid">
            <article className="summary-tile"><h2>已授权账号</h2><strong>请连接</strong><p>在账号管理中使用 Telegram 官方授权登录</p></article>
            <article className="summary-tile"><h2>运行任务</h2><strong>0</strong><p>目前不执行群消息发送</p></article>
            <article className="summary-tile"><h2>工程状态</h2><strong>TDLib 原生接入</strong><p>真实账号登录与手动群组发送；剧本运行仍单独受控</p></article>
            <article className="notice-tile"><h2 id="panel-title">当前开发范围</h2><p>已提供真实 TDLib 登录、群组同步和单次人工确认发送；未授权账号、未知回执或批量剧本不会自动发送。</p></article>
          </div> :
          shell.active === "running" ? <RuntimeDashboardPanel /> :
          shell.active === "settings" ? <><PreferencesPanel {...preferences} /><TelegramApiConfigurationPanel /><DiagnosticsPanel /><DesignShowcase /></> :
          shell.active === "groups" ? <><TelegramLivePanel view="groups"/><GroupCatalogPanel preferences={preferences.snapshot.preferences}/></> :
          shell.active === "accounts" ? <>{window.chrome?.webview?<TelegramLivePanel/>:<AuthStatusPreview/>}{window.chrome?.webview ? <AccountRegistryPanel preferences={preferences.snapshot.preferences}/> : <MockAccountIndex preferences={preferences.snapshot.preferences} />}</> :
          <div className="blank-panel">
            <h2 id="panel-title">{active.label}</h2>
            <p>{active.description}</p>
            <span className="muted-note">本页面业务功能尚未实现，目前不会连接 Telegram 或发送消息。</span>
          </div>}
      </section>
      <footer className="shell-footer">MOSEN Group Scheduler · Windows Telegram 集成测试版 · Alt + 1–6</footer>
    </main>
  </div>;
}
