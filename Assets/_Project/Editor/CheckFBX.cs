using UnityEditor;
using UnityEngine;

public class CheckFBX {
    [MenuItem("Tools/Check FBX")]
    public static void Run() {
        var path = "SourceArt/PSX_Forest_Level_byStarkCrafts/PSX_Forest_AssetCollection_byStarkCrafts.fbx";
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        Debug.Log("Checking " + path + ", found: " + assets.Length);
        foreach (var asset in assets) {
            if (asset is Mesh m) Debug.Log("Mesh: " + m.name);
            if (asset is GameObject g) Debug.Log("GameObject: " + g.name);
        }
    }
}
