using UnityEngine;
using UnityEngine.SceneManagement;

// Rankingシーンの「選択画面へ戻る」ボタンに取り付ける。
public class RankingBackButton : MonoBehaviour
{
    private bool isMoving; // 連打による重複したシーン移動を防ぐ。

    // ボタンのOn Clickから呼び出す。
    public void OnClickBackToSelect()
    {
        if (isMoving) return;

        isMoving = true;
        Debug.Log("[Ranking/UI] 選択画面へ戻ります。", this);

        try
        {
            SceneManager.LoadScene(SceneNames.Select); // 選択画面を開く。
        }
        catch (System.Exception exception)
        {
            isMoving = false; // 移動に失敗した場合は再試行できるようにする。
            Debug.LogError($"[Ranking/UI] 選択画面への移動に失敗しました: {exception}", this);
        }
    }
}