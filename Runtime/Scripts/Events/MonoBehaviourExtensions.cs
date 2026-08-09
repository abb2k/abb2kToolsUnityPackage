

using UnityEngine;

namespace Abb2kTools.Events
{
    public static class MonoBehaviourExtensions
    {
        public static ListenerHandle BindListener(this MonoBehaviour owner, ListenerHandle e) => e.BindTo(owner);
    }
}