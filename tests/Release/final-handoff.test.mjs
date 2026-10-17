import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,existsSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";

const root=resolve(dirname(fileURLToPath(import.meta.url)),"../..");
const load=x=>readFileSync(resolve(root,x),"utf8");
const requiredDocs=[
 "docs/USER_GUIDE.md","docs/TROUBLESHOOTING.md","docs/RECOVERY_GUIDE.md",
 "docs/FINAL_ACCEPTANCE.md","docs/RELEASE_CHECKLIST.md",
 "releases/V1.0/README.md","releases/V1.0/RELEASE_NOTES.md",
 "releases/V1.0/RELEASE_SIGNOFF.md","releases/V1.0/ARTIFACTS.md"
];
test("V1.0 development handoff includes every core manual and no misleading published claim",()=>{
 for(const path of requiredDocs){
  assert.ok(existsSync(resolve(root,path)),path+" missing");
  assert.ok(load(path).length>350,path+" unexpectedly empty");
 }
 assert.match(load("docs/USER_GUIDE.md"),/30秒/);
 assert.match(load("docs/USER_GUIDE.md"),/35秒/);
 assert.match(load("docs/USER_GUIDE.md"),/发送后等待/);
 assert.match(load("docs/RECOVERY_GUIDE.md"),/PrepareRestore/);
 assert.match(load("docs/RECOVERY_GUIDE.md"),/不.*直接.*覆盖|不自动覆盖|不直接替换|不能.*覆盖/);
 assert.match(load("releases/V1.0/README.md"),/BLOCKED/);
 assert.match(load("releases/V1.0/RELEASE_NOTES.md"),/NOT PUBLISHED/);
 assert.match(load("docs/FINAL_ACCEPTANCE.md"),/13项|13\/13/);
});
test("development package never includes EXE, secrets or changes real approval",()=>{
 const readiness=JSON.parse(load("release/readiness.json"));
 assert.equal(readiness.releaseAuthorized,false);
 assert.equal(readiness.requirements.length,13);
 assert.ok(readiness.requirements.every(x=>x.status==="DEFERRED"));
 const script=load("build/package-development-handoff.py");
 assert.match(script,/containsInstaller.*False/);
 assert.match(script,/containsCredentials.*False/);
 assert.match(script,/releaseAuthorized.*False/);
 assert.match(script,/check_blockers\(\)/);
 assert.doesNotMatch(script,/subprocess.*powershell|gh release create/);
 assert.equal(load("updater/trust/release-v1.spki.base64").trim(),"UNCONFIGURED");
 assert.equal(load("updater/trust/publisher-thumbprint.txt").trim(),"UNCONFIGURED");
});
test("document package CI must upload evidence and final aggregate require it",()=>{
 const workflow=load(".github/workflows/ci.yml");
 assert.match(workflow,/\n  handoff-docs:/);
 assert.match(workflow,/python build\/package-development-handoff\.py --verify/);
 assert.match(workflow,/v1-development-handoff/);
 assert.match(workflow,/node --test tests\/Release\/final-handoff\.test\.mjs/);
 assert.match(workflow,/needs: \[[^\]]*handoff-docs\]/);
 assert.match(workflow,/REGRESSION_REQUIRED_JOB_IDS:[^\n]*handoff-docs/);
});
test("user documentation traces to real automatic checks but flags native tests as deferred",()=>{
 const notes=load("releases/V1.0/RELEASE_NOTES.md");
 const acceptance=load("docs/FINAL_ACCEPTANCE.md");
 assert.match(notes,/39032907487/);
 assert.match(notes,/39033877609/);
 assert.match(notes,/39035133383/);
 assert.match(acceptance,/72小时/);
 assert.match(acceptance,/Cloudflare/);
 assert.match(acceptance,/Win10|Windows10/);
 assert.match(acceptance,/Win11|Windows11/);
 assert.match(acceptance,/NOT PUBLISHED|BLOCKED/);
});
