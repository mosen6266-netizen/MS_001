// Fail closed if any required job is missing, failed, cancelled, timed out or skipped.
// Called only in GitHub Actions with upstream needs metadata.
const raw=process.env.REGRESSION_NEEDS_JSON;
if(!raw){console.error("Missing GitHub Actions needs JSON; refusing green gate.");process.exitCode=1;}
else{
 let parsed;
 try{parsed=JSON.parse(raw);}catch{parsed=null;}
 const expected=new Set((process.env.REGRESSION_REQUIRED_JOB_IDS??"").split(",").filter(Boolean));
 const got=new Set(Object.keys(parsed??{}));
 const missing=[...expected].filter(id=>!got.has(id));
 const extra=[...got].filter(id=>!expected.has(id));
 const bad=Object.entries(parsed??{}).filter(([_,entry])=>entry?.result!=="success")
  .map(([id,entry])=>id+": "+(entry?.result??"missing"));
 if(expected.size===0||missing.length||extra.length||bad.length){
  console.error("Step29 aggregate P0 gate FAILED",JSON.stringify({missing,extra,bad}));
  process.exitCode=1;
 }else console.log("Step29 aggregate P0 gate PASS:",expected.size,"required upstream jobs ALL success");
}
