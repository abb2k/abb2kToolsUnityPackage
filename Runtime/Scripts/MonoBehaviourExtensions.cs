

using UnityEngine;

namespace Abb2kTools
{
    public static class MonoBehaviourExtensions
    {
        public static ListenerHandle BindListener(this MonoBehaviour owner, ListenerHandle e) => e.BindTo(owner);
    }
}