export type SearchItem=Readonly<{id:string;label:string;detail?:string}>;
export type Page<T>={items:readonly T[];page:number;pageCount:number;total:number};
export function normalizeSearch(q:string):string {return q.trim().normalize("NFC").toLocaleLowerCase();}
export function filterSearch(items:readonly SearchItem[],query:string):SearchItem[] {
  const term=normalizeSearch(query);
  if(!term)return [...items];
  return items.filter(x=>normalizeSearch(x.label).includes(term)||normalizeSearch(x.detail??"").includes(term));
}
export function paginate<T>(items:readonly T[],index:number,pageSize:number):Page<T> {
  if(!Number.isInteger(pageSize)||pageSize<1||pageSize>50)throw new RangeError("Invalid page size");
  const pageCount=Math.max(1,Math.ceil(items.length/pageSize));
  const page=Math.min(pageCount,Math.max(1,Number.isFinite(index)?Math.floor(index):1));
  return {items:items.slice((page-1)*pageSize,page*pageSize),page,pageCount,total:items.length};
}
/** Single trailing update and cancellation on dispose; no network request or UI redraw per keystroke. */
export class DebounceController<T> {
  private handle:ReturnType<typeof setTimeout>|null=null;
  private disposed=false;
  constructor(private readonly delayMs:number,private readonly publish:(v:T)=>void,
    private readonly schedule:(f:()=>void,ms:number)=>ReturnType<typeof setTimeout>=setTimeout,
    private readonly cancel:(handle:ReturnType<typeof setTimeout>)=>void=clearTimeout){
    if(!Number.isInteger(delayMs)||delayMs<150||delayMs>250)throw new RangeError("Debounce must be 150–250ms");
  }
  change(next:T) {
    if(this.disposed)return;
    if(this.handle!==null)this.cancel(this.handle);
    this.handle=this.schedule(()=>{this.handle=null;if(!this.disposed)this.publish(next);},this.delayMs);
  }
  dispose(){this.disposed=true;if(this.handle!==null)this.cancel(this.handle);this.handle=null;}
}
export function mockAccountIndex(size=300):SearchItem[] {
  if(!Number.isInteger(size)||size<0||size>300)throw new RangeError("Synthetic test index limited to 300");
  return Array.from({length:size},(_,i)=>({id:"mock-"+String(i+1).padStart(3,"0"),label:"测试备注"+String(i+1).padStart(3,"0"),detail:"本地模拟数据 · 尚无 Telegram 会话"}));
}
