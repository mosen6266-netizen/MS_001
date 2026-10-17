import {afterEach,describe,expect,it,vi} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {App} from "./App";
import {isTelegramLiveStatus,type TelegramLiveStatus} from "../../../src/contracts/ipc";

const clean:TelegramLiveStatus={
 nativeReady:false,connected:false,phase:"Initializing",title:"尚未连接",
 instruction:"请先保存参数",qrPng:null,canStartQr:false,canSubmit:false,
 canCancel:false,groups:[],delivery:"尚未发送",issue:null,outboundPending:false
};
afterEach(()=>vi.unstubAllGlobals());
describe("accounts click blank-page regression",()=>{
 it("renders full account route inside a Windows WebView2-like host, not just overview",()=>{
  vi.stubGlobal("window",{chrome:{webview:{
   postMessage:()=>{},addEventListener:()=>{},removeEventListener:()=>{}
  }}});
  const html=renderToStaticMarkup(<App initialRoute="accounts" />);
  expect(html).toContain("主导航");
  expect(html).toContain("账号管理");
  expect(html).toContain("Telegram 官方登录");
  expect(html).toContain("本地账号注册表");
  expect(html).toContain("登记新的本地账号槽位");
  expect(html).not.toContain("undefined");
 });
 it("renders group route with both real live panel and saved directory",()=>{
  vi.stubGlobal("window",{chrome:{webview:{
   postMessage:()=>{},addEventListener:()=>{},removeEventListener:()=>{}
  }}});
  const html=renderToStaticMarkup(<App initialRoute="groups" />);
  expect(html).toContain("Telegram 群组与手动消息");
  expect(html).toContain("Telegram 群组与话题目录");
 });
 it("rejects malformed async native response before status.groups.filter",()=>{
  expect(isTelegramLiveStatus(clean)).toBe(true);
  expect(isTelegramLiveStatus({...clean,groups:null})).toBe(false);
  expect(isTelegramLiveStatus({...clean,groups:undefined})).toBe(false);
  expect(isTelegramLiveStatus({...clean,groups:{}})).toBe(false);
  expect(isTelegramLiveStatus({...clean,groups:[null]})).toBe(false);
  expect(isTelegramLiveStatus({...clean,groups:[{chatId:"-1001234",title:"测试群",canSend:true}]})).toBe(true);
  expect(isTelegramLiveStatus({...clean,groups:[{chatId:"not-a-chat",title:"错误",canSend:true}]})).toBe(false);
  expect(isTelegramLiveStatus({...clean,qrPng:"javascript:alert(1)"})).toBe(false);
  expect(isTelegramLiveStatus({...clean,qrPng:"data:image/png;base64,QUJD"})).toBe(true);
 });
});
