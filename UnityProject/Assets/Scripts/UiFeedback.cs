using System;
using UnityEngine;
namespace CrossRhythm {
public sealed class UiFeedback : MonoBehaviour {
    public float Gain=.35f;
    AudioSource voice;AudioClip hover,confirm;
    double lastHover=-1;
    string lastHoverControl="";Vector2 lastHoverPoint;
    void Awake(){voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;hover=Tone("UI hover",680,.025f);confirm=Tone("UI confirm",920,.065f);Gain=PlayerPrefs.GetFloat("uiVolume",.35f);}
    static AudioClip Tone(string name,float hz,float length){int rate=48000;var data=new float[(int)(rate*length)];for(int i=0;i<data.Length;i++){double t=(double)i/rate;double fade=Math.Min(1,t/.003)*Math.Pow(1-t/length,2);data[i]=(float)(Math.Sin(2*Math.PI*hz*t)*fade*.18);}var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;}
    public void Hover(){if(Gain<=0||Time.unscaledTimeAsDouble-lastHover<.07)return;lastHover=Time.unscaledTimeAsDouble;voice.PlayOneShot(hover,Gain*.45f);}
    public static bool NewHoverTarget(string previous,Vector2 previousPoint,string current,Vector2 currentPoint)=>previous!=current||(currentPoint-previousPoint).sqrMagnitude>=64;
    public void Hover(string control,Vector2 point){if(!NewHoverTarget(lastHoverControl,lastHoverPoint,control,point))return;lastHoverControl=control;lastHoverPoint=point;Hover();}
    public void Leave(string control){if(lastHoverControl==control)lastHoverControl="";}
    public void Confirm(){if(Gain>0)voice.PlayOneShot(confirm,Gain);}
    void OnDestroy(){if(hover)Destroy(hover);if(confirm)Destroy(confirm);}
}
}
