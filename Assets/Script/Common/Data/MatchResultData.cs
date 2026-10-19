using System.Collections.Generic;

/// <summary>
/// 1試合分の結果データ
/// </summary>
[System.Serializable]
public class MatchResultData
{
    // ストーリーの時間切れは勝敗を付けず、王国兵による摘発エンドへ進む。
    public bool IsKingdomRaidEnding;

    // 毎ターンのイベントによる増加を含む、その試合の最終配当倍率。
    public double FinalPayoutMultiplier;

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
