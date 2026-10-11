(function () {
  'use strict';
  const state = { enabled: false, english: false, target: '', epoch: 0, active: false, depth: 0 };
  let overlay;
  const text = (jp, en) => state.english ? en : jp;
  const error = message => {
    if (window.crossRhythm && state.target) window.crossRhythm.SendMessage(state.target, 'OnMidiDropError', message);
  };
  const filesDrag = event => Array.from(event.dataTransfer?.types || []).includes('Files');
  const hide = () => { state.depth = 0; if (overlay) overlay.hidden = true; };
  const show = () => {
    if (!overlay) {
      overlay = document.createElement('div');
      overlay.id = 'cross-rhythm-midi-drop';
      Object.assign(overlay.style, { position: 'fixed', inset: '12px', zIndex: '12000', pointerEvents: 'none', border: '2px dashed #8fe5c2', borderRadius: '8px', background: 'rgba(7,12,16,.82)', color: '#8fe5c2', padding: '28px', font: '20px system-ui,sans-serif' });
      document.body.appendChild(overlay);
    }
    overlay.hidden = false;
    overlay.textContent = text('MIDI・音声をドロップして読込', 'Drop MIDI or audio to import');
  };
  window.CrossRhythmMidiDrop = {
    set(target, enabled, english) {
      if (state.target !== target || state.enabled !== enabled) { state.epoch++; hide(); }
      state.target = target; state.enabled = enabled; state.english = english;
    }
  };
  window.addEventListener('dragenter', event => {
    if (!filesDrag(event)) return;
    event.preventDefault(); state.depth++;
    if (state.enabled && !state.active) show();
  });
  window.addEventListener('dragover', event => {
    if (!filesDrag(event)) return;
    event.preventDefault();
    event.dataTransfer.dropEffect = state.enabled && !state.active ? 'copy' : 'none';
  });
  window.addEventListener('dragleave', event => {
    if (!filesDrag(event)) return;
    if (--state.depth <= 0 || !event.relatedTarget) hide();
  });
  window.addEventListener('drop', async event => {
    if (!filesDrag(event)) return;
    event.preventDefault(); hide();
    if (!state.enabled || state.active) { error(text('Editの読込完了後にMIDI・音声をドロップしてください', 'Drop MIDI or audio after Edit is ready')); return; }
    const files = Array.from(event.dataTransfer.files || []);
    if (files.length !== 1 || !/\.(mid|midi|wav|mp3|m4a|ogg|flac|aac)$/i.test(files[0].name)) { error(text('MIDIまたは音声ファイルを1つドロップしてください', 'Drop one MIDI or audio file')); return; }
    const file = files[0], epoch = state.epoch, target = state.target, midi = /\.midi?$/i.test(file.name), limit = (midi ? 32 : 128) * 1024 * 1024;
    if (file.size <= 0 || file.size > limit) { error(text('空でないファイルを選択: MIDI 32 MB / 音声128 MB以下', 'Choose a nonempty file: MIDI up to 32 MB / audio up to 128 MB')); return; }
    state.active = true;
    try {
      const bytes = new Uint8Array(await file.arrayBuffer());
      await window.CRFiles.serialized(async () => {
        if (!state.enabled || state.epoch !== epoch) throw Error(text('Editで読込をやり直してください', 'Import again in Edit'));
        await window.CRFiles.send(target, bytes, file.name, '', midi ? 'midi-drop' : 'audio-drop');
      });
    } catch (e) { error(e.message || String(e)); }
    finally { state.active = false; }
  });
  window.addEventListener('blur', hide);
})();
