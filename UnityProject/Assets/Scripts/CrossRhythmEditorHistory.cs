using System;
using System.Collections;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool restoringHistory;
    string historyLabel="";
    int historyRequestFrame,historyRequestPaints;
    bool HistoryPaintRequired=>string.IsNullOrEmpty(workspaceCheckRoot)&&!Application.isBatchMode;
    void RequestRestore(bool reverse){
        if(Current!=Page.Edit||InputBlocked||(reverse?redo:undo).Count==0)return;
        restoringHistory=true;historyLabel=reverse?"Redo":"Undo";
        historyRequestFrame=Time.frameCount;historyRequestPaints=loadingPaints;
        PrepareExclusiveOperation();
        StartCoroutine(RestoreHistoryRoutine(reverse,Project));
    }
    IEnumerator RestoreHistoryRoutine(bool reverse,ChartProject expected){
        // Visible use waits for a real dialog repaint before clone/rebuild. Hidden
        // workspace checks cannot repaint, so only that mode uses a bounded fallback.
        if(HistoryPaintRequired){
            while(loadingPaints<=historyRequestPaints)yield return null;
        }else{
            while(loadingPaints<=historyRequestPaints&&Time.frameCount<historyRequestFrame+4)yield return null;
        }
        yield return new WaitForEndOfFrame();
        try{if(Current==Page.Edit&&ReferenceEquals(Project,expected)){Restore(reverse);SyncEditorNote();}}
        catch(Exception e){status=historyLabel+": "+e.Message;}
        finally{restoringHistory=false;historyLabel="";}
    }
    void EditorBoundary(bool end){
        if(InputBlocked||Current!=Page.Edit)return;
        Audio.Stop();EditorSeek(end?Project.Length:0);
        if(!end)editScroll.x=0;
    }
    bool EditorTransportButton(Rect r,bool end){
        bool hit=Button(r,"",false,true,15,end?"edit-end":"edit-start");
        GUI.Label(r,new GUIContent("",end?T("末尾へ","Go to end"):T("先頭へ","Go to start")),GUIStyle.none);
        Color color=GUI.enabled?Color.white:muted;float direction=end?1:-1;
        RectFill(new Rect(r.center.x+direction*9-1,r.center.y-8,2,16),color);
        for(int row=-8;row<8;row++){
            float span=11*(1-Math.Abs(row+.5f)/8);
            RectFill(new Rect(end?r.center.x-6:r.center.x+6-span,r.center.y+row,span,1),color);
        }
        return hit;
    }
}
}
