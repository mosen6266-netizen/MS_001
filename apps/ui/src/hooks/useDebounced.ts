import {useEffect,useState} from "react";
import {DebounceController} from "../../../../src/web/settings/search";
export function useDebounced<T>(value:T,delayMs:number):T {
  const [ready,setReady]=useState(value);
  useEffect(()=>{
    const controller=new DebounceController(delayMs,setReady);
    controller.change(value);
    return ()=>controller.dispose();
  },[value,delayMs]);
  return ready;
}
