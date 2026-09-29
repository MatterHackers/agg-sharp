// Regenerates audio-outlasts-video.mp4 (MacVideoFrameReaderTests):
//   python3 -c "import wave;w=wave.open(\"audio.wav\",\"wb\");w.setnchannels(1);w.setsampwidth(2);w.setframerate(8000);w.writeframes(bytes(56000))"
//   afconvert -f m4af -d aac -b 16000 audio.wav audio.m4a
//   swift make-audio-outlasts-video.swift solid-colors.mp4 audio.m4a audio-outlasts-video.mp4
// Muxes solid-colors.mp4's video (3 s) with a 3.5 s audio track, passthrough, so the asset's duration
// is the audio's and outlasts the video - the shape of a phone clip whose audio runs past the last frame.
import AVFoundation
let args = CommandLine.arguments
let video = AVURLAsset(url: URL(fileURLWithPath: args[1]))
let audio = AVURLAsset(url: URL(fileURLWithPath: args[2]))
let comp = AVMutableComposition()
let vt = video.tracks(withMediaType: .video)[0]
let at = audio.tracks(withMediaType: .audio)[0]
let cv = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
try! cv.insertTimeRange(vt.timeRange, of: vt, at: .zero)
cv.preferredTransform = vt.preferredTransform
let ca = comp.addMutableTrack(withMediaType: .audio, preferredTrackID: kCMPersistentTrackID_Invalid)!
try! ca.insertTimeRange(at.timeRange, of: at, at: .zero)
let out = URL(fileURLWithPath: args[3])
try? FileManager.default.removeItem(at: out)
let ex = AVAssetExportSession(asset: comp, presetName: AVAssetExportPresetPassthrough)!
ex.outputURL = out
ex.outputFileType = .mp4
let sem = DispatchSemaphore(value: 0)
ex.exportAsynchronously { sem.signal() }
sem.wait()
print(ex.status.rawValue, ex.error as Any)
let r = AVURLAsset(url: out)
print("asset", r.duration.seconds, "video", r.tracks(withMediaType: .video)[0].timeRange.end.seconds, "audio", r.tracks(withMediaType: .audio)[0].timeRange.end.seconds)
