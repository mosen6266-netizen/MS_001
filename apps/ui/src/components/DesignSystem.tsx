import { useEffect, useId, useRef, type ReactNode, type ButtonHTMLAttributes } from "react";

export type Tone = "success" | "danger" | "warning";
export type ButtonTone = "success" | "danger" | "quiet" | "ghost";

export function ActionButton({ tone = "quiet", busy = false, children, disabled, type = "button", ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { tone?: ButtonTone; busy?: boolean }) {
  return <button {...rest} type={type} className={"ui-button ui-button--" + tone} disabled={disabled || busy} aria-busy={busy}>
    {busy ? "处理中…" : children}
  </button>;
}
export function StatusPill({ tone, children }: { tone?: Tone; children: ReactNode }) {
  return <span className={"ui-pill" + (tone ? " ui-pill--" + tone : "")}>{children}</span>;
}
export function TextField({ label, value, onChange, error, hint, placeholder, disabled = false }: {
  label: string; value: string; onChange: (value: string) => void; error?: string; hint?: string; placeholder?: string; disabled?: boolean;
}) {
  const id = useId();
  return <div className={"ui-field" + (error ? " ui-field--invalid" : "")}>
    <label htmlFor={id}>{label}</label>
    <input id={id} value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder} disabled={disabled}
      aria-invalid={!!error} aria-describedby={error || hint ? id + "-description" : undefined} />
    {error ? <small className="ui-field__error" id={id + "-description"}>{error}</small> :
      hint ? <small className="ui-field__help" id={id + "-description"}>{hint}</small> : null}
  </div>;
}
export function SelectField({ label, value, onChange, options, disabled = false, hint }: {
  label: string; value: string; onChange: (value: string) => void;
  options: readonly { value: string; label: string }[]; disabled?: boolean; hint?: string;
}) {
  const id = useId();
  return <div className="ui-field">
    <label htmlFor={id}>{label}</label>
    <select id={id} value={value} onChange={e => onChange(e.target.value)} disabled={disabled}
      aria-describedby={hint ? id + "-hint" : undefined}>
      {options.map(o => <option key={o.value} value={o.value}>{o.label}</option>)}
    </select>
    {hint ? <small id={id + "-hint"} className="ui-field__help">{hint}</small> : null}
  </div>;
}

/** Exactly one app-owned dialog; confirmation is always left/green, cancel right/red. */
export function ConfirmDialog({ open, title, description, confirmLabel = "确认", cancelLabel = "取消", busy = false, confirmDisabled = false, children, onConfirm, onCancel }: {
  open: boolean; title: string; description: string; confirmLabel?: string; cancelLabel?: string;
  busy?: boolean; confirmDisabled?: boolean; children?: ReactNode; onConfirm: () => void; onCancel: () => void;
}) {
  const titleId = useId(), descId = useId();
  const container = useRef<HTMLDivElement>(null);
  const confirmButton = useRef<HTMLButtonElement>(null);
  const cancelButton = useRef<HTMLButtonElement>(null);
  const previousFocus = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!open) return;
    previousFocus.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    (confirmDisabled ? cancelButton.current : confirmButton.current)?.focus();
    const keyHandler = (event: KeyboardEvent) => {
      if (event.key === "Escape") { event.preventDefault(); if (!busy) onCancel(); return; }
      if (event.key !== "Tab" || !container.current) return;
      const controls = Array.from(container.current.querySelectorAll<HTMLButtonElement>("button:not(:disabled)"));
      if (!controls.length) { event.preventDefault(); return; }
      const first = controls[0], last = controls[controls.length - 1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    };
    document.addEventListener("keydown", keyHandler, true);
    return () => { document.removeEventListener("keydown", keyHandler, true); previousFocus.current?.focus(); };
  }, [open, busy, confirmDisabled, onCancel]);

  if (!open) return null;
  return <div className="ui-dialog-backdrop" data-testid="ui-dialog-backdrop">
    <div ref={container} className="ui-dialog" role="alertdialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={descId}>
      <h2 id={titleId} className="ui-dialog__heading">{title}</h2>
      <p id={descId} className="ui-dialog__description">{description}</p>
      {children}
      <div className="ui-dialog__actions">
        <button type="button" ref={confirmButton} className="ui-button ui-button--success" disabled={busy || confirmDisabled} onClick={onConfirm}>{busy ? "处理中…" : confirmLabel}</button>
        <button type="button" ref={cancelButton} className="ui-button ui-button--danger" disabled={busy} onClick={onCancel}>{cancelLabel}</button>
      </div>
    </div>
  </div>;
}
export type ToastInfo = Readonly<{ id: number; tone: Tone; message: string }>;
export function ToastViewport({ toast, dismiss }: { toast: ToastInfo | null; dismiss: () => void }) {
  if (!toast) return null;
  return <div className="ui-toast-viewport" role="status" aria-live={toast.tone === "danger" ? "assertive" : "polite"}>
    <div className={"ui-toast ui-toast--" + toast.tone}>
      <p className="ui-toast__message">{toast.message}</p>
      <button className="ui-toast__close" type="button" onClick={dismiss} aria-label="关闭提示">×</button>
    </div>
  </div>;
}
