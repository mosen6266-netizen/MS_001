/** Offline-only, allowlisted IPC contract. No Telegram actions at this stage. */
export const IPC_PROTOCOL = "mosen.desktop.v1" as const;
export type RequestType = "system.ping" | "app.info";
export type BridgeRequest = { protocol: typeof IPC_PROTOCOL; requestId: string; type: RequestType };
export type BridgeResponse = { protocol: typeof IPC_PROTOCOL; requestId: string; ok: boolean; type: string; payload: Record<string, unknown> };
const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === "object" && v !== null && !Array.isArray(v);
export function isBridgeRequest(v: unknown): v is BridgeRequest {
  return isRecord(v) && v.protocol === IPC_PROTOCOL && typeof v.requestId === "string" && v.requestId.length > 0 && v.requestId.length <= 64 && (v.type === "system.ping" || v.type === "app.info") && Object.keys(v).length === 3;
}
export function isBridgeResponse(v: unknown): v is BridgeResponse {
  return isRecord(v) && v.protocol === IPC_PROTOCOL && typeof v.requestId === "string" && typeof v.ok === "boolean" && typeof v.type === "string" && isRecord(v.payload);
}
export function makeRequest(type: RequestType, requestId: string): BridgeRequest {
  const r = { protocol: IPC_PROTOCOL, type, requestId };
  if (!isBridgeRequest(r)) throw new Error("invalid IPC request");
  return r;
}
