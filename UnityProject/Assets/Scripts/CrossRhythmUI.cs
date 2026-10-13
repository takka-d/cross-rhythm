using System;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    readonly System.Collections.Generic.Dictionary<int,GUIStyle> textStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>(),buttonStyles=new System.Collections.Generic.Dictionary<int,GUIStyle>();
    GUIStyle TextStyle(int size,bool bold=false,bool centered=false){int key=size+(bold?256:0)+(centered?512:0);if(!textStyles.TryGetValue(key,out var value)){value=new GUIStyle(centered?center:label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,padding=new RectOffset(0,0,0,0),clipping=TextClipping.Overflow};textStyles[key]=value;}return value;}
    void Styles(){if(label!=null)return;label=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=false};label.normal.textColor=Color.white;small=new GUIStyle(label){fontSize=14};small.normal.textColor=muted;title=new GUIStyle(label){fontSize=38,fontStyle=FontStyle.Bold};big=new GUIStyle(title){fontSize=72};center=new GUIStyle(label){alignment=TextAnchor.MiddleCenter};button=new GUIStyle(label){alignment=TextAnchor.MiddleCenter,padding=new RectOffset(14,14,6,6)};field=new GUIStyle(GUI.skin.textField){font=font,fontSize=18,padding=new RectOffset(10,10,8,8)};}
    void RectFill(Rect r,Color c){var old=GUI.color;GUI.color=old*c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
    void Border(Rect r,Color c,float width=1){RectFill(new Rect(r.x,r.y,r.width,width),c);RectFill(new Rect(r.x,r.yMax-width,r.width,width),c);RectFill(new Rect(r.x,r.y,width,r.height),c);RectFill(new Rect(r.xMax-width,r.y,width,r.height),c);}
    void Text(Rect r,string s,int size=18,Color? color=null,bool bold=false){if(Event.current.type!=EventType.Repaint)return;var st=TextStyle(size,bold);st.normal.textColor=color??Color.white;GUI.Label(r,s,st);}
    enum ControlKind { Action, Menu, Dialog, Toggle, Tab, Choice }
    bool Button(Rect r,string text,bool primary=false,bool enabled=true,int fontSize=18,string focusKey=null,ControlKind kind=ControlKind.Action){
        bool active=GUI.enabled&&enabled,hover=active&&(r.Contains(Event.current.mousePosition)||ControllerContains(r));
        string id=focusKey??(Current+":"+text+":"+r.x+":"+r.y);RegisterMenu(id,r,active);bool focused=keyboardMenu&&menuFocus==id;
        if(Event.current.type==EventType.Repaint){if(hover&&hoverButton!=id){hoverButton=id;uiFeedback.Hover();}else if(!hover&&hoverButton==id)hoverButton="";}
        bool pressed=hover&&((UnityEngine.InputSystem.Mouse.current!=null&&UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)||(controllerPointerVisible&&controllerPrimaryHeld));
        Color fill=primary?mint:panel;if(!active)fill=Color.Lerp(bg,fill,.45f);else if(pressed)fill=Color.Lerp(fill,Color.white,.25f);else if(hover)fill=Color.Lerp(fill,mint,primary?.14f:.24f);
        if(flashButton==id&&Time.unscaledTimeAsDouble<buttonFlashUntil)fill=Color.Lerp(fill,Color.white,.22f);
        bool stateControl=kind==ControlKind.Toggle||kind==ControlKind.Tab||kind==ControlKind.Choice;
        if(stateControl)fill=Color.Lerp(panel,mint,primary?.17f:hover?.10f:0);
        float radius=kind==ControlKind.Toggle?r.height/2:5;
        if(kind==ControlKind.Tab){if(primary||hover)RoundFill(r,fill,5);if(primary)RectFill(new Rect(r.x+8,r.yMax-3,r.width-16,3),mint);}
        else {RoundFill(r,fill,radius);RoundBorder(r,active&&(primary||hover)?mint:line,hover?1.5f:1,radius);}
        if(active&&focused)RoundBorder(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),Color.white,2,radius+2);
        int pad=kind==ControlKind.Action?Mathf.Min(14,Mathf.RoundToInt(r.width*.14f)):2,styleKey=fontSize*32+pad;if(!buttonStyles.TryGetValue(styleKey,out var style)){style=new GUIStyle(button){fontSize=fontSize,padding=new RectOffset(pad,pad,2,2)};buttonStyles[styleKey]=style;}
        style.normal.textColor=!active?muted:primary&&!stateControl?bg:Color.white;style.hover.textColor=style.active.textColor=style.normal.textColor;
        string hint=kind==ControlKind.Menu?T("メニューを開く","Open menu"):kind==ControlKind.Dialog?T("ダイアログを開く","Open dialog"):kind==ControlKind.Toggle?(primary?"ON":"OFF"):"";
        bool previous=GUI.enabled;GUI.enabled=active;bool hit=GUI.Button(r,new GUIContent("",hint),GUIStyle.none);if(ControllerButtonHit(r,active))hit=true;
        var caption=r;
        if(kind==ControlKind.Menu||kind==ControlKind.Dialog){caption.width-=24;if(kind==ControlKind.Menu){for(int i=0;i<4;i++)RectFill(new Rect(r.xMax-18+i,r.center.y-2+i,8-2*i,1),style.normal.textColor);}else Text(new Rect(r.xMax-25,r.y,22,r.height),"…",fontSize+1,style.normal.textColor);}
        else if(kind==ControlKind.Toggle){
            if(r.width<100){caption.width-=10;var dot=new Rect(r.xMax-11,r.center.y-3,6,6);if(primary)RoundFill(dot,mint,3);else RoundBorder(dot,muted,1,3);}
            else {caption.width-=42;var track=new Rect(r.xMax-38,r.center.y-7,30,14);RoundFill(track,primary?mint:line,7);RoundFill(new Rect(track.x+(primary?17:3),track.y+3,8,8),primary?bg:muted,4);}
        }
        else if(kind==ControlKind.Choice){caption.x+=12;caption.width-=12;RoundBorder(new Rect(r.x+7,r.center.y-4,8,8),primary?mint:muted,1,4);if(primary)RoundFill(new Rect(r.x+9,r.center.y-2,4,4),mint,2);}
        GUI.Label(caption,text,style);if(active&&menuActivate==id){hit=true;menuActivate="";}GUI.enabled=previous;
        if(hit){if(GUI.GetNameOfFocusedControl()=="location-readonly")GUI.FocusControl(null);menuFocus=id;flashButton=id;buttonFlashUntil=Time.unscaledTimeAsDouble+.16;uiFeedback.Confirm();}return hit;
    }

    void OnGUI(){
        Styles();scale=Mathf.Min(Screen.width/1280f,Screen.height/800f);W=Screen.width/scale;H=Screen.height/scale;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));RectFill(new Rect(0,0,W,H),bg);
        if(InputBlocked){LoadingBackground();LoadingPage();DrawControllerPointer();return;}
        if(Project==null)return;DismissEditorFileMenu();DismissLaneTypeMenu();DismissGridMenu();BindingKeys();StageExitKeys();MenuKeys();if(Event.current.type==EventType.Repaint)menuItems.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
        if(Event.current.type==EventType.Repaint){fileButtonCount=0;PlatformFiles.CRProjectButtonsBegin();}
