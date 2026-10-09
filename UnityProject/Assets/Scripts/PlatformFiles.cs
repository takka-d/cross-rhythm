using System;
using System.IO;
using System.Runtime.InteropServices;
namespace CrossRhythm {
public static class PlatformFiles {
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]public static extern void CRInit();
    [DllImport("__Internal")]public static extern void CRUnsaved(int dirty);
    [DllImport("__Internal")]public static extern void CRKeyboardFileMode(int mode);
    [DllImport("__Internal")]public static extern void CRProjectButtonsBegin();
    [DllImport("__Internal")]public static extern void CRProjectButtonsEnd();
    [DllImport("__Internal")]public static extern void CRProjectButton(int id,float x,float y,float w,float h,int mode,int english,int enabled,int primary,float fontSize);
    [DllImport("__Internal")]public static extern void CREditorKeys(int enabled,int textFocus);
    [DllImport("__Internal")]public static extern void CRDisplayLayout(float x,float y,float w,float h,int english,int enabled);
    [DllImport("__Internal")]public static extern void CRPick(string target,int folder);
    [DllImport("__Internal")]public static extern void CRPrepareSave(string name,string token,string target,int saveAs);
    [DllImport("__Internal")]public static extern void CRSave(byte[] data,int length,string name,string token,string baseline,string target,int saveAs);
    [DllImport("__Internal")]public static extern void CRRestore(string target);
    [DllImport("__Internal")]public static extern void CRSaveEditorSession(byte[] data,int length,string metadata);
    [DllImport("__Internal")]public static extern void CRCache(byte[] data,int length,string name,string token);
    [DllImport("__Internal")]public static extern void CRPickAudio(string target);
    [DllImport("__Internal")]public static extern void CRPickMidi(string target);
    [DllImport("__Internal")]public static extern void CRExportMidi(byte[] data,int length,string name,string target);
    [DllImport("__Internal")]public static extern void CRMidiDropState(string target,int enabled,int english);
#else
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]class OpenFileName {
        public int size=Marshal.SizeOf(typeof(OpenFileName)); public IntPtr owner,instance;
        public string filter="Cross Rhythm (*.crproj)\0*.crproj\0All files\0*.*\0";
        public string customFilter;public int maxCustomFilter,filterIndex=1;
        public IntPtr file;public int maxFile;public string fileTitle;public int maxFileTitle;public string initialDir,title;public int flags;public short offset,extension;public string defExt="crproj";public IntPtr data,hook;public string template;public IntPtr reserved;public int reserved2,flagsEx;
    }
    [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetOpenFileName([In,Out]OpenFileName o);
    [DllImport("comdlg32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetSaveFileName([In,Out]OpenFileName o);
    public static string Pick(bool save,string name="",bool audio=false,bool midi=false){
        var o=new OpenFileName{maxFile=32768,file=Marshal.AllocHGlobal(65536),title=audio?"Audio":"Cross Rhythm",flags=0x00080000|0x00000008|(save?0x2:0x1000)};
        if(audio){o.filter="Audio\0*.wav;*.mp3;*.m4a;*.ogg;*.flac;*.aac\0All files\0*.*\0";o.defExt="wav";}
        if(midi){o.filter="MIDI\0*.mid;*.midi\0All files\0*.*\0";o.defExt="mid";o.title=save?"Export MIDI":"Import MIDI";}
        try{for(int i=0;i<65536;i++)Marshal.WriteByte(o.file,i,0);var chars=(name+"\0").ToCharArray();Marshal.Copy(chars,0,o.file,chars.Length);bool ok=save?GetSaveFileName(o):GetOpenFileName(o);return ok?Marshal.PtrToStringUni(o.file):null;}finally{Marshal.FreeHGlobal(o.file);}
    }
#endif
}
}
