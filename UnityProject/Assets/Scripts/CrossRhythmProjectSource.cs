using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    readonly Dictionary<string,JObject> projectOrigins=new Dictionary<string,JObject>();
    JObject incomingSource,lastSource;
    UiFeedback uiFeedback;
    string hoverButton="",flashButton="";double buttonFlashUntil;
    int fileButtonCount;
    public void OnUiFeedback(string kind){if(kind=="hover")uiFeedback.Hover();else uiFeedback.Confirm();}
    string SourceLine(){
#if UNITY_WEBGL && !UNITY_EDITOR
        return (string)lastSource?["kind"]=="folder"?(string)lastSource["name"]:T("フォルダー未選択","No folder selected");
#else
        return string.IsNullOrEmpty(projectPath)?T("フォルダー未選択","No folder selected"):projectPath;
#endif
    }
    string CurrentFileLine(){
        var p=editorProject;if(p==null)return "Edit: —";
#if UNITY_WEBGL && !UNITY_EDITOR
        projectOrigins.TryGetValue(p.FilePath??"",out var origin);return "Edit: "+((string)origin?["relativePath"]??p.FileName);
#else
        return "Edit: "+(string.IsNullOrEmpty(p.FilePath)?p.FileName:p.FilePath);
#endif
    }
    GUIStyle locationField;
    void ReadOnlyLocation(Rect rect){
        if(locationField==null)locationField=new GUIStyle(field){fontSize=16,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(10,10,0,0)};
        var e=Event.current;
        if(GUI.GetNameOfFocusedControl()=="location-readonly"){
            bool copy=(e.control||e.command)&&(e.keyCode==KeyCode.C||e.keyCode==KeyCode.A);
            bool navigate=e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.Home||e.keyCode==KeyCode.End||e.keyCode==KeyCode.Tab;
            if((e.type==EventType.KeyDown&&!copy&&!navigate)||((e.type==EventType.ExecuteCommand||e.type==EventType.ValidateCommand)&&(e.commandName=="Paste"||e.commandName=="Cut"||e.commandName=="Delete")))e.Use();
        }
        GUI.SetNextControlName("location-readonly");GUI.TextField(rect,SourceLine(),locationField);
    }
    public void OnSourceRestored(string json){try{lastSource=JObject.Parse(json);}catch{}}
    bool HasExternalProjects=>Library.Exists(p=>!string.IsNullOrEmpty(p.FilePath));
    void ProjectButton(Rect r,bool folder,bool editor=false,bool primary=false,int fontSize=16){
        if(bindingsOpen)return;
        string caption=folder?"Open Folder":"Open Project";bool enabled=GUI.enabled&&!busy&&!discardPrompt&&!importBatch;
#if UNITY_WEBGL && !UNITY_EDITOR
        Button(r,"",primary,enabled,fontSize,"file:"+(folder?1:editor?2:0));
        if(Event.current.type==EventType.Repaint)PlatformFiles.CRProjectButton(fileButtonCount++,r.x/W,r.y/H,r.width/W,r.height/H,folder?1:editor?2:0,english?1:0,enabled?1:0,primary?1:0,fontSize*scale);
#else
        if(Button(r,caption,primary,enabled,fontSize))PickProject(folder,editor);
#endif
    }
    float VolumeRow(float x,float y,string caption,float value,string key){Text(new Rect(x,y,145,28),caption,17);float next=GUI.HorizontalSlider(new Rect(x+150,y+11,290,20),value,0,1);Text(new Rect(x+456,y,70,28),Mathf.RoundToInt(next*100)+"%",17,mint);if(Math.Abs(next-value)>.0001){PlayerPrefs.SetFloat(key,next);PlayerPrefs.Save();}return next;}
}
}
