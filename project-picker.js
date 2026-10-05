/* These controls invoke the file chooser inside the real browser click event. */
(function(){
  const controls=[];let used=0;
  const style=document.createElement('style');style.textContent='.cr-project-button{position:fixed;z-index:19;box-sizing:border-box;margin:0;padding:0 10px;border:1px solid #263640;color:#fff;background:#0e161e;font-family:system-ui,sans-serif;cursor:pointer;transition:background .10s,box-shadow .10s,transform .08s}.cr-project-button.primary{color:#071e16;background:#8fe5c2;border-color:#8fe5c2}.cr-project-button:hover:enabled{background:#20493f;box-shadow:0 0 0 1px #8fe5c2,0 0 12px #8fe5c233;color:white}.cr-project-button:active:enabled{transform:translateY(1px);background:#396b5c}.cr-project-button:disabled{opacity:.45;cursor:default}';document.head.appendChild(style);
  function feedback(kind){window.crossRhythm?.SendMessage('CrossRhythm','OnUiFeedback',kind);}
  window.CrossRhythmPicker={
    begin(){used=0;},
    layout(id,x,y,w,h,mode,english,enabled,primary,fontSize){
      let b=controls[id];if(!b){b=document.createElement('button');b.type='button';b.className='cr-project-button';
        b.addEventListener('pointerenter',()=>{if(!b.disabled)feedback('hover');});
        b.addEventListener('click',()=>{if(b.disabled)return;feedback('confirm');window.CRFiles.pick('CrossRhythm',b.mode);});document.body.appendChild(b);controls[id]=b;}
      used=Math.max(used,id+1);b.mode=mode;b.textContent=mode===1?'Open Folder':'Open Project';b.classList.toggle('primary',primary);b.disabled=!enabled;
      Object.assign(b.style,{display:'block',left:(x*100)+'%',top:(y*100)+'%',width:(w*100)+'%',height:(h*100)+'%',fontSize:fontSize+'px'});
    },
    end(){for(let i=used;i<controls.length;i++)controls[i].style.display='none';}
  };
}());
