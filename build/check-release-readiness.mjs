import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { resolve, dirname } from "node:path";

export const REQUIRED_RELEASE_CHECKS = Object.freeze([
 "win10_clean_install_upgrade_uninstall",
 "win11_clean_install_upgrade_uninstall",
 "native_tdlib_verified_binary",
 "real_owned_account_auth_and_restart",
 "authorized_group_preflight_and_outbound_protection",
 "real_50_group_stability",
 "real_300_account_resource_capacity",
 "real_72h_wall_clock_soak",
 "power_loss_sqlite_restore",
 "windows_signed_setup_update_and_rollback",
 "cloudflare_independent_license_prod",
 "third_party_license_trademark_review",
 "human_final_acceptance"
]);

// This is not proof that the referenced real-world tests happened.
// The issuer must also review evidence and the environment reviewer must approve.
export function evaluateReadiness(record){
 const blockers=[];
 if(!record || typeof record!=="object") return ["Readiness manifest absent"];
 if(record.schemaVersion!==1 || record.product!=="MOSEN_TELEGRAM_SCHEDULER")
    blockers.push("Product/schema mismatch");
 if(record.releaseAuthorized!==true)blockers.push("Human release authorization not granted");
 if(typeof record.releaseCandidate!=="string" ||
   !/^[0-9]{1,5}\.[0-9]{1,5}\.[0-9]{1,5}$/.test(record.releaseCandidate))
    blockers.push("Invalid release candidate");
 const approval=record.approval;
 if(!approval||typeof approval.reviewer!=="string"||approval.reviewer.trim().length<3 ||
   !isUtcDate(approval.approvedAtUtc) || !evidence(approval.approvalEvidence))
    blockers.push("Missing final human sign-off evidence");
 if(!Array.isArray(record.requirements)){
    blockers.push("Release requirements missing");
    return blockers;
 }
 const ids=record.requirements.map(x=>x?.id);
 if(ids.length!==REQUIRED_RELEASE_CHECKS.length ||
    new Set(ids).size!==ids.length)
    blockers.push("Incomplete, duplicated or unexpected release requirements");
 for(const id of REQUIRED_RELEASE_CHECKS){
    const r=record.requirements.find(x=>x?.id===id);
    if(!r||r.status!=="VERIFIED" || !evidence(r.evidenceRef) ||
      !isUtcDate(r.reviewedAtUtc) ||
      typeof r.reviewedBy!=="string"||r.reviewedBy.trim().length<3)
       blockers.push("Release blocker pending: "+id);
 }
 for(const id of ids)if(!REQUIRED_RELEASE_CHECKS.includes(id))
    blockers.push("Unexpected release requirement: "+String(id));
 return blockers;
}
function isUtcDate(x){
 if(typeof x!=="string" || !/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?Z$/.test(x))return false;
 const dt=new Date(x);
 return Number.isFinite(dt.getTime()) && dt.getTime()>=Date.UTC(2026,0,1);
}
function evidence(x){
 if(typeof x!=="string" || x.trim().length<12 || x.length>1024)return false;
 return /^docs\/[a-zA-Z0-9/_-]+\.md(?:#[a-zA-Z0-9_-]+)?$/.test(x) ||
   /^https:\/\/github\.com\/mosen6266-netizen\/MS_001\/actions\/runs\/[0-9]{8,}$/.test(x);
}
const invoked=process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url);
if(invoked){
 const root=resolve(dirname(fileURLToPath(import.meta.url)),"..");
 let document;
 try{document=JSON.parse(readFileSync(resolve(root,"release/readiness.json"),"utf8"));}
 catch{console.error("RELEASE BLOCKED: readiness manifest unreadable");process.exitCode=1;}
 if(document){
   const failures=evaluateReadiness(document);
   if(failures.length){
     console.error("RELEASE BLOCKED: "+failures.length+" unmet approval or evidence requirements");
     for(const f of failures)console.error("- "+f);
     process.exitCode=1;
   }else {
     if(process.argv[2]!=="--enforce") {
       console.error("RELEASE BLOCKED: explicit --enforce required");
       process.exitCode=1;
     } else console.log("Release metadata gate passed; signed artifacts and protected environment approval must still be independently verified.");
   }
 }
}
