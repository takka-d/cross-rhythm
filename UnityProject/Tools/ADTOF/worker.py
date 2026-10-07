"""Runs one cancellable ADTOF inference process. No network calls."""
from pathlib import Path
import json, os, sys

def run(audio):
    folder = audio.parent
    def write(name, value):
        tmp = folder / (name + ".tmp"); tmp.write_text(json.dumps(value), encoding="utf-8"); os.replace(tmp, folder / name)
    def progress(value, stage): write("progress.json", {"status": "running", "progress": value, "stage": stage})
    try:
        progress(.03, "Loading model")
        import numpy as np
        import torch
        from adtof_pytorch import create_frame_rnn_model, calculate_n_bins, get_default_weights_path, load_audio_for_model, PeakPicker, LABELS_5
        torch.set_num_threads(2)
        weights = Path(get_default_weights_path())
        if not weights.is_file(): raise RuntimeError("ADTOF weights are missing. Run Setup ADTOF again.")
        checkpoint = torch.load(str(weights), map_location="cpu", weights_only=True)
        model = create_frame_rnn_model(calculate_n_bins())
        model.load_state_dict(checkpoint.get("model_weights", checkpoint), strict=True)
        model.eval()
        progress(.1, "Reading audio")
        x = load_audio_for_model(str(audio))
        frames = x.shape[1]
        if frames < 10: raise ValueError("Audio is too short")
        if frames > 180000: raise ValueError("Audio must be shorter than 30 minutes")
        # Bounded memory: 30 second sections with 2 second overlap for context.
        parts = []; block = 3000; context = 200
        with torch.inference_mode():
            for start in range(0, frames, block):
                end = min(start + block, frames); left = max(0, start-context); right = min(frames, end+context)
                prediction = model(x[:, left:right]).cpu().numpy()
                parts.append(prediction[:, start-left:end-left])
                progress(.2 + .7 * end/frames, "Detecting drums")
        pred = np.concatenate(parts, axis=1)
        peaks = PeakPicker().pick(pred, labels=LABELS_5)[0]
        names = {35: "BD", 38: "SN", 47: "MT", 42: "HH", 49: "CR"}
        events = []
        for column, pitch in enumerate(LABELS_5):
            for second in peaks[pitch]:
                frame = min(pred.shape[1]-1, round(second*100))
                events.append({"time": second, "instrument": names[pitch], "confidence": round(float(pred[0, frame, column]), 5)})
        events.sort(key=lambda e: e["time"])
        write("result.json", {"status": "done", "engine": "ADTOF-pytorch", "duration": frames/100, "events": events})
    except Exception as exc:
        write("result.json", {"status": "error", "error": str(exc)})
        raise
    finally:
        audio.unlink(missing_ok=True)
if __name__ == "__main__": run(Path(sys.argv[1]))
