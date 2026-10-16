import { describe, it, expect } from "vitest";
import { IPC_PROTOCOL, isBridgeRequest, isBridgeResponse, makeRequest } from "../../../src/contracts/ipc";
describe("strict IPC bridge contract", () => {
 it("accepts only declared requests", () => { expect(isBridgeRequest(makeRequest("system.ping", "abc"))).toBe(true); expect(isBridgeRequest({protocol:IPC_PROTOCOL, requestId:"a",type:"telegram.send"})).toBe(false); });
 it("rejects extra fields and unbounded identifiers", () => { expect(isBridgeRequest({...makeRequest("app.info","a"),token:"oops"})).toBe(false); expect(() => makeRequest("system.ping","x".repeat(65))).toThrow(); });
 it("rejects wrong protocol or invalid response", () => { expect(isBridgeRequest({protocol:"untrusted",requestId:"a",type:"app.info"})).toBe(false); expect(isBridgeResponse({protocol:IPC_PROTOCOL,requestId:"x",ok:true,type:"system.pong",payload:{}})).toBe(true); expect(isBridgeResponse({protocol:IPC_PROTOCOL,requestId:"x",ok:true,type:"system.pong",payload:null})).toBe(false); });
});
