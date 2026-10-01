using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AssignMissingTMP
{
    [MenuItem("Tools/TextMeshPro/Missingフォントを一括設定")]
    private static void AssignMissingFonts()
    {
        // Projectで選択中のFont Assetを取得
        TMP_FontAsset selectedFont =
            Selection.activeObject as TMP_FontAsset;

        if (selectedFont == null)
        {
            Debug.LogError("ProjectからTMP Font Assetを選択してください");
            return;
        }

        // 非アクティブを含む、開いているシーン内のTMPテキストを取得
        TMP_Text[] texts = Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int changedCount = 0;

        foreach (TMP_Text text in texts)
        {
            // 正常なフォントが設定されているものは変更しない
            if (text.font != null)
                continue;

            Undo.RecordObject(text, "Assign Missing TMP Font");

            text.font = selectedFont;

            EditorUtility.SetDirty(text);
            EditorSceneManager.MarkSceneDirty(text.gameObject.scene);

            changedCount++;
        }

        Debug.Log($"{changedCount}個のMissingフォントを修正しました");
    }

    [MenuItem("Tools/TextMeshPro/Missingフォントを一括設定", true)]
    private static bool Validate()
    {
        return Selection.activeObject is TMP_FontAsset;
    }
}