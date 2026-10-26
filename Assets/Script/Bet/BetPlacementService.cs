/// <summary>賭け条件の確認と保存。入力・演出・シーン遷移は扱わない。</summary>
public static class BetPlacementService
{
    public static bool TrySave(GameSession session, PredictionSide side, int amount)
    {
        if (session == null || session.LeftMonster == null || session.RightMonster == null
            || amount < 1 || amount > session.CurrentCoin) return false;

        session.SetBet(new BetData
        {
            Side = side,
            Amount = amount,
            InspectLevel = InspectLevel.Full,
            PayoutMultiplier = BetOddsCalculator.CalculatePayoutMultiplier(
                session.LeftMonster, session.RightMonster, side)
        });
        return true;
    }
}
