import {describe,expect,it} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {makeAccountRequest,isBridgeRequest,isAccountPage,DEFAULT_PREFERENCES} from "../../../src/contracts/ipc";
import {AccountRegistryPanel} from "./components/AccountRegistryPanel";
describe("safe account registry local IPC",()=>{
  it("limits account commands to whitelisted metadata",()=>{
    expect(isBridgeRequest(makeAccountRequest("accounts.create","acc-1",{alias:"客服A",groupTag:"业务"}))).toBe(true);
    expect(isBridgeRequest(makeAccountRequest("accounts.list","acc-2",{query:"A",group:"",page:1,pageSize:20}))).toBe(true);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"acc-3",type:"accounts.create",payload:{alias:"A",groupTag:"",apiHash:"secret"}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"acc-4",type:"accounts.archive",payload:{id:"1",expectedRevision:1,typedAlias:"X"}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"acc-5",type:"accounts.list",payload:{query:"",group:"",page:1,pageSize:300}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"acc-6",type:"accounts.send",payload:{}})).toBe(false);
  });
  it("validates bounded server page without trusting raw bridge response",()=>{
    expect(isAccountPage({items:[],total:300,page:1,pageSize:20})).toBe(true);
    expect(isAccountPage({items:Array(300).fill({}),total:300,page:1,pageSize:20})).toBe(false);
  });
  it("renders local metadata-only UI without claiming online sessions",()=>{
    const html=renderToStaticMarkup(<AccountRegistryPanel preferences={DEFAULT_PREFERENCES}/>);
    expect(html).toContain("仅登记元数据");
    expect(html).toContain("不代表真实登录或在线");
    expect(html).toContain("登记账号");
    expect(html).not.toContain("300 个账号已登录");
  });
});
