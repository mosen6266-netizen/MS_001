import type {RuntimeDashboardRow} from "../../../../src/contracts/ipc";
export type RuntimeFilter="all"|"active"|"review"|"failed"|"completed"|"stopped";
const HIGHLIGHT=new Set(["Review","Failed"]);
export function severity(task:RuntimeDashboardRow):number {
  return task.issueCode!==""||HIGHLIGHT.has(task.state)?0:task.state==="Running"?1:
    task.state==="Paused"?2:task.state==="Ready"?3:4;
}
export function selectRuntimePage(rows:readonly RuntimeDashboardRow[],filter:RuntimeFilter,search:string,
  page:number,pageSize=10){
  const query=search.trim().normalize("NFC").toLowerCase();
  const filtered=rows.filter(item=>{
    const match=filter==="all"||filter==="active"&&["Running","Paused","Ready"].includes(item.state)||
      filter==="review"&&(item.state==="Review"||item.issueCode!=="")||
      filter==="failed"&&item.state==="Failed"||filter==="completed"&&item.state==="Completed"||
      filter==="stopped"&&item.state==="Stopped";
    return match&&(!query||[item.runId,item.chatId,item.topicId??""].some(x=>x.toLowerCase().includes(query)));
  }).sort((a,b)=>severity(a)-severity(b)||a.chatId.localeCompare(b.chatId)||
    (a.topicId??"").localeCompare(b.topicId??"")||a.runId.localeCompare(b.runId));
  const limit=Math.max(1,Math.min(50,Math.floor(pageSize)||10));
  const pages=Math.max(1,Math.ceil(filtered.length/limit));
  const safePage=Math.max(1,Math.min(pages,Math.floor(page)||1));
  return {total:filtered.length,pages,page:safePage,items:filtered.slice((safePage-1)*limit,safePage*limit)};
}
export function countdownMs(item:RuntimeDashboardRow,now:number){
  if(!item.isClockActive)return Math.max(0,item.remainingMs);
  const anchor=Date.parse(item.lastClockUtc);
  if(!Number.isFinite(anchor))return Math.max(0,item.remainingMs);
  return Math.max(0,item.remainingMs-Math.max(0,now-anchor));
}
export function displayRemaining(item:RuntimeDashboardRow,now:number){
  if(!item.isClockActive)return item.state==="Completed"?"—":item.state==="Paused"?
    Math.ceil(item.remainingMs/1000)+"秒（已暂停）":"—";
  return Math.ceil(countdownMs(item,now)/1000)+"秒";
}
export function statusLabel(state:string){return ({
  Ready:"待开始",Running:"运行中（模拟）",Paused:"已暂停",Review:"待人工复核",
  Failed:"失败",Completed:"已完成",Stopped:"已停止"
} as Record<string,string>)[state]??"未知状态";}
export function issueLabel(code:string){return ({
  OUTCOME_UNKNOWN:"发送结果不确定",DELIVERY_REVIEW:"投递需要复核",
  REQUIRES_REVIEW:"任务需要复核",TASK_FAILED:"任务出现异常",
  USER_PAUSED:"用户暂停"
} as Record<string,string>)[code]??"请查看任务诊断";}
