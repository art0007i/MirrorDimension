using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AssetBundleBuilder : MonoBehaviour
{
    public Object bundleAsset;
    public bool CopyToResourcesDir = true;

    public void BuildBundles()
    {
#if UNITY_EDITOR
        AssetBundleBuild[] buildMap = new AssetBundleBuild[1];

        buildMap[0].assetBundleName = "shaderbundle";

        var assPath = AssetDatabase.GetAssetPath(bundleAsset);
        buildMap[0].assetNames = new string[] { assPath };

        // rename the bundled asset for easier access
        buildMap[0].addressableNames = new string[] { Path.GetFileName(assPath) };

        for (var i = 0; i < buildMap.Length; ++i)
        {
            var m = buildMap[i];
            Debug.Log("============== MAP " + i + "==============");
            for (int j = 0; j < m.assetNames.Length; j++)
            {
                Debug.Log($"{m.assetNames[j]} -> {m.addressableNames[j] ?? "NULL"}");
            }
        }

        if(!AssetDatabase.IsValidFolder("Assets/Bundles")) AssetDatabase.CreateFolder("Assets", "Bundles");
        BuildPipeline.BuildAssetBundles("Assets/Bundles", buildMap, 0, BuildTarget.StandaloneWindows);
        if (!CopyToResourcesDir) return;
        var rootPath = Path.GetDirectoryName(Application.dataPath);
        var srcPath = Path.Combine(Application.dataPath, "Bundles");
        var dstPath = Path.Combine(Path.GetDirectoryName(rootPath), "Resources");
        Debug.Log("Building Complete... Copying to " + dstPath);
        foreach (var bundle in buildMap)
        {
            var src = Path.Combine(srcPath, bundle.assetBundleName);
            var dst = Path.Combine(dstPath, bundle.assetBundleName);
            File.Copy(src, dst, true);
        }
#endif
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AssetBundleBuilder))]
public class AssetBundleMakerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (AssetBundleBuilder) target;
        base.OnInspectorGUI();
        if (GUILayout.Button("Build Bundle"))
        {
            t.BuildBundles();
        }
    }
}
#endif