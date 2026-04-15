using UnityEngine;
using UnityEngine.Events;

public class ExternalAudioSource : MonoBehaviour
{
    public event UnityAction<ExternalAudioSource> OnDestroyed;
    public void Setup(Transform followTarget)
    {
        
    }

    void OnDestroy()
    {
        OnDestroyed?.Invoke(this);
    }
}
