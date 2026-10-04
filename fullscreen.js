(function () {
  'use strict';
  let button, toast, frame, english = false, timer;
  const canvas = () => document.getElementById('unity-canvas');
  function refresh() {
    if (!button) return;
    const active = !!document.fullscreenElement;
    button.textContent = active ? (english ? 'Restore' : '元に戻す') : (english ? 'Full Screen' : '最大化');
    button.setAttribute('aria-pressed', String(active));
    const rect = canvas().getBoundingClientRect();
    Object.assign(button.style, {
      left: (rect.left + frame[0] * rect.width) + 'px',
      top: (rect.top + frame[1] * rect.height) + 'px',
      width: (frame[2] * rect.width) + 'px',
      height: (frame[3] * rect.height) + 'px',
      fontSize: (frame[3] * rect.height * 15 / 38) + 'px'
    });
  }
  function error(reason) {
    console.warn('Cross Rhythm fullscreen:', reason && reason.message ? reason.message : String(reason));
    if (!toast) {
      toast = document.createElement('div'); toast.setAttribute('role', 'status');
      toast.style.cssText = 'position:fixed;top:70px;left:50%;transform:translateX(-50%);z-index:21;padding:12px 18px;background:#0e161e;color:#fff;border:1px solid #8fe5c2;max-width:80%;font:15px system-ui';
      document.body.appendChild(toast);
    }
    toast.textContent = english ? 'Full screen is unavailable in this browser view.' : 'このブラウザー表示では最大化できません。';
    toast.hidden = false; clearTimeout(timer); timer = setTimeout(() => { toast.hidden = true; }, 5000);
    canvas().focus(); refresh();
  }
  window.CrossRhythmDisplay = {
    layout(x, y, w, h, en, enabled) {
      if (button && frame[0] === x && frame[1] === y && frame[2] === w && frame[3] === h && english === en && button.disabled === !enabled) return;
      frame = [x, y, w, h]; english = en;
      if (!button) {
        button = document.createElement('button'); button.id = 'cross-rhythm-fullscreen'; button.type = 'button';
        button.style.cssText = 'position:fixed;z-index:20;box-sizing:border-box;margin:0;padding:0;background:#0e161e;color:white;border:1px solid #263640;font-family:system-ui,sans-serif;cursor:pointer;line-height:1;white-space:nowrap;user-select:none';
        button.addEventListener('click', () => {
          try {
            // Invoke synchronously; awaiting anything first loses browser activation.
            const action = document.fullscreenElement ? document.exitFullscreen() : document.documentElement.requestFullscreen();
            Promise.resolve(action).then(() => { refresh(); canvas().focus(); }, error);
          } catch (reason) { error(reason); }
        });
        document.body.appendChild(button);
      }
      button.disabled = !enabled; button.style.visibility = enabled ? 'visible' : 'hidden'; refresh();
    }
  };
  document.addEventListener('fullscreenchange', () => { refresh(); canvas().focus(); });
  window.addEventListener('resize', refresh);
}());
