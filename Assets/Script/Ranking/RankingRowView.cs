using UnityEngine;
using TMPro;

/// <summary>
/// ランキングの1行分を表示する担当。順位の計算や保存は行わない。
/// RankingSceneControllerから渡された番号と成績を、Prefab内のテキストへ反映する。
/// </summary>
public class RankingRowView : MonoBehaviour
{
    //ランキングシーン
    [SerializeField] private TMP_Text rankText;     // 順位の表示先。
    [SerializeField] private TMP_Text userNameText; // 名前の表示先。
    [SerializeField] private TMP_Text coinText;     // コイン数の表示先。
    [SerializeField] private TMP_Text roundText;    // ラウンド数の表示先。

    // 1人分の記録を、それぞれのテキストへ表示する。
    public void SetData(int rank, RankingData data)
    {
        rankText.text = $"{rank}位";
        userNameText.text = $"{data.userName}";
        coinText.text = $"所持コイン数：{ data.coin.ToString("N0")}";
        roundText.text = $"到達ラウンド：{data.round.ToString()}";
    }
}
