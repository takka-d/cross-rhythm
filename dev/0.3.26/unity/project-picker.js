/* These controls invoke the file chooser inside the real browser click event. */
(function(){
  const controls=[];let used=0,english=true,overlay;
  function progressBox(){if(overlay)return overlay;overlay=document.createElement('div');overlay.style.cssText='position:fixed;z-index:90;bottom:38px;left:25%;width:50%;padding:18px;box-sizing:border-box;background:#10232c;color:white;border:1px solid #8fe5c2;font:16px system-ui';const text=document.createElement('div'),cancel=document.createElement('button');cancel.textContent='Cancel';cancel.style.cssText='margin-top:12px;padding:8px 25px';cancel.onclick=()=>window.CRFiles.cancel();overlay.append(text,cancel);overlay.message=text;document.body.appendChild(overlay);return overlay;}

  const style=document.createElement('style');style.textContent='.cr-project-button{position:fixed;z-index:19;user-select:none;box-sizing:border-box;margin:0;padding:0 10px;border:1px solid #263640;color:#fff;background:#0e161e;font-family:system-ui,sans-serif;cursor:pointer;transition:background .10s,box-shadow .10s,transform .08s}.cr-project-button.primary{color:#071e16;background:#8fe5c2;border-color:#8fe5c2}.cr-project-button:hover:enabled{background:#20493f;box-shadow:0 0 0 1px #8fe5c2,0 0 12px #8fe5c233;color:white}.cr-project-button:active:enabled{transform:translateY(1px);background:#396b5c}.cr-project-button:focus-visible{outline:2px solid #e7fff5;outline-offset:3px}.cr-project-button:disabled{color:#829498;background:#121c25;cursor:default}';document.head.appendChild(style);
  function feedback(kind){window.crossRhythm?.SendMessage('CrossRhythm','OnUiFeedback',kind);}
  window.CrossRhythmPicker={
    keyboardMode:-1,
    waiting(mode){const box=progressBox();box.style.display='block';box.message.textContent=english?'Choose a folder or project in the file window…':'ファイル選択画面でフォルダー / プロジェクトを選択…';},
    progress(path,count){const box=progressBox();box.style.display='block';box.message.textContent=(english?'Loading: ':'読込中: ')+count+' · '+path;},
    finish(){if(overlay)overlay.style.display='none';},
    begin(){used=0;},
    layout(id,x,y,w,h,mode,isEnglish,enabled,primary,fontSize){
      english=isEnglish;
      let b=controls[id];if(!b){b=document.createElement('button');b.type='button';b.className='cr-project-button';
        b.addEventListener('pointerenter',()=>{if(!b.disabled)feedback('hover');});
        b.addEventListener('click',()=>{if(b.disabled)return;feedback('confirm');if(b.mode===6)window.crossRhythm?.SendMessage('CrossRhythm','OnMidiExportButton','');else if(b.mode>=3)window.crossRhythm?.SendMessage('CrossRhythm','OnSaveButton',b.mode===5?'Continue':b.mode===4?'SaveAs':'Save');else window.CRFiles.pick('CrossRhythm',b.mode).finally(()=>{if(!window.CRFiles.picking)document.getElementById('unity-canvas')?.focus();});});document.body.appendChild(b);controls[id]=b;}
      used=Math.max(used,id+1);b.mode=mode;b.textContent=mode===6?'Export MIDI':mode===5?'Save & Continue':mode===4?'Save As':mode===3?'Save':mode===1?'Open Folder':'Open Project';b.classList.toggle('primary',primary);b.disabled=!enabled;
      Object.assign(b.style,{display:'block',left:(x*100)+'%',top:(y*100)+'%',width:(w*100)+'%',height:(h*100)+'%',fontSize:fontSize+'px'});
    },
    end(){for(let i=used;i<controls.length;i++)controls[i].style.display='none';}
  };
  document.addEventListener('keydown',e=>{
    if(e.key!=='Enter'||e.target!==document.getElementById('unity-canvas'))return;
    const mode=window.CrossRhythmPicker.keyboardMode;
    const b=controls.find(b=>b.mode===mode&&!b.disabled&&b.style.display!=='none');
    if(b){e.preventDefault();e.stopImmediatePropagation();b.click();}
  },true);
}());
