using UnityEngine;

namespace Abb2kTools
{
    public interface IHitReciever
    {
        public enum HitType{ Enter, Stay, Exit };

        public struct HitData
        {
            public int id;
            public HitType type;
            public GameObject hitSender;
        }

        virtual void OnTrigger2D(Collider2D hit, HitData data) {}
        virtual void OnCollision2D(Collision2D hit, HitData data) {}
        virtual void OnTrigger(Collider hit, HitData data) {}
        virtual void OnCollision(Collision hit, HitData data) {}
    }
}