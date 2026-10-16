export type ScriptRole={roleKey:string;roleLabel:string};
export type ScriptStep={stepKey:string;roleKey:string;body:string;preSendDelayMs:number;typingDurationMs:number;postSendDelayMs:number};
export type ScriptDoc={title:string;roles:ScriptRole[];steps:ScriptStep[]};
export type EditorState={past:ScriptDoc[];present:ScriptDoc;future:ScriptDoc[];selected:string[]};
export type EditorAction=
  |{type:"title";value:string}
  |{type:"choose";stepKey:string;roleKey:string}
  |{type:"body";stepKey:string;body:string}
  |{type:"add";roleKey:string;stepKey:string}
  |{type:"makeRole";roleKey:string;roleLabel:string}
  |{type:"copy";newStepKeys:string[]}
  |{type:"remove"}
  |{type:"reorder";from:string;to:string}
  |{type:"move";id:string;direction:-1|1}
  |{type:"select";stepKey:string;multiple:boolean}
  |{type:"undo"}|{type:"redo"};
export const DEFAULT_STEP_TIMES={preSendDelayMs:5000,typingDurationMs:5000,postSendDelayMs:0};
export const normalizedLabel=(v:string)=>v.trim().normalize("NFC");
export const newEditorState=(doc:ScriptDoc):EditorState=>({past:[],present:doc,future:[],selected:[]});
const snapshot=(x:ScriptDoc):ScriptDoc=>({title:x.title,roles:[...x.roles],steps:[...x.steps]});
const push=(old:EditorState,updated:ScriptDoc,selected=old.selected):EditorState=>{
  if(JSON.stringify(updated)===JSON.stringify(old.present))return old;
  return {past:[...old.past.slice(-79),snapshot(old.present)],present:updated,future:[],selected};
};
export function editorReducer(state:EditorState,action:EditorAction):EditorState {
  const doc=state.present;
  if(action.type==="undo"){
    if(!state.past.length)return state;
    return {past:state.past.slice(0,-1),present:state.past[state.past.length-1],
      future:[snapshot(doc),...state.future].slice(0,80),selected:[]};
  }
  if(action.type==="redo"){
    if(!state.future.length)return state;
    return {past:[...state.past.slice(-79),snapshot(doc)],present:state.future[0],
      future:state.future.slice(1),selected:[]};
  }
  if(action.type==="select"){
    const ids=doc.steps.map(x=>x.stepKey);
    if(!ids.includes(action.stepKey))return state;
    return {...state,selected:action.multiple ?
      (state.selected.includes(action.stepKey)?state.selected.filter(x=>x!==action.stepKey):
      [...state.selected,action.stepKey]) : [action.stepKey]};
  }
  if(action.type==="title")return push(state,{...doc,title:action.value});
  if(action.type==="makeRole"){
    const label=normalizedLabel(action.roleLabel);
    if(!label || label.length>64 ||
       doc.roles.some(x=>normalizedLabel(x.roleLabel)===label)||
       doc.roles.some(x=>x.roleKey===action.roleKey))return state;
    return push(state,{...doc,roles:[...doc.roles,{roleKey:action.roleKey,roleLabel:label}]});
  }
  if(action.type==="add"){
    if(!doc.roles.some(x=>x.roleKey===action.roleKey)||
       doc.steps.some(x=>x.stepKey===action.stepKey)||doc.steps.length>=1000)return state;
    return push(state,{...doc,steps:[...doc.steps,{stepKey:action.stepKey,roleKey:action.roleKey,body:"",...DEFAULT_STEP_TIMES}]},[action.stepKey]);
  }
  if(action.type==="choose"){
    if(!doc.roles.some(x=>x.roleKey===action.roleKey))return state;
    return push(state,{...doc,steps:doc.steps.map(x=>x.stepKey===action.stepKey?{...x,roleKey:action.roleKey}:x)});
  }
  if(action.type==="body")
    return push(state,{...doc,steps:doc.steps.map(x=>x.stepKey===action.stepKey?{...x,body:action.body.slice(0,8192)}:x)});
  if(action.type==="copy"){
    const selected=doc.steps.filter(s=>state.selected.includes(s.stepKey));
    if(!selected.length||selected.length!==action.newStepKeys.length||
       doc.steps.length+selected.length>1000 || new Set(action.newStepKeys).size!==selected.length ||
       action.newStepKeys.some(id=>doc.steps.some(x=>x.stepKey===id)))return state;
    const copies=selected.map((s,i)=>({...s,stepKey:action.newStepKeys[i]}));
    const last=doc.steps.findLastIndex(s=>state.selected.includes(s.stepKey));
    return push(state,{...doc,steps:[...doc.steps.slice(0,last+1),...copies,...doc.steps.slice(last+1)]},
      copies.map(x=>x.stepKey));
  }
  if(action.type==="remove"){
    if(!state.selected.length)return state;
    return push(state,{...doc,steps:doc.steps.filter(x=>!state.selected.includes(x.stepKey))},[]);
  }
  if(action.type==="move"||action.type==="reorder"){
    const from=doc.steps.findIndex(x=>x.stepKey===(action.type==="move"?action.id:action.from));
    const to=action.type==="move" ?from+action.direction:
      doc.steps.findIndex(x=>x.stepKey===action.to);
    if(from<0||to<0||to>=doc.steps.length||from===to)return state;
    const steps=[...doc.steps];const [item]=steps.splice(from,1);steps.splice(to,0,item);
    return push(state,{...doc,steps});
  }
  return state;
}
export function keyboardAction(event:{key:string;ctrlKey:boolean;metaKey:boolean;shiftKey:boolean},
  editingText:boolean):"undo"|"redo"|null{
  if(editingText||!(event.ctrlKey||event.metaKey))return null;
  if(event.key.toLowerCase()==="z")return event.shiftKey?"redo":"undo";
  if(event.key.toLowerCase()==="y")return "redo";
  return null;
}
