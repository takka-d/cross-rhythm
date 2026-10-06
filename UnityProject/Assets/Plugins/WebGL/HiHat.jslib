mergeInto(LibraryManager.library, {
  CRHatPrepare__deps:['$WEBAudio'],
  CRHatPrepare:function(data,frames,channels,rate){
    if(window.CRHats)window.CRHats.stop();
    const ctx=WEBAudio.audioContext,buffer=ctx.createBuffer(channels,frames,rate),pcm=HEAPF32.subarray(data/4,data/4+frames*channels);
    for(let ch=0;ch<channels;ch++){const out=buffer.getChannelData(ch);for(let i=0;i<frames;i++)out[i]=pcm[i*channels+ch];}
    window.CRHats={ctx:ctx,buffer:buffer,voices:[],stop:function(){for(const v of this.voices){try{v.source.stop();}catch(_){}}this.voices=[];}};
  },
  CRHatPlay:function(gain,pitch,delay){
    const h=window.CRHats;if(!h)return;const c=h.ctx,source=c.createBufferSource(),mix=c.createGain(),closure=c.createGain(),filter=c.createBiquadFilter();
    source.buffer=h.buffer;source.playbackRate.value=pitch;mix.gain.value=gain;closure.gain.value=1;filter.type='lowpass';filter.frequency.value=c.sampleRate*.5;filter.Q.value=20*Math.log10(Math.sqrt(.5));
    source.connect(mix);mix.connect(closure);closure.connect(filter);filter.connect(c.destination);
    const v={source:source,gain:closure,filter:filter,start:c.currentTime+Math.max(0,delay),close:Infinity};h.voices.push(v);
    source.onended=function(){h.voices=h.voices.filter(x=>x!==v);source.disconnect();mix.disconnect();closure.disconnect();filter.disconnect();};source.start(v.start);
    if(c.state==='suspended')c.resume().catch(()=>{});
  },
  CRHatClose:function(delay){
    const h=window.CRHats;if(!h)return;const c=h.ctx,t=c.currentTime+Math.max(0,delay);
    for(const v of h.voices){if(v.start>t+1e-6||v.close<=t+1e-6)continue;v.close=t;const g=v.gain.gain,f=v.filter.frequency;
      g.cancelScheduledValues(t);g.setValueAtTime(1,t);g.linearRampToValueAtTime(.82,t+.010);g.exponentialRampToValueAtTime(.16,t+.052);g.exponentialRampToValueAtTime(.006,t+.112);g.linearRampToValueAtTime(0,t+.132);
      f.cancelScheduledValues(t);f.setValueAtTime(c.sampleRate*.5,t);f.exponentialRampToValueAtTime(Math.min(5200,c.sampleRate*.45),t+.052);v.source.stop(t+.142);
    }
  },
  CRHatStop:function(){if(window.CRHats)window.CRHats.stop();},
  CRHatCancelFuture:function(){const h=window.CRHats;if(!h)return;for(const v of h.voices)if(v.start>h.ctx.currentTime){try{v.source.stop();}catch(_){}}}
});
