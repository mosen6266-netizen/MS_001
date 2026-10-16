import { describe, expect, it } from "vitest";
import { renderToStaticMarkup } from "react-dom/server";
import { createElement } from "react";
import { ALL_ROUTE_IDS, initialShellState, isShellRoute, SHELL_ROUTES, shortcutRoute, transitionShellRoute } from "../../../src/web/routes";
import { App } from "./App";
describe("single-document navigation", () => {
  it("has six unique routes and rejects unsupported paths", () => {
    expect(ALL_ROUTE_IDS).toHaveLength(6);
    expect(new Set(ALL_ROUTE_IDS).size).toBe(6);
    for (const r of SHELL_ROUTES) expect(isShellRoute(r.id)).toBe(true);
    expect(isShellRoute("../outside")).toBe(false);
  });
  it("saves scroll position independently when routes change", () => {
    let state = transitionShellRoute(initialShellState(), "accounts", 218.6);
    expect(state.scroll.overview).toBe(218);
    state = transitionShellRoute(state, "scripts", 740);
    const back = transitionShellRoute(state, "overview", 121);
    expect(back.scroll.overview).toBe(218);
    expect(back.scroll.accounts).toBe(740);
    expect(transitionShellRoute(back, "overview", 10)).toBe(back);
  });
  it("clamps bad scroll inputs", () => {
    expect(transitionShellRoute(initialShellState(), "accounts", -200).scroll.overview).toBe(0);
    expect(transitionShellRoute(initialShellState(), "accounts", Infinity).scroll.overview).toBe(0);
  });
  it("maps keyboard shortcuts without touching Ctrl or Meta", () => {
    expect(shortcutRoute(true, false, false, "1")).toBe("overview");
    expect(shortcutRoute(true, false, false, "6")).toBe("settings");
    expect(shortcutRoute(true, true, false, "1")).toBe(null);
    expect(shortcutRoute(false, false, false, "2")).toBe(null);
  });
  it("SSR smoke: one main, one nav, no page reload anchors", () => {
    const html = renderToStaticMarkup(createElement(App));
    expect((html.match(/<main /g) ?? []).length).toBe(1);
    expect((html.match(/<aside /g) ?? []).length).toBe(1);
    expect((html.match(/class="nav-action/g) ?? []).length).toBe(6);
    expect(html).not.toMatch(/<a\s+href=/);
    expect(html).toContain("无消息发送");
  });
});
