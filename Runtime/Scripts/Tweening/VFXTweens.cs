using DG.Tweening;
using UnityEngine;
using UnityEngine.VFX;

public static class VFXTweens
{
    public static Tweener DOColor(this VisualEffect vfx, string property, Color endValue, float duration)
    {
        if (!vfx.HasVector4(property))
            throw new System.ArgumentException($"VisualEffect does not have a color property named '{property}'");

        Color startValue = vfx.GetVector4(property);
        return DOTween.To(() => startValue, x => vfx.SetVector4(property, x), endValue, duration);
    }

    public static Tweener DOColor(this VisualEffect vfx, int propertyId, Color endValue, float duration)
    {
        if (!vfx.HasVector4(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have a color property with ID '{propertyId}'");

        Color startValue = vfx.GetVector4(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetVector4(propertyId, x), endValue, duration);
    }

    public static Tweener DOVector4(this VisualEffect vfx, string property, Vector4 endValue, float duration)
    {
        if (!vfx.HasVector4(property))
            throw new System.ArgumentException($"VisualEffect does not have a Vector4 property named '{property}'");

        Vector4 startValue = vfx.GetVector4(property);
        return DOTween.To(() => startValue, x => vfx.SetVector4(property, x), endValue, duration);
    }
    
    public static Tweener DOVector4(this VisualEffect vfx, int propertyId, Vector4 endValue, float duration)
    {
        if (!vfx.HasVector4(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have a Vector4 property with ID '{propertyId}'");

        Vector4 startValue = vfx.GetVector4(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetVector4(propertyId, x), endValue, duration);
    }

    public static Tweener DOInt(this VisualEffect vfx, string property, int endValue, float duration)
    {
        if (!vfx.HasInt(property))
            throw new System.ArgumentException($"VisualEffect does not have an int property named '{property}'");

        int startValue = vfx.GetInt(property);
        return DOTween.To(() => startValue, x => vfx.SetInt(property, x), endValue, duration);
    }

    public static Tweener DOInt(this VisualEffect vfx, int propertyId, int endValue, float duration)
    {
        if (!vfx.HasInt(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have an int property with ID '{propertyId}'");

        int startValue = vfx.GetInt(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetInt(propertyId, x), endValue, duration);
    }

    public static Tweener DOFloat(this VisualEffect vfx, string property, float endValue, float duration)
    {
        if (!vfx.HasFloat(property))
            throw new System.ArgumentException($"VisualEffect does not have a float property named '{property}'");

        float startValue = vfx.GetFloat(property);
        return DOTween.To(() => startValue, x => vfx.SetFloat(property, x), endValue, duration);
    }

    public static Tweener DOFloat(this VisualEffect vfx, int propertyId, float endValue, float duration)
    {
        if (!vfx.HasFloat(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have a float property with ID '{propertyId}'");

        float startValue = vfx.GetFloat(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetFloat(propertyId, x), endValue, duration);
    }

    public static Tweener DOUInt(this VisualEffect vfx, string property, uint endValue, float duration)
    {
        if (!vfx.HasUInt(property))
            throw new System.ArgumentException($"VisualEffect does not have a uint property named '{property}'");

        uint startValue = vfx.GetUInt(property);
        return DOTween.To(() => startValue, x => vfx.SetUInt(property, x), endValue, duration);
    }

    public static Tweener DOUInt(this VisualEffect vfx, int propertyId, uint endValue, float duration)
    {
        if (!vfx.HasUInt(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have a uint property with ID '{propertyId}'");

        uint startValue = vfx.GetUInt(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetUInt(propertyId, x), endValue, duration);
    }

    public static Tweener DOVector3(this VisualEffect vfx, string property, Vector3 endValue, float duration)
    {
        if (!vfx.HasVector3(property))
            throw new System.ArgumentException($"VisualEffect does not have a Vector3 property named '{property}'");

        Vector3 startValue = vfx.GetVector3(property);
        return DOTween.To(() => startValue, x => vfx.SetVector3(property, x), endValue, duration);
    }

    public static Tweener DOVector3(this VisualEffect vfx, int propertyId, Vector3 endValue, float duration)
    {
        if (!vfx.HasVector3(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have a Vector3 property with ID '{propertyId}'");

        Vector3 startValue = vfx.GetVector3(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetVector3(propertyId, x), endValue, duration);
    }

    public static Tweener DOVector2(this VisualEffect vfx, string property, Vector2 endValue, float duration)
    {
        if (!vfx.HasVector2(property))
            throw new System.ArgumentException($"VisualEffect does not have a Vector2 property named '{property}'");

        Vector2 startValue = vfx.GetVector2(property);
        return DOTween.To(() => startValue, x => vfx.SetVector2(property, x), endValue, duration);
    }

    public static Tweener DOVector2(this VisualEffect vfx, int propertyId, Vector2 endValue, float duration)
    {
        if (!vfx.HasVector2(propertyId))
            throw new System.ArgumentException($"VisualEffect does not have a Vector2 property with ID '{propertyId}'");

        Vector2 startValue = vfx.GetVector2(propertyId);
        return DOTween.To(() => startValue, x => vfx.SetVector2(propertyId, x), endValue, duration);
    }
}
