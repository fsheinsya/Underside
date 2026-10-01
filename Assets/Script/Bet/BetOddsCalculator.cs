using UnityEngine;

/// <summary>
/// モンスターの強さをざっくり評価して、賭け倍率を決めるクラス
/// 弱い側に賭けるほど倍率が高くなる
/// </summary>
public static class BetOddsCalculator
{
    /// <summary>
    /// 指定した側に賭けた場合の配当倍率を返す
    /// </summary>
    public static float CalculatePayoutMultiplier(MonsterData left, MonsterData right, PredictionSide selectedSide)
    {
        // 左右の強さスコアを計算する
        float leftScore = CalculateMonsterScore(left, right);
        float rightScore = CalculateMonsterScore(right, left);

        // 賭け対象側と相手側のスコアを決める
        float selectedScore = selectedSide == PredictionSide.Left ? leftScore : rightScore;
        float opponentScore = selectedSide == PredictionSide.Left ? rightScore : leftScore;

        // 同じくらいならほぼ等倍
        if (Mathf.Approximately(selectedScore, opponentScore))
        {
            return 2.0f;
        }

        // 自分が弱いほど倍率が高くなる
        float ratio = opponentScore / Mathf.Max(1f, selectedScore);

        // 倍率を制限する
        // 強い方に賭けたら低め、弱い方に賭けたら高め
        float payout = Mathf.Clamp(ratio, 2.0f, 5.0f);

        return payout;
    }

    /// <summary>
    /// モンスターの総合力をざっくり数値化する
    /// </summary>
    private static float CalculateMonsterScore(MonsterData self, MonsterData opponent)
    {
        float score = 0f;

        // 基本能力値
        score += self.Stats.MaxHp * 0.35f;
        score += self.Stats.Attack * 1.8f;
        score += self.Stats.Defense * 1.5f;
        score += self.Stats.Growth * 1.0f;
        score += self.Stats.Speed * 1.3f;
        score += self.Stats.Accuracy * 0.4f;
        score += self.Stats.Evasion * 0.5f;
        score += self.Stats.CriticalRate * 0.6f;

        // 属性相性ボーナス
        float elementBonus = GetElementBonus(self.Element, opponent.Element);
        score *= elementBonus;

        // 性格による簡易補正
        score *= GetPersonalityBonus(self.Personality);

        // スキル数が多いほど少し有利
        if (self.Skills != null)
        {
            score += self.Skills.Count * 8f;
        }

        return score;
    }

    /// <summary>
    /// 属性相性による補正
    /// </summary>
    private static float GetElementBonus(ElementType attacker, ElementType defender)
    {
        if (attacker == ElementType.Water && defender == ElementType.Fire) return 1.15f;
        if (attacker == ElementType.Fire && defender == ElementType.Grass) return 1.15f;
        if (attacker == ElementType.Grass && defender == ElementType.Water) return 1.15f;
        return 1.0f;
    }

    /// <summary>
    /// 性格による簡易補正
    /// </summary>
    private static float GetPersonalityBonus(PersonalityType personality)
    {
        switch (personality)
        {
            case PersonalityType.Bold:
                return 1.08f;
            case PersonalityType.Calm:
                return 1.05f;
            case PersonalityType.Flexible:
                return 1.03f;
            case PersonalityType.Taunt:
                return 1.02f;
            case PersonalityType.Aggressive:
                return 1.06f;
            case PersonalityType.Timid:
                return 1.00f;
            default:
                return 1.0f;
        }
    }
}