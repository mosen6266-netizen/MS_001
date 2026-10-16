import {useMemo,useState} from "react";
import {ActionButton,TextField,StatusPill} from "./DesignSystem";
import {useDebounced} from "../hooks/useDebounced";
import {mockAccountIndex,filterSearch,paginate} from "../../../../src/web/settings/search";
import type {UserPreferences} from "../../../../src/contracts/ipc";

const fixture=mockAccountIndex(300);
export function MockAccountIndex({preferences}:{preferences:UserPreferences}) {
  const [query,setQuery]=useState("");
  const [page,setPage]=useState(1);
  const debounced=useDebounced(query,preferences.searchDebounceMs);
  const filtered=useMemo(()=>filterSearch(fixture,debounced),[debounced]);
  const result=useMemo(()=>paginate(filtered,page,preferences.pageSize),[filtered,page,preferences.pageSize]);
  const handleQuery=(next:string)=>{setQuery(next);setPage(1);};
  return <div className="ui-demo-section">
    <div className="ui-demo-box">
      <h2 id="panel-title">账号目录搜索与分页验证</h2>
      <StatusPill tone="warning">300 条模拟数据 · 非真实 Telegram 账号</StatusPill>
      <p className="ui-preview-caption">用于验证搜索输入、防抖、分页和大列表性能；正式账号列表将在后续接入。</p>
      <TextField label="搜索账号备注" value={query} onChange={handleQuery} placeholder="例如：测试备注125" />
      <div className="ui-list-summary" role="status">
        匹配 {result.total} 条 · 第 {result.page} / {result.pageCount} 页 · 每页 {preferences.pageSize} 条
      </div>
      <ul className={"ui-bounded-list "+(preferences.density==="compact"?"ui-bounded-list--compact":"")} aria-label="模拟账号列表">
        {result.items.map(item=><li key={item.id}><span>{item.label}</span><small>{item.detail}</small></li>)}
        {result.items.length===0?<li className="ui-empty">没有匹配结果</li>:null}
      </ul>
      <div className="ui-action-row">
        <ActionButton disabled={result.page<=1} onClick={()=>setPage(v=>Math.max(1,v-1))}>上一页</ActionButton>
        <span className="ui-pagination-label">{result.page} / {result.pageCount}</span>
        <ActionButton disabled={result.page>=result.pageCount} onClick={()=>setPage(v=>Math.min(result.pageCount,v+1))}>下一页</ActionButton>
      </div>
    </div>
  </div>;
}
