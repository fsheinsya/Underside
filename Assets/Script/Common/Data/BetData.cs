/// <summary>
/// 賭け情報
/// </summary>
[System.Serializable]
public class BetData
{
    // 左右どちらに賭けるか
    public PredictionSide Side;

    // 賭け金
    public int Amount;

    // 情報開示レベル
    public InspectLevel InspectLevel;

    // 的中時の配当倍率
    public float PayoutMultiplier;
}