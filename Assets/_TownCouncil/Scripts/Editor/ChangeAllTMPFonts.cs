
using UnityEditor;
using UnityEngine;
using TMPro;

public class ChangeAllTMPFonts : EditorWindow
{
    [MenuItem("Town Council/UI/Replace All TMP Fonts")]
    public static void ReplaceFonts()
    {
        TMP_FontAsset newFont =
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_TownCouncil/Fonts/Cinzel-Regular SDF.asset"
            );

        if (newFont == null)
        {
            Debug.LogError("Cinzel font asset not found!");
            return;
        }

        int count = 0;

        foreach (TMP_Text text in
                 Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (!text.gameObject.scene.IsValid())
                continue;

            Undo.RecordObject(text, "Change TMP Font");
            text.font = newFont;
            EditorUtility.SetDirty(text);
            count++;
        }

        Debug.Log($"Updated {count} TMP components.");
    }
}
