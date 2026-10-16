import {useState} from "react";
import type {TimingProfile} from "../../../../src/contracts/ipc";
export type TimingKey=keyof TimingProfile;
export const TIMING_PRESETS=[0,3,5,10,15,30] as const;
export function TimingField({label,value,maxSeconds,onChange}:{label:string;value:number;maxSeconds:number;onChange:(millis:number)=>void}){
  const [manual,setManual]=useState(false);
  const seconds=value/1000;
  const selected=!manual&&TIMING_PRESETS.includes(seconds as typeof TIMING_PRESETS[number])?String(seconds):"custom";
  return <div className="ui-field script-timing-field">
    <label>{label}</label>
    <select aria-label={label+"快捷设置"} value={selected} onChange={e=>{
      if(e.target.value==="custom"){setManual(true);return;}
      setManual(false);onChange(Number(e.target.value)*1000);
    }}>
      <option value="0">{label==="正在输入"?"关闭（0秒）":"0 秒"}</option>
      {TIMING_PRESETS.filter(x=>x>0&&x<=maxSeconds).map(x=><option key={x} value={x}>{x} 秒</option>)}
      <option value="custom">自定义…</option>
    </select>
    {selected==="custom"?<label className="script-timing-custom">
      <span>自定义秒数</span><input type="number" aria-label={label+"自定义秒数"}
        value={seconds} min={0} max={maxSeconds} step="any"
        onChange={e=>{
          if(e.target.value.trim()==="")return;
          const next=Number(e.target.value);
          if(Number.isFinite(next)&&next>=0&&next<=maxSeconds&&Number.isInteger(next*1000))
            onChange(next*1000);
        }}/>
    </label>:null}
  </div>;
}
