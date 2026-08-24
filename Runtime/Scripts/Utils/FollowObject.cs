using UnityEngine;

namespace Abb2kTools.Utils
{
    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/FollowObject.png")]
    [AddComponentMenu("Abb2kTools/Utils/FollowObject")]
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

        // Position Lerp Settings
        public float lerpSpeed = 2f;
        public bool useCurve;
        public AnimationCurve lerpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2),
            new Keyframe(1f, 1f, 0, 0f)
        );
        public float moveSpeed = 10f;

        // --- Rotation Variables ---
        public bool rotateMovementTargetToMovement = false;
        public Transform movementRotationTarget; 

        public bool rotateToTarget = false;

        public bool syncRotationToCustomTarget = false;
        public Transform syncRotationTarget;

        public MoveModes rotationMode = MoveModes.Lerp;
        public float rotationLerpSpeed = 5f;
        public bool rotationUseCurve = false;
        public AnimationCurve rotationLerpCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2),
            new Keyframe(1f, 1f, 0, 0f)
        );
        public float rotationMoveSpeed = 180f; // in degrees per second

        public Vector3 CurrentMoveDirection { get; private set; }

        // Position State
        private float lerpProgress;
        private Vector3 startPos;
        private Vector3 lastTargetPos;
        private Vector3 previousTargetPos;
        private Vector3 currentDynamicOffset;

        // Rotation State
        private float moveRotProgress;
        private Quaternion startMoveRot;
        private Quaternion lastTargetMoveRot;

        private float targetLookRotProgress;
        private Quaternion startTargetLookRot;
        private Quaternion lastTargetLookRot;

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

                // Option 1: Rotate a specific target object in the direction of movement
                if (rotateMovementTargetToMovement)
                {
                    Transform activeMoveRotTarget = movementRotationTarget != null ? movementRotationTarget : target;
                    Quaternion targetRot = Quaternion.LookRotation(moveDirection);

                    // Reset curve if target rotation changes significantly
                    if (Quaternion.Angle(targetRot, lastTargetMoveRot) > 0.1f)
                    {
                        startMoveRot = activeMoveRotTarget.rotation;
                        moveRotProgress = 0f;
                        lastTargetMoveRot = targetRot;
                    }

                    moveRotProgress += delta * rotationLerpSpeed;
                    float rt = rotationLerpCurve.Evaluate(Mathf.Clamp01(moveRotProgress));

                    activeMoveRotTarget.rotation = CalculateRotation(activeMoveRotTarget.rotation, targetRot, startMoveRot, delta, rt);
                }
            }

            currentDynamicOffset = Vector3.Lerp(currentDynamicOffset, targetDynamicOffset, delta * forwardOffsetSmoothness);

            Vector3 targetPos = target.position + offset + currentDynamicOffset;

            // Update positional state for curves
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

            // Option 2: Rotate the current object in the direction of the target object
            if (rotateToTarget)
            {
                Vector3 directionToTarget = target.position - transform.position;
                if (directionToTarget.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(directionToTarget.normalized);
                    
                    // Reset curve if target look rotation changes significantly
                    if (Quaternion.Angle(lookRot, lastTargetLookRot) > 0.1f)
                    {
                        startTargetLookRot = transform.rotation;
                        targetLookRotProgress = 0f;
                        lastTargetLookRot = lookRot;
                    }

                    targetLookRotProgress += delta * rotationLerpSpeed;
                    float rtLook = rotationLerpCurve.Evaluate(Mathf.Clamp01(targetLookRotProgress));

                    transform.rotation = CalculateRotation(transform.rotation, lookRot, startTargetLookRot, delta, rtLook);
                }
            }

            // Option 3: Send current object's exact rotation to a custom target
            if (syncRotationToCustomTarget && syncRotationTarget != null)
            {
                syncRotationTarget.rotation = transform.rotation;
            }
        }

        // Helper Method to apply the correct rotation style based on chosen Rotation Mode
        private Quaternion CalculateRotation(Quaternion currentRot, Quaternion targetRot, Quaternion startRot, float delta, float curveT)
        {
            switch (rotationMode)
            {
                case MoveModes.Snap:
                    return targetRot;
                    
                case MoveModes.Lerp:
                    return rotationUseCurve 
                        ? Quaternion.Lerp(startRot, targetRot, curveT) 
                        : Quaternion.Lerp(currentRot, targetRot, delta * rotationLerpSpeed);
                        
                case MoveModes.SLerp:
                    return rotationUseCurve 
                        ? Quaternion.Slerp(startRot, targetRot, curveT) 
                        : Quaternion.Slerp(currentRot, targetRot, delta * rotationLerpSpeed);
                        
                case MoveModes.ConstantSpeed:
                    return Quaternion.RotateTowards(currentRot, targetRot, delta * rotationMoveSpeed);
                    
                default:
                    return targetRot;
            }
        }
    }
}