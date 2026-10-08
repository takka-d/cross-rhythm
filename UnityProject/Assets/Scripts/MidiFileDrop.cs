using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
namespace CrossRhythm {
public static class MidiDropFiles {
    public const long MaxBytes=32L*1024*1024;
    public static bool IsMidi(string path){string ext=Path.GetExtension(path);return string.Equals(ext,".mid",StringComparison.OrdinalIgnoreCase)||string.Equals(ext,".midi",StringComparison.OrdinalIgnoreCase);}
}
// The window callback only copies paths. Unity state and file IO run in Update.
public sealed class MidiFileDrop : IDisposable {
    readonly ConcurrentQueue<string[]> queue=new ConcurrentQueue<string[]>();
    public bool Attached {get;private set;}
    public bool TryRead(out string[] paths)=>queue.TryDequeue(out paths);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    const int WndProcIndex=-4;const uint DropFiles=0x233;
    IntPtr window,previous,callbackPointer;WindowProc callback;bool accepting;
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate IntPtr WindowProc(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    delegate bool WindowEnum(IntPtr hwnd,IntPtr data);
    [DllImport("user32.dll")]static extern bool EnumWindows(WindowEnum callback,IntPtr data);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr hwnd,StringBuilder name,int length);
    [DllImport("user32.dll")]static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
    [DllImport("user32.dll",EntryPoint="CallWindowProcW")]static extern IntPtr CallWindowProc(IntPtr proc,IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("shell32.dll")]static extern void DragAcceptFiles(IntPtr hwnd,bool accept);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern uint DragQueryFile(IntPtr drop,uint index,StringBuilder path,uint size);
    [DllImport("shell32.dll")]static extern void DragFinish(IntPtr drop);
    // Keep delegates alive if another component has chained a callback above ours.
    static readonly System.Collections.Generic.List<WindowProc> chained=new System.Collections.Generic.List<WindowProc>();
    public void Update(bool enabled){
        if(window!=IntPtr.Zero&&!IsWindow(window)){window=IntPtr.Zero;Attached=false;callback=null;}
        if(window==IntPtr.Zero){
            uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((hwnd,data)=>{GetWindowThreadProcessId(hwnd,out uint pid);if(pid!=own)return true;var name=new StringBuilder(128);GetClassName(hwnd,name,name.Capacity);if(name.ToString()!="UnityWndClass")return true;window=hwnd;return false;},IntPtr.Zero);
            if(window==IntPtr.Zero)return;
            callback=Receive;callbackPointer=Marshal.GetFunctionPointerForDelegate(callback);previous=SetWindowLongPtr(window,WndProcIndex,callbackPointer);
            if(previous==IntPtr.Zero){window=IntPtr.Zero;callback=null;return;}Attached=true;accepting=false;
        }
        if(accepting!=enabled){accepting=enabled;DragAcceptFiles(window,enabled);}
    }
    IntPtr Receive(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam){
        if(message!=DropFiles)return CallWindowProc(previous,hwnd,message,wParam,lParam);
        try{
            uint count=DragQueryFile(wParam,uint.MaxValue,null,0);var paths=new string[Math.Min(count,2)];
            for(uint i=0;i<paths.Length;i++){uint size=DragQueryFile(wParam,i,null,0);if(size>32767){paths[i]="";continue;}var path=new StringBuilder((int)size+1);DragQueryFile(wParam,i,path,(uint)path.Capacity);paths[i]=path.ToString();}
            queue.Enqueue(paths);
        }catch{queue.Enqueue(Array.Empty<string>());}finally{DragFinish(wParam);}
        return IntPtr.Zero;
    }
    public void Dispose(){
        if(window!=IntPtr.Zero&&IsWindow(window)){
            DragAcceptFiles(window,false);
            if(GetWindowLongPtr(window,WndProcIndex)==callbackPointer)SetWindowLongPtr(window,WndProcIndex,previous);else if(callback!=null)chained.Add(callback);
        }
        window=IntPtr.Zero;Attached=false;callback=null;
    }
#else
    public void Update(bool enabled){}
    public void Dispose(){}
#endif
}
}
