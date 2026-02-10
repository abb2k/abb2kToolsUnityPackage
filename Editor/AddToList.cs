using UnityEditor;
using UnityEngine;

public class AddToList
{
    [MenuItem("GameObject/3D Object/3D Sprite", priority = 10)]
    private static void CreateCustomSprite(MenuCommand menuCommand)
    {
        GameObject spr = GameObject.Instantiate(Resources.Load<GameObject>("3DSprite"));
        spr.name = spr.name.Replace("(Clone)", "");

        Undo.RegisterCreatedObjectUndo(spr, "Create " + spr.name);

        GameObject parent = menuCommand.context as GameObject;
        if (parent != null)
        {
            GameObjectUtility.SetParentAndAlign(spr, parent);
        }

        Vector3 spawnPosition = Vector3.zero;

        if (Selection.activeTransform != null)
        {
            spawnPosition = Selection.activeTransform.position;
        }
        else
        {
            if (SceneView.lastActiveSceneView != null)
            {
                spawnPosition = SceneView.lastActiveSceneView.pivot;
            }
        }

        spr.transform.position = spawnPosition;

        Selection.activeObject = spr;
    }
}
