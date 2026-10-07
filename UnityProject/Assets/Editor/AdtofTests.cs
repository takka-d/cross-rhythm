using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public static class AdtofTests {
    static void Check(bool ok,string name){if(!ok)throw new Exception("ADTOF TEST FAILED: "+name);Debug.Log("PASS "+name);}
    public static void Run(){
        var source=ChartProject.Demo();source.Chart["measures"]=new JArray(3.5,4);source.Chart["timeSignatures"]=new JArray(new JObject{{"numerator",7},{"denominator",8}},new JObject{{"numerator",4},{"denominator",4}});source.Chart["quantize"]=12;source.Chart["audioOffsetSec"]=1;source.Chart["bpm"]=120;source.Chart["events"]=new JArray();source.FilePath="folder/song.crproj";source.Baseline="original-hash";source.Rebuild();
        var before=source.Chart.ToString();var events=new JArray(new JObject{{"time",1+(3.5+1.0/3)*.5},{"instrument","HH"},{"confidence",.8}},new JObject{{"time",1+(3.5+1.0/3)*.5+.002},{"instrument","HH"}},new JObject{{"time",.5},{"instrument","BD"}},new JObject{{"time",6.5},{"instrument","CR"}},new JObject{{"time",2},{"instrument","MT"}});
        var result=AdtofDraft.Convert(source,events,8,true);
        Check(source.Chart.ToString()==before,"ADTOF generation does not mutate source");
        var hit=result.Events.Single(n=>(string)n["instrument"]=="HH");Check((int)hit["measure"]==1&&Math.Abs((double)hit["beat"]-1.0/3)<1e-8,"ADTOF audio offset and local triplet grid after 7/8");
        Check(result.Events.Count(n=>(string)n["instrument"]=="HHSTATE")==1&&result.Events.Count==4,"deduplicates snapped hats and rejects pre-offset hits");
        Check(result.Layout.Measures.Length>2&&(int)result.Events.Single(n=>(string)n["instrument"]=="CR")["measure"]>=2,"ADTOF extends measures for later detections");
        var trimmed=AdtofDraft.Convert(source,events,8,false);Check(trimmed.Layout.Measures.Length==2&&!trimmed.Events.Any(n=>(string)n["instrument"]=="CR"),"no extension discards out-of-chart notes");
        var applied=result.Apply(source,true);var roundtrip=new ChartProject{Chart=applied,Manifest=source.Manifest};roundtrip.Rebuild();Check(JToken.DeepEquals(applied,ChartProject.Read(roundtrip.Write(),"test.crproj").Chart),"ADTOF draft persists losslessly");
        var copy=CrossRhythmApp.CopyForEditor(source);copy.Chart["title"]="Changed";copy.Manifest["artist"]="Editor";copy.Files["test"]=new byte[]{1};Check(source.Chart.ToString()==before&&source.Manifest["artist"]==null&&!source.Files.ContainsKey("test"),"Songs to Edit isolates chart metadata and files");
        Check(copy.FilePath==source.FilePath&&copy.Baseline==source.Baseline,"Songs Edit keeps the original save identity and conflict baseline");
        bool rejected=false;try{AdtofDraft.Convert(source,null,2,true);}catch(ArgumentException){rejected=true;}Check(rejected,"malformed ADTOF response cannot change chart");
        Debug.Log("CROSS_RHYTHM_ADTOF_TESTS_PASS");
    }
}
}
