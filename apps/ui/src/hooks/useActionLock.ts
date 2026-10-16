import {useCallback,useRef,useState} from "react";
export function useActionLock() {
  const lock=useRef(false);
  const [busy,setBusy]=useState(false);
  const run=useCallback(async <T,>(job:()=>Promise<T>):Promise<T|undefined>=>{
    if(lock.current)return undefined;
    lock.current=true;setBusy(true);
    try {return await job();}
    finally {lock.current=false;setBusy(false);}
  },[]);
  return {busy,run};
}
