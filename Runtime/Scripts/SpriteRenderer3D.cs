#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
[DefaultExecutionOrder(-10)]
public class SpriteRenderer3D : MonoBehaviour
{
    [SerializeField]
#if ODIN_INSPECTOR
    [OnValueChanged("UpdateMaterialData"), LabelText("Sprite")]
#endif
    private Sprite _sprite;
    private Sprite _oldSpr;

    [SerializeField]
#if ODIN_INSPECTOR
    [OnValueChanged("UpdateMaterialData"), LabelText("Tiling")]
#endif
    private Vector2 _tiling = Vector2.one;

    [SerializeField, HideInInspector]
    private MaterialPropertyBlock _mpb;

    public enum SurfaceType
    {
        Opaque,
        Transparent
    }

    [SerializeField]
#if ODIN_INSPECTOR
    [OnValueChanged("UpdateMaterialData"), LabelText("Surface Type")]
#endif
    private SurfaceType _surfaceType = SurfaceType.Opaque;

    [SerializeField]
#if ODIN_INSPECTOR
    [Required, LabelText("Opaque Material")]
#endif
    private Material opaqueMaterial;

    [SerializeField]
#if ODIN_INSPECTOR
    [Required, LabelText("Transparent Material")]
#endif
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
#if !ODIN_INSPECTOR
    void OnValidate()
    {
        UpdateMaterialData();
    }
#endif

    void Start()
    {
        UpdateMaterialData();
    }
    
#if !ODIN_INSPECTOR
    private void Update()
    {
        if (_oldSpr != _sprite)
        {
            _oldSpr = _sprite;
            UpdateMaterialData();
        }
    }
#endif

    private void UpdateMaterialData()
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

        _mpb.SetTexture("_MainTex", _sprite.texture);
        _mpb.SetVector("_SprTiling", rectSize / textureSize);
        _mpb.SetVector("_SprOffset", _sprite.rect.position / textureSize);
        _mpb.SetVector("_tilingExtra", _tiling);

        _renderer.SetPropertyBlock(_mpb);
    }

#if ODIN_INSPECTOR
    [Button]
#else
    [ContextMenu("SetNativeSize")]
#endif
    private void SetNativeSize()
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

        transform.localScale = scale;

#if UNITY_EDITOR
        EditorUtility.SetDirty(transform);
#endif
    }
}
