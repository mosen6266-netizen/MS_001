import test from "node:test";
import assert from "node:assert/strict";
import {DatabaseSync} from "node:sqlite";
import {readFileSync} from "node:fs";
import {webcrypto} from "node:crypto";
import worker,{digestCard,PRODUCT} from "../src/worker.mjs";

globalThis.crypto=webcrypto;
const schema=readFileSync(new URL("../migrations/0001_license.sql",import.meta.url),"utf8");

class FakeD1 {
 constructor(){this.db=new DatabaseSync(":memory:");this.db.exec("PRAGMA foreign_keys=ON;"+schema);this.tail=Promise.resolve();}
 prepare(sql){const db=this.db;return {bind(...params){
  return {async first(){return db.prepare(sql).get(...params)??null;},
   async run(){const r=db.prepare(sql).run(...params);return {success:true,meta:{changes:r.changes}};}};
 }};}
 async batch(actions){
  const execute=async()=>{
    this.db.exec("BEGIN IMMEDIATE;");
    try{const result=[];for(const a of actions)result.push(await a.run());
      this.db.exec("COMMIT;");return result;}
    catch(e){this.db.exec("ROLLBACK;");throw e;}
  };
  const work=this.tail.then(execute);
  this.tail=work.catch(()=>{});
  return work;
 }
 count(sql){return Number(this.db.prepare(sql).get().n);}
 close(){this.db.close();}
}
const json=async res=>{const data=await res.json();return {data,status:res.status};};
function req(path,body,authorization){
 const headers={"content-type":"application/json"};
 if(authorization!==undefined)headers.authorization="Bearer "+authorization;
 return new Request("https://license-telegram.test"+path,{method:"POST",headers,body:JSON.stringify(body)});
}
function device(ch){return ch.repeat(64);}
function nonce(ch){return ch.repeat(32);}
async function createEnv(){
 const pair=await crypto.subtle.generateKey({name:"ECDSA",namedCurve:"P-256"},true,["sign","verify"]);
 const pkcs8=Buffer.from(await crypto.subtle.exportKey("pkcs8",pair.privateKey)).toString("base64");
 return {LICENSE_DB:new FakeD1(),PRODUCT_ID:PRODUCT,ENVIRONMENT:"staging",
  LICENSE_PEPPER:"isolated-telegram-pepper-"+("a".repeat(50)),
  LICENSE_SIGNING_PKCS8:pkcs8,
  ADMIN_TOKEN:"only-telegram-admin-token-"+("Z".repeat(48)),publicKey:pair.publicKey};
}
async function call(env,path,body,admin=false){return json(await worker.fetch(req(path,body,admin?env.ADMIN_TOKEN:undefined),env));}
const issues={durationDays:30,maxDevices:1};
async function signed(lease,env){
 assert.equal(lease.status,200);
 const canon=Object.values(lease.data.claims).join("\n");
 const sig=Buffer.from(lease.data.signature.replace(/-/g,"+").replace(/_/g,"/"),"base64");
 return crypto.subtle.verify({name:"ECDSA",hash:"SHA-256"},env.publicKey,sig,new TextEncoder().encode(canon));
}
test("isolated Worker+D1 card issue / activation / binding / expiry / renewal / disable",async()=>{
 const env=await createEnv();let clock=Date.now();const original=Date.now;Date.now=()=>clock;
 try{
  let response=await call(env,"/v1/admin/cards",issues);
  assert.equal(response.status,401,"no admin secret must never issue a card");
  response=await call(env,"/v1/admin/cards",{...issues,durationDays:0},true);
  assert.equal(response.status,400);
  response=await call(env,"/v1/admin/cards",issues,true);
  assert.equal(response.status,201);
  assert.match(response.data.code,/^TG-[A-F0-9]{8}(-[A-F0-9]{8}){3}$/);
  const code=response.data.code,id=response.data.licenseId;
  assert.ok(env.LICENSE_DB.db.prepare("SELECT card_hmac FROM licenses WHERE id=?").get(id).card_hmac);
  assert.equal(env.LICENSE_DB.db.prepare("SELECT card_hmac FROM licenses WHERE id=?").get(id).card_hmac,
    await digestCard(code,env.LICENSE_PEPPER));
  assert.ok(!JSON.stringify(env.LICENSE_DB.db.prepare("SELECT * FROM licenses WHERE id=?").get(id)).includes(code));
  const originalDuration=30*86400*1000;
  const body={card:code,deviceHash:device("a"),nonce:nonce("f")};
  assert.equal((await call(env,"/v1/license/check",body)).status,403,
    "checking must not silently activate or bind a new device");
  const first=await call(env,"/v1/license/activate",body);
  assert.ok(await signed(first,env));
  assert.equal(first.data.claims.productId,PRODUCT);
  assert.equal(first.data.claims.expiresMs,clock+originalDuration);
  assert.equal(first.data.claims.leaseUntilMs,clock+3600000);
  assert.equal(first.data.claims.deviceHash,body.deviceHash);
  assert.equal(first.data.claims.nonce,body.nonce);
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_devices"),1);
  const again=await call(env,"/v1/license/activate",body);
  assert.equal(again.status,200,"same device may reactivate idempotently at limit");
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_devices"),1);
  assert.equal((await call(env,"/v1/license/activate",{...body,deviceHash:device("b")})).status,403,
    "new device after max devices must be denied");
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_devices"),1);
  const badNonce=await call(env,"/v1/license/check",{...body,nonce:"not-an-unguessable-nonce"});
  assert.equal(badNonce.status,400);
  assert.equal((await call(env,"/v1/license/check",{...body,card:code.toLowerCase()})).status,400);
  const conflictId=crypto.randomUUID();
  const renewal={licenseId:id,operationId:conflictId,durationDays:10};
  const renewed=await call(env,"/v1/admin/renew",renewal,true);
  assert.equal(renewed.status,200);
  assert.equal(renewed.data.expiresMs,clock+40*86400*1000);
  const repeated=await call(env,"/v1/admin/renew",renewal,true);
  assert.equal(repeated.data.expiresMs,renewed.data.expiresMs);
  assert.equal((await call(env,"/v1/admin/renew",{...renewal,durationDays:11},true)).status,409,
    "idempotency key cannot switch requested duration");
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_renewals"),1);
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_admin_audit WHERE event_type='renewed'"),1);
  const disabled=await call(env,"/v1/admin/disable",{licenseId:id,disabled:true},true);
  assert.equal(disabled.data.disabled,true);
  assert.equal((await call(env,"/v1/license/check",body)).status,403,"revoked card denied online");
  const enabled=await call(env,"/v1/admin/disable",{licenseId:id,disabled:false},true);
  assert.equal(enabled.data.disabled,false);
  const checked=await call(env,"/v1/license/check",body);
  assert.ok(await signed(checked,env));
  clock+=41*86400*1000;
  assert.equal((await call(env,"/v1/license/check",body)).status,403,"expired card denied");
  assert.equal((await call(env,"/v1/license/activate",body)).status,403,"expired card cannot rebind");
  const restore=await call(env,"/v1/admin/renew",
   {licenseId:id,operationId:crypto.randomUUID(),durationDays:7},true);
  assert.equal(restore.status,200);
  assert.equal(restore.data.expiresMs,clock+7*86400*1000);
  assert.ok(await signed(await call(env,"/v1/license/check",body),env));
 }finally{Date.now=original;env.LICENSE_DB.close();}
});
test("isolation: Telegram cards and signing keys not accepted in another independent tenant",async()=>{
 const a=await createEnv(),b=await createEnv();
 try{
  const issued=await call(a,"/v1/admin/cards",{durationDays:1,maxDevices:1},true);
  const body={card:issued.data.code,deviceHash:device("1"),nonce:nonce("2")};
  assert.equal((await call(b,"/v1/license/activate",body)).status,403);
  assert.equal(b.LICENSE_DB.count("SELECT COUNT(*) AS n FROM licenses"),0);
  assert.equal(a.LICENSE_DB.count("SELECT COUNT(*) AS n FROM licenses"),1);
  const lease=await call(a,"/v1/license/activate",body);
  assert.ok(await signed(lease,a));
  assert.equal(await signed(lease,b).catch(()=>false),false,
    "different tenant signing key cannot verify Telegram lease");
  const wrongProduct={...a,PRODUCT_ID:"SIGNAL_AUTO_SCHEDULER"};
  assert.equal((await call(wrongProduct,"/v1/license/check",body)).status,503,
    "Signal product or crossed Worker configuration fails closed");
  const missingPepper={...a,LICENSE_PEPPER:""};
  assert.equal((await call(missingPepper,"/v1/license/check",body)).status,503);
 }finally{a.LICENSE_DB.close();b.LICENSE_DB.close();}
});
test("concurrent activation of same card stays within device quota and no duplicate license",async()=>{
 const env=await createEnv();
 try{
  const issued=await call(env,"/v1/admin/cards",{durationDays:1,maxDevices:2},true);
  const a=Array.from({length:15},(_,i)=>call(env,"/v1/license/activate",
    {card:issued.data.code,deviceHash:i.toString(16).padStart(64,"0"),nonce:nonce("a")}));
  const results=await Promise.all(a);
  assert.equal(results.filter(x=>x.status===200).length,2,
    "no more than two concurrent devices can bind");
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_devices"),2);
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM licenses"),1);
  const again=await Promise.all(Array.from({length:12},()=>call(env,"/v1/admin/renew",
    {licenseId:issued.data.licenseId,operationId:"22222222-2222-4222-8222-222222222222",durationDays:3},true)));
  assert.ok(again.every(r=>r.status===200));
  const row=env.LICENSE_DB.db.prepare("SELECT expires_ms,activated_ms FROM licenses").get();
  assert.equal(row.expires_ms-row.activated_ms,4*86400*1000,"concurrent renewal adds exactly once");
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM license_renewals"),1);
 }finally{env.LICENSE_DB.close();}
});
test("security gates: invalid shape, disallowed verbs, malformed credentials and no-cache",async()=>{
 const env=await createEnv();
 try{
  assert.equal((await call(env,"/v1/admin/cards",{durationDays:7,maxDevices:99},true)).status,400);
  assert.equal((await call(env,"/v1/admin/cards",{durationDays:7,maxDevices:1,role:"root"},true)).status,400);
  assert.equal((await worker.fetch(new Request("https://license-telegram.test/v1/admin/cards"),env)).status,404);
  const forbidden=await call(env,"/v1/admin/cards",{durationDays:7,maxDevices:1});
  assert.equal(forbidden.status,401);
  assert.ok(!JSON.stringify(forbidden.data).includes(env.ADMIN_TOKEN));
  const invalid=await worker.fetch(new Request("https://license-telegram.test/v1/license/activate",{
    method:"POST",headers:{"content-type":"text/plain"},body:"{}"}),env);
  assert.equal(invalid.status,400);
  assert.equal(invalid.headers.get("cache-control"),"no-store");
  const missing={...env,LICENSE_SIGNING_PKCS8:""};
  assert.equal((await call(missing,"/v1/admin/cards",{durationDays:1,maxDevices:1},true)).status,503);
  assert.equal(env.LICENSE_DB.count("SELECT COUNT(*) AS n FROM licenses"),0);
 }finally{env.LICENSE_DB.close();}
});
