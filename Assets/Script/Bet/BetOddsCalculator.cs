using UnityEngine;

/// <summary>
/// モンスターの強さをざっくり評価して、賭け倍率を決めるクラス
/// 弱い側に賭けるほど倍率が高くなる
/// </summary>
public static class BetOddsCalculator
{
    /// <summary>
    /// ここでBetの計算をする
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
        float payout = Mathf.Max(1.0f+ratio, 1.1f);

        return payout;
    }

    /// <summary>
    /// モンスターの総合力をざっくり数値化する
    /// </summary>
    private static float CalculateMonsterScore(MonsterData self, MonsterData opponent)
    {
        float score = 0f;

        // 基本能力値
        score += self.Stats.MaxHp * 1.0f;
        score += self.Stats.Attack * 2.5f;
        score += self.Stats.Defense * 1.5f;
        score += self.Stats.Growth * 1.0f;
        score += self.Stats.Speed * 1.3f;
        score += self.Stats.Accuracy * 1.0f;
        score += self.Stats.Evasion * 1.0f;
        score += self.Stats.CriticalRate * 1.0f;

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
    /// 

    //余裕があったら改造しよう！

    private static float GetElementBonus(ElementType attacker, ElementType defender)
    {
        //水vs炎
        if (attacker == ElementType.Water && defender == ElementType.Fire) return 3.0f;
        //炎vs草
        if (attacker == ElementType.Fire && defender == ElementType.Grass) return 3.0f;
        //草vs水
        if (attacker == ElementType.Grass && defender == ElementType.Water) return 3.0f;

        //それ以外
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
                return 1.0f;
            case PersonalityType.Calm:
                return 1.0f;
            case PersonalityType.faith:
                return 2.0f;
            case PersonalityType.Taunt:
                return 1.0f;
            case PersonalityType.Aggressive:
                return 5.0f;
            case PersonalityType.Timid:
                return 1.4f;
            default:
                return 1.0f;
        }
    }
}
