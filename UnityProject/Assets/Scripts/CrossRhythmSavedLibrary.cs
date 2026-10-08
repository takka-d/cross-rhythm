using System;
using System.IO;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    static bool SameProjectFile(string left,string right){
        if(string.IsNullOrEmpty(left)||string.IsNullOrEmpty(right))return false;
#if UNITY_WEBGL && !UNITY_EDITOR
        return string.Equals(left,right,StringComparison.Ordinal);
#else
        try{return string.Equals(Path.GetFullPath(left),Path.GetFullPath(right),StringComparison.OrdinalIgnoreCase);}catch{return false;}
#endif
    }
    void RefreshSavedLibrary(ChartProject saved,byte[] writtenBytes=null){
        // Only refresh an existing file. Saving an unrelated Edit document must not add it to Songs.
        for(int i=0;i<Library.Count;i++){
            var old=Library[i];if(!SameProjectFile(old.FilePath,saved.FilePath))continue;
            var updated=writtenBytes==null?CopyForEditor(saved):ChartProject.Read(writtenBytes,saved.FileName,saved.FilePath);
            updated.Dirty=false;Library[i]=updated;
            if(Current==Page.Songs&&ReferenceEquals(Project,old))ActivateProject(updated);
        }
    }
}
}
