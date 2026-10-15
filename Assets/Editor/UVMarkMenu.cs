#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// GameObject > UV Light > Create UV Mark (Quad)
/// Creates a hidden UV mark in front of the Scene view camera (or under the selected object).
/// Move it ~1-2 cm in front of the blackboard / desk / wall, scale it, and assign a Mark Texture.
/// </summary>
public static class UVMarkMenu
{
    [MenuItem("GameObject/UV Light/Create UV Mark (Quad)", false, 10)]
    static void CreateUVMark(MenuCommand command)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "UVMark";

        // A collider would make the flashlight retract against it and block the player.
        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        GameObject parent = command.context as GameObject;
        if (parent != null) go.transform.SetParent(parent.transform, false);

        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null && sv.camera != null)
        {
            go.transform.position = sv.pivot;
            // The visible side of a Quad faces -Z, so look along the camera direction.
            go.transform.rotation = Quaternion.LookRotation(sv.camera.transform.forward);
        }
        go.transform.localScale = new Vector3(0.4f, 0.2f, 1f);

        go.AddComponent<UVRevealable>();

        Undo.RegisterCreatedObjectUndo(go, "Create UV Mark");
        Selection.activeGameObject = go;
    }
}
#endif
