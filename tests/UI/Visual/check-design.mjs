import fs from "node:fs";
import assert from "node:assert/strict";
const tokens=fs.readFileSync("src/web/design/tokens.css","utf8");
const primitive=fs.readFileSync("src/web/design/primitives.css","utf8");
const layout=fs.readFileSync("apps/ui/src/style.css","utf8");
const app=fs.readFileSync("apps/ui/src/components/DesignSystem.tsx","utf8");
const tests = [
  ["theme dark selected", tokens.includes("color-scheme: dark")],
  ["type scale", tokens.includes("--font-size-md") && tokens.includes("--font-body")],
  ["shared spacing/radii", tokens.includes("--space-4") && tokens.includes("--radius-md")],
  ["success green", tokens.includes("--action-success: #62d49b")],
  ["danger red", tokens.includes("--action-danger: #ef8991")],
  ["focus visible", primitive.includes(":focus-visible")],
  ["no unsafe HTML injection", !app.includes("dangerouslySetInnerHTML")],
  ["dialog accessible", app.includes('role="alertdialog"') && app.includes('aria-modal="true"')],
  ["green confirm first", app.indexOf("ui-button--success") < app.indexOf("ui-button--danger")],
  ["one toast slot", app.includes("toast: ToastInfo | null")],
  ["fixed navigation", layout.includes(".shell-nav") && layout.includes("height:100%")],
  ["one scrolling main", layout.includes(".shell-main") && layout.includes("overflow-y:auto")],
  ["mobile layout", layout.includes("@media(max-width:620px)")],
  ["reduced motion", tokens.includes("prefers-reduced-motion")],
  ["design imports", layout.includes('tokens.css') && layout.includes('primitives.css')],
  ["disabled states", primitive.includes(".ui-button:disabled")]
];
for(const [name,ok] of tests){assert.ok(ok,name);process.stdout.write("PASS "+name+"\n");}
console.log("SUMMARY:", tests.length, "design/static checks passed. Browser pixel-diff not yet performed.");
