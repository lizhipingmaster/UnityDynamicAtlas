using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
//[ExecuteInEditMode]
public class SpriteRef : MonoBehaviour
{
    private static int TrimLen = "Assets/".Length;
    [HideInInspector]
    public string spritePath = null;
#if UNITY_EDITOR
    private void Reset()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr !=  null)
        {
            spritePath = AssetDatabase.GetAssetPath(sr.sprite).Substring(TrimLen);
        }
    }
#endif

    // Start is called before the first frame update
    void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var id = sr.sprite.name;
        sr.sprite = null;

#if UNITY_EDITOR
        DaVikingCode.AssetPacker.AssetPacker.Instance.OnProcessCompleted.AddListener(() =>
        {
            var sprite = DaVikingCode.AssetPacker.AssetPacker.Instance.GetSprite(id);
            if (sprite != null)
                sr.sprite = sprite;
        });
#endif
    }


    private void OnDestroy()
    {
        
    }
}
