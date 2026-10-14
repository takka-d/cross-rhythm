using System;
using UnityEngine;
namespace CrossRhythm {
public sealed class UiFeedback : MonoBehaviour {
    public float Gain=.35f;
    AudioSource voice;AudioClip hover,confirm;
    double lastHover=-1;
    Rect lastHoverRegion;bool hoverLatched,hoverSeenThisFrame;
    void Awake(){voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;hover=Tone("UI hover",680,.025f);confirm=Tone("UI confirm",920,.065f);Gain=PlayerPrefs.GetFloat("uiVolume",.35f);}
    static AudioClip Tone(string name,float hz,float length){int rate=48000;var data=new float[(int)(rate*length)];for(int i=0;i<data.Length;i++){double t=(double)i/rate;double fade=Math.Min(1,t/.003)*Math.Pow(1-t/length,2);data[i]=(float)(Math.Sin(2*Math.PI*hz*t)*fade*.18);}var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;}
    public void Hover(){if(Gain<=0||Time.unscaledTimeAsDouble-lastHover<.07)return;lastHover=Time.unscaledTimeAsDouble;voice.PlayOneShot(hover,Gain*.45f);}
    public static bool NewHoverTarget(bool latched,Rect previousRegion,Vector2 currentPoint)=>!latched||!previousRegion.Contains(currentPoint);
    public void BeginHoverFrame(){hoverSeenThisFrame=false;}
    public bool Hover(string _,Rect screenRegion,Vector2 screenPoint){
        hoverSeenThisFrame=true;bool entered=NewHoverTarget(hoverLatched,lastHoverRegion,screenPoint);
        lastHoverRegion=screenRegion;hoverLatched=true;if(entered)Hover();return entered;
    }
    public void EndHoverFrame(){if(hoverSeenThisFrame)return;hoverLatched=false;lastHoverRegion=new Rect();}
    public void Confirm(){if(Gain>0)voice.PlayOneShot(confirm,Gain);}
    void OnDestroy(){if(hover)Destroy(hover);if(confirm)Destroy(confirm);}
}
}
