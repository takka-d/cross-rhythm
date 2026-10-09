using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    int laneTypeRow=-1;
    bool LaneTypeOpen=>laneTypeRow>=0;
    Rect LaneTypeMenuRect=>new Rect(124,Math.Min(EditorTop+72+Math.Max(0,laneTypeRow)*EditorRowHeight,H-220),240,48+(LaneTypeOpen?Types[laneTypeRow].Length:0)*36);
    void DismissLaneTypeMenu(){
        if(!LaneTypeOpen)return;
        var e=Event.current;
        if(Current!=Page.Edit||(e.type==EventType.MouseDown&&!LaneTypeMenuRect.Contains(e.mousePosition))||(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)){laneTypeRow=-1;GUIUtility.hotControl=0;e.Use();}
    }
    void SetLaneInputType(int row,string type){
        if(EditorNoteTypes.Get(Project,row)==type)return;
        PushUndo();EditorNoteTypes.Set(Project,row,type);Project.Dirty=true;QueueEditorRecovery();
        status=LaneNames[row]+" · "+T("入力する種別: ","Input Type: ")+EditorTypeLabel(type);
    }
    void EditorLaneTypeMenu(){
        if(!LaneTypeOpen||Current!=Page.Edit)return;
        var r=LaneTypeMenuRect;RectFill(r,panel);Border(r,mint);
        Text(new Rect(r.x+12,r.y+9,r.width-24,25),Instruments[laneTypeRow]+" · Input Type",15,mint,true);
        var types=Types[laneTypeRow];for(int i=0;i<types.Length;i++)if(Button(new Rect(r.x+10,r.y+39+i*36,r.width-20,30),EditorTypeLabel(types[i]),EditorNoteTypes.Get(Project,laneTypeRow)==types[i],true,14)){
            SetLaneInputType(laneTypeRow,types[i]);laneTypeRow=-1;GUI.FocusControl(null);return;
        }
    }
}
}
