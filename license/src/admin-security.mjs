// Admin protection for isolated Telegram licensing. Token, recovery code and TOTP
// material are never persisted or reflected in a log / HTTP error.
const encoder=new TextEncoder();
const HEX=/^[0-9a-f]{64}$/;
const TOKEN=/^[a-zA-Z0-9_-]{43}$/;
const CSRF=/^[a-zA-Z0-9_-]{43}$/;
const B32=/^[A-Z2-7]{32,128}$/;
const RED=/^[A-F0-9]{8}-[A-F0-9]{8}$/;
const SESSION_MS=15*60*1000;
function respond(data,status=200){return new Response(JSON.stringify(data),{
 status,headers:{"content-type":"application/json; charset=utf-8","cache-control":"no-store",
 "x-content-type-options":"nosniff","referrer-policy":"no-referrer"}});}
const denied=(status=403)=>respond({ok:false,code:"DENIED"},status);
const digest=async text=>Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256",encoder.encode(text))))
 .map(b=>b.toString(16).padStart(2,"0")).join("");
function random(){const buffer=crypto.getRandomValues(new Uint8Array(32));return btoa(String.fromCharCode(...buffer))
 .replaceAll("+","-").replaceAll("/","_").replace(/=+$/,"");}
async function hmac(secret,data){
 const key=await crypto.subtle.importKey("raw",encoder.encode(secret),{name:"HMAC",hash:"SHA-256"},false,["sign"]);
 const v=new Uint8Array(await crypto.subtle.sign("HMAC",key,encoder.encode(data)));
 return Array.from(v).map(b=>b.toString(16).padStart(2,"0")).join("");
}
export async function secureEquals(a,b){
 if(typeof a!=="string"||typeof b!=="string")return false;
 const aa=new Uint8Array(await crypto.subtle.digest("SHA-256",encoder.encode(a)));
 const bb=new Uint8Array(await crypto.subtle.digest("SHA-256",encoder.encode(b)));
 let diff=a.length===b.length?0:1;
 for(let i=0;i<32;i++)diff|=aa[i]^bb[i];
 return diff===0;
}
function decode32(raw){
 if(typeof raw!=="string"||!B32.test(raw))throw new Error("Invalid TOTP secret");
 let accumulator=0,bits=0;
 const result=[];
 for(const char of raw){accumulator=(accumulator<<5)|"ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".indexOf(char);
  bits+=5;if(bits>=8){bits-=8;result.push((accumulator>>>bits)&255);}}
 return new Uint8Array(result);
}
export async function totp(secret,step){
 const key=await crypto.subtle.importKey("raw",decode32(secret),{name:"HMAC",hash:"SHA-1"},false,["sign"]);
 const bytes=new Uint8Array(8);
 let n=BigInt(step);
 for(let i=7;i>=0;i--){bytes[i]=Number(n&255n);n>>=8n;}
 const raw=new Uint8Array(await crypto.subtle.sign("HMAC",key,bytes));
 const off=raw[raw.length-1]&15;
 const number=((raw[off]&127)<<24)|(raw[off+1]<<16)|(raw[off+2]<<8)|raw[off+3];
 return String(number%1000000).padStart(6,"0");
}
export function configured(env){
 return typeof env?.ADMIN_TOKEN==="string"&&env.ADMIN_TOKEN.length>=32 &&
 typeof env?.ADMIN_TOTP_SECRET==="string"&&B32.test(env.ADMIN_TOTP_SECRET) &&
 typeof env?.LICENSE_PEPPER==="string"&&env.LICENSE_PEPPER.length>=32;
}
async function audit(env,action,ok,now){
 try{await env.LICENSE_DB.prepare("INSERT INTO admin_security_audit(id,action,occurred_ms,ok) VALUES(?,?,?,?)")
 .bind(crypto.randomUUID(),action,now,ok?1:0).run();}catch{/* Audit failure must not log or expose secrets. */}
}
export async function rateLimit(env,request,scope,limit,intervalMs,now){
 // Only CF-Connecting-IP is trusted on production Workers, NEVER x-forwarded-for.
 const ip=request.headers.get("cf-connecting-ip")||"anonymous";
 const fingerprint=await hmac(env.LICENSE_PEPPER,"ms001-rate:"+scope+":"+ip.slice(0,128));
 const window=Math.floor(now/intervalMs)*intervalMs;
 await env.LICENSE_DB.prepare(`INSERT INTO admin_rate_limits(bucket_key,bucket_ms,hits)
 VALUES(?,?,1) ON CONFLICT(bucket_key,bucket_ms) DO UPDATE SET hits=hits+1`)
 .bind(fingerprint,window).run();
 const state=await env.LICENSE_DB.prepare("SELECT hits FROM admin_rate_limits WHERE bucket_key=? AND bucket_ms=?")
 .bind(fingerprint,window).first();
 const exceeded=!state||state.hits>limit;
 if(exceeded&&scope==="login")await audit(env,"rate_limited",false,now);
 return !exceeded;
}
function validOrigin(request){
 const origin=request.headers.get("origin");
 return !origin||origin===new URL(request.url).origin;
}
export async function login(env,request,body,now){
 if(!configured(env)||!validOrigin(request))return denied(403);
 if(!await rateLimit(env,request,"login",8,5*60*1000,now))return denied(429);
 const header=request.headers.get("authorization")||"";
 if(!header.startsWith("Bearer ")||header.length>1024||
   !await secureEquals(header.slice(7),env.ADMIN_TOKEN)){
  await audit(env,"login_denied",false,now);return denied(401);
 }
 const methods=Object.keys(body||{});
 if(methods.length!==1||!["totp","recoveryCode"].includes(methods[0])){
  await audit(env,"login_denied",false,now);return denied(400);
 }
 let accepted=false,recovered=false;
 if(methods[0]==="totp"&&typeof body.totp==="string"&&/^\d{6}$/.test(body.totp)){
  const step=Math.floor(now/30000);
  for(const offset of [-1,0,1]){
   const used=step+offset;
   if(used<0)continue;
   const code=await totp(env.ADMIN_TOTP_SECRET,used);
   if(await secureEquals(code,body.totp)){
    const r=await env.LICENSE_DB.prepare(
     "UPDATE admin_auth_state SET last_totp_step=? WHERE id=1 AND last_totp_step<?")
      .bind(used,used).run();
    accepted=Number(r.meta?.changes)===1;
    break;
   }
  }
 }
 if(methods[0]==="recoveryCode"&&typeof body.recoveryCode==="string"&&RED.test(body.recoveryCode)){
  const codeHash=await hmac(env.LICENSE_PEPPER,"MS001-RECOVERY:"+body.recoveryCode);
  const r=await env.LICENSE_DB.prepare("DELETE FROM admin_recovery WHERE code_digest=?")
    .bind(codeHash).run();
  accepted=Number(r.meta?.changes)===1;recovered=accepted;
 }
 if(!accepted){await audit(env,"login_denied",false,now);return denied(401);}
 const token=random(),csrf=random();
 const session=await env.LICENSE_DB.prepare(
  "SELECT epoch FROM admin_auth_state WHERE id=1").bind().first();
 await env.LICENSE_DB.prepare(`INSERT INTO admin_sessions
  (token_digest,csrf_digest,created_ms,expires_ms,auth_epoch,used_recovery)
  VALUES(?,?,?,?,?,?)`).bind(await digest(token),await digest(csrf),
   now,now+SESSION_MS,session.epoch,recovered?1:0).run();
 await audit(env,recovered?"login_recovery":"login_totp",true,now);
 return respond({ok:true,token,csrfToken:csrf,expiresMs:now+SESSION_MS});
}
export async function authorize(env,request,now){
 if(!configured(env)||!validOrigin(request))return null;
 if(!await rateLimit(env,request,"admin",90,60*1000,now))return null;
 const auth=request.headers.get("authorization")||"";
 const csrf=request.headers.get("x-csrf-token")||"";
 if(!auth.startsWith("Bearer ")||!TOKEN.test(auth.slice(7))||!CSRF.test(csrf))return null;
 const row=await env.LICENSE_DB.prepare(`SELECT s.csrf_digest,s.expires_ms,s.auth_epoch,a.epoch
  FROM admin_sessions s CROSS JOIN admin_auth_state a
  WHERE s.token_digest=? AND a.id=1`).bind(await digest(auth.slice(7))).first();
 if(!row||Number(row.expires_ms)<=now||row.auth_epoch!==row.epoch)return null;
 const csrfHash=await digest(csrf);
 if(!HEX.test(csrfHash)||!await secureEquals(csrfHash,row.csrf_digest))return null;
 return {tokenDigest:await digest(auth.slice(7)),expiresMs:row.expires_ms};
}
export async function rotateRecovery(env,now){
 const codes=Array.from({length:8},()=>{
  const bytes=crypto.getRandomValues(new Uint8Array(8));
  const hex=Array.from(bytes).map(n=>n.toString(16).padStart(2,"0")).join("").toUpperCase();
  return hex.slice(0,8)+"-"+hex.slice(8);
 });
 const statements=[
  env.LICENSE_DB.prepare("DELETE FROM admin_recovery").bind(),
  ...await Promise.all(codes.map(async code=>env.LICENSE_DB.prepare(
    "INSERT INTO admin_recovery(code_digest,created_ms) VALUES(?,?)")
     .bind(await hmac(env.LICENSE_PEPPER,"MS001-RECOVERY:"+code),now)))
 ];
 await env.LICENSE_DB.batch(statements);
 await audit(env,"recovery_rotated",true,now);
 return respond({ok:true,recoveryCodes:codes}); // displayed exactly once, never persisted plaintext
}
export async function logout(env,tokenDigest,now){
 await env.LICENSE_DB.prepare("DELETE FROM admin_sessions WHERE token_digest=?").bind(tokenDigest).run();
 await audit(env,"logout",true,now);return respond({ok:true});
}
export async function revokeSessions(env,now){
 // Epoch invalidation atomically makes every previous session stale.
 await env.LICENSE_DB.prepare("UPDATE admin_auth_state SET epoch=epoch+1 WHERE id=1").bind().run();
 await audit(env,"sessions_revoked",true,now);return respond({ok:true});
}
export async function readSecurityAudit(env,body){
 if(body===null||Object.keys(body).length!==2||!Number.isInteger(body.limit)||
  body.limit<1||body.limit>50||!Number.isInteger(body.beforeMs)||
  body.beforeMs<0)return denied(400);
 const {results}=await env.LICENSE_DB.prepare(
  "SELECT action,occurred_ms,ok FROM admin_security_audit WHERE occurred_ms<? ORDER BY occurred_ms DESC LIMIT ?")
  .bind(body.beforeMs||Number.MAX_SAFE_INTEGER,body.limit).all();
 return respond({ok:true,events:results.map(x=>({action:x.action,occurredMs:x.occurred_ms,ok:!!x.ok}))});
}
