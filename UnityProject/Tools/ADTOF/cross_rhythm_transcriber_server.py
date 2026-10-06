#!/usr/bin/env python3
from __future__ import annotations
import importlib.util, math, tempfile
from pathlib import Path
import numpy as np
import librosa
import pretty_midi
from scipy import signal
from sklearn.mixture import GaussianMixture
from fastapi import FastAPI, UploadFile, File, Form, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
import uvicorn

app = FastAPI(title="Cross Rhythm Transcriber")
app.add_middleware(CORSMiddleware,allow_origins=["*"],allow_credentials=False,allow_methods=["*"],allow_headers=["*"])

@app.middleware("http")
async def private_network_header(request: Request, call_next):
    response = await call_next(request)
    # Chromium Private Network Access preflights can require this for an HTTPS WebGL page -> localhost request.
    response.headers["Access-Control-Allow-Private-Network"] = "true"
    return response

def adtof_available() -> bool:
    return importlib.util.find_spec("adtof_pytorch") is not None

@app.get("/health")
def health():
    return {"ok":True,"adtof_available":adtof_available(),"engine":"ADTOF-pytorch" if adtof_available() else None}

def map_pitch(pitch:int)->str:
    if pitch in (35,36): return "BD"
    if pitch in (37,38,39,40): return "SN"
    if pitch==42: return "HH"
    if pitch==44: return "PEDAL"
    if pitch==46: return "OHH"
    if pitch in (41,43): return "FT"
    if pitch in (45,47): return "MT"
    if pitch in (48,50): return "HT"
    if pitch in (51,53,59): return "RD"
    if pitch in (49,52,55,57): return "CR"
    return "CR" if pitch>=49 else "SN"

def classify_hh_openness(audio_path:str,events:list[dict])->None:
    hh=[e for e in events if e["instrument"] in ("HH","OHH")]
    if len(hh)<4:return
    y,sr=librosa.load(audio_path,sr=22050,mono=True)
    sos=signal.butter(4,4000,btype="highpass",fs=sr,output="sos")
    yh=signal.sosfilt(sos,y)
    def rms(a,b):
        i=max(0,int(a*sr));j=min(len(yh),int(b*sr))
        if j<=i:return 0.0
        return float(np.sqrt(np.mean(yh[i:j]**2)+1e-12))
    feats=[];valid=[]
    for e in hh:
        t=float(e["time"])
        if t<0 or t+.24>=len(yh)/sr:continue
        early=rms(t,t+.045);late=rms(t+.12,t+.22)
        feats.append([math.log((late+1e-7)/(early+1e-7))]);valid.append(e)
    if len(feats)<4:return
    try:
        X=np.asarray(feats);gm=GaussianMixture(n_components=2,random_state=0).fit(X);labels=gm.predict(X)
        means=[float(X[labels==c].mean()) for c in range(2)];open_c=int(np.argmax(means))
        for e,l in zip(valid,labels):e["instrument"]="OHH" if int(l)==open_c else "HH"
    except Exception:pass

@app.post("/transcribe")
async def transcribe(file:UploadFile=File(...),bpm:float=Form(130.0),offset_sec:float=Form(0.0),measures:str=Form("[]")):
    if not adtof_available():
        raise HTTPException(503,'ADTOF-pytorch is not installed. Run install_adtof.bat.')
    from adtof_pytorch import transcribe_to_midi
    suffix=Path(file.filename or "input.wav").suffix or ".wav"
    with tempfile.TemporaryDirectory(prefix="cross-rhythm-") as td:
        td=Path(td);inp=td/("input"+suffix);midi=td/"drums.mid";inp.write_bytes(await file.read())
        transcribe_to_midi(str(inp),str(midi));pm=pretty_midi.PrettyMIDI(str(midi));events=[]
        for inst in pm.instruments:
            for n in inst.notes:
                events.append({"time":float(n.start),"instrument":map_pitch(int(n.pitch)),"velocity":float(n.velocity)/127.0,"confidence":None,"midi_pitch":int(n.pitch)})
        events.sort(key=lambda x:x["time"]);classify_hh_openness(str(inp),events)
    return {"engine":"ADTOF-pytorch","events":events,"bpm_hint":bpm,"offset_hint":offset_sec,"warning":"HH open/closed is post-classified from high-frequency decay; review ambiguous passages in the editor."}

if __name__=="__main__":uvicorn.run(app,host="127.0.0.1",port=8765)
