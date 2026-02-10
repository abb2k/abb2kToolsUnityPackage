#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace Abb2kTools
{
    public class FollowObject : MonoBehaviour
    {
        public enum MoveModes { Snap, Lerp, SLerp }
        public enum FrameMovement { Update, Fixed }

        #if ODIN_INSPECTOR
        [BoxGroup("General")]
        #else
        [Header("General")]
        #endif
        public Transform target;
        #if ODIN_INSPECTOR
        [BoxGroup("General")]
        #endif
        public Vector3 offset;
        #if ODIN_INSPECTOR
        [BoxGroup("General")]
        #endif
        public MoveModes moveMode;
        #if ODIN_INSPECTOR
        [BoxGroup("General")]
        #endif
        public FrameMovement frameMovement;

        [System.Serializable]
        public class Constrains
        {
            #if ODIN_INSPECTOR
            [HorizontalGroup("Constrains", LabelWidth = 15)]
            #endif
            public bool x = false;
            #if ODIN_INSPECTOR
            [HorizontalGroup("Constrains", LabelWidth = 15)]
            #endif
            public bool y = false;
            #if ODIN_INSPECTOR
            [HorizontalGroup("Constrains", LabelWidth = 15)]
            #endif
            public bool z = false;
        }
        #if ODIN_INSPECTOR
        [BoxGroup("General"), InlineProperty]
        #endif
        public Constrains constrains;

        #if ODIN_INSPECTOR
        [BoxGroup("Lerp Settings"), ShowIf("@moveMode != MoveModes.Snap")]
        #else
        [Header("Lerp Settings")]
        #endif
        public float lerpSpeed = 2f;
        #if ODIN_INSPECTOR
        [BoxGroup("Lerp Settings"), ShowIf("@moveMode != MoveModes.Snap")]
        #endif
        public bool useCurve;
        #if ODIN_INSPECTOR
        [BoxGroup("Lerp Settings"), ShowIf("@moveMode != MoveModes.Snap"), EnableIf("useCurve")]
        #endif
        public AnimationCurve lerpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2),
            new Keyframe(1f, 1f, 0, 0f)
        );

        private float lerpProgress;
        private Vector3 startPos;
        private Vector3 lastTargetPos;

        private void Update()
        {
            if (frameMovement == FrameMovement.Update)
                Move(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (frameMovement == FrameMovement.Fixed)
                Move(Time.fixedDeltaTime);
        }

        private void Move(float delta)
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;

            if (targetPos != lastTargetPos)
            {
                startPos = transform.position;
                lerpProgress = 0f;
                lastTargetPos = targetPos;
            }

            lerpProgress += delta * lerpSpeed;

            float t = lerpCurve.Evaluate(Mathf.Clamp01(lerpProgress));

            Vector3 newPos = transform.position;

            switch (moveMode)
            {
                case MoveModes.Snap:
                    newPos = targetPos;
                    break;

                case MoveModes.Lerp:
                    newPos = Vector3.Lerp(startPos, targetPos, useCurve ? t : lerpProgress);
                    break;

                case MoveModes.SLerp:
                    newPos = Vector3.Slerp(startPos, targetPos, useCurve ? t : lerpProgress);
                    break;
            }

            if (constrains.x) newPos.x = transform.position.x;
            if (constrains.y) newPos.y = transform.position.y;
            if (constrains.z) newPos.z = transform.position.z;

            transform.position = newPos;
        }
    }
}
