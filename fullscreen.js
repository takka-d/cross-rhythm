(function () {
  'use strict';
  let button, frame, active = false;
  const canvas = () => document.getElementById('unity-canvas');
  const embedded = window.parent !== window;
  let parentOrigin = '*';
  try { if (document.referrer) parentOrigin = new URL(document.referrer).origin; } catch (_) {}
  function request(on) {
    if (embedded) window.parent.postMessage({type:'cross-rhythm-maximize',on:!!on},parentOrigin);
    else { active=!!on;document.body.classList.toggle('cross-rhythm-maximized',active);refresh();canvas()?.focus(); }
  }
  function refresh() {
    if (!button || !frame || !canvas()) return;
    const label = active ? 'Restore' : 'Maximize';
    button.setAttribute('aria-label',label);button.title=label;button.setAttribute('aria-pressed',String(active));
    button.innerHTML='<svg aria-hidden="true" viewBox="0 0 24 24" width="58%" height="58%" fill="none" stroke="currentColor" stroke-width="1.5"><path d="'+(active?'M3 9H9V3 M15 3V9H21 M3 15H9V21 M15 21V15H21':'M9 3H3V9 M15 3H21V9 M3 15V21H9 M21 15V21H15')+'"/></svg>';
    const rect=canvas().getBoundingClientRect();
    Object.assign(button.style,{left:(rect.left+frame[0]*rect.width)+'px',top:(rect.top+frame[1]*rect.height)+'px',width:(frame[2]*rect.width)+'px',height:(frame[3]*rect.height)+'px'});
  }
  window.CrossRhythmDisplay={
    layout(x,y,w,h,en,enabled) {
      if(button&&frame&&frame[0]===x&&frame[1]===y&&frame[2]===w&&frame[3]===h&&button.disabled===!enabled)return;
      frame=[x,y,w,h];
      if (!button) {
        const style=document.createElement('style');style.textContent='body.cross-rhythm-maximized{position:fixed!important;inset:0!important;width:100vw!important;height:100vh!important;height:100dvh!important;margin:0!important;overflow:hidden!important}';document.head.appendChild(style);
        button=document.createElement('button');button.id='cross-rhythm-fullscreen';button.type='button';
        button.style.cssText='position:fixed;z-index:20;box-sizing:border-box;margin:0;padding:0;display:grid;place-items:center;background:#0e161e;color:white;border:1px solid #263640;cursor:pointer;line-height:1;user-select:none';
        button.addEventListener('pointerenter',()=>{if(!button.disabled){button.style.boxShadow='0 0 0 1px #8fe5c2,0 0 12px #8fe5c233';window.crossRhythm?.SendMessage('CrossRhythm','OnUiFeedback','hover');}});
        button.addEventListener('pointerleave',()=>{button.style.boxShadow='none';});
        button.addEventListener('click',()=>{window.crossRhythm?.SendMessage('CrossRhythm','OnUiFeedback','confirm');request(!active);});document.body.appendChild(button);
        if(embedded)window.parent.postMessage({type:'cross-rhythm-maximize-ready'},parentOrigin);
      }
      button.disabled=!enabled;button.style.visibility=enabled?'visible':'hidden';refresh();
    }
  };
  window.addEventListener('message',e=>{if(e.source!==window.parent||(parentOrigin!=='*'&&e.origin!==parentOrigin)||e.data?.type!=='cross-rhythm-maximize-state')return;active=!!e.data.on;refresh();canvas()?.focus();});
  document.addEventListener('keydown',e=>{if(e.key==='Escape'&&active){request(false);e.preventDefault();e.stopPropagation();}},true);
  window.addEventListener('resize',refresh);
}());
