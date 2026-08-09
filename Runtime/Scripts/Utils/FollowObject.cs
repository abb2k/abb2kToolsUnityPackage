#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace Abb2kTools.Utils
{
    public class FollowObject : MonoBehaviour
    {
        public enum MoveModes { Snap, Lerp, SLerp }
        public enum FrameMovement { Update, LateUpdate, Fixed, Manual }

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
        public Vector3 forwardOffset;
#if ODIN_INSPECTOR
        [BoxGroup("General")]
#endif
        public float forwardOffsetSmoothness = 15f;
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
        public Constrains constrains = new();

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
        
        // New variables for movement tracking
        private Vector3 previousTargetPos;
        private Vector3 currentDynamicOffset;

        private void Update()
        {
            if (frameMovement == FrameMovement.Update)
                Move(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (frameMovement == FrameMovement.LateUpdate)
                Move(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (frameMovement == FrameMovement.Fixed)
                Move(Time.fixedDeltaTime);
        }

        public void ManualMove(float delta)
        {
            if (frameMovement == FrameMovement.Manual)
                Move(delta);
        }

        private void Move(float delta)
        {
            if (target == null) return;

            Vector3 moveDelta = target.position - previousTargetPos;
            previousTargetPos = target.position;

            Vector3 targetDynamicOffset = Vector3.zero;
            
            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 moveDirection = moveDelta.normalized;
                
                targetDynamicOffset = Vector3.Scale(moveDirection, forwardOffset);
            }

            currentDynamicOffset = Vector3.Lerp(currentDynamicOffset, targetDynamicOffset, delta * forwardOffsetSmoothness);

            Vector3 targetPos = target.position + offset + currentDynamicOffset;

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
                    newPos = useCurve 
                        ? Vector3.Lerp(startPos, targetPos, t) 
                        : Vector3.Lerp(transform.position, targetPos, delta * lerpSpeed);
                    break;

                case MoveModes.SLerp:
                    newPos = useCurve 
                        ? Vector3.Slerp(startPos, targetPos, t) 
                        : Vector3.Slerp(transform.position, targetPos, delta * lerpSpeed);
                    break;
            }

            if (constrains.x) newPos.x = transform.position.x;
            if (constrains.y) newPos.y = transform.position.y;
            if (constrains.z) newPos.z = transform.position.z;

            transform.position = newPos;
        }
    }
}