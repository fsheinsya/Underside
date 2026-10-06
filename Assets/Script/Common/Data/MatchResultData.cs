using System.Collections.Generic;

/// <summary>
/// 1試合分の結果データ
/// </summary>
[System.Serializable]
public class MatchResultData
{
    // 勝者名
    public string WinnerName;

    // 勝者が左右どちらか
    public PredictionSide WinnerSide;

    // 予想が当たったかどうか
    public bool IsPredictionSuccess;

    // 的中時にもらえるコイン
    public int RewardCoin;

    // 戦闘ログ一覧
    public List<BattleLogEntry> BattleLogs;
}