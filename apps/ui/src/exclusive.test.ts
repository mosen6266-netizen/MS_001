import {describe,expect,it} from "vitest";
import {ExclusiveAction} from "../../../src/web/settings/exclusive";
describe("async submit deduplication",()=>{
  it("1000 rapid submits execute exactly once and all duplicates return null",async()=>{
    const actor=new ExclusiveAction();
    let executed=0,release:()=>void=()=>{};
    const gate=new Promise<void>(resolve=>{release=resolve;});
    const first=actor.execute(async()=>{executed++;await gate;return "saved";});
    const duplicates=await Promise.all(Array.from({length:999},()=>actor.execute(async()=>{executed++;return "duplicate";})));
    expect(executed).toBe(1);expect(duplicates.every(x=>x===null)).toBe(true);
    release();expect(await first).toEqual({executed:true,value:"saved"});
    expect(actor.busy).toBe(false);
  });
  it("releases lock on rejection so a later manual retry can run",async()=>{
    const actor=new ExclusiveAction();
    await expect(actor.execute(async()=>{throw new Error("Disk full");})).rejects.toThrow("Disk full");
    expect(actor.busy).toBe(false);
    expect(await actor.execute(async()=>42)).toEqual({executed:true,value:42});
  });
});
