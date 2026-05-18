

using UnityEngine;

public static class MonoBehaviourExtensions
{
    public static ListenerHandle BindEvent(this MonoBehaviour owner, ListenerHandle e)
    {
        return e.BindTo(owner);
    }
}