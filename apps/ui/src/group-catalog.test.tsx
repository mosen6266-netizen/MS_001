import {describe,it,expect} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {DEFAULT_PREFERENCES,makeGroupRequest,isBridgeRequest,isGroupPage,isTopicPage,isMemberList} from "../../../src/contracts/ipc";
import {GroupCatalogPanel} from "./components/GroupCatalogPanel";
describe("trusted group directory read contract",()=>{
  it("allows only read-only local catalog commands",()=>{
    expect(isBridgeRequest(makeGroupRequest("groups.list","grp-1",{query:"项目",page:1,pageSize:20}))).toBe(true);
    expect(isBridgeRequest(makeGroupRequest("groups.topics","grp-2",{chatId:"-1001234567",page:1,pageSize:10}))).toBe(true);
    expect(isBridgeRequest(makeGroupRequest("groups.members","grp-3",{chatId:"-1001234567"}))).toBe(true);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"groups.members",payload:{chatId:"../secrets"}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"groups.send",payload:{chatId:"1",message:"spam"}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"groups.list",payload:{query:"",page:1,pageSize:300}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"groups.list",payload:{query:"",page:1,pageSize:20,password:"secret"}})).toBe(false);
  });
  it("rejects fake oversized result lists",()=>{
    expect(isGroupPage({items:[],total:0,page:1,pageSize:20})).toBe(true);
    expect(isGroupPage({items:Array(40).fill({}),total:40,page:1,pageSize:20})).toBe(false);
    expect(isTopicPage({items:[],total:0,page:1,pageSize:20})).toBe(true);
    expect(isMemberList(Array(301).fill({}))).toBe(false);
  });
  it("labels empty state as disconnected rather than simulated online groups",()=>{
    const html=renderToStaticMarkup(<GroupCatalogPanel preferences={DEFAULT_PREFERENCES}/>);
    expect(html).toContain("真实群组同步尚未接入");
    expect(html).toContain("正在读取群组");
    expect(html).not.toContain("群组消息已发送");
  });
});
