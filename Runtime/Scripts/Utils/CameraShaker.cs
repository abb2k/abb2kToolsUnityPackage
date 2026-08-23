#if DOTWEEN
using Abb2kTools.Singletons;
using DG.Tweening;
using UnityEngine;

namespace Abb2kTools.Utils
{
    [System.Serializable]
    public class CamShake
    {
        public float duration;
        public Vector3 strength = Vector3.zero;
        public int vibrato = 10;
        public float randomness = 90;
        public bool fadeOut = true;
        public ShakeRandomnessMode randomnessMode;
    }

    [System.Serializable]
    public class CamShakeData
    {
        public CamShake positionShake;
        public CamShake rotationShake;
    }

    [Icon("packages/com.abb2k.abb2ktools/Editor/Icons/CameraShaker.png")]
    public class CameraShaker : PersistentSingleton<CameraShaker>
    {
        private Camera _camera;
        private Camera Cam
        {
            get {
                if (_camera == null) _camera = Camera.main;
                
                return _camera;
            }
        }

        private Tweener positionShake;
        private Tweener rotationShake;

        private Vector3 trueOriginalPos;
        private Quaternion trueOriginalRot;

        public void Shake(CamShakeData shake)
        {
            if (Cam == null) return;

            if (positionShake == null || positionShake != null && !positionShake.IsActive())
            {
                trueOriginalPos = Cam.transform.localPosition;
            }

            if (rotationShake == null || rotationShake != null && !rotationShake.IsActive())
            {
                trueOriginalRot = Cam.transform.localRotation;
            }

            if (shake.positionShake != null && shake.positionShake.duration > 0)
            {
                if (positionShake != null)
                    positionShake.Complete();
                positionShake = Cam.DOShakePosition(
                    shake.positionShake.duration,
                    shake.positionShake.strength,
                    shake.positionShake.vibrato,
                    shake.positionShake.randomness,
                    shake.positionShake.fadeOut,
                    shake.positionShake.randomnessMode
                ).OnComplete(() =>
                {
                    if (_camera == null) return;

                    _camera.transform.localPosition = trueOriginalPos;
                });
            }
            
            if (shake.rotationShake != null && shake.rotationShake.duration > 0)
            {
                if (rotationShake != null)
                    rotationShake.Complete();
                rotationShake = Cam.DOShakeRotation(
                    shake.rotationShake.duration,
                    shake.rotationShake.strength,
                    shake.rotationShake.vibrato,
                    shake.rotationShake.randomness,
                    shake.rotationShake.fadeOut,
                    shake.rotationShake.randomnessMode
                ).OnComplete(() =>
                {
                    if (_camera == null) return;

                    _camera.transform.localRotation = trueOriginalRot;
                });
            }
        }
    }
}
#endif