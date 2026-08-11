using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Abb2kTools.Utils
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshRenderer))]
    [DefaultExecutionOrder(-10)]
    public class SpriteRenderer3D : MonoBehaviour
    {
        [SerializeField]
        private Sprite _sprite;
        private Sprite _oldSpr;

        [SerializeField]
        private Color _color = Color.white;

        [SerializeField]
        private bool _flipX;

        [SerializeField]
        private bool _flipY;

        public enum DrawMode
        {
            Simple,
            Sliced,
            Tiled
        }

        [SerializeField]
        private DrawMode _drawMode = DrawMode.Simple;

        [SerializeField]
        private Vector2 _size = Vector2.one;

        private MaterialPropertyBlock _mpb;

        public enum SurfaceType
        {
            Opaque,
            Transparent
        }

        [SerializeField]
        private SurfaceType _surfaceType = SurfaceType.Opaque;

        [SerializeField]
        private Material opaqueMaterial;

        [SerializeField]
        private Material transparentMaterial;

        [SerializeField]
        private bool setNativeSizeForY;

        private MeshRenderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();
        }

        void OnValidate()
        {
            UpdateMaterialData();
        }

        void Start()
        {
            UpdateMaterialData();
        }

        private void Update()
        {
            if (_oldSpr != _sprite)
            {
                _oldSpr = _sprite;
                UpdateMaterialData();
            }
        }

        public void UpdateMaterialData()
        {
            if (_renderer == null)
                _renderer = GetComponent<MeshRenderer>();

            // Switch material based on surface type
            switch (_surfaceType)
            {
                case SurfaceType.Opaque:
                    _renderer.sharedMaterial = opaqueMaterial;
                    break;
                case SurfaceType.Transparent:
                    _renderer.sharedMaterial = transparentMaterial;
                    break;
            }

            // Apply MPB
            if (_sprite == null)
            {
                _renderer.SetPropertyBlock(null);
                return;
            }

            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();

            _renderer.GetPropertyBlock(_mpb);

            Vector2 textureSize = new Vector2(_sprite.texture.width, _sprite.texture.height);
            Vector2 rectSize = new Vector2(_sprite.rect.width, _sprite.rect.height);

            // Calculate Flip logic for Tiling and Offset
            Vector2 finalTiling = rectSize / textureSize;
            Vector2 finalOffset = _sprite.rect.position / textureSize;

            if (_flipX)
            {
                finalTiling.x *= -1;
                finalOffset.x += rectSize.x / textureSize.x;
            }
            
            if (_flipY)
            {
                finalTiling.y *= -1;
                finalOffset.y += rectSize.y / textureSize.y;
            }

            _mpb.SetTexture("_MainTex", _sprite.texture);
            _mpb.SetColor("_Color", _color); 
            _mpb.SetVector("_SprTiling", finalTiling);
            _mpb.SetVector("_SprOffset", finalOffset);
            
            // Push DrawMode settings to shader so your custom material can handle 9-slicing/tiling logic
            _mpb.SetFloat("_DrawMode", (float)_drawMode);
            _mpb.SetVector("_Size", _size);

            _renderer.SetPropertyBlock(_mpb);
        }

#if UNITY_EDITOR
        [ContextMenu("Open Sprite Editor")]
        public void OpenSpriteEditor()
        {
            if (_sprite == null || _sprite.texture == null) 
            {
                Debug.LogWarning("No Sprite assigned to open in the Sprite Editor.");
                return;
            }

            // Selects the texture in the project window
            Selection.activeObject = _sprite.texture;
            
            // Opens the Sprite Editor window natively
            EditorApplication.ExecuteMenuItem("Window/2D/Sprite Editor");
        }
#endif

        [ContextMenu("SetNativeSize")]
        public void SetNativeSize()
        {
            if (_sprite == null) return;

#if UNITY_EDITOR
            Undo.RecordObject(transform, "SR3D - Set Native Size");
#endif

            float textureLargest = Mathf.Max(_sprite.texture.width, _sprite.texture.height);
            Vector2 rectSize = _sprite.rect.size;

            float rectLargest = Mathf.Max(rectSize.x, rectSize.y);
            float scaleFactor = textureLargest / rectLargest;

            Vector3 scale = transform.localScale;
            scale.x = rectSize.x / textureLargest * scaleFactor;
            scale.y = rectSize.y / textureLargest * scaleFactor;
            scale.z = !setNativeSizeForY ? rectSize.x / textureLargest * scaleFactor : 1;

            // If drawing as sliced or tiled, we should also initialize the size property to the rect size
            if (_drawMode != DrawMode.Simple)
            {
                _size = new Vector2(scale.x, scale.y);
                UpdateMaterialData();
            }

            transform.localScale = scale;

#if UNITY_EDITOR
            EditorUtility.SetDirty(transform);
#endif
        }
    }
}