using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    bool gridMenuOpen;int gridMenuIndex;Vector2 gridMenuScroll;
    static readonly Rect GridButtonRect=new Rect(278,162,210,30);
    Rect GridMenuRect=>new Rect(278,198,292,Math.Min(416,H-230));
    string GridLabel(int q){int n=q==6||q==12||q==24||q==48?3:q==10||q==20||q==40?5:q==14||q==28||q==56?7:q==36?9:q==44?11:q==52?13:q==60?15:0;return "1/"+q+(n>0?" · "+n+T("連符","-tuplet"):"");}
    void SetEditorGrid(int q){if(q<1||q>1024)return;if((int?)Project.Chart["quantize"]!=q){PushUndo();Project.Chart["quantize"]=q;Edited();}gridField=q.ToString();gridMenuOpen=false;GUI.FocusControl(null);}
    void DismissGridMenu(){
        if(!gridMenuOpen)return;var e=Event.current;
        if(Current!=Page.Edit||(e.type==EventType.MouseDown&&!GridMenuRect.Contains(e.mousePosition))||(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape)){gridMenuOpen=false;GUIUtility.hotControl=0;e.Use();}
    }
    void EditorGridMenu(){
        if(!gridMenuOpen||Current!=Page.Edit)return;var r=GridMenuRect;
        RectFill(r,panel);Border(r,mint);Text(new Rect(r.x+12,r.y+8,r.width-24,24),T("入力間隔","Note spacing"),15,mint,true);
        var view=new Rect(r.x+8,r.y+38,r.width-16,r.height-112);var e=Event.current;
        if(e.type==EventType.KeyDown&&GUI.GetNameOfFocusedControl()!="edit-grid-custom"){
            if(e.keyCode==KeyCode.DownArrow||e.keyCode==KeyCode.UpArrow){gridMenuIndex=Mathf.Clamp(gridMenuIndex+(e.keyCode==KeyCode.DownArrow?1:-1),0,Grids.Length-1);gridMenuScroll.y=Mathf.Clamp(gridMenuIndex*32-64,0,Grids.Length*32-view.height);e.Use();}
            else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){SetEditorGrid(Grids[gridMenuIndex]);e.Use();return;}
        }
        gridMenuScroll=GUI.BeginScrollView(view,gridMenuScroll,new Rect(0,0,view.width-18,Grids.Length*32));int chosen=0;
        for(int i=0;i<Grids.Length;i++){if(Button(new Rect(0,i*32,view.width-22,28),GridLabel(Grids[i]),(int?)Project.Chart["quantize"]==Grids[i],true,14))chosen=Grids[i];if(i==gridMenuIndex)Border(new Rect(1,i*32+1,view.width-24,26),Color.white,1);}
        GUI.EndScrollView();if(chosen>0){SetEditorGrid(chosen);return;}
        Text(new Rect(r.x+12,r.yMax-65,r.width-24,22),T("その他: 1～1024分音符","Custom: 1 to 1024"),12,muted);
        Text(new Rect(r.x+12,r.yMax-37,24,26),"1/",14);gridField=EditField("grid-custom",new Rect(r.x+36,r.yMax-39,94,30),gridField);
        bool valid=int.TryParse(gridField,out int q)&&q>=1&&q<=1024;
        if(Button(new Rect(r.x+144,r.yMax-39,r.width-156,30),T("適用","Apply"),false,valid,14)||(e.type==EventType.KeyDown&&(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)&&valid)){SetEditorGrid(q);e.Use();}
    }
}
}
