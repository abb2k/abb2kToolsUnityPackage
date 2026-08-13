using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System.Linq;

namespace Abb2kTools.AudioSystem
{
    // Custom attribute to turn string fields into Dropdowns in the Inspector
    public class SoundPartAttribute : PropertyAttribute { }

    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/SoundCoding.png")]
    [CreateAssetMenu(fileName = "SoundCoding", menuName = "Audio/Sound Coding")]
    public class SoundCoding : SoundModificationBase
    {
        [Header("Source")]
        [Tooltip("The base audio or composition to slice up.")]
        public SoundModificationBase sourceAudio;

        [System.Serializable]
        public class SoundPart
        {
            public string name = "New Part";
            [Min(0)] public float startTime;
            [Min(0)] public float endTime = 10f;
            [Tooltip("If true, this part will loop indefinitely until a transition is called.")]
            public bool loop;
            [Tooltip("If not looping, automatically transition to this part when finished.")]
            [SoundPart] public string defaultNextPart;
        }

        [System.Serializable]
        public class SoundTransition
        {
            [SoundPart] public string fromPart;
            [SoundPart] public string toPart;
            [Min(0)] public float fadeOutDuration = 0.5f;
            [Min(0)] public float fadeInDuration = 0.5f;
        }

        [Header("Routing")]
        [SoundPart] public string startingPart;
        public List<SoundPart> parts = new List<SoundPart>();
        public List<SoundTransition> transitions = new List<SoundTransition>();

        [SerializeField] public AudioFilterSettings filters;

        public SoundCodingInstance CreateInstance()
        {
            GameObject go = new GameObject($"[SoundCoding Runner] {this.name}");
            Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<SoundCodingRunner>();
            runner.codingAsset = this;
            return new SoundCodingInstance(runner);
        }

        public List<PlayableClipData> GetPartClips(string partName, float volMult = 1f, float pitchMult = 1f)
        {
            List<PlayableClipData> partClips = new List<PlayableClipData>();
            if (sourceAudio == null) return partClips;

            var part = parts.FirstOrDefault(p => p.name == partName);
            if (part == null) return partClips;

            List<PlayableClipData> sourceClips = new List<PlayableClipData>();
            sourceAudio.CollectPlayableClips(sourceClips, volMult, pitchMult, 0f);

            foreach (var clipData in sourceClips)
            {
                var clip = clipData; 
                float clipStart = clip.Delay;
                float clipDur = (clip.Clip.length - clip.StartOffset - clip.EndOffset) / Mathf.Max(0.001f, clip.Pitch);
                float clipEnd = clipStart + clipDur;

                if (clipEnd <= part.startTime || clipStart >= part.endTime) continue;

                if (clipStart < part.startTime)
                {
                    float cutAmount = part.startTime - clipStart;
                    clip.StartOffset += cutAmount * clip.Pitch;
                    clip.Delay = 0f; 
                }
                else clip.Delay -= part.startTime;

                float newClipDur = (clip.Clip.length - clip.StartOffset - clip.EndOffset) / Mathf.Max(0.001f, clip.Pitch);
                float newClipEnd = clip.Delay + newClipDur;
                float partDur = part.endTime - part.startTime;

                if (newClipEnd > partDur)
                {
                    float excess = newClipEnd - partDur;
                    clip.EndOffset += excess * clip.Pitch;
                }

                clip.StartOffset = Mathf.Clamp(clip.StartOffset, 0f, clip.Clip.length);
                clip.EndOffset = Mathf.Clamp(clip.EndOffset, 0f, clip.Clip.length - clip.StartOffset);

                if (clip.Filters == null) clip.Filters = new AudioFilterSettings();
                else clip.Filters = clip.Filters.Clone();
                clip.Filters.MergeWithParent(this.filters);

                partClips.Add(clip);
            }
            return partClips;
        }

        public override void CollectPlayableClips(List<PlayableClipData> result, float currentVolume = 1f, float currentPitch = 1f, float currentDelay = 0f)
        {
            if (sourceAudio == null || parts.Count == 0) return;

            string currentPartName = string.IsNullOrEmpty(startingPart) ? parts[0].name : startingPart;
            float currentSequenceDelay = currentDelay;
            int maxIterations = 20; 
            int iterations = 0;

            while (!string.IsNullOrEmpty(currentPartName) && iterations < maxIterations)
            {
                var part = parts.FirstOrDefault(p => p.name == currentPartName);
                if (part == null) break;

                var clips = GetPartClips(currentPartName, currentVolume, currentPitch);
                foreach (var c in clips)
                {
                    var shiftedClip = c;
                    shiftedClip.Delay += currentSequenceDelay;
                    result.Add(shiftedClip);
                }

                currentSequenceDelay += (part.endTime - part.startTime);
                if (part.loop) break; 

                currentPartName = part.defaultNextPart;
                iterations++;
            }
        }
    }

    public static class AudioTweenSafeExtensions
    {
        // Universally safe volume tween that bypasses DOTween Module requirements
        public static Tweener DOFadeSafe(this AudioSource source, float endValue, float duration)
        {
            return DOTween.To(() => source.volume, x => source.volume = x, endValue, duration).SetTarget(source);
        }
    }