#endif
        bool stage=Current==Page.Play||Current==Page.Practice;
        bool uiEnabled=GUI.enabled;GUI.enabled=uiEnabled&&!stageExitPrompt&&!bindingsOpen&&!discardPrompt&&!MidiPromptOpen&&!LaneTypeOpen&&!gridMenuOpen&&!(Current==Page.Edit&&(showMeterPanel||draftRunning||editorFileOpen));
        if(!stage)Header();
        GUI.enabled=uiEnabled&&!stageExitPrompt&&!bindingsOpen&&!discardPrompt&&!MidiPromptOpen&&!LaneTypeOpen&&!gridMenuOpen;
        switch(Current){case Page.Title:TitlePage();break;case Page.Songs:SongsPage();break;case Page.Config:ConfigPage();break;case Page.Result:ResultPage();break;case Page.Play:case Page.Practice:Stage();break;case Page.Edit:EditorPage();break;}
        editTextFocused=Current==Page.Edit&&GUI.GetNameOfFocusedControl().StartsWith("edit-");
        DisplaySizeButton();
        GUI.enabled=uiEnabled;
        if(bindingsOpen)BindingSettings();
        StageExitDialog();UnsavedPrompt();MidiOverlapPrompt();EditorLaneTypeMenu();EditorGridMenu();
