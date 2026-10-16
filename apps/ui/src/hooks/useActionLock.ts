import {useCallback,useRef,useState} from "react";
export function useActionLock() {
  const lock=useRef(false);
  const [busy,setBusy]=useState(false);
  const run=useCallback(async <T,>(job:()=>Promise<T>):Promise<{executed:true;value:T}|null>=>{
    if(lock.current)return null;
    lock.current=true;setBusy(true);
    try {const value=await job();return {executed:true,value};}
    finally {lock.current=false;setBusy(false);}
  },[]);
  return {busy,run};
}
