import {useCallback,useEffect,useRef,useState} from "react";
import {DEFAULT_PREFERENCES,isBridgeResponse,isPreferencesSnapshot,makeRequest,makeSaveRequest,
  type PreferencesSnapshot,type UserPreferences} from "../../../../src/contracts/ipc";

type Mode="loading"|"ready"|"preview"|"error";
type Waiter={resolve:(value:PreferencesSnapshot)=>void;reject:(error:Error)=>void;timeout:ReturnType<typeof setTimeout>};
export function usePreferences() {
  const [snapshot,setSnapshot]=useState<PreferencesSnapshot>({revision:0,preferences:DEFAULT_PREFERENCES});
  const [mode,setMode]=useState<Mode>("loading");
  const pending=useRef(new Map<string,Waiter>());
  const sequence=useRef(0);
  useEffect(()=>{
    const host=window.chrome?.webview;
    if(!host){setMode("preview");return;}
    const onMessage=({data}:{data:unknown})=>{
      if(!isBridgeResponse(data))return;
      if(data.requestId==="settings-initial"){
        if(data.ok&&data.type==="settings.get.result"&&isPreferencesSnapshot(data.payload)){
          setSnapshot(data.payload);setMode("ready");
        }else setMode("error");
        return;
      }
      const job=pending.current.get(data.requestId);
      if(!job)return;
      pending.current.delete(data.requestId);clearTimeout(job.timeout);
      if(data.ok&&data.type==="settings.save.result"&&isPreferencesSnapshot(data.payload)){
        setSnapshot(data.payload);job.resolve(data.payload);
      }else{
        const code=typeof data.payload.code==="string"?data.payload.code:"UNAVAILABLE";
        if(code==="CONFLICT"){
          // Conflict does not overwrite local settings; prompt a reload rather than blind retry.
          job.reject(new Error("设置已被其它操作修改，请重新打开设置页读取。"));
        }else job.reject(new Error(typeof data.payload.message==="string"?data.payload.message:"保存失败，请稍后重试。"));
      }
    };
    host.addEventListener("message",onMessage);
    host.postMessage(JSON.stringify(makeRequest("settings.get","settings-initial")));
    return ()=>{
      host.removeEventListener("message",onMessage);
      for(const job of pending.current.values()){clearTimeout(job.timeout);job.reject(new Error("页面已关闭"));}
      pending.current.clear();
    };
  },[]);

  const save=useCallback((preferences:UserPreferences):Promise<PreferencesSnapshot>=>{
    if(mode!=="ready"||!window.chrome?.webview) return Promise.reject(new Error("本地设置服务不可用；浏览器预览无法真正保存。"));
    const id="settings-save-"+(++sequence.current);
    const request=makeSaveRequest(id,{revision:snapshot.revision,preferences});
    return new Promise<PreferencesSnapshot>((resolve,reject)=>{
      const timeout=setTimeout(()=>{
        pending.current.delete(id);reject(new Error("本地保存响应超时，请重新打开设置核对结果，勿重复提交。"));
      },10000);
      pending.current.set(id,{resolve,reject,timeout});
      try{window.chrome!.webview!.postMessage(JSON.stringify(request));}
      catch{clearTimeout(timeout);pending.current.delete(id);reject(new Error("无法向本地程序发送保存请求。"));}
    });
  },[mode,snapshot.revision]);

  const reload=useCallback(()=>{
    if(!window.chrome?.webview)return;
    setMode("loading");
    window.chrome.webview.postMessage(JSON.stringify(makeRequest("settings.get","settings-initial")));
  },[]);
  return {snapshot,mode,save,reload};
}
