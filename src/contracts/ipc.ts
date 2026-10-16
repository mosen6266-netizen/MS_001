/** Allowlisted offline host commands; no Telegram sends, login, sessions or secrets. */
export const IPC_PROTOCOL = "mosen.desktop.v1" as const;
export type ReadRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: "system.ping"|"app.info"|"settings.get"|"diagnostics.get" };
export type SettingsSaveRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: "settings.save"; payload: {expectedRevision:number;preferences:UserPreferences} };
export type AccountAction="accounts.list"|"accounts.create"|"accounts.edit"|"accounts.archive"|"accounts.restore";
export type AccountItem={id:string;alias:string;groupTag:string;displayName:string;revision:number;verifiedUserId:string|null;removed:boolean;canAttachSession:boolean};
export type AccountPage={items:AccountItem[];total:number;page:number;pageSize:number};
export type AccountRequest={protocol:typeof IPC_PROTOCOL;requestId:string;type:AccountAction;payload:Record<string,unknown>};
export type BridgeRequest = ReadRequest | SettingsSaveRequest | AccountRequest;
export type BridgeResponse = { protocol: typeof IPC_PROTOCOL; requestId:string; ok:boolean; type:string; payload:Record<string,unknown> };
export type Density = "compact" | "comfortable";
export type UserPreferences = {density:Density;searchDebounceMs:number;pageSize:10|20|50;desktopNotifications:boolean};
export type PreferencesSnapshot = {revision:number;preferences:UserPreferences};
export const DEFAULT_PREFERENCES: UserPreferences = {density:"comfortable",searchDebounceMs:200,pageSize:20,desktopNotifications:true};
const isRecord=(v:unknown):v is Record<string,unknown> => typeof v==="object"&&v!==null&&!Array.isArray(v);
export function isPreferences(value:unknown):value is UserPreferences {
  if(!isRecord(value)||Object.keys(value).length!==4)return false;
  return (value.density==="compact"||value.density==="comfortable") &&
    typeof value.searchDebounceMs==="number" && Number.isInteger(value.searchDebounceMs) &&
    value.searchDebounceMs>=150 && value.searchDebounceMs<=250 &&
    (value.pageSize===10||value.pageSize===20||value.pageSize===50) &&
    typeof value.desktopNotifications==="boolean";
}
export function isPreferencesSnapshot(v:unknown):v is PreferencesSnapshot {
  return isRecord(v) && Number.isSafeInteger(v.revision) && (v.revision as number)>=0 && isPreferences(v.preferences);
}
export function isBridgeRequest(v:unknown):v is BridgeRequest {
  if(!isRecord(v)||v.protocol!==IPC_PROTOCOL||typeof v.requestId!=="string"||v.requestId.length===0||v.requestId.length>64)return false;
  if(v.type==="settings.save"){
    if(Object.keys(v).length!==4||!isRecord(v.payload)||Object.keys(v.payload).length!==2)return false;
    return Number.isSafeInteger(v.payload.expectedRevision) && (v.payload.expectedRevision as number)>=0 && isPreferences(v.payload.preferences);
  }
  const schemas:Record<AccountAction,readonly string[]>={
    "accounts.list":["query","group","page","pageSize","archived"],
    "accounts.create":["alias","groupTag"],
    "accounts.edit":["id","expectedRevision","alias","groupTag"],
    "accounts.archive":["id","expectedRevision","typedAlias"],
    "accounts.restore":["id","expectedRevision"]
  };
  if(typeof v.type==="string" && v.type in schemas){
    const type=v.type as AccountAction;
    if(!isRecord(v.payload)||Object.keys(v).length!==4)return false;
    if(Object.keys(v.payload).sort().join(",")!==[...schemas[type]].sort().join(","))return false;
    const body=v.payload;
    const label=(k:string,max:number,empty=false)=>
      typeof body[k]==="string" && (body[k] as string).length<=max &&
      (empty || (body[k] as string).trim().length>0) &&
      !/[\x00-\x1f\x7f]/.test(body[k] as string);
    const validId=type==="accounts.list"||type==="accounts.create"||
      (typeof body.id==="string" && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(body.id as string));
    const validRev=type==="accounts.list"||type==="accounts.create"||
      (Number.isSafeInteger(body.expectedRevision)&&Number(body.expectedRevision)>0);
    if(!validId||!validRev)return false;
    if(type==="accounts.list")return label("query",128,true)&&label("group",64,true)&&
      typeof body.archived==="boolean"&&
      Number.isInteger(body.page)&&Number(body.page)>=1&&Number(body.page)<=1000000&&[10,20,50].includes(Number(body.pageSize));
    if(type==="accounts.create"||type==="accounts.edit")return label("alias",64)&&label("groupTag",64,true);
    if(type==="accounts.archive")return label("typedAlias",64);
    return true;
  }
  return (v.type==="system.ping"||v.type==="app.info"||v.type==="settings.get"||v.type==="diagnostics.get") && Object.keys(v).length===3;
}
export function isBridgeResponse(v:unknown):v is BridgeResponse {
  return isRecord(v)&&v.protocol===IPC_PROTOCOL&&typeof v.requestId==="string"&&v.requestId.length>0&&
    typeof v.ok==="boolean"&&typeof v.type==="string"&&isRecord(v.payload);
}
export function makeRequest(type:ReadRequest["type"],requestId:string):ReadRequest {
  const request={protocol:IPC_PROTOCOL,requestId,type};
  if(!isBridgeRequest(request))throw new Error("Invalid IPC request");
  return request;
}
export function makeSaveRequest(requestId:string,snapshot:PreferencesSnapshot):SettingsSaveRequest {
  if(!isPreferencesSnapshot(snapshot))throw new Error("Invalid preference snapshot");
  const request={protocol:IPC_PROTOCOL,requestId,type:"settings.save" as const,
    payload:{expectedRevision:snapshot.revision,preferences:snapshot.preferences}};
  if(!isBridgeRequest(request))throw new Error("Invalid settings save request");
  return request;
}

export function makeAccountRequest(type:AccountAction,requestId:string,payload:Record<string,unknown>):AccountRequest{
  const v={protocol:IPC_PROTOCOL,requestId,type,payload};
  if(!isBridgeRequest(v))throw new Error("Invalid account IPC request");
  return v;
}
export function isAccountItem(v:unknown):v is AccountItem{
  if(!isRecord(v))return false;
  return typeof v.id==="string" && typeof v.alias==="string" && typeof v.groupTag==="string" &&
    typeof v.displayName==="string" && Number.isSafeInteger(v.revision) && Number(v.revision)>0 &&
    (v.verifiedUserId===null||typeof v.verifiedUserId==="string") &&
    typeof v.removed==="boolean" && typeof v.canAttachSession==="boolean";
}
export function isAccountPage(v:unknown):v is AccountPage{
  if(!isRecord(v)||!Array.isArray(v.items))return false;
  return Number.isSafeInteger(v.total)&&Number(v.total)>=0&&Number.isSafeInteger(v.page)&&
    Number(v.page)>=1&&[10,20,50].includes(Number(v.pageSize))&&v.items.length<=Number(v.pageSize)&&v.items.every(isAccountItem);
}
