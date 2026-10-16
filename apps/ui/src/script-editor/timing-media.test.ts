import {describe,it,expect} from "vitest";
import {editorReducer,newEditorState,DEFAULT_STEP_TIMES} from "./state";
import {isMediaReference,isEditorDoc,isBridgeRequest,makeMediaRequest,isTimingProfile} from "../../../../src/contracts/ipc";
const id=(n:number)=>"11111111-1111-4111-8111-"+String(n).padStart(12,"0");
const reference={assetId:"a".repeat(64),mimeType:"image/png" as const,byteLength:92,width:1,height:1,fileName:"example.png"};
const base={title:"图文消息",roles:[{roleKey:id(1),roleLabel:"客服A"}],
  steps:[{stepKey:id(2),roleKey:id(1),body:"English中文",preSendDelayMs:30000,typingDurationMs:5000,postSendDelayMs:0}]};
describe("three sequential timings and media references",()=>{
  it("stores the three fields independently without overlapping the pre-wait and typing",()=>{
    expect(isTimingProfile(DEFAULT_STEP_TIMES)).toBe(true);
    let state=newEditorState(base);
    state=editorReducer(state,{type:"timing",stepKey:id(2),values:{typingDurationMs:10000}});
    expect(state.present.steps[0].preSendDelayMs).toBe(30000);
    expect(state.present.steps[0].typingDurationMs).toBe(10000);
    expect(state.present.steps[0].preSendDelayMs+state.present.steps[0].typingDurationMs).toBe(40000);
    state=editorReducer(state,{type:"undo"});
    expect(state.present.steps[0].typingDurationMs).toBe(5000);
    state=editorReducer(state,{type:"redo"});
    expect(state.present.steps[0].typingDurationMs).toBe(10000);
  });
  it("changes default timing for future steps only, preserving previous per-step overrides",()=>{
    let s=newEditorState(base);
    s=editorReducer(s,{type:"defaults",values:{preSendDelayMs:0,typingDurationMs:0,postSendDelayMs:3000}});
    s=editorReducer(s,{type:"add",roleKey:id(1),stepKey:id(3)});
    expect(s.present.steps[0].preSendDelayMs).toBe(30000);
    expect(s.present.steps[1].preSendDelayMs).toBe(0);
    expect(s.present.steps[1].postSendDelayMs).toBe(3000);
    expect(isEditorDoc(s.present)).toBe(true);
  });
  it("batch edits only explicitly included fields for selected messages",()=>{
    let s=newEditorState(base);
    s=editorReducer(s,{type:"add",roleKey:id(1),stepKey:id(3)});
    s=editorReducer(s,{type:"select",stepKey:id(2),multiple:false});
    s=editorReducer(s,{type:"select",stepKey:id(3),multiple:true});
    s=editorReducer(s,{type:"batchTiming",values:{typingDurationMs:15000}});
    expect(s.present.steps.map(x=>x.typingDurationMs)).toEqual([15000,15000]);
    expect(s.present.steps.map(x=>x.preSendDelayMs)).toEqual([30000,5000]);
    s=editorReducer(s,{type:"undo"});
    expect(s.present.steps.map(x=>x.typingDurationMs)).toEqual([5000,5000]);
  });
  it("keeps hashed images when copying, reordering and undoing image removal",()=>{
    expect(isMediaReference(reference)).toBe(true);
    let s=newEditorState(base);
    s=editorReducer(s,{type:"media",stepKey:id(2),media:reference});
    expect(isEditorDoc(s.present)).toBe(true);
    s=editorReducer(s,{type:"select",stepKey:id(2),multiple:false});
    s=editorReducer(s,{type:"copy",newStepKeys:[id(4)]});
    expect(s.present.steps.map(x=>x.media?.assetId)).toEqual([reference.assetId,reference.assetId]);
    s=editorReducer(s,{type:"media",stepKey:id(4),media:null});
    expect(s.present.steps[0].media?.assetId).toBe(reference.assetId);
    s=editorReducer(s,{type:"undo"});
    expect(s.present.steps[1].media?.assetId).toBe(reference.assetId);
    expect(isEditorDoc(s.present)).toBe(true);
  });
  it("forbids unsafe media paths or inline base64 inside the portable script",()=>{
    expect(isMediaReference({...reference,fileName:"../secrets.png"})).toBe(false);
    expect(isMediaReference({...reference,mimeType:"image/svg+xml"})).toBe(false);
    expect(isMediaReference({...reference,byteLength:99999999})).toBe(false);
    expect(isEditorDoc({...base,steps:[{...base.steps[0],media:{...reference,sourcePath:"C:\\PRIVATE"}}]})).toBe(false);
    expect(isBridgeRequest(makeMediaRequest("media.preview","media-1",{reference}))).toBe(true);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"media-2",type:"media.import",
      payload:{fileName:"photo.png",mimeType:"image/png",base64:"A".repeat(7000000)}})).toBe(false);
  });
});
