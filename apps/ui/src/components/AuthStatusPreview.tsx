import { useState } from "react";
import { ActionButton, SelectField, StatusPill } from "./DesignSystem";

/** Offline design preview. Real login commands stay disabled until a verified tdjson DLL
 * and protected per-account credentials are attached in the later account steps. */
export const AUTH_UI_STATES = [
  { id:"waitParameters", title:"配置连接", guide:"需要配置本机 Telegram API 参数与受保护的会话目录。" },
  { id:"waitPhone", title:"选择登录方式", guide:"使用本人的手机号接收官方验证码，或通过已登录手机扫描二维码。" },
  { id:"waitQr", title:"等待手机扫码确认", guide:"请用本人已登录的 Telegram 手机客户端确认；二维码链接由 TDLib 自动更新。" },
  { id:"waitCode", title:"输入登录验证码", guide:"请只在本机验证界面输入 Telegram 官方验证码，切勿提供给他人。" },
  { id:"waitEmail", title:"验证电子邮箱", guide:"Telegram 当前要求先填写本人能够访问的邮箱。" },
  { id:"waitEmailCode", title:"输入邮箱验证码", guide:"请输入官方发至邮箱的验证码。" },
  { id:"waitPassword", title:"两步验证密码", guide:"仅在本机输入本人设置的 Telegram 二次验证密码。" },
  { id:"waitRegistration", title:"注册与条款确认", guide:"必须本人主动填写姓名并明确同意 Telegram 服务条款。" },
  { id:"premium", title:"需官方处理", guide:"Telegram 要求先完成 Premium 购买或联系客服；本程序不会绕过。" },
  { id:"ready", title:"授权成功", guide:"只有收到官方授权成功更新，才会显示已登录；仍须核实群权限。" },
  { id:"loggingOut", title:"正在退出", guide:"正在等待 Telegram 完成退出。" },
  { id:"closing", title:"正在关闭", guide:"正在等待 TDLib 安全关闭本地客户端。" },
  { id:"closed", title:"已关闭", guide:"本地客户端已关闭；重新登录必须重新建立受控会话。" },
  { id:"unsupported", title:"当前版本不支持此状态", guide:"请更新软件或联系管理员，不会自行尝试绕过验证。" }
] as const;

export function AuthStatusPreview() {
  const [selected,setSelected]=useState<string>("waitPhone");
  const state=AUTH_UI_STATES.find(s=>s.id===selected)??AUTH_UI_STATES[0];
  return <section className="ui-demo-box" aria-label="登录状态示例" style={{marginBottom:18}}>
    <h2>登录状态与中文提示</h2>
    <StatusPill tone="warning">纯界面模拟 · 未连接 Telegram</StatusPill>
    <p className="ui-preview-caption" style={{marginTop:10}}>
      查看各类验证状态的中文显示规则。目前无法扫码或提交验证码；真正登录入口需在原生客户端与账号管理步骤通过验收后启用。
    </p>
    <SelectField label="预览官方授权状态" value={selected} onChange={setSelected}
      options={AUTH_UI_STATES.map(x=>({value:x.id,label:x.title}))}/>
    <div className="ui-auth-state" aria-live="polite">
      <h3>{state.title}</h3><p>{state.guide}</p>
      {selected==="waitQr"?<p>二维码：等待 Telegram 官方返回临时链接，本预览不生成可扫描的二维码。</p>:null}
    </div>
    <div className="ui-action-row" style={{marginTop:12}}>
      <ActionButton tone="success" disabled>提交登录信息（待接入）</ActionButton>
      <ActionButton tone="danger" disabled>取消登录（待接入）</ActionButton>
    </div>
  </section>;
}
