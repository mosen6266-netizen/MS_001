import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { ActionButton, ConfirmDialog, SelectField, StatusPill, TextField, ToastViewport } from "./components/DesignSystem";
import { contrastRatio, palette } from "../../../src/web/design/tokens";
import { DesignShowcase } from "./components/DesignShowcase";

describe("UI design contracts", () => {
  it("meets text and action foreground contrast thresholds", () => {
    expect(contrastRatio(palette.text, palette.root)).toBeGreaterThanOrEqual(7);
    expect(contrastRatio(palette.muted, palette.panel)).toBeGreaterThanOrEqual(4.5);
    expect(contrastRatio(palette.success, palette.onSuccess)).toBeGreaterThanOrEqual(7);
    expect(contrastRatio(palette.danger, palette.onDanger)).toBeGreaterThanOrEqual(7);
  });
  it("refuses malformed palette inputs", () => expect(() => contrastRatio("red", palette.panel)).toThrow());
  it("buttons default to non-submitting and expose busy/disabled states", () => {
    const standard = renderToStaticMarkup(<ActionButton>继续</ActionButton>);
    expect(standard).toContain('type="button"');
    const busy = renderToStaticMarkup(<ActionButton tone="success" busy>继续</ActionButton>);
    expect(busy).toContain("disabled");
    expect(busy).toContain("aria-busy");
  });
  it("keeps modal accessible and places confirmation on left before cancel on right", () => {
    const html = renderToStaticMarkup(<ConfirmDialog open title="确认操作" description="操作演示" onConfirm={() => {}} onCancel={() => {}} />);
    expect(html).toContain('role="alertdialog"');
    expect(html).toContain('aria-modal="true"');
    const confirm = html.indexOf('ui-button--success');
    const cancel = html.indexOf('ui-button--danger');
    expect(confirm).toBeGreaterThan(0);
    expect(cancel).toBeGreaterThan(confirm);
    expect(html).toContain("确认");
    expect(html).toContain("取消");
    expect(renderToStaticMarkup(<ConfirmDialog open={false} title="不显示" description="隐藏" onConfirm={() => {}} onCancel={() => {}} />)).toBe("");
  });
  it("links visible field labels to controls, exposes errors and native scrolling select", () => {
    const html = renderToStaticMarkup(<TextField label="账号备注" value="" onChange={() => {}} error="请填写备注" />);
    const inputId = html.match(/<input id="([^"]+)"/)?.[1];
    expect(inputId).toBeTruthy();
    expect(html).toContain('for="' + inputId + '"');
    expect(html).toContain('aria-invalid="true"');
    const select = renderToStaticMarkup(<SelectField label="等待" value="5" onChange={() => {}} options={[{value:"5",label:"5 秒"}]} />);
    expect(select).toContain("<select");
    expect(select).toContain("<option");
  });
  it("renders one toast and a dismiss control with live announcements", () => {
    const html = renderToStaticMarkup(<ToastViewport toast={{id:1,tone:"success",message:"已完成"}} dismiss={() => {}} />);
    expect(html).toContain('role="status"');
    expect(html).toContain('aria-live="polite"');
    expect((html.match(/class="ui-toast ui-toast--success"/g) ?? []).length).toBe(1);
    expect(html).toContain('aria-label="关闭提示"');
    expect(renderToStaticMarkup(<ToastViewport toast={null} dismiss={() => {}} />)).toBe("");
  });
  it("has consistent style components in the offline settings preview", () => {
    const html = renderToStaticMarkup(<DesignShowcase />);
    expect(html).toContain("发送前等待");
    expect(html).toContain("不会保存真实数据");
    expect(html).toContain("绿色提示演示");
    expect(html).toContain("红色操作演示");
    expect(html).not.toContain('role="alertdialog"');
    expect(renderToStaticMarkup(<StatusPill tone="warning">需处理</StatusPill>)).toContain("ui-pill--warning");
  });
});
