(function () {
  'use strict';
  let active = false, textFocus = false;
  const commands = {a:'SelectAll',c:'Copy',v:'Paste',z:'Undo',y:'Redo',s:'Save',o:'Open'};
  function accepts(event) {
    const target = event.target;
    return active && !textFocus && !target?.isContentEditable && !/^(INPUT|TEXTAREA|SELECT)$/.test(target?.tagName || '');
  }
  function send(event, command) {
    if (!window.crossRhythm || !accepts(event)) return;
    event.preventDefault();
    event.stopImmediatePropagation();
    window.crossRhythm.SendMessage('CrossRhythm', 'OnEditorShortcut', command);
  }
  window.addEventListener('keydown', event => {
    if (!(event.ctrlKey || event.metaKey)) return;
    let command = commands[event.key.toLowerCase()];
    if (!command) return;
    if (event.shiftKey && command === 'Undo') command = 'Redo';
    if (event.shiftKey && command === 'Save') command = 'SaveAs';
    if (event.repeat && accepts(event)) {event.preventDefault();event.stopImmediatePropagation();return;}
    send(event, command);
  }, true);
  // Browsers can deliver these as editing commands rather than key events.
  // The chart has its own clipboard; no system clipboard contents are read.
  window.addEventListener('copy', event => send(event, 'Copy'), true);
  window.addEventListener('paste', event => send(event, 'Paste'), true);
  window.CrossRhythmEditorKeys = {set(enabled, editingText) {active = enabled; textFocus = editingText;}};
})();
