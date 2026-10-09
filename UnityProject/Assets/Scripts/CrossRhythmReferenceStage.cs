using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    readonly Dictionary<string,Texture2D> referenceTextures=new Dictionary<string,Texture2D>();
    sealed class StageEffect {public float X,Y;public double Time;public string Judge;}
    readonly List<StageEffect> stageEffects=new List<StageEffect>();
    Texture2D ReferenceTexture(string name){if(!referenceTextures.TryGetValue(name,out var t)){var data=Resources.Load<TextAsset>("Stage175/"+name);if(data!=null){t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(data.bytes,true);t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;}referenceTextures[name]=t;}return t;}
    void ReferenceSprite(string name,Rect rect,float z,float alpha=1,Color? tint=null,Rect? uv=null){var texture=ReferenceTexture(name);if(texture==null)return;var old=GUI.color;GUI.color=(tint??Color.white)*new Color(1,1,1,alpha);var target=new Rect(rect.x*z,rect.y*z,rect.width*z,rect.height*z);if(uv.HasValue)GUI.DrawTextureWithTexCoords(target,texture,uv.Value);else GUI.DrawTexture(target,texture);GUI.color=old;}
    static Rect AtlasUV(int i,int cols,int rows)=>new Rect((i%cols)/(float)cols,1-(i/cols+1)/(float)rows,1f/cols,1f/rows);
    double ReferencePosition(double b){int bar=Project.BarAt(b);double start=bar<0?bar*CountIn.Length(Project):Project.Starts[bar],length=bar<0?CountIn.Length(Project):Project.Measures[bar];return bar+Math.Max(0,Math.Min(1,(b-start)/length));}
    float ReferenceTop(int bar,double b)=>520+(float)(bar-ReferencePosition(b))*204;
    void ReferenceGlyph(ChartNote note,float x,float y,float z,float alpha,bool outline=false,bool missed=false){
        if(note.Articulation=="tambourine"){
            float radius=(12+note.Velocity*2)*z;var center=new Vector2(x*z,y*z);
            Color color=(missed?muted:C("#e3c18c"))*new Color(1,1,1,alpha);
            // A ring with four paired jingles stays legible at the same note footprint.
            for(int i=0;i<24;i++){float a=i*Mathf.PI/12;RectFill(new Rect(center.x+Mathf.Cos(a)*radius-z,center.y+Mathf.Sin(a)*radius-z,2*z,2*z),color);}
            for(int i=0;i<4;i++){float a=i*Mathf.PI/2;RectFill(new Rect(center.x+Mathf.Cos(a)*radius-3*z,center.y+Mathf.Sin(a)*radius-2*z,6*z,4*z),color);}
            return;
        }
        ReferenceSprite(note.Instrument+"-"+(outline?"outline":missed?"miss":"normal")+"-"+note.Velocity,new Rect(x-40,y-40,80,80),z,alpha);}
    void ReferenceLine(float x,float y,float width,float height,float z,Color color)=>RectFill(new Rect(x*z,y*z,width*z,height*z),color);
    void ReferenceBeat(float x,float top,float width,float z,float alpha){
        if(Math.Abs(width-242.5f)<.001f){ReferenceSprite("beat",new Rect(x-16,top-16,274.5f,200),z,alpha);return;}
        // Preserve the original eight-pixel shadow rather than stretching it with the beat width.
        float[] sourceX={0,32,242.5f,274.5f},targetX={x-16,x+16,x+width-16,x+width+16};
        for(int i=0;i<3;i++)ReferenceSprite("beat",new Rect(targetX[i],top-16,targetX[i+1]-targetX[i],200),z,alpha,uv:new Rect(sourceX[i]/274.5f,0,(sourceX[i+1]-sourceX[i])/274.5f,1));
    }
    void ReferenceMeasure(int m,double b,float z){
        float top=ReferenceTop(m,b);if(top+168<-80||top>1020)return;
        bool current=m==Project.BarAt(b),count=m<0;float d=Math.Abs(top+84-520),alpha=d<125?1:d<360?.46f:.16f;double length=count?CountIn.Length(Project):Project.Measures[m],start=count?m*length:Project.Starts[m];
        ReferenceSprite("panel-"+(current?"active":"quiet")+(count?"-count":""),new Rect(0,top-20,1260,208),z,alpha);
        foreach(double q in StageGrid(m)){
            double subdivision=q/ChartVisuals.BeatUnit(Project,m)*2;
            bool beat=ChartVisuals.IsBeatLine(Project,m,q),eighth=Math.Abs(subdivision-Math.Round(subdivision))<1e-8,edge=Math.Abs(q)<1e-8||Math.Abs(q-length)<1e-8;
            float w=beat?(edge?1.2f:1.05f):eighth?.9f:.75f,a=alpha*(beat?(edge?.13f:.10f)*.94f:eighth?.055f*.82f:.032f*.72f);
            float x=220+(float)(q/length)*970;ReferenceLine(x-w/2,top,w,168,z,new Color(1,1,1,a));
        }
        if(current){double unit=ChartVisuals.BeatUnit(Project,m),beat=Math.Floor(Math.Max(0,Math.Min(length-1e-8,b-start))/unit)*unit;ReferenceBeat(220+(float)(beat/length)*970,top,Math.Min(970,(float)(970*unit/length)),z,alpha);}
        int labelIndex=count?0:m+1;Color labelColor=C(current?"#f1fbff":"#d9e3ec");
        if(labelIndex<=512)ReferenceSprite("bar-labels",new Rect(224,top-36,80,32),z,alpha,labelColor,AtlasUV(labelIndex,16,33));
        else Text(new Rect(224*z,(top-36)*z,120*z,32*z),"M"+(m+1),Mathf.RoundToInt(18*z),labelColor*new Color(1,1,1,alpha),true);
        int meterIndex=(int)Math.Round(length*16)-1;float meterX=count?292:290;
        var meter=Project.Meter(count?0:m);
        if(meter.Item2!=4)Text(new Rect(meterX*z,(top-31)*z,100*z,26*z),meter.Item1+"/"+meter.Item2,Mathf.RoundToInt(13*z),muted*new Color(1,1,1,alpha));
        else if(meterIndex>=0&&meterIndex<256&&Math.Abs(length*16-Math.Round(length*16))<1e-7)ReferenceSprite("meters",new Rect(meterX,top-36,80,32),z,alpha,C(current?"#a9e6ff":"#a7b6c4"),AtlasUV(meterIndex,16,16));
        else Text(new Rect(meterX*z,(top-31)*z,100*z,26*z),length.ToString("0.##")+"/4",Mathf.RoundToInt(13*z),muted);
        if(count){int beats=Project.Meter(0).Item1;for(int q=0;q<beats;q++){
            var rect=new Rect(220+(q+.5f)/beats*970-20,top-40,40,32);
            if(q<4)ReferenceSprite("count-numbers",rect,z,uv:AtlasUV(q,4,1));
            else Text(new Rect(rect.x*z,(top-24)*z,40*z,32*z),(q+1).ToString(),Mathf.RoundToInt(10*z),muted);
        }return;}
        var visible=Project.Notes.Where(n=>n.Measure==m&&!n.Pedal&&NoteVisible(n)).ToArray();var offsets=ChartVisuals.SimultaneousOffsets(visible);
        foreach(var n in visible){bool missed=judged.TryGetValue(n.Index,out var hit)&&hit.Judge=="MISS";ReferenceGlyph(n,ChartVisuals.CellX(Project,m,n.Local,220,970)+offsets[n.Index],LaneY(n.Lane,top,42),z,(missed?.66f:.98f)*alpha,false,missed);}
        DrawPedalRanges(m,start,length,b,top*z,42*z,220*z,970*z,z,alpha);
    }
    void ReferencePedalBar(float x,float y,float width,float z,float alpha){
        if(width<24){ReferenceSprite("pedal-bar",new Rect(x-4,y-16,width+8,32),z,alpha);return;}
        float[] sx={0,16,112,128},tx={x-4,x+12,x+width-12,x+width+4};
        for(int i=0;i<3;i++)ReferenceSprite("pedal-bar",new Rect(tx[i],y-16,tx[i+1]-tx[i],32),z,alpha,uv:new Rect(sx[i]/128,0,(sx[i+1]-sx[i])/128,1));
    }
    void ReferenceGuide(double b,float z){
        float top=ReferenceTop(Math.Max(0,Project.BarAt(b)),b);if(top+168<-90||top>1030)return;
        ReferenceSprite(pro?"guide-pro":"guide-normal",new Rect(0,top,220,168),z);
        if(!pro){DrawKey(0,0,72,top+42,Key.Digit4,true);DrawKey(0,1,128,top+42,Key.Digit9,true);DrawKey(1,0,72,top+126,Key.V,true);DrawKey(1,1,128,top+126,Key.M,true);}
        else{
            var keys=new[]{new[]{Key.R,Key.O},new[]{Key.E,Key.P},new[]{Key.Digit4,Key.Digit9},new[]{Key.Digit3,Key.Digit0},new[]{Key.V,Key.M},new[]{Key.C,Key.Comma}};
            for(int i=0;i<6;i++){float x=14+(i%2)*88,cy=top+(i<4?2+(i/2)*43+25:122);DrawKey(i+2,0,x+31,cy,keys[i][0],false);DrawKey(i+2,1,x+64,cy,keys[i][1],false);}
        }
        void DrawKey(int group,int key,float x,float y,Key code,bool large){float w=large?72:53,h=large?62:46;ReferenceSprite($"key-{group}-{key}-"+(held.Contains(code)?1:0),new Rect(x-w/2,y-h/2,w,h),z);string text=BoundLabel(code);if(text!=ControlBindings.KeyName(code)){var box=new Rect((x-(large?20:14))*z,(y-11)*z,(large?40:28)*z,22*z);RectFill(box,held.Contains(code)?C("#31546b"):C("#141e27"));FittedText(box,text,Math.Max(9,Mathf.RoundToInt(15*z)),Color.white);}}
    }
    void AddReferenceEffect(ChartNote note,string judge){double b=Audio.Beat;stageEffects.Add(new StageEffect{X=ChartVisuals.CellX(Project,note.Measure,note.Local,220,970),Y=LaneY(note.Lane,ReferenceTop(note.Measure,b),42),Time=Time.realtimeSinceStartupAsDouble,Judge=judge});}
    void ReferencePlayfield(Rect area,double b){
        float z=Math.Min(area.width/1260,area.height/940);var canvas=new Rect(area.x+(area.width-1260*z)/2,area.y+(area.height-940*z)/2,1260*z,940*z);
        GUI.BeginGroup(canvas);ReferenceSprite("backdrop",new Rect(0,0,1260,940),z);
        int current=Project.BarAt(b);for(int m=Math.Max(-2,current-2);m<=Math.Min(Project.Measures.Length-1,current+3);m++)ReferenceMeasure(m,b,z);
        if(b>=0){var visible=Project.Notes.Where(n=>!n.Pedal&&n.Lane!="F2"&&NoteVisible(n)&&n.Beat>=b-1e-8&&n.Beat<=b+1.02).ToArray();var offsets=ChartVisuals.SimultaneousOffsets(visible);foreach(var n in visible){double d=n.Beat-b;float t=Mathf.Clamp01((float)(1-d));t=t*t*(3-2*t);float a=Mathf.Clamp01((float)(d/.14));if(a<=.01)continue;ReferenceGlyph(n,ChartVisuals.CellX(Project,n.Measure,n.Local,220,970)+offsets[n.Index],58+(LaneY(n.Lane,ReferenceTop(n.Measure,b),42)-58)*t,z,.98f*a,true);}}
        ReferenceGuide(b,z);
        stageEffects.RemoveAll(e=>Time.realtimeSinceStartupAsDouble-e.Time>=.7);foreach(var effect in stageEffects){int frame=Math.Min(69,(int)((Time.realtimeSinceStartupAsDouble-effect.Time)*100));ReferenceSprite("fx-"+effect.Judge,new Rect(effect.X-64,effect.Y-100,128,160),z,uv:AtlasUV(frame,10,7));}
        GUI.EndGroup();
    }
}
}
