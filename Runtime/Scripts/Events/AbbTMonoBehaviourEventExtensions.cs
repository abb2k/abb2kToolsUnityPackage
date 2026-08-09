

using UnityEngine;

namespace Abb2kTools.Events
{
    public static class AbbTMonoBehaviourEventExtensions
    {
        public static ListenerHandle BindListener(this MonoBehaviour owner, ListenerHandle e) => e.BindTo(owner);
    }
}