using UnityEngine;

namespace Abb2kTools
{
    public class FollowObject : MonoBehaviour
    {
        public enum MoveModes { Snap, Lerp, SLerp }

        [Header("General")]
        public Transform target;
        public Vector3 offset;
        public MoveModes moveMode;

        [System.Serializable]
        public class Constrains
        {
            public bool x = false;
            public bool y = false;
            public bool z = false;
        }
        public Constrains constrains;

        [Header("Lerp Settings")]
        public float lerpTime = 2f;
        public AnimationCurve lerpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2),
            new Keyframe(1f, 1f, 0, 0f)
        );

        private float lerpProgress;
        private Vector3 startPos;
        private Vector3 lastTargetPos;

        void Update()
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;

            if (targetPos != lastTargetPos)
            {
                startPos = transform.position;
                lerpProgress = 0f;
                lastTargetPos = targetPos;
            }

            lerpProgress += Time.deltaTime * lerpTime;
            lerpProgress = Mathf.Clamp01(lerpProgress);

            float t = lerpCurve.Evaluate(lerpProgress);

            Vector3 newPos = transform.position;

            switch (moveMode)
            {
                case MoveModes.Snap:
                    newPos = targetPos;
                    break;

                case MoveModes.Lerp:
                    newPos = Vector3.Lerp(startPos, targetPos, t);
                    break;

                case MoveModes.SLerp:
                    newPos = Vector3.Slerp(startPos, targetPos, t);
                    break;
            }

            if (constrains.x) newPos.x = transform.position.x;
            if (constrains.y) newPos.y = transform.position.y;
            if (constrains.z) newPos.z = transform.position.z;

            transform.position = newPos;
        }
    }
}
