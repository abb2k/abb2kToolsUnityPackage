// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using DG.Tweening;
// using System.Linq;

// namespace Abb2kTools.AudioSystem
// {
//     public class SoundCodingPlayer : MonoBehaviour
//     {
//         public SoundCoding codingAsset;
        
//         private string _currentPartName;
//         private List<AudioSource> _activeSources = new List<AudioSource>();
//         private Coroutine _playbackRoutine;
        
//         public void Play(string partName = null)
//         {
//             if (codingAsset == null || codingAsset.parts.Count == 0) return;
            
//             if (string.IsNullOrEmpty(partName)) 
//                 partName = string.IsNullOrEmpty(codingAsset.startingPart) ? codingAsset.parts[0].name : codingAsset.startingPart;
                
//             TransitionTo(partName, true);
//         }

//         public void TransitionTo(string nextPartName, bool immediate = false)
//         {
//             var nextPart = codingAsset.parts.FirstOrDefault(p => p.name == nextPartName);
//             if (nextPart == null) return;

//             float fadeOutTime = 0f;
//             float fadeInTime = 0f;

//             if (!immediate && !string.IsNullOrEmpty(_currentPartName))
//             {
//                 var transition = codingAsset.transitions.FirstOrDefault(t => t.fromPart == _currentPartName && t.toPart == nextPartName);
//                 if (transition != null)
//                 {
//                     fadeOutTime = transition.fadeOutDuration;
//                     fadeInTime = transition.fadeInDuration;
//                 }
//                 else
//                 {
//                     // Fallback crossfade if no specific transition rule exists
//                     fadeOutTime = 0.5f;
//                     fadeInTime = 0.5f;
//                 }
//             }

//             // 1. Fade out current sources safely
//             foreach (var source in _activeSources)
//             {
//                 if (source == null) continue;
//                 var s = source; 
                
//                 if (fadeOutTime > 0f)
//                 {
//                     s.DOFade(0f, fadeOutTime).OnComplete(() => {
//                         if (s != null) Destroy(s.gameObject);
//                     });
//                 }
//                 else
//                 {
//                     Destroy(s.gameObject);
//                 }
//             }
            
//             _activeSources.Clear();
//             _currentPartName = nextPartName;

//             if (_playbackRoutine != null) StopCoroutine(_playbackRoutine);
//             _playbackRoutine = StartCoroutine(HandlePartPlayback(nextPart, fadeInTime));
//         }

//         private IEnumerator HandlePartPlayback(SoundCoding.SoundPart part, float fadeInTime)
//         {
//             // Give the engine a tiny buffer to prepare the DSP schedule
//             double dspTime = AudioSettings.dspTime + 0.1f; 
            
//             while (true)
//             {
//                 var clips = codingAsset.GetPartClips(part.name);

//                 foreach (var clipData in clips)
//                 {
//                     GameObject go = new GameObject($"CodingClip_{clipData.Clip.name}");
//                     go.transform.parent = transform;
//                     var source = go.AddComponent<AudioSource>();
                    
//                     source.clip = clipData.Clip;
//                     source.pitch = clipData.Pitch;
//                     if (clipData.PreferredMixerGroup != null) source.outputAudioMixerGroup = clipData.PreferredMixerGroup;
                    
//                     clipData.Filters?.ApplyTo(source);
                    
//                     source.time = clipData.StartOffset;
//                     double duration = (clipData.Clip.length - clipData.StartOffset - clipData.EndOffset) / Mathf.Max(0.001f, source.pitch);
                    
//                     // Sample-accurate scheduling
//                     source.PlayScheduled(dspTime + clipData.Delay);
//                     source.SetScheduledEndTime(dspTime + clipData.Delay + duration);
                    
//                     // Handle dynamic fade in
//                     float targetVol = clipData.Volume;
//                     if (fadeInTime > 0f)
//                     {
//                         source.volume = 0f;
//                         // Safe DOFade scheduled to begin exactly when the audio starts
//                         source.DOFade(targetVol, fadeInTime).SetDelay((float)(dspTime + clipData.Delay - AudioSettings.dspTime));
//                     }
//                     else
//                     {
//                         source.volume = targetVol;
//                     }

//                     _activeSources.Add(source);
                    
//                     // Destroy GameObject ~4 seconds after playback finishes to preserve echo/reverb tails
//                     Destroy(go, (float)((dspTime + clipData.Delay + duration) - AudioSettings.dspTime) + 4f); 
//                 }

//                 _activeSources.RemoveAll(s => s == null);

//                 float partDuration = part.endTime - part.startTime;
                
//                 if (part.loop)
//                 {
//                     // Yield until just before the clip ends to queue up the next loop iteration seamlessly
//                     float waitTime = partDuration - 0.2f;
//                     if (waitTime > 0) yield return new WaitForSeconds(waitTime);
//                     else yield return null;
                    
//                     dspTime += partDuration;
//                 }
//                 else
//                 {
//                     yield return new WaitForSeconds(partDuration);
                    
//                     if (!string.IsNullOrEmpty(part.defaultNextPart))
//                     {
//                         TransitionTo(part.defaultNextPart, false);
//                     }
//                     break;
//                 }
//             }
//         }
//     }
// }