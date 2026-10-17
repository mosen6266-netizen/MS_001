import {execFileSync} from "node:child_process";
import {readFileSync,statSync} from "node:fs";
import {resolve,extname,relative,dirname} from "node:path";
import {fileURLToPath} from "node:url";

const allowedExt=new Set([".cs",".mjs",".js",".ts",".tsx",".json",".toml",".yml",".yaml",".md",".sql",".ps1",".iss",".xml",".props",".csproj"]);
const forbiddenFile=/(?:^|\/)(?:\.env(?:\..+)?|id_rsa|id_ed25519|[^/]+\.(?:pfx|p12|p8|pem|key|kdbx))$/i;
export const detectors=Object.freeze([
 {name:"private-key-block",pattern:/-----BEGIN (?:RSA |EC |OPENSSH |ENCRYPTED )?PRIVATE KEY-----/g},
 {name:"github-credential",pattern:/(?:gh[pousr]_[a-zA-Z0-9_]{36,}|github_pat_[a-zA-Z0-9_]{70,})/g},
 {name:"aws-access-key",pattern:/AKIA[0-9A-Z]{16}/g},
 {name:"google-api-key",pattern:/AIza[0-9A-Za-z_-]{35}/g},
 {name:"telegram-bot-secret",pattern:/\b[0-9]{8,10}:[A-Za-z0-9_-]{35}\b/g},
 {name:"slack-credential",pattern:/xox[baprs]-[A-Za-z0-9-]{20,}/g},
 {name:"committed-worker-secret",pattern:/^\s*(?:ADMIN_TOKEN|ADMIN_TOTP_SECRET|LICENSE_PEPPER|LICENSE_SIGNING_PKCS8)\s*=\s*["'][^"']{8,}["']/gm},
]);
export function scanText(path,source){
 const hits=[];
 for(const {name,pattern} of detectors){
    pattern.lastIndex=0;
    if(pattern.test(source))hits.push({path,kind:name});
 }
 return hits;
}
export function auditTrackedFiles(root,paths){
 const hits=[];
 let scanned=0;
 for(const path of paths){
   const canonical=path.replaceAll("\\","/");
   if(canonical.startsWith("node_modules/") || canonical.includes("/node_modules/") ||
      canonical.startsWith("artifacts/") || canonical.startsWith("reports/performance/step30-"))
      continue;
   if(forbiddenFile.test(canonical)){hits.push({path:canonical,kind:"tracked-private-file"});continue;}
   if(!allowedExt.has(extname(canonical).toLowerCase()))continue;
   const p=resolve(root,canonical);
   if(!relative(root,p).startsWith("..") && statSync(p).isFile()){
     if(statSync(p).size>2*1024*1024){hits.push({path:canonical,kind:"unexpected-large-source"});continue;}
     scanned++;hits.push(...scanText(canonical,readFileSync(p,"utf8")));
   }
 }
 return {hits,scanned};
}
const invoked=process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url);
if(invoked){
 const root=resolve(dirname(fileURLToPath(import.meta.url)),"..");
 const tracked=execFileSync("git",["ls-files","-z"],{cwd:root,maxBuffer:8*1024*1024}).toString("utf8").split("\0").filter(Boolean);
 const report=auditTrackedFiles(root,tracked);
 if(report.hits.length){
   // Never print actual detected secret value or surrounding source.
   for(const {path,kind} of report.hits)console.error("SECURITY BLOCKER "+kind+" in "+path);
   console.error("Release source audit failed; remove and rotate exposed secrets before attempting release.");
   process.exitCode=1;
 }else console.log("Tracked-source high-confidence secret audit PASS:",report.scanned,"text files checked (not a history scan).");
}
