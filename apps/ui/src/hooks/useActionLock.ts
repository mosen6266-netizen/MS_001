import {useCallback,useRef,useState} from "react";
import {ExclusiveAction} from "../../../../src/web/settings/exclusive";
export function useActionLock() {
  const actor=useRef(new ExclusiveAction());
  const [busy,setBusy]=useState(false);
  const run=useCallback(async <T,>(job:()=>Promise<T>):Promise<{executed:true;value:T}|null>=>{
    // Duplicate calls must never produce a successful result or a false toast.
    if(actor.current.busy)return null;
    setBusy(true);
    try{return await actor.current.execute(job);}
    finally{setBusy(false);}
  },[]);
  return {busy,run};
}
