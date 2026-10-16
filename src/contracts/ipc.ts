/** Allowlisted offline host commands; no Telegram sends, login, sessions or secrets. */
export const IPC_PROTOCOL = "mosen.desktop.v1" as const;
export type ReadRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: "system.ping"|"app.info"|"settings.get"|"diagnostics.get" };
export type SettingsSaveRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: "settings.save"; payload: {expectedRevision:number;preferences:UserPreferences} };
export type AccountAction="accounts.list"|"accounts.create"|"accounts.edit"|"accounts.archive"|"accounts.restore";
export type AccountItem={id:string;alias:string;groupTag:string;displayName:string;revision:number;verifiedUserId:string|null;removed:boolean;canAttachSession:boolean};
export type AccountPage={items:AccountItem[];total:number;page:number;pageSize:number};
export type AccountRequest={protocol:typeof IPC_PROTOCOL;requestId:string;type:AccountAction;payload:Record<string,unknown>};
export type GroupAction="groups.list"|"groups.topics"|"groups.members";
export type GroupRecord={chatId:string;title:string;kind:"group"|"supergroup"|"channel";isForum:boolean;checkedUtc:string};
export type TopicRecord={chatId:string;topicId:string;title:string;isClosed:boolean;isHidden:boolean;checkedUtc:string};
export type MemberRecord={accountId:string;alias:string;status:string;canSendText:boolean;canSendPhotos:boolean;slowModeDelaySeconds:number;checkedUtc:string};
export type GroupPage={items:GroupRecord[];total:number;page:number;pageSize:number};
export type TopicPage={items:TopicRecord[];total:number;page:number;pageSize:number};
export type GroupRequest={protocol:typeof IPC_PROTOCOL;requestId:string;type:GroupAction;payload:Record<string,unknown>};
export type ScriptAction="scripts.list"|"scripts.get"|"scripts.create"|"scripts.save"|"scripts.duplicate"|"scripts.archive"|"scripts.restore";
export type EditorRole={roleKey:string;roleLabel:string};
export type TimingProfile={preSendDelayMs:number;typingDurationMs:number;postSendDelayMs:number};
export type MediaReference={assetId:string;mimeType:"image/png"|"image/jpeg"|"image/webp";byteLength:number;width:number;height:number;fileName:string};
export type EditorMessage={stepKey:string;roleKey:string;body:string;preSendDelayMs:number;typingDurationMs:number;postSendDelayMs:number;media?:MediaReference|null};
export type EditorDoc={title:string;roles:EditorRole[];steps:EditorMessage[];defaults?:TimingProfile|null};
export type MediaAction="media.import"|"media.preview";
export type MediaRequest={protocol:typeof IPC_PROTOCOL;requestId:string;type:MediaAction;payload:Record<string,unknown>};
export type ScriptSnapshot={scriptId:string;revision:number;document:EditorDoc;archived:boolean};
export type ScriptSummary={scriptId:string;title:string;revision:number;stepCount:number;archived:boolean};
export type ScriptRequest={protocol:typeof IPC_PROTOCOL;requestId:string;type:ScriptAction;payload:Record<string,unknown>};
export type BridgeRequest = ReadRequest | SettingsSaveRequest | AccountRequest | GroupRequest | ScriptRequest | MediaRequest;
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
  if(v.type==="media.import"||v.type==="media.preview"){
    if(!isRecord(v.payload)||Object.keys(v).length!==4)return false;
    const p=v.payload;
    if(v.type==="media.preview")return Object.keys(p).length===1&&"reference" in p&&isMediaReference(p.reference);
    return Object.keys(p).sort().join(",")==="base64,fileName,mimeType"&&
      typeof p.fileName==="string"&&p.fileName.length>0&&p.fileName.length<=128&&
      ["image/png","image/jpeg","image/webp"].includes(String(p.mimeType))&&
      typeof p.base64==="string"&&p.base64.length>=40&&p.base64.length<=6990516&&
      /^[a-zA-Z0-9+/]+={0,2}$/.test(p.base64);
  }
  if(typeof v.type==="string" && v.type.startsWith("scripts.")){
    const schema:Record<ScriptAction,readonly string[]>={
      "scripts.list":["query","archived"],"scripts.get":["id"],
      "scripts.create":["title"],"scripts.save":["id","expectedRevision","document"],
      "scripts.duplicate":["id"],"scripts.archive":["id","expectedRevision","confirmation"],
      "scripts.restore":["id","expectedRevision"]
    };
    if(!(v.type in schema)||!isRecord(v.payload)||Object.keys(v).length!==4)return false;
    const action=v.type as ScriptAction;
    if(Object.keys(v.payload).sort().join(",")!==[...schema[action]].sort().join(","))return false;
    const p=v.payload;
    if(action==="scripts.list")return typeof p.query==="string"&&p.query.length<=128&&typeof p.archived==="boolean";
    if(action==="scripts.create")return typeof p.title==="string"&&p.title.trim().length>0&&p.title.length<=128;
    if(typeof p.id!=="string"||!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(p.id))return false;
    if(action==="scripts.save"||action==="scripts.archive"||action==="scripts.restore"){
      if(!Number.isSafeInteger(p.expectedRevision)||Number(p.expectedRevision)<1)return false;
    }
    if(action==="scripts.archive")return p.confirmation==="归档剧本";
    if(action==="scripts.save")return isEditorDoc(p.document);
    return true;
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
  if(v.type==="groups.list"||v.type==="groups.topics"||v.type==="groups.members"){
    if(!isRecord(v.payload)||Object.keys(v).length!==4)return false;
    const keys=v.type==="groups.list"?["query","page","pageSize"]:
      v.type==="groups.topics"?["chatId","page","pageSize"]:["chatId"];
    if(Object.keys(v.payload).sort().join(",")!==keys.sort().join(","))return false;
    const p=v.payload;
    if(v.type!=="groups.list" &&
       (typeof p.chatId!=="string"||! /^-?[1-9][0-9]{0,18}$/.test(p.chatId)))return false;
    if(v.type==="groups.members")return true;
    if(v.type==="groups.list" &&
      (typeof p.query!=="string"||p.query.length>128||/[\x00-\x1f\x7f]/.test(p.query)))return false;
    return Number.isInteger(p.page)&&Number(p.page)>=1&&Number(p.page)<=1000000 &&
      [10,20,50].includes(Number(p.pageSize));
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

export function makeGroupRequest(type:GroupAction,requestId:string,payload:Record<string,unknown>):GroupRequest {
  const req={protocol:IPC_PROTOCOL,requestId,type,payload};
  if(!isBridgeRequest(req))throw new Error("Invalid group read request");
  return req;
}
const finitePage=(v:unknown):v is {items:unknown[];total:number;page:number;pageSize:number} =>
  isRecord(v)&&Array.isArray(v.items)&&Number.isSafeInteger(v.total)&&Number(v.total)>=0&&
  Number.isSafeInteger(v.page)&&Number(v.page)>=1&&[10,20,50].includes(Number(v.pageSize))&&
  v.items.length<=Number(v.pageSize);
const safeId=(id:unknown)=>typeof id==="string"&&/^-?[1-9][0-9]{0,18}$/.test(id);
export function isGroupPage(v:unknown):v is GroupPage {
  return finitePage(v)&&v.items.every(x=>isRecord(x)&&safeId(x.chatId)&&
    typeof x.title==="string"&&["group","supergroup","channel"].includes(String(x.kind))&&
    typeof x.isForum==="boolean"&&typeof x.checkedUtc==="string");
}
export function isTopicPage(v:unknown):v is TopicPage {
  return finitePage(v)&&v.items.every(x=>isRecord(x)&&safeId(x.chatId)&&safeId(x.topicId)&&
    typeof x.title==="string"&&typeof x.isClosed==="boolean"&&typeof x.isHidden==="boolean"&&
    typeof x.checkedUtc==="string");
}
export function isMemberList(v:unknown):v is MemberRecord[] {
  return Array.isArray(v)&&v.length<=300&&v.every(x=>isRecord(x)&&typeof x.accountId==="string"&&
    typeof x.alias==="string"&&typeof x.status==="string"&&typeof x.canSendText==="boolean"&&
    typeof x.canSendPhotos==="boolean"&&Number.isSafeInteger(x.slowModeDelaySeconds)&&
    typeof x.checkedUtc==="string");
}

export function isTimingProfile(t:unknown):t is TimingProfile {
  if(!isRecord(t)||Object.keys(t).sort().join(",")!=="postSendDelayMs,preSendDelayMs,typingDurationMs")return false;
  return (["preSendDelayMs","typingDurationMs","postSendDelayMs"] as const).every(k=>
    Number.isSafeInteger(t[k])&&Number(t[k])>=0&&
    Number(t[k])<=(k==="typingDurationMs"?60000:3600000));
}
export function isMediaReference(m:unknown):m is MediaReference {
  if(!isRecord(m)||Object.keys(m).sort().join(",")!=="assetId,byteLength,fileName,height,mimeType,width")return false;
  return typeof m.assetId==="string"&&/^[a-f0-9]{64}$/.test(m.assetId)&&
    ["image/png","image/jpeg","image/webp"].includes(String(m.mimeType))&&
    Number.isSafeInteger(m.byteLength)&&Number(m.byteLength)>=32&&Number(m.byteLength)<=5242880&&
    Number.isSafeInteger(m.width)&&Number(m.width)>=1&&Number(m.width)<=8192&&
    Number.isSafeInteger(m.height)&&Number(m.height)>=1&&Number(m.height)<=8192&&
    typeof m.fileName==="string"&&m.fileName.length>0&&m.fileName.length<=128&&
    !/[\/\\:\x00-\x1f\x7f]/.test(m.fileName);
}
export function isEditorDoc(value:unknown):value is EditorDoc{
  if(!isRecord(value))return false;
  const fields=Object.keys(value).sort().join(",");
  if(fields!=="roles,steps,title"&&fields!=="defaults,roles,steps,title")return false;
  if(typeof value.title!=="string"||!value.title.trim()||value.title.length>128||
     !Array.isArray(value.roles)||value.roles.length>300||
     !Array.isArray(value.steps)||value.steps.length>1000||
     (value.defaults!==undefined&&value.defaults!==null&&!isTimingProfile(value.defaults)))return false;
  const ids=new Set<string>(),labels=new Set<string>(),stepIds=new Set<string>();
  for(const r of value.roles){
    if(!isRecord(r)||Object.keys(r).sort().join(",")!=="roleKey,roleLabel"||
       typeof r.roleKey!=="string"||typeof r.roleLabel!=="string"||
       r.roleLabel.trim().length===0||r.roleLabel.length>64||
       !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(r.roleKey)||
       ids.has(r.roleKey)||labels.has(r.roleLabel.trim().normalize("NFC")))return false;
    ids.add(r.roleKey);labels.add(r.roleLabel.trim().normalize("NFC"));
  }
  for(const x of value.steps){
    if(!isRecord(x))return false;
    const keys=Object.keys(x).sort().join(",");
    if(keys!=="body,postSendDelayMs,preSendDelayMs,roleKey,stepKey,typingDurationMs"&&
       keys!=="body,media,postSendDelayMs,preSendDelayMs,roleKey,stepKey,typingDurationMs")return false;
    if(typeof x.stepKey!=="string"||typeof x.roleKey!=="string"||
       !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(x.stepKey)||
       stepIds.has(x.stepKey)||!ids.has(x.roleKey)||typeof x.body!=="string"||x.body.length>8192||
       !isTimingProfile({preSendDelayMs:x.preSendDelayMs,
          typingDurationMs:x.typingDurationMs,postSendDelayMs:x.postSendDelayMs})||
       (x.media!==undefined&&x.media!==null&&!isMediaReference(x.media)))return false;
    stepIds.add(x.stepKey);
  }
  return true;
}
export function makeMediaRequest(type:MediaAction,requestId:string,payload:Record<string,unknown>):MediaRequest{
  const req={protocol:IPC_PROTOCOL,requestId,type,payload};
  if(!isBridgeRequest(req))throw new Error("Invalid media request");
  return req;
}
export function makeScriptRequest(type:ScriptAction,requestId:string,payload:Record<string,unknown>):ScriptRequest{
  const req={protocol:IPC_PROTOCOL,requestId,type,payload};
  if(!isBridgeRequest(req))throw new Error("Invalid script request");
  return req;
}
export function isScriptSnapshot(v:unknown):v is ScriptSnapshot{
  return isRecord(v)&&typeof v.scriptId==="string"&&Number.isSafeInteger(v.revision)&&Number(v.revision)>0&&
    typeof v.archived==="boolean"&&isEditorDoc(v.document);
}
export function isScriptSummaries(v:unknown):v is ScriptSummary[]{
  return Array.isArray(v)&&v.length<=300&&v.every(x=>isRecord(x)&&typeof x.scriptId==="string"&&
    typeof x.title==="string"&&Number.isSafeInteger(x.revision)&&
    Number.isSafeInteger(x.stepCount)&&typeof x.archived==="boolean");
}
