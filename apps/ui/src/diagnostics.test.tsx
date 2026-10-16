import {describe,expect,it} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {DiagnosticsPanel} from "./components/DiagnosticsPanel";
import {isBridgeRequest,makeRequest} from "../../../src/contracts/ipc";
describe("local-safe diagnostics contract",()=>{
  it("contains no network or Telegram send operation",()=>{
    const request=makeRequest("diagnostics.get","diag-1");
    expect(isBridgeRequest(request)).toBe(true);
    expect(isBridgeRequest({...request,type:"diagnostics.export.private-data"})).toBe(false);
    expect(isBridgeRequest({...request,type:"diagnostics.get",payload:{phone:"+12025550001"}})).toBe(false);
  });
  it("renders honest browser preview with no synthetic live metrics",()=>{
    const html=renderToStaticMarkup(<DiagnosticsPanel/>);
    expect(html).toContain("浏览器预览");
    expect(html).toContain("没有手机号");
    expect(html).not.toContain("Telegram 已连接");
    expect(html).toContain('aria-label="本地运行诊断"');
  });
});
