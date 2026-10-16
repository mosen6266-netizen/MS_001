import {describe,it,expect} from "vitest";
import {newEditorState,editorReducer,normalizedLabel,keyboardAction,type ScriptDoc} from "./state";
const id=(last:number)=>"11111111-1111-4111-8111-"+last.toString().padStart(12,"0");
describe("visual script editor undo/drag/roles",()=>{
  it("normalizes searchable aliases and never stores the local account id",()=>{
    expect(normalizedLabel(" 客服A ")).toBe("客服A");
    let s=newEditorState({title:"团队",roles:[],steps:[]});
    s=editorReducer(s,{type:"makeRole",roleKey:id(1),roleLabel:" 客服A "});
    s=editorReducer(s,{type:"makeRole",roleKey:id(2),roleLabel:"客服A"});
    expect(s.present.roles).toHaveLength(1);
    expect(JSON.stringify(s.present)).not.toContain("accountId");
  });
  it("supports add, duplicate, multiselect, reorder, undo and redo exactly",()=>{
    let s=newEditorState({title:"剧本",roles:[{roleKey:id(1),roleLabel:"角色A"}],steps:[]});
    s=editorReducer(s,{type:"add",roleKey:id(1),stepKey:id(10)});
    s=editorReducer(s,{type:"add",roleKey:id(1),stepKey:id(11)});
    s=editorReducer(s,{type:"select",stepKey:id(10),multiple:false});
    s=editorReducer(s,{type:"select",stepKey:id(11),multiple:true});
    expect(s.selected).toHaveLength(2);
    s=editorReducer(s,{type:"copy",newStepKeys:[id(12),id(13)]});
    expect(s.present.steps.map(x=>x.stepKey)).toEqual([id(10),id(11),id(12),id(13)]);
    s=editorReducer(s,{type:"reorder",from:id(13),to:id(10)});
    expect(s.present.steps[0].stepKey).toBe(id(13));
    s=editorReducer(s,{type:"undo"});
    expect(s.present.steps[3].stepKey).toBe(id(13));
    s=editorReducer(s,{type:"redo"});
    expect(s.present.steps[0].stepKey).toBe(id(13));
    s=editorReducer(s,{type:"select",stepKey:id(13),multiple:false});
    s=editorReducer(s,{type:"remove"});
    expect(s.present.steps).toHaveLength(3);
    s=editorReducer(s,{type:"undo"});
    expect(s.present.steps).toHaveLength(4);
  });
  it("preserves 30s pre-wait + 5s typing sequential metadata through reorder and cloning",()=>{
    const source:ScriptDoc={title:"序列",roles:[{roleKey:id(1),roleLabel:"角色A"}],
      steps:[{stepKey:id(10),roleKey:id(1),body:"中文 English",preSendDelayMs:30000,typingDurationMs:5000,postSendDelayMs:0}]};
    let s=newEditorState(source);
    s=editorReducer(s,{type:"select",stepKey:id(10),multiple:false});
    s=editorReducer(s,{type:"copy",newStepKeys:[id(11)]});
    expect(s.present.steps[1].preSendDelayMs+s.present.steps[1].typingDurationMs).toBe(35000);
    expect(s.present.steps[1].body).toBe("中文 English");
  });
  it("keeps keystrokes inside textarea native; only global Ctrl+Z outside editor",()=>{
    expect(keyboardAction({key:"z",ctrlKey:true,metaKey:false,shiftKey:false},true)).toBe(null);
    expect(keyboardAction({key:"z",ctrlKey:true,metaKey:false,shiftKey:false},false)).toBe("undo");
    expect(keyboardAction({key:"z",ctrlKey:true,metaKey:false,shiftKey:true},false)).toBe("redo");
  });
});
