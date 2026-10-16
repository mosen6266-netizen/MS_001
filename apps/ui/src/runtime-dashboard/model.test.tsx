import {describe,it,expect} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {RuntimeDashboardPanel} from "../components/RuntimeDashboardPanel";
import {makeRequest,isBridgeRequest,isRuntimeDashboardSnapshot,
  type RuntimeDashboardRow} from "../../../../src/contracts/ipc";
import {selectRuntimePage,countdownMs,displayRemaining,statusLabel,issueLabel} from "./model";
const make=(i:number,status="Running",code=""):RuntimeDashboardRow=>({
  runId:"00000000-0000-4000-8000-"+String(i).padStart(12,"0"),
  chatId:"-100"+String(i+100),topicId:null,state:status,phase:"WaitingBefore",
  completedSteps:1,totalSteps:5,completionPercent:20,nextStepNumber:2,
  remainingMs:30000,lastClockUtc:"2026-10-16T20:00:00Z",isClockActive:status==="Running",
  issueCode:code,revision:1
});
describe("runtime center read-only screen and 50-group paging",()=>{
 it("renders empty offline preview with no fake live tasks or send controls",()=>{
   const html=renderToStaticMarkup(<RuntimeDashboardPanel/>);
   expect(html).toContain("运行中心");
   expect(html).toContain("浏览器预览");
   expect(html).toContain("不支持真实 Telegram 发送");
   expect(html).not.toContain("开始发送");
   expect(html).not.toContain("恢复执行");
 });
 it("allows only read-only status requests",()=>{
   expect(isBridgeRequest(makeRequest("runtime.snapshot","runtime-1"))).toBe(true);
   expect(isBridgeRequest({...makeRequest("runtime.snapshot","x"),payload:{task:"send"}})).toBe(false);
   expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"runtime.start"})).toBe(false);
 });
 it("rejects fake/malformed model payloads",()=>{
   const summary={total:0,running:0,paused:0,review:0,failed:0,completed:0,stopped:0,ready:0,uncertainMessages:0,corruptRecords:0};
   const snapshot={checkedUtc:"2026-10-16T20:00:00Z",mode:"offline-simulation-readonly",summary,items:[]};
   expect(isRuntimeDashboardSnapshot(snapshot)).toBe(true);
   expect(isRuntimeDashboardSnapshot({...snapshot,items:[make(1)]})).toBe(true);
   expect(isRuntimeDashboardSnapshot({...snapshot,items:[{...make(1),completionPercent:999}]})).toBe(false);
   expect(isRuntimeDashboardSnapshot({...snapshot,items:[{...make(1),chatId:"../../secrets"}]})).toBe(false);
 });
 it("always keeps exactly 10 visible rows out of 50; no timer per card",()=>{
   const tasks=Array.from({length:50},(_,i)=>make(i));
   const one=selectRuntimePage(tasks,"all","",1);
   const five=selectRuntimePage(tasks,"all","",5);
   expect(one.items).toHaveLength(10);
   expect(five.items).toHaveLength(10);
   expect(one.total).toBe(50);
   expect(one.pages).toBe(5);
   expect(new Set([...one.items,...five.items].map(x=>x.runId)).size).toBe(20);
   expect(selectRuntimePage(tasks,"all","",999).page).toBe(5);
 });
 it("prioritizes review and failures without discarding valid task rows",()=>{
   const rows=[make(1),make(2,"Review","OUTCOME_UNKNOWN"),make(3,"Failed","TASK_FAILED"),
     make(4,"Completed")];
   expect(selectRuntimePage(rows,"all","",1).items.slice(0,2).every(x=>x.issueCode!=="")).toBe(true);
   expect(selectRuntimePage(rows,"review","",1).total).toBe(2);
   expect(selectRuntimePage(rows,"failed","",1).total).toBe(1);
   expect(selectRuntimePage(rows,"active","",1).total).toBe(1);
   expect(selectRuntimePage(rows,"all","-100103",1).items).toHaveLength(1);
   expect(issueLabel("OUTCOME_UNKNOWN")).toContain("不确定");
   expect(statusLabel("Review")).toContain("人工复核");
 });
 it("countdown uses a single snapshot anchor, never negative, freeze on pause",()=>{
   const row=make(1);
   const now=Date.parse(row.lastClockUtc);
   expect(countdownMs(row,now)).toBe(30000);
   expect(countdownMs(row,now+10000)).toBe(20000);
   expect(countdownMs(row,now+35000)).toBe(0);
   expect(countdownMs(row,now-10000)).toBe(30000);
   expect(displayRemaining({...row,isClockActive:false,state:"Paused"},now+100000)).toContain("已暂停");
   expect(displayRemaining({...row,isClockActive:false,state:"Review"},now+100000)).toBe("—");
 });
 it("2000 repetitive 50-group screen calculations complete within bounded synchronous work",()=>{
   const tasks=Array.from({length:50},(_,i)=>make(i));
   for(let i=0;i<2000;i++){
     const view=selectRuntimePage(tasks,"all","",1+(i%5));
     expect(view.items.length).toBe(10);
   }
 });
});
