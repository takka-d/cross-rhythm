using System;
using UnityEngine;
namespace CrossRhythm {
public partial class CrossRhythmApp {
    enum ConfigSection { Folder, Audio, Timing, Controls, Display }
    ConfigSection configSection;
    void SetConfigSection(ConfigSection section){configSection=section;GUI.FocusControl(null);menuActivate="";}
    void ConfigPage(){
        float x=(W-1160)/2;Text(new Rect(x,102,800,50),"Config",38,Color.white,true);
        var menu=new Rect(x,174,216,H-226);RoundFill(menu,panel);
        var sections=(ConfigSection[])Enum.GetValues(typeof(ConfigSection));
        for(int i=0;i<sections.Length;i++)if(Button(new Rect(x+12,190+i*59,192,46),sections[i].ToString(),configSection==sections[i],true,18,"config-section:"+i,ControlKind.Tab))SetConfigSection(sections[i]);
        var r=new Rect(x+240,174,920,H-226);RoundFill(r,panel);
        float left=r.x+28,top=r.y+24,width=r.width-56;
        string heading=configSection==ConfigSection.Folder?"Project Folder":configSection.ToString();
        Text(new Rect(left,top,width,36),heading,25,Color.white,true);
        RectFill(new Rect(left,top+54,width,1),line);
        switch(configSection){
            case ConfigSection.Folder:
                ProjectFolderRow(new Rect(left,top+86,width,38));
                Text(new Rect(left,top+143,width,26),T("Songsに表示する曲のフォルダー","Folder containing the tracks shown in Songs"),16,muted);
                FittedText(new Rect(left,top+190,width,30),CurrentFileLine(),15,muted);
                break;
            case ConfigSection.Audio:
                Audio.BackingGain=VolumeRow(left,top+89,"Music",Audio.BackingGain,"musicVolume");
                Audio.DrumGain=VolumeRow(left,top+157,"Drums",Audio.DrumGain,"drumsVolume");
                float old=uiFeedback.Gain;uiFeedback.Gain=VolumeRow(left,top+225,T("効果音","UI Sounds"),old,"uiVolume");
                if(Math.Abs(old-uiFeedback.Gain)>.001&&Event.current.type==EventType.MouseUp)uiFeedback.Confirm();
                break;
            case ConfigSection.Timing:
                Text(new Rect(left,top+89,width,32),T("入力タイミングの補正","Input offset"),19);
                Text(new Rect(left,top+139,180,40),$"{inputOffset:+0;-0;0} ms",28,mint,true);
                float shift=GUI.HorizontalSlider(new Rect(left+190,top+154,530,20),(float)inputOffset,-200,200);
                if(Math.Abs(shift-inputOffset)>.5){inputOffset=Math.Round(shift);PlayerPrefs.SetFloat("offset",(float)inputOffset);PlayerPrefs.Save();}
                Text(new Rect(left,top+203,width,26),T("＋: 入力を早い時刻として補正","＋: compensate delayed input"),16,muted);
                break;
            case ConfigSection.Controls:
                BindingPanel(new Rect(r.x+4,top+16,r.width-8,304),false);
                break;
            case ConfigSection.Display:
                Text(new Rect(left,top+89,220,34),"Language",19);
                if(Button(new Rect(left+230,top+85,184,42),"日本語",!english,true,16,kind:ControlKind.Choice))SetLanguage(false);
                if(Button(new Rect(left+426,top+85,184,42),"English",english,true,16,kind:ControlKind.Choice))SetLanguage(true);
                Text(new Rect(left,top+159,220,34),T("レーンの上下","Lane Order"),19);
                LaneOrderControls(new Rect(left+230,top+155,380,42));
                Text(new Rect(left,top+221,width,26),T("Songs・Practiceと共通の設定です。変更は自動保存されます。","Shared with Songs and Practice. Changes save automatically."),15,muted);
                break;
        }
    }
    void SetLanguage(bool value){english=value;status="";PlayerPrefs.SetInt("language",value?1:0);PlayerPrefs.Save();}
}
}