    public class SoundCodingInstance
    {
        private SoundCodingRunner _runner;
        public SoundCodingInstance(SoundCodingRunner runner) { _runner = runner; }
        public void Play(string partName = null) { if (_runner != null) _runner.Play(partName); }
        public void TransitionTo(string partName, bool immediate = false) { if (_runner != null) _runner.TransitionTo(partName, immediate); }
        public void Stop(float fadeOut = 0.5f) { if (_runner != null) _runner.StopPlayback(fadeOut); }
        public bool IsPlaying => _runner != null && _runner.IsPlaying;
        public string CurrentPart => _runner != null ? _runner.CurrentPart : "";
    }

    [AddComponentMenu("")]
    public class SoundCodingRunner : MonoBehaviour
    {
        public SoundCoding codingAsset;
        public bool IsPlaying { get; private set; }
        public string CurrentPart { get; private set; }
        
        private List<AudioSource> _activeSources = new List<AudioSource>();
        private Coroutine _playbackRoutine;
        
        public void Play(string partName = null)
        {
            if (codingAsset == null || codingAsset.parts.Count == 0) return;
            if (string.IsNullOrEmpty(partName)) partName = string.IsNullOrEmpty(codingAsset.startingPart) ? codingAsset.parts[0].name : codingAsset.startingPart;
            IsPlaying = true;
            TransitionTo(partName, true);
        }

        public void TransitionTo(string nextPartName, bool immediate = false)
        {
            if (!IsPlaying) return;

            var nextPart = codingAsset.parts.FirstOrDefault(p => p.name == nextPartName);
            if (nextPart == null) return;

            float fadeOutTime = 0f, fadeInTime = 0f;

            if (!immediate && !string.IsNullOrEmpty(CurrentPart))
            {
                var transition = codingAsset.transitions.FirstOrDefault(t => t.fromPart == CurrentPart && t.toPart == nextPartName);
                if (transition != null) { fadeOutTime = transition.fadeOutDuration; fadeInTime = transition.fadeInDuration; }
                else { fadeOutTime = 0.5f; fadeInTime = 0.5f; } 
            }

            foreach (var source in _activeSources)
            {
                if (source == null) continue;
                var s = source; 
                if (fadeOutTime > 0f) s.DOFadeSafe(0f, fadeOutTime).OnComplete(() => { if (s != null) Destroy(s.gameObject); });
                else Destroy(s.gameObject);
            }
            
            _activeSources.Clear();
            CurrentPart = nextPartName;

            if (_playbackRoutine != null) StopCoroutine(_playbackRoutine);
            _playbackRoutine = StartCoroutine(HandlePartPlayback(nextPart, fadeInTime));
        }

        public void StopPlayback(float fadeOutTime)
        {
            IsPlaying = false;
            if (_playbackRoutine != null) StopCoroutine(_playbackRoutine);

            foreach (var source in _activeSources)
            {
                if (source == null) continue;
                var s = source; 
                if (fadeOutTime > 0f) s.DOFadeSafe(0f, fadeOutTime).OnComplete(() => { if (s != null) Destroy(s.gameObject); });
                else Destroy(s.gameObject);
            }
            _activeSources.Clear();
            Destroy(gameObject, fadeOutTime + 0.1f);
        }

        private IEnumerator HandlePartPlayback(SoundCoding.SoundPart part, float fadeInTime)
        {
            double dspTime = AudioSettings.dspTime + 0.1f; 
            
            while (true)
            {
                var clips = codingAsset.GetPartClips(part.name);

                foreach (var clipData in clips)
                {
                    GameObject go = new GameObject($"[Clip] {clipData.Clip.name}");
                    go.transform.parent = transform;
                    var source = go.AddComponent<AudioSource>();
                    
                    source.clip = clipData.Clip;
                    source.pitch = clipData.Pitch;
                    if (clipData.PreferredMixerGroup != null) source.outputAudioMixerGroup = clipData.PreferredMixerGroup;
                    clipData.Filters?.ApplyTo(source);
                    
                    source.time = clipData.StartOffset;
                    double duration = (clipData.Clip.length - clipData.StartOffset - clipData.EndOffset) / Mathf.Max(0.001f, source.pitch);
                    
                    source.PlayScheduled(dspTime + clipData.Delay);
                    source.SetScheduledEndTime(dspTime + clipData.Delay + duration);
                    
                    if (fadeInTime > 0f)
                    {
                        source.volume = 0f;
                        source.DOFadeSafe(clipData.Volume, fadeInTime).SetDelay((float)(dspTime + clipData.Delay - AudioSettings.dspTime));
                    }
                    else source.volume = clipData.Volume;

                    _activeSources.Add(source);
                    float tail = clipData.Filters != null && (clipData.Filters.enableEcho || clipData.Filters.enableReverb) ? 4f : 0f;
                    Destroy(go, (float)((dspTime + clipData.Delay + duration) - AudioSettings.dspTime) + tail); 
                }

                _activeSources.RemoveAll(s => s == null);
                float partDuration = part.endTime - part.startTime;
                
                if (part.loop)
                {
                    float waitTime = partDuration - 0.2f;
                    if (waitTime > 0) yield return new WaitForSeconds(waitTime);
                    else yield return null;
                    dspTime += partDuration;
                }
                else
                {
                    yield return new WaitForSeconds(partDuration);
                    if (!string.IsNullOrEmpty(part.defaultNextPart)) TransitionTo(part.defaultNextPart, false);
                    break;
                }
            }
        }
    }
}