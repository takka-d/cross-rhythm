mergeInto(LibraryManager.library, {
  $CRStretchAudio: {base:0,source:0,rate:1,nodes:[],gain:null,lastEnd:0},
  CRStretchAudioClock__deps: ['$WEBAudio'],
  CRStretchAudioClock: function(){return WEBAudio.audioContext?WEBAudio.audioContext.currentTime:0;},
  CRStretchAudioStop__deps: ['$CRStretchAudio'],
  CRStretchAudioStop: function(){
    for(const item of CRStretchAudio.nodes){try{item.node.stop();}catch(_){}item.node.disconnect();}
    CRStretchAudio.nodes=[];CRStretchAudio.lastEnd=0;
    if(CRStretchAudio.gain){CRStretchAudio.gain.disconnect();CRStretchAudio.gain=null;}
  },
  CRStretchAudioSchedule__deps: ['$CRStretchAudio','$WEBAudio'],
  CRStretchAudioSchedule: function(ptr,frames,channels,sampleRate,outputOffset,startTime,sourceSecond,rate,gain){
    const ctx=WEBAudio.audioContext;if(!ctx)return 0;
    if(outputOffset===0){CRStretchAudio.base=startTime;CRStretchAudio.source=sourceSecond;CRStretchAudio.rate=rate;
      CRStretchAudio.gain=ctx.createGain();CRStretchAudio.gain.gain.value=gain;CRStretchAudio.gain.connect(ctx.destination);}
    const when=CRStretchAudio.base+outputOffset;if(when<ctx.currentTime+.005)return 0;
    const buffer=ctx.createBuffer(channels,frames,sampleRate),data=HEAPF32;
    for(let c=0;c<channels;c++){const out=buffer.getChannelData(c);for(let i=0;i<frames;i++)out[i]=data[(ptr>>2)+i*channels+c];}
    const node=ctx.createBufferSource();node.buffer=buffer;node.connect(CRStretchAudio.gain);
    const item={node,start:when,end:when+frames/sampleRate};CRStretchAudio.nodes.push(item);CRStretchAudio.lastEnd=item.end;
    node.onended=()=>{node.disconnect();CRStretchAudio.nodes=CRStretchAudio.nodes.filter(x=>x!==item);};node.start(when);return 1;
  },
  CRStretchAudioPosition__deps: ['$CRStretchAudio','$WEBAudio'],
  CRStretchAudioPosition: function(){return CRStretchAudio.source+Math.max(0,WEBAudio.audioContext.currentTime-CRStretchAudio.base)*CRStretchAudio.rate;},
  CRStretchAudioPlaying__deps: ['$CRStretchAudio','$WEBAudio'],
  CRStretchAudioPlaying: function(){const t=WEBAudio.audioContext.currentTime;return CRStretchAudio.nodes.some(x=>t>=x.start&&t<x.end)?1:0;},
  CRStretchAudioGain__deps: ['$CRStretchAudio'],
  CRStretchAudioGain: function(gain){if(CRStretchAudio.gain)CRStretchAudio.gain.gain.value=gain;}
});
