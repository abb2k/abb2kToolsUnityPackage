using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ExternalAudioSource : MonoBehaviour
{
    public event UnityAction<ExternalAudioSource> OnDestroyed;
    public HashSet<AudioSource> AddedSources {get; private set;} = new();
    public void SetFollow(Transform followTarget)
    {
        
    }

    public AudioSource AddAudioSource()
    {
        var source = gameObject.AddComponent<AudioSource>();
        AddedSources.Add(source);
        return source;
    }

    public void DeleteSource(AudioSource source)
    {
        if (!AddedSources.Contains(source)) return;
        AddedSources.Remove(source);

        Destroy(source);
    }

    void OnDestroy()
    {
        OnDestroyed?.Invoke(this);
    }
}
