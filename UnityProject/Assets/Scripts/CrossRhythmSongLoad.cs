using System;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    ChartProject pendingPerformance;
    string stageLoadError="";
    bool SongActionsAvailable=>Project!=null&&!importBatch&&incoming==null;
    void ProjectAudioReady(string error){
        if(!ReferenceEquals(Audio.Project,Project))return;
        if(FinishAudioImport(error))return;
        busy=false;loaded=string.IsNullOrEmpty(error);
        if(audioImportFailure!=""){status=audioImportFailure;audioImportFailure="";loadingFile="";Audio.AnchorBeat=Math.Min(audioImportBeat,Project.Length);return;}
        if(!loaded)status=error;
        else if(!status.StartsWith("読込:")&&!status.StartsWith("Opened:"))status=T("読込完了","Ready");
        if(!ReferenceEquals(pendingPerformance,Project))return;
        pendingPerformance=null;
        if(Current!=Page.Play&&Current!=Page.Practice)return;
        if(!loaded){stageLoadError=error;return;}
        StartPreparedPerformance();
    }
    void StartPreparedPerformance(){
        pendingPerformance=null;Audio.Rate=Current==Page.Practice?practiceSpeed:1;
        if(Current==Page.Practice){Audio.Stop();Audio.AnchorBeat=CountIn.Start(Project);}
        else Audio.Play(CountIn.Start(Project));
    }
}
}
