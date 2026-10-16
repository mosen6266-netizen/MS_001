/** Allowlisted offline host commands; no Telegram sends, login, sessions or secrets. */
export const IPC_PROTOCOL = "mosen.desktop.v1" as const;
export type ReadRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: "system.ping"|"app.info"|"settings.get" };
export type SettingsSaveRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: "settings.save"; payload: {expectedRevision:number;preferences:UserPreferences} };
export type BridgeRequest = ReadRequest | SettingsSaveRequest;
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
  return (v.type==="system.ping"||v.type==="app.info"||v.type==="settings.get") && Object.keys(v).length===3;
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
