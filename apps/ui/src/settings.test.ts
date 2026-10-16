import {describe,it,expect,vi} from "vitest";
import {DEFAULT_PREFERENCES,isPreferences,isPreferencesSnapshot,isBridgeRequest,makeSaveRequest} from "../../../src/contracts/ipc";
import {mockAccountIndex,filterSearch,paginate,DebounceController} from "../../../src/web/settings/search";
describe("settings IPC contract",()=>{
  it("only permits validated preference keys and values",()=>{
    expect(isPreferences(DEFAULT_PREFERENCES)).toBe(true);
    expect(isPreferences({...DEFAULT_PREFERENCES,pageSize:300})).toBe(false);
    expect(isPreferences({...DEFAULT_PREFERENCES,apiHash:"secret"})).toBe(false);
    expect(isPreferences({...DEFAULT_PREFERENCES,searchDebounceMs:10})).toBe(false);
    expect(isPreferencesSnapshot({revision:0,preferences:DEFAULT_PREFERENCES})).toBe(true);
  });
  it("rejects arbitrary IPC commands and stale payload shapes",()=>{
    expect(isBridgeRequest(makeSaveRequest("save-1",{revision:1,preferences:DEFAULT_PREFERENCES}))).toBe(true);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"r",type:"telegram.send"})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"r",type:"settings.save",payload:{expectedRevision:0,preferences:DEFAULT_PREFERENCES,token:"oops"}})).toBe(false);
  });
});
describe("300-record index search and pages",()=>{
  it("filters 300 synthetic records by remark with NFC normalization",()=>{
    const data=mockAccountIndex();
    expect(data).toHaveLength(300);
    expect(filterSearch(data,"  测试备注025 ")).toHaveLength(1);
    expect(filterSearch(data,"模拟数据")).toHaveLength(300);
    expect(filterSearch(data,"不存在")).toHaveLength(0);
  });
  it("limits every page to at most 50 rows with stable identities",()=>{
    const src=mockAccountIndex();
    const first=paginate(src,1,20),last=paginate(src,15,20);
    expect(first.pageCount).toBe(15);expect(first.items).toHaveLength(20);
    expect(last.items[19].id).toBe("mock-300");
    expect(paginate(src,999,50).page).toBe(6);
    expect(paginate([],1,20).pageCount).toBe(1);
    expect(()=>paginate(src,1,300)).toThrow();
  });
  it("coalesces repeated input updates and disposes timers on unmount",()=>{
    vi.useFakeTimers();
    try{
      const published:string[]=[];
      const controller=new DebounceController<string>(200,v=>published.push(v));
      for(let i=0;i<1000;i++)controller.change("value-"+i);
      vi.advanceTimersByTime(199);expect(published).toHaveLength(0);
      vi.advanceTimersByTime(1);expect(published).toEqual(["value-999"]);
      controller.change("late");controller.dispose();vi.advanceTimersByTime(1000);
      expect(published).toEqual(["value-999"]);
    }finally{vi.useRealTimers();}
  });
});
