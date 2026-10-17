import test from "node:test";
import assert from "node:assert/strict";
import {execFileSync,spawnSync} from "node:child_process";
import {readFileSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";
import {scanText,auditTrackedFiles} from "../../build/audit-repository-secrets.mjs";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../..");

test("detects high-confidence credential strings without reporting their content",()=>{
 const value=["ghp_","A".repeat(42)].join("");
 assert.deepEqual(scanText("example.txt",value),[{path:"example.txt",kind:"github-credential"}]);
 assert.deepEqual(scanText("example.txt",["-----BEGIN ","PRIVATE KEY-----"].join("")),
   [{path:"example.txt",kind:"private-key-block"}]);
 assert.deepEqual(scanText("sample","ADMIN_TOKEN = "+String.fromCharCode(34)+"definitely-real-looking-secret"+String.fromCharCode(34)),
   [{path:"sample",kind:"committed-worker-secret"}]);
 assert.equal(scanText("staging","database_id = "+String.fromCharCode(34)+"REPLACE_WITH_NEW_TELEGRAM_D1_DATABASE_ID"+String.fromCharCode(34)).length,0);
});
test("scans the checked-in repository without reporting secrets or tracking private keys",()=>{
 const paths=execFileSync("git",["ls-files","-z"],{cwd:root}).toString("utf8").split("\0").filter(Boolean);
 const {hits,scanned}=auditTrackedFiles(root,paths);
 assert.ok(scanned>100,"audit unexpectedly skipped most of the source repository");
 assert.deepEqual(hits,[],"sensitive tracked source or cryptographic material found");
});
test("manual draft release remains fail-closed until explicit human acceptance",()=>{
 const p=spawnSync(process.execPath,["build/check-release-readiness.mjs","--enforce"],{cwd:root,encoding:"utf8"});
 assert.notEqual(p.status,0,"unverified native and 72h physical tests must stop formal release");
 assert.match(p.stderr,/RELEASE BLOCKED/);
 const wf=readFileSync(resolve(root,".github/workflows/release.yml"),"utf8");
 assert.match(wf,/node build\/check-release-readiness\.mjs --enforce/);
 assert.match(wf,/environment: telegram-signed-release/);
 assert.match(wf,/--draft/);
});
test("tracked files may not include a private .env or signing key bundle",()=>{
 const {hits}=auditTrackedFiles(root,["license/.env.production","updater/private-key.pfx"]);
 assert.ok(hits.some(x=>x.kind==="tracked-private-file"&&x.path.includes(".env")));
 assert.ok(hits.some(x=>x.kind==="tracked-private-file"&&x.path.endsWith(".pfx")));
});
