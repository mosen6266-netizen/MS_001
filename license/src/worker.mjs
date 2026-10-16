// MS_001 Telegram ONLY. No Signal product IDs, bindings, URLs or secrets.
export const PRODUCT = "MOSEN_TELEGRAM_SCHEDULER";
const MAX_BODY = 2048;
const MAX_DURATION = 1825 * 86400;
const DEVICE_RE = /^[0-9a-f]{64}$/;
const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
const CARD_RE = /^TG-[A-F0-9]{8}-[A-F0-9]{8}-[A-F0-9]{8}-[A-F0-9]{8}$/;
const NONCE_RE = /^[0-9a-f]{32}$/;
const encoder = new TextEncoder();
const json = (body,status=200)=>new Response(JSON.stringify(body),{status,headers:{
  "content-type":"application/json; charset=utf-8","cache-control":"no-store",
  "x-content-type-options":"nosniff","referrer-policy":"no-referrer"}});
const denied = (status=403)=>json({ok:false,code:"DENIED"},status);
function environment(env){
  return env?.PRODUCT_ID===PRODUCT && env?.LICENSE_DB &&
   typeof env?.LICENSE_PEPPER==="string" && env.LICENSE_PEPPER.length>=32 &&
   typeof env?.ADMIN_TOKEN==="string" && env.ADMIN_TOKEN.length>=32 &&
   typeof env?.LICENSE_SIGNING_PKCS8==="string" && env.LICENSE_SIGNING_PKCS8.length>50;
}
async function payload(request){
  if(request.headers.get("content-type")?.split(";")[0]?.trim().toLowerCase()!=="application/json")return null;
  const length=Number(request.headers.get("content-length")||"0");
  if(!Number.isFinite(length)||length>MAX_BODY)return null;
  const raw=await request.text();
  if(raw.length>MAX_BODY)return null;
  try{const item=JSON.parse(raw);return item && typeof item==="object" && !Array.isArray(item)?item:null;}
  catch{return null;}
}
function exact(obj,keys){return obj && Object.keys(obj).length===keys.length &&
   keys.every(k=>Object.hasOwn(obj,k));}
function bytesToHex(bytes){return [...bytes].map(x=>x.toString(16).padStart(2,"0")).join("");}
function b64url(buffer){return btoa(String.fromCharCode(...new Uint8Array(buffer)))
    .replaceAll("+","-").replaceAll("/","_").replace(/=+$/,"");}
function pkcs8Decode(value){const binary=atob(value.replace(/-/g,"+").replace(/_/g,"/"));
 return Uint8Array.from(binary,c=>c.charCodeAt(0));}
