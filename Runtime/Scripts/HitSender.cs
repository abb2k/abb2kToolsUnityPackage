using System;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;

namespace Abb2kTools
{
    public class HitSender : MonoBehaviour
    {
        #if !ODIN_INSPECTOR
        [SerializeField]
        [Header("Reference")]
        #endif
        private GameObject _hitRecieverObj;
        #if ODIN_INSPECTOR
        [BoxGroup("Reference")]
        [ShowInInspector, PropertyOrder(-1), SceneObjectsOnly, ValidateInput(
            "ValidateGameObject",
            "To work the 'HitReciever' interface must be set, or the object in this field must have a 'IHitReciever' component on it.",
            InfoMessageType.Warning
        )]
        #endif
        public GameObject HitRecieverObj
        {
            get => _hitRecieverObj;
            set
            {
                _hitRecieverObj = value;
                if (_hitRecieverObj && _hitRecieverObj.TryGetComponent(out IHitReciever hitReciever))
                    _hitReciever = hitReciever;
                else
                    _hitReciever = null;
            }
        }
        private IHitReciever _hitReciever;
        #if ODIN_INSPECTOR
        [BoxGroup("Reference")]
        [ShowInInspector, SceneObjectsOnly, PropertyOrder(-1)]
        #endif
        public IHitReciever HitReciever
        {
            get
            {
                if (_hitReciever == null && _hitRecieverObj && _hitRecieverObj.TryGetComponent(out IHitReciever hitReciever))
                {
                    _hitReciever = hitReciever;
                }

                return _hitReciever;
            }
            set
            {
                _hitReciever = value;
            }
        }

        #if ODIN_INSPECTOR
        [BoxGroup("Options")]
        #else
        [Header("Options")]
        #endif
        public int hitID;

        [Flags]
        public enum HitTypes
        {
            None = 0,
            Enter = 1 << 0,
            Stay = 1 << 1,
            Exit = 1 << 2
        }

        #if ODIN_INSPECTOR
        [BoxGroup("Options")]
        #else
        [Header("3D")]
        #endif
        public bool send3D;

        #if ODIN_INSPECTOR
        [BoxGroup("Options/3D"), ShowIf("send3D"), LabelText("Collision"), LabelWidth(52.5f)]
        #endif
        public HitTypes sendCollision3D;
        #if ODIN_INSPECTOR
        [BoxGroup("Options/3D"), ShowIf("send3D"), LabelText("Trigger"), LabelWidth(52.5f)]
        #endif
        public HitTypes sendTrigger3D;

        #if ODIN_INSPECTOR
        [BoxGroup("Options")]
        #else
        [Header("2D")]
        #endif
        public bool send2D;

        #if ODIN_INSPECTOR
        [BoxGroup("Options/2D"), ShowIf("send2D"), LabelText("Collision"), LabelWidth(52.5f)]
        #endif
        public HitTypes sendCollision2D;
        #if ODIN_INSPECTOR
        [BoxGroup("Options/2D"), ShowIf("send2D"), LabelText("Trigger"), LabelWidth(52.5f)]
        #endif
        public HitTypes sendTrigger2D;

        #if ODIN_INSPECTOR
        private bool ValidateGameObject(GameObject HitRecieverObj)
        {
            return HitReciever == null ? HitRecieverObj.TryGetComponent(out IHitReciever _) : true;
        }
        #endif

        #region Collision

        void OnCollisionEnter(Collision collision)
        {
            if (send3D && sendCollision3D.HasFlag(HitTypes.Enter))
                HitReciever?.OnCollision(collision, MakeData(IHitReciever.HitType.Enter));
        }

        void OnCollisionStay(Collision collision)
        {
            if (send3D && sendCollision3D.HasFlag(HitTypes.Stay))
                HitReciever?.OnCollision(collision, MakeData(IHitReciever.HitType.Stay));
        }

        void OnCollisionExit(Collision collision)
        {
            if (send3D && sendCollision3D.HasFlag(HitTypes.Exit))
                HitReciever?.OnCollision(collision, MakeData(IHitReciever.HitType.Exit));
        }

        #endregion
        #region Trigger

        void OnTriggerEnter(Collider other)
        {
            if (send3D && sendTrigger3D.HasFlag(HitTypes.Enter))
                HitReciever?.OnTrigger(other, MakeData(IHitReciever.HitType.Enter));
        }

        void OnTriggerStay(Collider other)
        {
            if (send3D && sendTrigger3D.HasFlag(HitTypes.Stay))
                HitReciever?.OnTrigger(other, MakeData(IHitReciever.HitType.Stay));
        }

        void OnTriggerExit(Collider other)
        {
            if (send3D && sendTrigger3D.HasFlag(HitTypes.Exit))
                HitReciever?.OnTrigger(other, MakeData(IHitReciever.HitType.Exit));
        }

        #endregion
        #region Collision2D

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (send2D && sendCollision2D.HasFlag(HitTypes.Enter))
                HitReciever?.OnCollision2D(collision, MakeData(IHitReciever.HitType.Enter));
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (send2D && sendCollision2D.HasFlag(HitTypes.Stay))
                HitReciever?.OnCollision2D(collision, MakeData(IHitReciever.HitType.Stay));
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (send2D && sendCollision2D.HasFlag(HitTypes.Exit))
                HitReciever?.OnCollision2D(collision, MakeData(IHitReciever.HitType.Exit));
        }

        #endregion
        #region Trigger2D

        void OnTriggerEnter2D(Collider2D collision)
        {
            if (send2D && sendTrigger2D.HasFlag(HitTypes.Enter))
                HitReciever?.OnTrigger2D(collision, MakeData(IHitReciever.HitType.Enter));
        }

        void OnTriggerStay2D(Collider2D collision)
        {
            if (send2D && sendTrigger2D.HasFlag(HitTypes.Stay))
                HitReciever?.OnTrigger2D(collision, MakeData(IHitReciever.HitType.Stay));
        }

        void OnTriggerExit2D(Collider2D collision)
        {
            if (send2D && sendTrigger2D.HasFlag(HitTypes.Exit))
                HitReciever?.OnTrigger2D(collision, MakeData(IHitReciever.HitType.Exit));
        }

        #endregion

        private IHitReciever.HitData MakeData(IHitReciever.HitType type) => new IHitReciever.HitData
        {
            id = hitID,
            type = type,
            hitSender = gameObject
        };
    }
}