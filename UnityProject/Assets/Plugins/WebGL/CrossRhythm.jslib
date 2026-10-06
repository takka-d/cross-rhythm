mergeInto(LibraryManager.library, {
  CRKeyboardFileMode: function(mode){if(window.CrossRhythmPicker)window.CrossRhythmPicker.keyboardMode=mode;},
  CRProjectButtonsBegin: function(){if(window.CrossRhythmPicker)window.CrossRhythmPicker.begin();},
  CRProjectButtonsEnd: function(){if(window.CrossRhythmPicker)window.CrossRhythmPicker.end();},
  CRProjectButton: function(id,x,y,w,h,mode,english,enabled,primary,fontSize){if(window.CrossRhythmPicker)window.CrossRhythmPicker.layout(id,x,y,w,h,mode,!!english,!!enabled,!!primary,fontSize);},
  CREditorKeys: function(enabled,textFocus) {if(window.CrossRhythmEditorKeys)window.CrossRhythmEditorKeys.set(!!enabled,!!textFocus);},
  CRDisplayLayout: function(x,y,w,h,english,enabled) {
    if(window.CrossRhythmDisplay) window.CrossRhythmDisplay.layout(x,y,w,h,!!english,!!enabled);
  },
  CRInit: function() {
    if(Module.canvas&&!Module.canvas.crossRhythmContextHandled){Module.canvas.addEventListener('contextmenu',function(e){e.preventDefault();});Module.canvas.crossRhythmContextHandled=true;}
    if (window.CRFiles) return;
    window.CRFiles = {
      handles: new Map(), catalog: new Map(), queue: Promise.resolve(), operation:0,
      delay(){return new Promise(resolve=>setTimeout(resolve,0));},
      limited(promise,ms){let timer;return Promise.race([promise,new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error('Storage or folder access timed out. Please try again.')),ms||5000);})]).finally(()=>clearTimeout(timer));},
      progress(target,path,count){SendMessage(target,'OnImportProgress',JSON.stringify({path,count:String(count)}));if(window.CrossRhythmPicker)window.CrossRhythmPicker.progress(path,count);},
      saveSource(source,handle){if(source.kind!=='folder')return;this.source=source;try{localStorage.setItem('CrossRhythmPlaySource',JSON.stringify(source));}catch(_){}if(handle){this.directory=handle;this.cache({token:'@directory',directory:true,handle,source}).catch(e=>console.warn(e.message));}},
      cancel(){this.operation++;this.picking=false;if(window.CrossRhythmPicker)window.CrossRhythmPicker.finish();},
      check(operation){if(operation!=null&&operation!==this.operation){const e=Error('Cancelled');e.name='AbortError';throw e;}},
      serialized(work) {const operation=this.queue.then(work);this.queue=operation.catch(()=>{});return operation;},
      async fingerprint(name,bytes) {return name.toLowerCase()+'\n'+await this.hash(bytes);},
      async remember(item) {item.source=item.source||(this.catalog.get(item.token)||{}).source;this.catalog.set(item.token,{token:item.token,name:item.name,source:item.source,fingerprint:item.fingerprint||await this.fingerprint(item.name,item.bytes)});if(item.handle)this.handles.set(item.token,item.handle);await this.cache(item);},
      async readRows(name){const db=await this.db(name);try{return await this.limited(new Promise((resolve,reject)=>{const r=db.transaction('projects').objectStore('projects').getAll();r.onsuccess=()=>resolve(r.result);r.onerror=()=>reject(r.error);}));}finally{db.close();}},
      async restore(target) {
        const operation=this.operation;
        return this.serialized(async()=>{
          try{const raw=localStorage.getItem('CrossRhythmPlaySource')||localStorage.getItem('CrossRhythmSource');const source=raw&&JSON.parse(raw);if(source&&source.kind==='folder')this.source=source;}catch(_){}
          try{const locations=await this.readRows('CrossRhythmLocations');const directory=locations.find(x=>x.directory);if(directory){this.directory=directory.handle;if(!this.source)this.source=directory.source;}}catch(e){console.warn('Folder restore: '+e.message);}
          const items=await this.readRows(),byToken=new Map();
          for(const item of items){if(item.directory||!item.bytes)continue;byToken.set(item.token,item);this.catalog.set(item.token,{token:item.token,name:item.name,source:item.source,fingerprint:await this.fingerprint(item.name,item.bytes)});if(item.handle)this.handles.set(item.token,item.handle);}
          const workspace=await this.readRows('CrossRhythmWorkspace');this.check(operation);
          const library=workspace.find(x=>x.token==='@library'),editor=workspace.find(x=>x.token==='@editor');
          if(library&&library.source)this.source=library.source;
          if(this.source&&this.source.kind==='folder')SendMessage(target,'OnSourceRestored',JSON.stringify(this.source));
          // Legacy cache entries are retained for recovery, never used as Play membership.
          if(library){
            SendMessage(target,'OnImportBatch','restore');let count=0;
            for(const token of library.tokens||[]){this.check(operation);const item=byToken.get(token);if(!item)continue;await this.send(target,item.bytes,item.name,item.token,null,item.source);count++;}
            SendMessage(target,'OnLibrarySource',JSON.stringify(Object.assign({},library.source,{count})));
          }
          if(editor&&editor.bytes&&editor.metadata){this.check(operation);const m=editor.metadata;if(editor.handle&&m.filePath)this.handles.set(m.filePath,editor.handle);await this.send(target,editor.bytes,m.fileName,m.filePath||'', 'editor-restore',{session:m});}
        });
      },
      canUseFilePicker() {try{return window.top.location.origin===window.location.origin;}catch(_){return false;}},
      async db(name) { return new Promise((resolve,reject)=>{let expired=false;const timer=setTimeout(()=>{expired=true;reject(Error('Browser storage is unavailable.'));},5000);const r=indexedDB.open(name||'CrossRhythmUnity',1);r.onupgradeneeded=()=>r.result.createObjectStore('projects',{keyPath:'token'});r.onsuccess=()=>{clearTimeout(timer);if(expired)r.result.close();else{r.result.onversionchange=()=>r.result.close();resolve(r.result);}};r.onerror=()=>{clearTimeout(timer);reject(r.error);};r.onblocked=()=>{clearTimeout(timer);expired=true;reject(Error('Browser storage is busy in another tab.'));};}); },
      async cache(item) {const db=await this.db(item.workspace?'CrossRhythmWorkspace':item.directory?'CrossRhythmLocations':undefined);try{await this.limited(new Promise((resolve,reject)=>{const tx=db.transaction('projects','readwrite');tx.objectStore('projects').put(item);tx.oncomplete=resolve;tx.onerror=tx.onabort=()=>reject(tx.error||Error('Storage cancelled'));}));}finally{db.close();}},
      async hash(bytes) {return Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256',bytes))).map(b=>b.toString(16).padStart(2,'0').toUpperCase()).join('-');},
      async send(target,bytes,name,token,kind,source) {
        SendMessage(target,'OnImportStart',JSON.stringify({name,token,size:bytes.length,kind:kind||'project',source}));
        for(let i=0;i<bytes.length;i+=49152){let s='';for(const b of bytes.subarray(i,i+49152))s+=String.fromCharCode(b);SendMessage(target,'OnImportChunk',btoa(s));if(i%393216===0)await this.delay();}
        SendMessage(target,'OnImportEnd','');
      },
      async open(file,handle,target,kind,source) {return this.serialized(()=>this.openOne(file,handle,target,kind,source));},
      async openOne(file,handle,target,kind,source) {
          const bytes=new Uint8Array(await file.arrayBuffer());let token=null;
          if(kind==='audio'||kind==='midi'){await this.send(target,bytes,file.name,crypto.randomUUID(),kind);return;}
          if(handle){for(const [key,h] of this.handles){try{if(h.name===handle.name&&await this.limited(h.isSameEntry(handle),1500)){token=key;break;}}catch(_){}}}
          const fingerprint=await this.fingerprint(file.name,bytes);
          if(!token){for(const item of this.catalog.values()){if(item.fingerprint===fingerprint){token=item.token;break;}}}
          token=token||crypto.randomUUID();handle=handle||this.handles.get(token);
          source=source||{kind:'project',name:file.name};
          await this.send(target,bytes,file.name,token,kind,source);
          let cacheError=null;try{await this.remember({token,name:file.name,bytes,handle,source,fingerprint});}catch(error){cacheError=error;}
          if(cacheError)console.warn('Project opened, but browser storage could not be updated: '+cacheError.message);return token;
      },
      async batch(target,entries,source,operation) {
        return this.serialized(async()=>{
          this.check(operation);const folder=source.kind==='folder',tokens=[],errors=(source.errors||[]).slice();let count=0;
          if(folder){this.saveSource(source);SendMessage(target,'OnImportBatch','open');}
          try {for(const entry of entries){this.check(operation);this.progress(target,entry.path||entry.name,count+1);try {
            const file=entry.file||await this.limited(entry.handle.getFile(),15000);
            const token=await this.openOne(file,entry.handle||null,target,folder?null:'editor',Object.assign({},source,{relativePath:entry.path||file.name}));tokens.push(token);count++;
          }catch(e){errors.push((entry.path||entry.name||'Project')+': '+e.message);}await this.delay();}}
          finally{if(folder){SendMessage(target,'OnLibrarySource',JSON.stringify(Object.assign({},source,{count,errors})));await this.cache({token:'@library',workspace:true,source,tokens});}else if(errors.length)SendMessage(target,'OnFileError',errors.join('; '));}
        });
      },
      async directoryEntries(dir,prefix,entries,errors,target,operation){
        const iterator=dir.values()[Symbol.asyncIterator]();let seen=0;
        while(true){this.check(operation);const next=await this.limited(iterator.next(),15000);if(next.done)break;const entry=next.value,path=prefix+'/'+entry.name;
          if(++seen%32===0){if(target)this.progress(target,path,entries.length);await this.delay();}
          if(entry.kind==='directory'){try{await this.directoryEntries(entry,path,entries,errors,target,operation);}catch(e){if(e.name==='AbortError')throw e;errors.push(path+': '+e.message);}}
          else if(/\.crproj$/i.test(entry.name)){entries.push({handle:entry,path});if(target)this.progress(target,path,entries.length);}
        }
      },
      fallback(target,kind,folder,single) {
        const operation=this.operation;const input=document.createElement('input');input.type='file';input.accept=kind==='midi'?'.mid,.midi':kind?'audio/*':'.crproj';input.multiple=!kind&&!single;
        if(folder)input.webkitdirectory=true;
        input.onchange=async()=>{try{
          this.check(operation);const files=Array.from(input.files||[]);
          if(kind){for(const f of files)await this.open(f,null,target,kind);return;}
          const source={kind:folder?'folder':'project',name:folder?((files[0]||{}).webkitRelativePath||'').split('/')[0]:files.map(f=>f.name).join(', ')};
          await this.batch(target,files.filter(f=>/\.crproj$/i.test(f.name)).map(file=>({file,path:file.webkitRelativePath||file.name})),source,operation);
        }catch(e){SendMessage(target,'OnFileError',e.message);}finally{input.remove();this.picking=false;if(window.CrossRhythmPicker)window.CrossRhythmPicker.finish();}};
        input.oncancel=()=>{SendMessage(target,'OnFileError','Cancelled');input.remove();this.picking=false;if(window.CrossRhythmPicker)window.CrossRhythmPicker.finish();};
        input.style.display='none';document.body.appendChild(input);input.click();
      },
      async pick(target,mode){
        if(this.picking)return;this.picking=true;const operation=++this.operation;let fallback=false;if(window.CrossRhythmPicker)window.CrossRhythmPicker.waiting(mode);
        try {
          if(mode===1&&this.canUseFilePicker()&&window.showDirectoryPicker){
            const options={mode:'read',id:'cross-rhythm-projects'};if(this.directory)options.startIn=this.directory;const dir=await window.showDirectoryPicker(options),entries=[],errors=[];this.check(operation);this.saveSource({kind:'folder',name:dir.name},dir);this.progress(target,dir.name,0);
            await this.directoryEntries(dir,dir.name,entries,errors,target,operation);
            entries.sort((a,b)=>a.path.localeCompare(b.path));
            await this.batch(target,entries,{kind:'folder',name:dir.name,errors},operation);
          }else if(mode!==1&&this.canUseFilePicker()&&window.showOpenFilePicker){
            const handles=await window.showOpenFilePicker({multiple:false,types:[{description:'Cross Rhythm',accept:{'application/zip':['.crproj']}}]});
            this.check(operation);if(handles.length)await this.batch(target,handles.map(handle=>({handle,path:handle.name})),{kind:'project',name:handles.map(h=>h.name).join(', ')},operation);
          }else {fallback=true;this.fallback(target,null,mode===1,mode===2);}
        }catch(e){SendMessage(target,'OnFileError',e.name==='AbortError'?'Cancelled':e.message);}finally{if(!fallback&&operation===this.operation){this.picking=false;if(window.CrossRhythmPicker)window.CrossRhythmPicker.finish();}}
      }
    };
  },
  CRPick: function(targetPtr,folder) {
    window.CRFiles.pick(UTF8ToString(targetPtr),folder);
  },
  CRPickMidi: function(targetPtr) {window.CRFiles.fallback(UTF8ToString(targetPtr),'midi',false);},
  CRRestore: function(targetPtr) {
    const target=UTF8ToString(targetPtr);window.CRFiles.restore(target).catch(e=>SendMessage(target,'OnFileError',e.message));
  },
  CRSave: function(data,length,namePtr,tokenPtr,baselinePtr,targetPtr,saveAs) {
    const bytes=HEAPU8.slice(data,data+length),name=UTF8ToString(namePtr),token=UTF8ToString(tokenPtr),baseline=UTF8ToString(baselinePtr),target=UTF8ToString(targetPtr),bridge=window.CRFiles;
    let handle=saveAs||!bridge.canUseFilePicker()?null:bridge.handles.get(token);
    (async()=>{try{
      const picker=!handle&&bridge.canUseFilePicker()&&window.showSaveFilePicker?showSaveFilePicker({suggestedName:name,types:[{description:'Cross Rhythm',accept:{'application/zip':['.crproj']}}]}):null;
      const permission=handle?handle.requestPermission({mode:'readwrite'}):null;
      if(picker)handle=await picker;
      if(!handle){const a=document.createElement('a'),url=URL.createObjectURL(new Blob([bytes],{type:'application/zip'}));a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),30000);SendMessage(target,'OnSaved',JSON.stringify({status:'download'}));return;}
      if(permission&&await permission!=='granted')throw Error('Write permission was not granted. Use Save As.');
      let same=bridge.handles.has(token)&&await handle.isSameEntry(bridge.handles.get(token));
      if(same){const current=await handle.getFile();if(await bridge.hash(await current.arrayBuffer())!==baseline)throw Error('The original file changed. Use Save As with a different file.');}
      const writable=await handle.createWritable();try{await writable.write(bytes);await writable.close();}catch(e){try{await writable.abort();}catch(_){}throw e;}
      const verify=new Uint8Array(await (await handle.getFile()).arrayBuffer());if(await bridge.hash(verify)!==await bridge.hash(bytes))throw Error('Saved file verification failed.');
      const nextToken=same?token:crypto.randomUUID();bridge.handles.set(nextToken,handle);try{await bridge.remember({token:nextToken,name:handle.name,bytes,handle,source:same?(bridge.catalog.get(token)||{}).source:{kind:'project',name:handle.name}});}catch(cacheError){console.warn('File saved and verified; browser cache update failed: '+cacheError.message);}
      SendMessage(target,'OnSaved',JSON.stringify({status:'saved',token:nextToken,name:handle.name}));
    }catch(e){SendMessage(target,'OnFileError',e.name==='AbortError'?'Save cancelled':e.message);}})();
  },
  CRSaveEditorSession: function(data,length,metadataPtr) {
    const bridge=window.CRFiles,bytes=HEAPU8.slice(data,data+length),metadata=JSON.parse(UTF8ToString(metadataPtr));
    bridge.serialized(()=>bridge.cache({token:'@editor',workspace:true,bytes,metadata,handle:bridge.handles.get(metadata.filePath)})).catch(e=>SendMessage('CrossRhythm','OnEditorRecoveryError',e.message));
  },
  CRCache: function(data,length,name,token) {},
  CRPickAudio: function(targetPtr) {window.CRFiles.fallback(UTF8ToString(targetPtr),'audio',false);},
  CRDecodeAudio: function(data,length,targetPtr,generation) {
    const bytes=HEAPU8.slice(data,data+length),target=UTF8ToString(targetPtr);
    (async()=>{let ctx;try{ctx=new (window.AudioContext||window.webkitAudioContext)();const audio=await ctx.decodeAudioData(bytes.buffer);const channels=audio.numberOfChannels,count=audio.length*channels,ptr=_malloc(count*4);if(!ptr)throw Error('Insufficient audio memory');const out=HEAPF32.subarray(ptr/4,ptr/4+count);for(let c=0;c<channels;c++){const source=audio.getChannelData(c);for(let i=0;i<audio.length;i++)out[i*channels+c]=source[i];}SendMessage(target,'OnAudioDecoded',JSON.stringify({pointer:ptr,count,channels,rate:audio.sampleRate,generation}));}catch(e){SendMessage(target,'OnAudioError',JSON.stringify({error:e.message,generation}));}finally{if(ctx)await ctx.close();}})();
  },
  CRFree: function(pointer) {_free(pointer);}
});
