import test from "node:test";
import assert from "node:assert/strict";
import {existsSync,readFileSync} from "node:fs";
import {resolve} from "node:path";
const file=resolve(process.env.MS001_PERF_REPORT??"reports/performance/step30-local-report.json");
test("Step30 benchmark report is measured and distinguishes synthetic from real",()=>{
 assert.ok(existsSync(file),"No benchmark report present");
 const report=JSON.parse(readFileSync(file,"utf8"));
 assert.equal(report.schemaVersion,1);
 assert.equal(report.kind,"step30-development-synthetic-capacity");
 assert.equal(report.outcome,"synthetic-ci-pass");
 assert.equal(report.scenario.registeredAccountMetadata,300);
 assert.equal(report.scenario.simulatedGroups,50);
 assert.equal(report.scenario.mappings,300);
 assert.equal(report.scenario.simulatedTelegramClientsOpened,0);
 assert.equal(report.scenario.authorizedLiveAccounts,0);
 assert.equal(report.scenario.simulatedExternalMessages,0);
 assert.equal(report.scenario.logicalHours,72);
 assert.equal(report.scenario.logicalResourceSamples,4320);
 assert.ok(report.scenario.measuredWallSeconds>0&&report.scenario.measuredWallSeconds<72*3600,
    "72h must be labelled accelerated, not claimed physical time");
 assert.ok(report.machine.os&&report.machine.runtime&&report.machine.logicalCpuCount>0);
 assert.ok(report.observations.assertionsPassed>=18);
 assert.ok(report.observations.sqliteMainFileBytes>0);
 assert.equal(report.observations.syntheticFaultsInjected,2);
 assert.equal(report.observations.automaticRetries,0);
 assert.equal(report.observations.exceptionCount,0);
 assert.ok(report.observations.measuredWorkingSetPeakMiB>0);
 assert.ok(report.observations.samples.length>=4);
 for(const p of report.observations.samples){
   assert.ok(p.phase&&p.wallElapsedMs>=0);
   assert.ok(p.workingSetMiB>0&&p.managedHeapMiB>=0);
   assert.ok(p.threads>=1);
   assert.ok(p.cpuPercentSincePreviousSample>=0&&p.cpuPercentSincePreviousSample<=100);
   assert.ok(p.handleCount===null||p.handleCount>=0);
 }
 assert.ok(report.limitations.some(x=>x.includes("not 72h elapsed")));
 assert.ok(report.limitations.some(x=>x.includes("0 native TDLib")));
 assert.ok(report.limitations.some(x=>x.includes("no Telegram")));
});
