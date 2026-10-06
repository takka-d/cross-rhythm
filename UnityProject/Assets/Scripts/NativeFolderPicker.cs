using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace CrossRhythm {
public static class NativeFolderPicker {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct BrowseInfo {public IntPtr owner,root,displayName;[MarshalAs(UnmanagedType.LPWStr)]public string title;public uint flags;public IntPtr callback,param;public int image;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHBrowseForFolder(ref BrowseInfo info);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern bool SHGetPathFromIDList(IntPtr id,StringBuilder path);
    [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();
    [DllImport("ole32.dll")]static extern int CoInitializeEx(IntPtr pointer,uint flags);
    [DllImport("ole32.dll")]static extern void CoUninitialize();
    public static Task<string> PickAsync(){var completion=new TaskCompletionSource<string>();string result=null;Exception error=null;var owner=GetActiveWindow();var thread=new Thread(()=>{int initialized=CoInitializeEx(IntPtr.Zero,2);IntPtr id=IntPtr.Zero,display=Marshal.AllocHGlobal(65536);try{var info=new BrowseInfo{owner=owner,displayName=display,title="Open Folder",flags=0x1|0x40|0x200};id=SHBrowseForFolder(ref info);if(id!=IntPtr.Zero){var path=new StringBuilder(32768);if(SHGetPathFromIDList(id,path))result=path.ToString();}}catch(Exception e){error=e;}finally{if(id!=IntPtr.Zero)Marshal.FreeCoTaskMem(id);Marshal.FreeHGlobal(display);if(initialized>=0)CoUninitialize();}if(error!=null)completion.TrySetException(error);else completion.TrySetResult(result);});thread.SetApartmentState(ApartmentState.STA);thread.IsBackground=true;thread.Start();return completion.Task;}
#else
    public static System.Threading.Tasks.Task<string> PickAsync()=>System.Threading.Tasks.Task.FromResult<string>(null);
#endif
}
}
