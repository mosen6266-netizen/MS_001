/** Prevent duplicate asynchronous submits before React has time to repaint. */
export class ExclusiveAction {
  private locked=false;
  async execute<T>(work:()=>Promise<T>):Promise<{executed:true;value:T}|null> {
    if(this.locked)return null;
    this.locked=true;
    try {const value=await work();return {executed:true,value};}
    finally {this.locked=false;}
  }
  get busy():boolean{return this.locked;}
}
