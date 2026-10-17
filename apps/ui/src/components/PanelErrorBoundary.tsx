import {Component,type ReactNode} from "react";

/** An individual panel cannot take down the window, navigation or other panels.
 * Do not copy error messages, stack traces, credentials or TDLib payloads to UI/logs.
 */
type Props={children:ReactNode;label:string};
type State={failed:boolean};
export class PanelErrorBoundary extends Component<Props,State>{
 state:State={failed:false};
 static getDerivedStateFromError():State{return {failed:true};}
 componentDidCatch():void{
   // Intentional: diagnostic text could contain a phone, QR token or Telegram secret.
   // A fixed category is sufficient; never send React component stacks externally.
 }
 render(){
   if(!this.state.failed)return this.props.children;
   return <section className="ui-demo-box" role="alert" aria-label={this.props.label+"加载异常"}>
     <h2>{this.props.label}暂时无法显示</h2>
     <p className="ui-preview-caption">
       页面发生显示异常，其他功能和已保存资料不会被清除。可点击下面的按钮重建这一模块。
     </p>
     <div className="ui-action-row">
       <button type="button" className="ui-button ui-button--success"
         onClick={()=>this.setState({failed:false})}>重试加载</button>
     </div>
   </section>;
 }
}
