using UnityEngine;

namespace Abb2kTools.Utils
{
    public class FollowObject : MonoBehaviour
    {
        public enum MoveModes { Snap, Lerp, SLerp, ConstantSpeed }
        public enum FrameMovement { Update, LateUpdate, Fixed, Manual }

        public Transform target;
        public Vector3 offset;
        public Vector3 forwardOffset;
        public float forwardOffsetSmoothness = 15f;
        public MoveModes moveMode;
        public FrameMovement frameMovement;

        [System.Serializable]
        public class Constrains
        {
            public bool x = false;
            public bool y = false;
            public bool z = false;
        }
        public Constrains constrains = new();

        public float lerpSpeed = 2f;
        public bool useCurve;
        public AnimationCurve lerpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2),
            new Keyframe(1f, 1f, 0, 0f)
        );

        public float moveSpeed = 10f;

        public Vector3 CurrentMoveDirection { get; private set; }

        private float lerpProgress;
        private Vector3 startPos;
        private Vector3 lastTargetPos;
        
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

                case MoveModes.ConstantSpeed:
                    newPos = Vector3.MoveTowards(transform.position, targetPos, delta * moveSpeed);
                    break;
            }

            if (constrains.x) newPos.x = transform.position.x;
            if (constrains.y) newPos.y = transform.position.y;
            if (constrains.z) newPos.z = transform.position.z;

            CurrentMoveDirection = (newPos - transform.position).normalized;
            transform.position = newPos;
        }
    }
}