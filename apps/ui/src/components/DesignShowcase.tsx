import { useCallback, useRef, useState } from "react";
import { ActionButton, ConfirmDialog, SelectField, StatusPill, TextField, ToastViewport, type ToastInfo, type Tone } from "./DesignSystem";

const choices = [
  { value: "5", label: "5 秒" }, { value: "10", label: "10 秒" },
  { value: "30", label: "30 秒" }, { value: "60", label: "60 秒" },
] as const;

/** UI-only component gallery, not real app settings or Telegram messaging. */
export function DesignShowcase() {
  const [name, setName] = useState("客服A");
  const [delay, setDelay] = useState("30");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [toast, setToast] = useState<ToastInfo | null>(null);
  const nextToastId = useRef(1);
  const dismiss = useCallback(() => setToast(null), []);
  const cancelDialog = useCallback(() => setDialogOpen(false), []);
  const notify = (tone: Tone, message: string) => setToast({ id: nextToastId.current++, tone, message });
  const confirm = () => {
    setDialogOpen(false);
    notify("success", "已确认交互演示。实际保存功能会在后续步骤接入。");
  };
  return <div className="ui-demo-section">
    <div className="ui-demo-box">
      <h2 id="panel-title">界面组件规范预览</h2>
      <p className="ui-preview-caption">以下用于测试正式版的输入框、下拉菜单、按钮与弹窗样式。不会保存真实数据或发送消息。</p>
      <div className="ui-form-row">
        <TextField label="账号备注（样式演示）" value={name} onChange={setName}
          hint="此处只预览界面，真实账号会在后续步骤接入。" placeholder="输入备注" />
        <SelectField label="发送前等待（样式演示）" value={delay} onChange={setDelay} options={choices}
          hint="正式计时顺序：先等待，再显示正在输入，然后发送。" />
      </div>
    </div>
    <div className="ui-demo-box">
      <h3>状态与提示</h3>
      <div className="ui-action-row">
        <StatusPill tone="success">状态正常</StatusPill><StatusPill tone="warning">需要处理</StatusPill>
        <StatusPill tone="danger">操作失败</StatusPill><StatusPill>尚未接入</StatusPill>
      </div>
      <p className="ui-preview-caption">提示气泡一次只展示一条，点击关闭，不叠加弹窗。</p>
      <div className="ui-action-row">
        <ActionButton tone="quiet" onClick={() => notify("success", "绿色提示演示：操作已被确认。")}>绿色提示演示</ActionButton>
        <ActionButton tone="quiet" onClick={() => notify("danger", "红色提示演示：请检查输入内容。")}>红色提示演示</ActionButton>
        <ActionButton tone="quiet" onClick={() => notify("warning", "黄色提示演示：请先完成必要设置。")}>警告提示演示</ActionButton>
      </div>
    </div>
    <div className="ui-demo-box">
      <h3>按钮与确认弹窗</h3>
      <p className="ui-preview-caption">固定顺序：左侧绿色确认，右侧红色取消；Esc 取消、Tab 键在弹窗内部切换。</p>
      <div className="ui-action-row">
        <ActionButton tone="success" onClick={() => setDialogOpen(true)}>查看确认弹窗</ActionButton>
        <ActionButton tone="danger" onClick={() => notify("danger", "这是红色操作按钮的视觉演示。")}>红色操作演示</ActionButton>
        <ActionButton disabled>禁用状态</ActionButton>
        <ActionButton busy>处理中</ActionButton>
      </div>
    </div>
    <ConfirmDialog open={dialogOpen} title="确认交互演示" description="此弹窗仅用于验证按钮顺序、颜色、键盘焦点及取消行为，不执行实际操作。"
      onConfirm={confirm} onCancel={cancelDialog} confirmLabel="确认" cancelLabel="取消" />
    <ToastViewport toast={toast} dismiss={dismiss} />
  </div>;
}