export async function digestCard(card,pepper){
 const key=await crypto.subtle.importKey("raw",encoder.encode(pepper),{name:"HMAC",hash:"SHA-256"},false,["sign"]);
 return bytesToHex(new Uint8Array(await crypto.subtle.sign("HMAC",key,encoder.encode(card))));
}
async function adminOk(request,env){
 const key=request.headers.get("authorization");
 if(!key?.startsWith("Bearer ")||key.length>1024)return false;
 // HMAC comparison of equally-sized constant-length digests avoids string-compare timing leaks.
 const hash=async value=>new Uint8Array(await crypto.subtle.digest("SHA-256",encoder.encode(value)));
 const a=await hash(key.slice(7)),b=await hash(env.ADMIN_TOKEN);
 let difference=0;for(let i=0;i<32;i++)difference|=a[i]^b[i];
 return difference===0;
}
function newCode(){
 const bytes=new Uint8Array(16);crypto.getRandomValues(bytes);
 const s=bytesToHex(bytes).toUpperCase();
 return "TG-"+s.match(/.{8}/g).join("-");
}
function legalSeconds(days){return Number.isSafeInteger(days)&&days>=1&&days<=1825?days*86400:null;}
function validId(id){return typeof id==="string"&&UUID_RE.test(id);}
async function signLease(env, license,device,nonce,now){
 const expires=Number(license.expires_ms);
 const leaseUntil=Math.min(expires,now+60*60*1000);
 const status=license.disabled?"disabled":now>=expires?"expired":"valid";
 const claims={productId:PRODUCT,licenseId:license.id,deviceHash:device,
   expiresMs:expires,checkedMs:now,leaseUntilMs:leaseUntil,
   version:license.version,status,nonce};
 const canonical=Object.values(claims).join("\n");
 const key=await crypto.subtle.importKey("pkcs8",pkcs8Decode(env.LICENSE_SIGNING_PKCS8),
   {name:"ECDSA",namedCurve:"P-256"},false,["sign"]);
 const signature=await crypto.subtle.sign({name:"ECDSA",hash:"SHA-256"},key,encoder.encode(canonical));
 return json({ok:status==="valid",claims,signature:b64url(signature),keyId:env.LICENSE_SIGNING_KID||"tg-v1"},status==="valid"?200:403);
}
async function issue(env,body,now){
 if(!exact(body,["durationDays","maxDevices"])||!legalSeconds(body.durationDays)||
  !Number.isInteger(body.maxDevices)||body.maxDevices<1||body.maxDevices>5)return denied(400);
 const code=newCode(),id=crypto.randomUUID(),hash=await digestCard(code,env.LICENSE_PEPPER);
 await env.LICENSE_DB.batch([
  env.LICENSE_DB.prepare("INSERT INTO licenses(id,card_hmac,product_id,duration_seconds,max_devices,created_ms) VALUES(?,?,?,?,?,?)")
    .bind(id,hash,PRODUCT,legalSeconds(body.durationDays),body.maxDevices,now),
  env.LICENSE_DB.prepare("INSERT INTO license_admin_audit(id,event_type,license_id,created_ms) VALUES(?,?,?,?)")
    .bind(crypto.randomUUID(),"issued",id,now)
 ]);
 return json({ok:true,licenseId:id,code,durationDays:body.durationDays,maxDevices:body.maxDevices},201);
}
async function renew(env,body,now){
 if(!exact(body,["licenseId","operationId","durationDays"])||!validId(body.licenseId)||
  !validId(body.operationId)||!legalSeconds(body.durationDays))return denied(400);
 const seconds=legalSeconds(body.durationDays);
 const old=await env.LICENSE_DB.prepare("SELECT license_id,duration_seconds,applied,result_expires_ms FROM license_renewals WHERE operation_id=?")
  .bind(body.operationId).first();
 if(old&&(old.license_id!==body.licenseId||Number(old.duration_seconds)!==seconds))return denied(409);
 const license=await env.LICENSE_DB.prepare("SELECT id FROM licenses WHERE id=? AND product_id=?")
  .bind(body.licenseId,PRODUCT).first();
 if(!license)return denied(404);
 // D1 batch runs atomically. Renewal row flag prevents two concurrent identical requests
 // from adding duration twice; no operation ID is permitted to target a different license.
 await env.LICENSE_DB.batch([
  env.LICENSE_DB.prepare("INSERT OR IGNORE INTO license_renewals(operation_id,license_id,duration_seconds,created_ms) VALUES(?,?,?,?)")
   .bind(body.operationId,body.licenseId,seconds,now),
  env.LICENSE_DB.prepare(`UPDATE licenses SET
      duration_seconds=CASE WHEN activated_ms IS NULL THEN duration_seconds+? ELSE duration_seconds END,
      expires_ms=CASE WHEN activated_ms IS NULL THEN NULL ELSE MAX(expires_ms,?)+? END,
      version=version+1
    WHERE id=? AND product_id=? AND EXISTS(SELECT 1 FROM license_renewals
      WHERE operation_id=? AND license_id=? AND duration_seconds=? AND applied=0)`)
   .bind(seconds,now,seconds*1000,body.licenseId,PRODUCT,body.operationId,body.licenseId,seconds),
  env.LICENSE_DB.prepare("UPDATE license_renewals SET applied=1, result_expires_ms=(SELECT expires_ms FROM licenses WHERE id=?) WHERE operation_id=? AND license_id=? AND duration_seconds=? AND applied=0")
   .bind(body.licenseId,body.operationId,body.licenseId,seconds)
 ]);
 const latest=await env.LICENSE_DB.prepare("SELECT applied,result_expires_ms FROM license_renewals WHERE operation_id=?")
   .bind(body.operationId).first();
 if(!latest?.applied)return denied(409);
 // Audit once per logical renewal ID.
 await env.LICENSE_DB.prepare("INSERT OR IGNORE INTO license_admin_audit(id,event_type,license_id,created_ms) VALUES(?,?,?,?)")
   .bind(body.operationId,"renewed",body.licenseId,now).run();
 return json({ok:true,licenseId:body.licenseId,expiresMs:latest.result_expires_ms});
}
async function disable(env,body,now){
 if(!exact(body,["licenseId","disabled"])||!validId(body.licenseId)||typeof body.disabled!=="boolean")return denied(400);
 const update=await env.LICENSE_DB.prepare("UPDATE licenses SET disabled=?,version=version+1 WHERE id=? AND product_id=? AND disabled<>?")
  .bind(body.disabled?1:0,body.licenseId,PRODUCT,body.disabled?1:0).run();
 if(update.meta.changes)
  await env.LICENSE_DB.prepare("INSERT INTO license_admin_audit(id,event_type,license_id,created_ms) VALUES(?,?,?,?)")
   .bind(crypto.randomUUID(),body.disabled?"disabled":"enabled",body.licenseId,now).run();
 const row=await env.LICENSE_DB.prepare("SELECT disabled,expires_ms,version FROM licenses WHERE id=? AND product_id=?")
   .bind(body.licenseId,PRODUCT).first();
 return row?json({ok:true,disabled:Boolean(row.disabled),expiresMs:row.expires_ms,version:row.version}):denied(404);
}
async function client(env,body,now,activate){
 if(!exact(body,["card","deviceHash","nonce"])||typeof body.card!=="string"||
    !CARD_RE.test(body.card)||typeof body.deviceHash!=="string"||!DEVICE_RE.test(body.deviceHash)||
    typeof body.nonce!=="string"||!NONCE_RE.test(body.nonce))return denied(400);
 const digest=await digestCard(body.card,env.LICENSE_PEPPER);
 let license=await env.LICENSE_DB.prepare("SELECT * FROM licenses WHERE card_hmac=? AND product_id=?")
   .bind(digest,PRODUCT).first();
 // Planned pepper rotation: issue with current pepper; check old cards with the
 // previous Secret until their card_hmac values are migrated. No plaintext stored.
 if(!license&&typeof env.LICENSE_PEPPER_PREVIOUS==="string"&&
    env.LICENSE_PEPPER_PREVIOUS.length>=32&&env.LICENSE_PEPPER_PREVIOUS!==env.LICENSE_PEPPER){
   const older=await digestCard(body.card,env.LICENSE_PEPPER_PREVIOUS);
   license=await env.LICENSE_DB.prepare("SELECT * FROM licenses WHERE card_hmac=? AND product_id=?")
    .bind(older,PRODUCT).first();
 }
 if(!license||license.disabled)return denied();
 if(activate && license.activated_ms===null){
  await env.LICENSE_DB.prepare("UPDATE licenses SET activated_ms=?,expires_ms=? WHERE id=? AND activated_ms IS NULL AND disabled=0")
    .bind(now,now+Number(license.duration_seconds)*1000,license.id).run();
 }
 license=await env.LICENSE_DB.prepare("SELECT * FROM licenses WHERE id=?").bind(license.id).first();
 if(license.disabled||license.expires_ms===null||now>=Number(license.expires_ms))return denied();
 if(activate){
  // One SQLite atomic conditional insert; LIMIT is read in same write statement.
  await env.LICENSE_DB.prepare(`INSERT INTO license_devices(license_id,device_hash,first_seen_ms,last_seen_ms)
    SELECT id,?,?,? FROM licenses WHERE id=? AND disabled=0 AND expires_ms>?
      AND (SELECT COUNT(*) FROM license_devices WHERE license_id=?)<max_devices
    ON CONFLICT(license_id,device_hash) DO UPDATE SET last_seen_ms=excluded.last_seen_ms`)
    .bind(body.deviceHash,now,now,license.id,now,license.id).run();
 }
 const bound=await env.LICENSE_DB.prepare("SELECT 1 AS found FROM license_devices WHERE license_id=? AND device_hash=?")
   .bind(license.id,body.deviceHash).first();
 if(!bound)return denied();
 return signLease(env,license,body.deviceHash,body.nonce,now);
}
export default {
 async fetch(request,env){
  try{
   if(!environment(env))return denied(503);
   const url=new URL(request.url);
   if(url.protocol!=="https:" && url.hostname!=="localhost" && url.hostname!=="127.0.0.1")return denied(403);
   if(request.method!=="POST"||!url.pathname.startsWith("/v1/"))return denied(404);
   const body=await payload(request);if(!body)return denied(400);
   const now=Date.now();
   if(url.pathname.startsWith("/v1/admin/")){
    if(!await adminOk(request,env))return denied(401);
    if(url.pathname==="/v1/admin/cards")return issue(env,body,now);
    if(url.pathname==="/v1/admin/renew")return renew(env,body,now);
    if(url.pathname==="/v1/admin/disable")return disable(env,body,now);
    return denied(404);
   }
   if(url.pathname==="/v1/license/activate")return client(env,body,now,true);
   if(url.pathname==="/v1/license/check")return client(env,body,now,false);
   return denied(404);
  }catch{ // No DB driver errors, token or sensitive data in HTTP responses or logs.
   return denied(503);
  }
 }
};
