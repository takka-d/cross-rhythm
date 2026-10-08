using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    string songInfoField;
    ChartProject songInfoProject;
    void EndSongInfoEdit(){songInfoField=null;songInfoProject=null;}
    void SetSongInfoField(string name,string value){
        if(name=="title"?value==Project.SongTitle:value==Project.Artist)return;
        // One chart snapshot per text-edit session, never per character.
        if(songInfoField!=name||!ReferenceEquals(songInfoProject,Project)){
            PushUndo();songInfoField=name;songInfoProject=Project;
        }
        Project.SetSongInfo(name=="title"?value:Project.SongTitle,name=="artist"?value:Project.Artist);
        QueueEditorRecovery();
    }
    void TrackSongInfoFocus(){
        string focused=GUI.GetNameOfFocusedControl();
        if(songInfoField!=null&&focused!="edit-"+songInfoField)EndSongInfoEdit();
    }
}
}
