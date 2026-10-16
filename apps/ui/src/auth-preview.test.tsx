import {describe,expect,it} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {AuthStatusPreview,AUTH_UI_STATES} from "./components/AuthStatusPreview";
describe("authorization state UI preview",()=>{
  it("covers every official TDLib authorization state plus future fallback",()=>{
    expect(AUTH_UI_STATES).toHaveLength(14);
    expect(new Set(AUTH_UI_STATES.map(s=>s.id)).size).toBe(14);
    expect(AUTH_UI_STATES.every(s=>s.title.length>0&&s.guide.length>5)).toBe(true);
  });
  it("labels UI as offline simulation; does not present functional credentials fields",()=>{
    const html=renderToStaticMarkup(<AuthStatusPreview/>);
    expect(html).toContain("纯界面模拟");
    expect(html).toContain("未连接 Telegram");
    expect(html).toContain("真正登录入口需在原生客户端");
    expect((html.match(/disabled=""/g)??[]).length).toBeGreaterThanOrEqual(2);
    expect(html).not.toContain('type="password"');
  });
});
