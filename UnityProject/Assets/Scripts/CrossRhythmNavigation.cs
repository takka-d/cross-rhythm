using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    sealed class MenuItem {public string Id;public Vector2 Center;}
    readonly List<MenuItem> menuItems=new List<MenuItem>();
    string menuFocus="",menuActivate="";
    bool keyboardMenu,previewRequested,songPreviewEnabled;
    double songPreviewDue;
    ChartProject pendingSongAudio;
    void ResetMenuFocus(){menuFocus="";menuActivate="";keyboardMenu=false;menuItems.Clear();}
    void RequestSongPreview(){previewRequested=songPreviewEnabled;songPreviewDue=Time.unscaledTimeAsDouble+.2;}
    void SetSongPreview(bool enabled){songPreviewEnabled=enabled;PlayerPrefs.SetInt("songPreview",enabled?1:0);PlayerPrefs.Save();Audio.Stop();RequestSongPreview();}
    void UpdateSongPreview(){
        if(Current!=Page.Songs){previewRequested=false;return;}
        if(Time.unscaledTimeAsDouble<songPreviewDue)return;
        if(pendingSongAudio!=null){var p=pendingSongAudio;pendingSongAudio=null;Audio.Load(p);return;}
        if(songPreviewEnabled&&previewRequested&&loaded&&!busy){previewRequested=false;Audio.Preview();}
    }
    void FocusSong(int index){
        index=Mathf.Clamp(index,0,Library.Count-1);
        if(index!=selected)SelectProject(index);else if(!Audio.Backing.isPlaying)RequestSongPreview();
        menuFocus="song:"+index;
        float visible=H-344;if(index*138<songScroll.y)songScroll.y=index*138;else if(index*138+126>songScroll.y+visible)songScroll.y=index*138+126-visible;
    }
    void RegisterMenu(string id,Rect rect,bool active){
        if(Event.current.type!=EventType.Repaint||!active)return;
        menuItems.Add(new MenuItem{Id=id,Center=GUIUtility.GUIToScreenPoint(rect.center)});
    }
    void MenuKeys(){
        if(InputBlocked)return;
        var e=Event.current;if(e.type==EventType.Repaint&&padMenu.Count>0)e=new Event{type=EventType.KeyDown,keyCode=padMenu.Dequeue()};if(e.type!=EventType.KeyDown||discardPrompt||(Current==Page.Edit&&!MidiPromptOpen)||Current==Page.Play||Current==Page.Practice||GUI.GetNameOfFocusedControl().StartsWith("config-")||GUI.GetNameOfFocusedControl()=="location-readonly"||bindingsOpen)return;
        if(e.control||e.command||e.alt)return;
        if(e.keyCode==KeyCode.Escape){if(MidiPromptOpen)CancelMidi();else Navigate(Page.Title);e.Use();return;}
        bool enter=e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter;
        bool vertical=e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.DownArrow;
        if(Current==Page.Songs&&(menuFocus==""||menuFocus.StartsWith("song:"))){
            if(vertical){keyboardMenu=true;FocusSong(selected+(e.keyCode==KeyCode.DownArrow?1:-1));uiFeedback.Hover();e.Use();return;}
            if(enter){RequestBegin(false);e.Use();return;}
            if(e.keyCode==KeyCode.RightArrow){keyboardMenu=true;menuFocus="start";e.Use();return;}
        }
        if(Current==Page.Songs&&e.keyCode==KeyCode.LeftArrow&&menuFocus!=""){keyboardMenu=true;FocusSong(selected);e.Use();return;}
        if(enter){if(menuFocus==""){if(Current==Page.Title)Navigate(Page.Songs);else if(Current==Page.Result)Navigate(Page.Songs);}else menuActivate=menuFocus;e.Use();return;}
        bool tab=e.keyCode==KeyCode.Tab;
        if(!vertical&&!tab&&e.keyCode!=KeyCode.LeftArrow&&e.keyCode!=KeyCode.RightArrow)return;
        if(menuItems.Count==0)return;keyboardMenu=true;
        var current=menuItems.FirstOrDefault(i=>i.Id==menuFocus);
        if(tab||current==null){int at=current==null?-1:menuItems.IndexOf(current);int step=e.shift?-1:1;menuFocus=menuItems[(at+step+menuItems.Count)%menuItems.Count].Id;}
        else {
            Vector2 direction=e.keyCode==KeyCode.DownArrow?Vector2.up:e.keyCode==KeyCode.UpArrow?Vector2.down:e.keyCode==KeyCode.RightArrow?Vector2.right:Vector2.left;
            var next=menuItems.Where(i=>i!=current&&Vector2.Dot(i.Center-current.Center,direction)>1).OrderBy(i=>{var d=i.Center-current.Center;return Vector2.Dot(d,direction)+4*Math.Abs(direction.x==0?d.x:d.y);}).FirstOrDefault();
            if(next!=null)menuFocus=next.Id;
        }
        if(Current==Page.Songs&&menuFocus.StartsWith("song:")&&int.TryParse(menuFocus.Substring(5),out var song))FocusSong(song);
        uiFeedback.Hover();e.Use();
    }
}
}
