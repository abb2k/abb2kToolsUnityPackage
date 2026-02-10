using UnityEngine;

namespace Abb2kTools
{    
    public class FollowObject : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset;
        public bool ignoreX = false;
        public bool ignoreY = false;
        public bool ignoreZ = true;

        void Update()
        {
            if (target == null) return;

            var newPos = target.position + offset;
            if (ignoreX) newPos.x = transform.position.x;
            if (ignoreY) newPos.y = transform.position.y;
            if (ignoreZ) newPos.z = transform.position.z;
            transform.position = newPos;
        }
    }
}