#if UNITY_WEBGL && !UNITY_EDITOR
        if(Event.current.type==EventType.Repaint){PlatformFiles.CRProjectButtonsEnd();PlatformFiles.CRKeyboardFileMode(keyboardMenu&&menuFocus.StartsWith("file:")?int.Parse(menuFocus.Substring(5)):-1);}
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        PlatformFiles.CREditorKeys(Current==Page.Edit&&!showMeterPanel&&!discardPrompt&&!draftRunning&&!editorFileOpen&&!MidiPromptOpen&&!LaneTypeOpen&&!gridMenuOpen?1:0,editTextFocused?1:0);
#endif
        if(!stage&&Current!=Page.Result)Text(new Rect(26,H-28,W-52,24),busy?T("読込中…","Loading…"):status,13,muted);
        DrawControllerPointer();
    }
    void Header(){
        Text(new Rect(26,18,250,42),"CROSS RHYTHM",21,mint,true);
        bool baseEnabled=GUI.enabled;
        var homeRect=new Rect(20,8,270,56);if(baseEnabled&&(GUI.Button(homeRect,GUIContent.none,GUIStyle.none)||ControllerButtonHit(homeRect,true)))Navigate(Page.Title);
        GUI.enabled=baseEnabled;
        float x=W-522;
        if(Button(new Rect(x,14,140,42),"Songs",Current==Page.Songs,kind:ControlKind.Tab))Navigate(Page.Songs);
        if(Button(new Rect(x+152,14,140,42),"Edit",Current==Page.Edit,kind:ControlKind.Tab))Navigate(Page.Edit);
        if(Button(new Rect(x+304,14,140,42),"Config",Current==Page.Config,kind:ControlKind.Tab))Navigate(Page.Config);
        GUI.enabled=baseEnabled;
        RectFill(new Rect(0,70,W,1),line);
    }
    void TitlePage(){
        float x=Mathf.Max(80,(W-1080)/2);
        Text(new Rect(x,215,900,100),"CROSS",88,Color.white,true);
        Text(new Rect(x,302,950,112),"RHYTHM",88,mint,true);
        Text(new Rect(x,434,1000,38),"Play the music you own to the fullest",26,Color.white);
        Text(new Rect(x,473,1000,38),"Feel the groove through drums",26,muted);
        if(Button(new Rect(x,553,290,64),"Songs",true))Navigate(Page.Songs);
        if(Button(new Rect(x+308,553,180,64),"Edit"))Navigate(Page.Edit);
        if(Button(new Rect(x+506,553,180,64),"Config"))Navigate(Page.Config);
        Text(new Rect(x,H-85,900,30),"Windows / Web   ·   Unity Preview "+Application.version,14,muted);
        for(int i=0;i<7;i++){float h=35+i%3*15;RectFill(new Rect(W-260+i*22,240+i*16,7,h),new Color(mint.r,mint.g,mint.b,.18f+i*.04f));}
    }
    void FittedText(Rect r,string value,int size,Color color,bool bold=false){
        if(Event.current.type!=EventType.Repaint)return;
        var content=new GUIContent(value,value);var style=TextStyle(size,bold);
        while(size>12&&style.CalcSize(content).x>r.width)style=TextStyle(--size,bold);
        var old=style.clipping;style.clipping=TextClipping.Clip;style.normal.textColor=color;GUI.Label(r,content,style);style.clipping=old;
    }
    string ArtistDisplay(ChartProject p)=>string.IsNullOrWhiteSpace(p.Artist)?T("アーティスト未設定","Artist not set"):p.Artist;
    void SongsPage(){float x=(W-1160)/2;Text(new Rect(x,107,800,55),"Songs",38,Color.white,true);
        ProjectFolderRow(new Rect(x,169,1160,35));
        Text(new Rect(x,212,680,25),Library.Count+" tracks",14,muted);
        if(Button(new Rect(x+934,210,226,32),"Preview "+(songPreviewEnabled?"ON":"OFF"),songPreviewEnabled,true,14,"song-preview",ControlKind.Toggle))SetSongPreview(!songPreviewEnabled);
        var view=new Rect(x,254,660,H-344);songScroll=ControllerScrollView(view,songScroll,640,Library.Count*138);songScroll=GUI.BeginScrollView(view,songScroll,new Rect(0,0,640,Library.Count*138));
        for(int i=0;i<Library.Count;i++){
            var p=Library[i];var r=new Rect(0,i*138,630,126);bool choose=Button(r,"",false,true,18,"song:"+i);bool mouseMoved=UnityEngine.InputSystem.Mouse.current!=null&&UnityEngine.InputSystem.Mouse.current.delta.ReadValue().sqrMagnitude>0;bool hoverFocus=GUI.enabled&&Event.current.type==EventType.Repaint&&r.Contains(Event.current.mousePosition)&&mouseMoved||GUI.enabled&&Event.current.type==EventType.Repaint&&ControllerPointerMoved&&ControllerContains(r);if(i==selected){RectFill(r,new Color(.09f,.19f,.18f));Border(r,mint,2);}
            FittedText(new Rect(20,r.y+10,500,32),p.Title+(p.Dirty?" *":""),23,Color.white,true);
            FittedText(new Rect(20,r.y+46,490,25),ArtistDisplay(p),15,muted);
            FittedText(new Rect(20,r.y+70,510,21),p.FileName,12,muted);
            Text(new Rect(20,r.y+95,500,24),$"{p.TempoLabel} BPM   ·   {p.PlayableNoteCount} notes",13,muted);
            Text(new Rect(548,r.y+18,64,25),"LEVEL",11,muted);Text(new Rect(556,r.y+45,60,55),p.Difficulty,36,mint,true);
            if(choose||hoverFocus){if(hoverFocus)keyboardMenu=false;FocusSong(i);}
        }GUI.EndScrollView();
        float right=x+695;RectFill(new Rect(right,254,465,H-344),panel);
        FittedText(new Rect(right+28,268,408,43),Project.Title,26,Color.white,true);FittedText(new Rect(right+28,313,408,28),ArtistDisplay(Project),17,muted);
        Text(new Rect(right+28,337,315,32),$"{Project.TempoLabel} BPM    {(int)(Project.DurationSeconds/60)}:{(int)Project.DurationSeconds%60:00}",20,mint);
        Text(new Rect(right+341,332,70,22),"LEVEL",11,muted);Text(new Rect(right+352,355,70,50),Project.Difficulty,36,mint,true);
        Text(new Rect(right+28,425,118,25),busy?T("準備中…","Preparing…"):!songPreviewEnabled?"":Audio.Backing.isPlaying?"Preview ♪":Audio.Song==null?T("音源なし","No audio"):"",14,mint);
        LaneOrderControls(new Rect(right+154,418,282,32),13);
        float best=PlayerPrefs.GetFloat("best:"+Project.Title,-1);Text(new Rect(right+28,387,295,32),"Best   "+(best<0?"—":best.ToString("0.0")+" / 100"),18,muted);
        if(Button(new Rect(right+28,452,198,44),"Normal",!pro,kind:ControlKind.Choice)){pro=false;PlayerPrefs.SetInt("pro",0);}if(Button(new Rect(right+238,452,198,44),"Pro",pro,kind:ControlKind.Choice)){pro=true;PlayerPrefs.SetInt("pro",1);}
        if(Button(new Rect(right+28,H-256,408,62),"Start",true,SongActionsAvailable,18,"start"))RequestBegin(false);
        if(Button(new Rect(right+28,H-180,198,48),"Practice",false,SongActionsAvailable,18,"practice"))RequestBegin(true);
        if(Button(new Rect(right+238,H-180,198,48),"Edit",false,SongActionsAvailable,18,"edit-selected"))RequestEditSelected();
        if(libraryRefreshFailure!="")FittedText(new Rect(x,H-73,1150,30),LibraryRefreshMessage(),14,C("#edc779"));
        else Text(new Rect(x,H-73,1150,30),!HasExternalProjects?T("Open Folderで曲を開けます。Rhythm Checkは動作確認用です。","Open a folder to add tracks. Rhythm Check is a test track."):T("Open Folderで対象フォルダーを変更できます。","Change the project folder with Open Folder."),14,muted);
    }
    void ResultPage(){float x=(W-1160)/2;Text(new Rect(x,106,700,55),"Result",38,Color.white,true);Text(new Rect(x,170,1080,36),Project.Title,22,muted);RectFill(new Rect(x,240,275,345),panel);Text(new Rect(x+28,266,230,115),Result.Rank,92,mint,true);Text(new Rect(x+28,395,240,102),Result.Score.ToString("0.0"),64,Color.white,true);Text(new Rect(x+28,500,230,35),"/ 100",22,muted);
        string[] names={T("ノーツ判定","Accuracy"),T("打点の安定性","Hit stability"),T("拍の安定性","Beat stability")};double[] vals={Result.Accuracy*.4,Result.Hits*.3,Result.Beats*.3};for(int i=0;i<3;i++){float xx=x+299+i*290;RectFill(new Rect(xx,240,274,190),panel);Text(new Rect(xx+20,261,240,35),names[i],20);Text(new Rect(xx+20,316,240,66),vals[i].ToString("0.0"),44,mint,true);Text(new Rect(xx+20,385,240,30),"/ "+(i==0?40:30),16,muted);}
        Text(new Rect(x+319,459,820,40),$"JUST {Result.Just}     FAST {Result.Fast}     LATE {Result.Late}     MISS {Result.Miss}",20);
        string Ms(double? v)=>v.HasValue?v.Value.ToString("0.0")+" ms":"—";
        Text(new Rect(x+319,517,820,30),T("平均のずれ ","Mean offset ")+Ms(Result.Bias)+"    "+T("打点のばらつき ","Hit spread ")+Ms(Result.Spread),17,muted);Text(new Rect(x+319,552,820,30),T("拍間 ","Between beats ")+Ms(Result.Between)+"    "+T("拍内の位置 ","Within beat ")+Ms(Result.Within),17,muted);
        var graph=new Rect(x,606,1160,78);RectFill(graph,panel);RectFill(new Rect(graph.x,graph.center.y,graph.width,1),line);foreach(var r in Records.Where(r=>r.Judge!="MISS")){float xx=graph.x+(float)(r.Beat/Project.Length)*graph.width,yy=graph.center.y+(float)(r.Ms/120)*34;RectFill(new Rect(xx,yy,3,3),mint);}Text(new Rect(x,H-105,1100,30),T("ハイハットペダルは採点対象外。判定基準は調整中です。","HH pedal is not scored. Scoring thresholds are provisional."),14,muted);
        if(Button(new Rect(x,H-66,220,43),"Songs",true))NavigateNow(Page.Songs);if(Button(new Rect(x+238,H-66,160,43),"Retry"))Begin(false);
    }
}
}
