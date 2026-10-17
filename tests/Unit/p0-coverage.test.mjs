import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync,existsSync} from "node:fs";
import {resolve,dirname} from "node:path";
import {fileURLToPath} from "node:url";
const root=resolve(dirname(fileURLToPath(import.meta.url)),"../..");
const read=path=>readFileSync(resolve(root,path),"utf8");
const source=JSON.parse(read("tests/Unit/p0-regression-catalog.json"));
const ci=read(".github/workflows/ci.yml");
const jobs=new Set([...ci.matchAll(/^  ([a-z][a-z0-9-]+):\s*$/gm)].map(m=>m[1]));
test("P0 registry uses unique ids, executable evidence and explicit offline-only scope",()=>{
 assert.equal(source.schemaVersion,1);
 assert.equal(source.mode,"offline-simulation-only");
 assert.match(source.statement,/not real Telegram/i);
 assert.ok(source.cases.length>=30,"30+ separately traceable P0 requirements");
 assert.equal(new Set(source.cases.map(x=>x.id)).size,source.cases.length);
 for(const row of source.cases){
  assert.match(row.id,/^P0-\d{2}$/);
  assert.ok(typeof row.description==="string"&&row.description.trim().length>=4,row.id);
  assert.match(row.testPath,/^(tests|apps\/ui\/src|license\/tests)\/[a-zA-Z0-9./_-]+$/);
  assert.ok(!row.testPath.split("/").includes(".."),row.id+" traversal");
  assert.ok(existsSync(resolve(root,row.testPath)),row.id+": missing "+row.testPath);
  assert.ok(jobs.has(row.ciJob),row.id+": CI job "+row.ciJob+" missing");
  assert.match(read(row.testPath),/\b(Check|Reject|Assert|test|describe|it|expect|assert)\b/,
   row.id+": mapped file does not contain executable assertions");
 }
});
test("critical P0 business and safety categories remain covered",()=>{
 const required=["migrations","security","tdlib-adapter","authorization","group-catalog",
 "script-timing-media","script-portable","script-preflight","virtual-scheduler",
 "outbound-policy","delivery-dedup","recovery-safety","notifications-stop",
 "telegram-license-worker","telegram-license-client","installer-data","updater-security",
 "ui","p0-e2e"];
 for(const job of required)
  assert.ok(source.cases.some(x=>x.ciJob===job),"Uncovered domain: "+job);
});
test("final CI gate includes P0 E2E plus full upstream job set",()=>{
 assert.ok(jobs.has("p0-e2e")&&jobs.has("regression-gate"));
 assert.match(ci,/name: Step29 cross-feature offline E2E/);
 assert.match(ci,/node --test tests\/Unit\/p0-coverage\.test\.mjs/);
 assert.match(ci,/node --test [^\n]*tests\/Integration\/ci-contract\.test\.mjs/);
 assert.match(ci,/needs: \[.*p0-e2e.*\]/);
 assert.match(ci,/REGRESSION_NEEDS_JSON: \$\{\{ toJson\(needs\) \}\}/);
 assert.match(ci,/if: always\(\)/);
 assert.match(ci,/node tests\/Integration\/verify-ci-needs\.mjs/);
});
