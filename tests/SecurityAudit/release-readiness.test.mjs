import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
import {fileURLToPath} from "node:url";
import {resolve,dirname} from "node:path";
import {evaluateReadiness,REQUIRED_RELEASE_CHECKS} from "../../build/check-release-readiness.mjs";

const root=resolve(dirname(fileURLToPath(import.meta.url)),"../..");
const current=JSON.parse(readFileSync(resolve(root,"release/readiness.json"),"utf8"));
const approveDate="2026-10-17T12:00:00Z";
const example="docs/FINAL_ACCEPTANCE.md#manual-validation";

test("production readiness is explicitly blocked pending Win10/11, native TDLib and physical 72h validation",()=>{
 const errors=evaluateReadiness(current);
 assert.equal(current.releaseAuthorized,false);
 assert.equal(current.requirements.length,13);
 for(const id of REQUIRED_RELEASE_CHECKS){
    assert.ok(errors.some(x=>x.includes(id)),"missing blocker "+id);
 }
 assert.ok(errors.length>=14);
});
test("unknown, duplicate or deleted gates cannot reduce minimum release requirements",()=>{
 const ok=approvedFixture();
 assert.deepEqual(evaluateReadiness(ok),[]);
 assert.ok(evaluateReadiness({...ok,requirements:ok.requirements.slice(0,-1)}).length>0);
 assert.ok(evaluateReadiness({...ok,requirements:[...ok.requirements.slice(1),ok.requirements[1]]}).length>0);
 assert.ok(evaluateReadiness({...ok,requirements:[...ok.requirements,{id:"skippable",status:"VERIFIED"}]}).length>0);
});
test("human review, explicit approval and evidence may not be omitted",()=>{
 const ok=approvedFixture();
 assert.ok(evaluateReadiness({...ok,releaseAuthorized:false}).length>0);
 assert.ok(evaluateReadiness({...ok,approval:{...ok.approval,reviewer:""}}).length>0);
 assert.ok(evaluateReadiness({...ok,approval:{...ok.approval,approvedAtUtc:"never"}}).length>0);
 assert.ok(evaluateReadiness({...ok,requirements:ok.requirements.map((r,i)=>i===0?{...r,status:"DEFERRED"}:r)}).length>0);
 assert.ok(evaluateReadiness({...ok,requirements:ok.requirements.map((r,i)=>i===1?{...r,evidenceRef:"https://evil.test/claims"}:r)}).length>0);
});
test("independent GitHub Release gate runs before signing and only drafts after separate permission review",()=>{
 const wf=readFileSync(resolve(root,".github/workflows/release.yml"),"utf8");
 const first=wf.indexOf("node build/check-release-readiness.mjs --enforce");
 const sign=wf.indexOf("Refuse missing signing secrets");
 assert.ok(first>=0&&first<sign,"readiness must be checked before credentials/signing");
 assert.match(wf,/publish-draft:[\s\S]*?environment: telegram-signed-release/);
 assert.match(wf,/publish-draft:[\s\S]*?contents: write/);
 assert.match(wf,/--draft/);
 assert.doesNotMatch(wf,/\n  push:/);
});
function approvedFixture(){
 return {...current,releaseAuthorized:true,
  approval:{reviewer:"Independent reviewer",approvedAtUtc:approveDate,approvalEvidence:example},
  requirements:REQUIRED_RELEASE_CHECKS.map(id=>({id,status:"VERIFIED",evidenceRef:example,
    reviewedBy:"Independent reviewer",reviewedAtUtc:approveDate}))
 };
}
