using System; // 文字列の比較と例外処理に使用する。
using System.Collections.Generic; // 生成した行をListで管理する。
using UnityEngine;
using UnityEngine.UI; // ScrollRectとレイアウト更新に使用する。
using TMPro; // 管理者コードの入力欄に使用する。

/// <summary>
/// 保存済みランキングを全件表示する画面。並べ替えはRankingServiceへ任せる。
/// 返された一覧の順にRankingRowViewを生成し、表示番号を1から付ける。
/// 同じ成績でも同順位にはせず、名前順に並んだ行へ連番を表示する。
/// AllResultの上位5人表示とは異なり、この画面では全員をスクロール表示する。
/// </summary>
public class RankingSceneController : MonoBehaviour
{
    [Header("ランキング表示")]
    [SerializeField] private ScrollRect scrollRect; // ランキングビューのScroll Rect。
    [SerializeField] private RectTransform content; // 行を追加するContent。
    [SerializeField] private RankingRowView rowPrefab; // 1人分の行Prefab。

    [Header("管理者用の全件削除")]
    [SerializeField] private TMP_InputField adminCodeInput; // 削除コードの入力欄。
    [SerializeField] private string adminDeleteCode = ""; // Inspectorで設定。空欄なら削除不可。

    // このControllerが生成した行だけを管理する。
    private readonly List<RankingRowView> rows = new List<RankingRowView>();

    private void Start()
    {
        RefreshRanking(); // シーンを開いた時に保存済みのランキングを表示する。
    }

    // 更新ボタンからも呼び出せるようにpublicにする。
    public void RefreshRanking()
    {
        // Inspectorの必須項目が未設定なら、生成せずに原因を表示する。
        if (scrollRect == null || content == null || rowPrefab == null)
        {
            Debug.LogError("[Ranking/View] InspectorのScroll Rect・Content・Row Prefabを設定してください。", this);
            return;
        }

        try
        {
            // コイン優先で並べ替えられた、記録のある全員分を読み込む。
            var entries = RankingService.GetRanking();

            // 再表示したときに同じ行が重複しないよう、以前の生成行を消す。
            ClearRows();

            for (int index = 0; index < entries.Count; index++)
            {
                // Contentの子として、1人分のPrefabを生成する。
                RankingRowView row = Instantiate(rowPrefab, content, false);
                rows.Add(row); // 次回の再表示時に削除できるよう記録する。
                row.gameObject.SetActive(true); // 行を表示する。
                row.SetData(index + 1, entries[index]); // 順位と成績を設定する。
            }

            // 行の数に合わせてContentの高さを再計算する。
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            // スクロールの慣性を止め、先頭の順位から表示する。
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;

            if (entries.Count == 0) Debug.Log("[Ranking/View] まだ記録がありません。", this);
            Debug.Log($"[Ranking/View] {entries.Count}人のランキングを表示しました。", this);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Ranking/View] 表示に失敗しました: {exception}", this);
        }
    }

    private void ClearRows()
    {
        foreach (RankingRowView row in rows)
        {
            if (row == null) continue; // すでに削除された行は飛ばす。

            // Destroyはフレーム末に行われるため、先に非表示にしてレイアウトから外す。
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }

        rows.Clear(); // 生成した行の管理リストを空にする。
    }

    // 管理者用の削除ボタンのOn Clickに設定する。
    public void OnClickDeleteAllRanking()
    {
        // 削除コードが空欄の場合、空の入力で削除できないようにする。
        if (string.IsNullOrWhiteSpace(adminDeleteCode) || adminCodeInput == null)
        {
            Debug.LogWarning("[Ranking/Admin] 削除設定が未完了のため中止しました。", this);
            return;
        }

        // 大文字・小文字・空白を含め、設定したコードと完全一致するか確認する。
        if (!string.Equals(adminCodeInput.text, adminDeleteCode, StringComparison.Ordinal))
        {
            Debug.LogWarning("[Ranking/Admin] コード不一致のため削除しませんでした。", this);
            return;
        }

        try
        {
            // プレイヤー名と記録の一覧を削除する。他のPlayerPrefsには触れない。
            RankingStore.DeleteAllRankingData();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Ranking/Admin] 削除に失敗しました: {exception}", this);
            return;
        }

        adminCodeInput.text = ""; // 処理後は入力されたコードを消す。
        ClearRows(); // 削除前の記録が画面に残らないようにする。
        RefreshRanking(); // 空になったランキングを再表示する。
        Debug.Log("[Ranking/Admin] ランキングの全件削除が完了しました。", this);
    }

}