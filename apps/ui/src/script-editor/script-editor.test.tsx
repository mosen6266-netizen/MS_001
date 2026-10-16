import {describe,it,expect} from "vitest";
import {renderToStaticMarkup} from "react-dom/server";
import {ScriptEditorPanel} from "../components/ScriptEditorPanel";
import {DEFAULT_PREFERENCES,isBridgeRequest,isEditorDoc,makeScriptRequest,isScriptSnapshot,isTimingProfile} from "../../../../src/contracts/ipc";
const id=(n:number)=>"77777777-7777-4777-8777-"+String(n).padStart(12,"0");
const doc={title:"中文 English",roles:[{roleKey:id(1),roleLabel:"客服A"}],
  steps:[{stepKey:id(2),roleKey:id(1),body:"Hi\n中文",preSendDelayMs:30000,typingDurationMs:5000,postSendDelayMs:0}]};
describe("script editor portability and bridge security",()=>{
  it("accepts portable role keys without a local account ID",()=>{
    console.log("TIMING_VALIDATION_DIAGNOSTIC",{profile:isTimingProfile(doc.steps[0]), defaults:isTimingProfile({preSendDelayMs:30000,typingDurationMs:5000,postSendDelayMs:0}), keys:Object.keys(doc.steps[0]).sort().join(","),isEditorDoc:isEditorDoc(doc)});
    console.log("ROLE_VALIDATION_DIAGNOSTIC",{
      role:doc.roles[0].roleKey,step:doc.steps[0].stepKey,
      roleMatch:/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(doc.roles[0].roleKey),
      stepMatch:/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(doc.steps[0].stepKey),
      noSteps:isEditorDoc({...doc,steps:[]}),noRoles:isEditorDoc({...doc,roles:[],steps:[]}),
      stepOnly:isEditorDoc({...doc,roles:doc.roles,steps:[doc.steps[0]]})});
    expect(isEditorDoc(doc)).toBe(true);
    expect(isBridgeRequest(makeScriptRequest("scripts.save","script-1",
      {id:id(10),expectedRevision:1,document:doc}))).toBe(true);
    expect(isScriptSnapshot({scriptId:id(10),revision:2,document:doc,archived:false})).toBe(true);
    expect(doc.steps[0].preSendDelayMs+doc.steps[0].typingDurationMs).toBe(35000);
  });
  it("refuses credential and account identity fields in every edited step",()=>{
    const suspect={...doc,steps:[{...doc.steps[0],accountId:id(88)}]};
    expect(isEditorDoc(suspect)).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"scripts.save",
      payload:{id:id(10),expectedRevision:1,document:suspect}})).toBe(false);
    expect(isEditorDoc({...doc,session:"SECRET"})).toBe(false);
  });
  it("rejects stale revision, wrong save value and arbitrary direct message sends",()=>{
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"scripts.save",
      payload:{id:id(10),expectedRevision:0,document:doc}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"scripts.send",
      payload:{message:"unsolicited"}})).toBe(false);
    expect(isBridgeRequest({protocol:"mosen.desktop.v1",requestId:"x",type:"scripts.archive",
      payload:{id:id(10),expectedRevision:2,confirmation:"delete"}})).toBe(false);
  });
  it("renders honest offline preview without any active Telegram message action",()=>{
    const html=renderToStaticMarkup(<ScriptEditorPanel preferences={DEFAULT_PREFERENCES}/>);
    expect(html).toContain("可视化剧本编辑器");
    expect(html).toContain("仅本地草稿");
    expect(html).toContain("正在读取本地剧本");
    expect(html).not.toContain("立即发送");
  });
});
