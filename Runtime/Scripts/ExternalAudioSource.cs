using System.Collections;
using System.Collections.Generic;
using Abb2kTools;
using UnityEngine;
using UnityEngine.Events;

public class ExternalAudioSource : MonoBehaviour
{
    public event UnityAction<ExternalAudioSource> OnDestroyed;
    public HashSet<AudioSource> AddedSources {get; private set;} = new();
    [HideInInspector]
    public bool DestroyEntireObjectOnDeplete;
    public Transform attached;
    private FollowObject follow;
    public void SetAttached(Transform attach)
    {
        this.attached = attach;
        
        if (!gameObject.TryGetComponent(out follow))
            follow = gameObject.AddComponent<FollowObject>();

        follow.target = attach;
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

        if (AddedSources.Count != 0) return;

        if (DestroyEntireObjectOnDeplete)
            Destroy(gameObject);
        else
            Destroy(this);
    }

    public void SetKillTimerForSource(AudioSource source, float time)
    {
        StartCoroutine(Timer(source, time));
        IEnumerator Timer(AudioSource source, float time)
        {
            yield return new WaitForSeconds(time);
            DeleteSource(source);
        }
    }

    void OnDestroy()
    {
        OnDestroyed?.Invoke(this);
    }
}
