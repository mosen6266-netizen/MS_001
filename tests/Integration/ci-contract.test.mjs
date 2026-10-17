import test from "node:test";
import assert from "node:assert/strict";
import {readFileSync} from "node:fs";
const read=path=>readFileSync(new URL(path,import.meta.url),"utf8");
const e2e=read("../E2E/Program.cs");
const project=read("../E2E/Mosen.GroupScheduler.E2E.Tests.csproj");
const workflow=read("../../.github/workflows/ci.yml");
const aggregator=read("./verify-ci-needs.mjs");
test("E2E traverses real offline SQLite, publication, permissions and durable scheduler",()=>{
 for(const api of ["PortableScriptPackage.Export","PortableScriptPackage.Decode","RoleRemarkResolver.Resolve",
  "EditorDraftRepository","PublishedScriptRepository","ScriptPreviewBuilder.Build",
  "PreflightPlanner.Check","SchedulerCheckpointStore","FairScheduler","PrepareRestore"])
   assert.ok(e2e.includes(api),"Missing E2E boundary "+api);
 for(const edge of ["E2E-02","E2E-06","E2E-11","E2E-13","E2E-18","E2E-21","E2E-26","E2E-30"])
   assert.ok(e2e.includes(edge),"Missing mandatory case "+edge);
 assert.match(project,/Mosen\.GroupScheduler\.Scheduler\.csproj/);
 assert.match(project,/Mosen\.GroupScheduler\.Scripts\.Editor\.csproj/);
 assert.match(project,/Mosen\.GroupScheduler\.Persistence\.csproj/);
 assert.doesNotMatch(project,/Telegram\.csproj|AccountAuth\.csproj/);
 assert.doesNotMatch(e2e,/SendMessageAsync|HttpClient|tdjson|TelegramClient/);
});
test("CI cancelled or skipped jobs cannot yield a green final gate",()=>{
 assert.match(aggregator,/result!=="success"/);
 assert.match(aggregator,/process\.exitCode=1/);
 assert.match(workflow,/needs:\s*\[[^\]]*windows-installer/);
 assert.match(workflow,/cancel-in-progress: true/);
});
