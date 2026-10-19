using UnityEngine;

/// <summary>
/// モンスターの強さをざっくり評価して、賭け倍率を決めるクラス
/// パラメータが低いほどオッズが上がる仕組み
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
        //　倍率 = 相手のスコア/選択したスライムのスコア(1未満にならないように設定)
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
        score += self.Stats.MaxHp * 0.2f;
        score += self.Stats.Attack * 1.1f;
        score += self.Stats.Defense * 0.9f;
        score += self.Stats.Growth * 0.8f;
        score += self.Stats.Speed * 1.2f;
        score += self.Stats.Accuracy * 0.7f;
        score += self.Stats.Evasion * 0.9f;
        score += self.Stats.CriticalRate * 1.3f;

        // 属性相性ボーナス
        float elementBonus = GetElementBonus(self.Element, opponent.Element);
        score *= elementBonus;

        // 性格の強さを見積もる賭け倍率用の係数。実戦中の性格効果そのものは変更しない。
        score *= GetPersonalityBonus(self.Personality);
        if (self.Personality == PersonalityType.Adversity && ElementCalculator.IsDisadvantaged(self.Element, opponent.Element))
            score *= 2f;

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
        // 戦闘と同じ相性表で、与ダメージと被ダメージの比を評価する。
        return Mathf.Sqrt(ElementCalculator.GetAttackMultiplier(attacker, defender)
            / ElementCalculator.GetAttackMultiplier(defender, attacker));
    }

    /// <summary>
    /// オッズ計算向けに性格ごとの強さを簡易評価する係数を返す。
    /// 戦闘ルールとは別の見積もり値なので、試合中の能力倍率には使わない。
    /// </summary>
    private static float GetPersonalityBonus(PersonalityType personality)
    {
        switch (personality)
        {
            case PersonalityType.Bold: // 大胆：威力の高い技を優先するため、オッズ上の評価を高める。
                return 1.5f;
            case PersonalityType.Calm: // 冷静：序盤の防御・回復を考慮し、少し高めに評価する。
                return 1.2f;
            case PersonalityType.faith: // 信仰：相手の技に左右されるため、低めに評価する。
                return 0.5f;
            case PersonalityType.Taunt: // 挑発：標準の強さとして扱う。
                return 1.0f;
            case PersonalityType.Aggressive: // 挑戦：成功率の低い技を優先するため、かなり低く評価する。
                return 0.1f;
            case PersonalityType.Timid: // 臆病：回復などを考慮し、やや低めに評価する。
                return 0.8f;
            case PersonalityType.Delusional: // 中二：効果のない技を使うため、最も低く評価する。
                return 0.000012f;
            case PersonalityType.Inept: // 無能：覚醒前の戦いにくさを考慮し、低めに評価する。
                return 0.2f;
            case PersonalityType.Stoic: // 克己：被弾するほど能力が上がるため、高めに評価する。
                return 1.25f;
            case PersonalityType.Explorer: // 探求：発掘効果にばらつきがあるため、少し低めに評価する。
                return 0.85f;
            case PersonalityType.Devoted: // 献身：大ダメージを狙えるがHP1になるため、少し高めに評価する。
                return 1.5f;
            case PersonalityType.Adversity: // 逆境：相性で強化される場合を含め、標準評価にする。
                return 1f;
            case PersonalityType.Unyielding: // 不屈：致死攻撃を耐えるため、高めに評価する。
                return 1.3f;
            case PersonalityType.Impatient: // 短気：攻撃する確率が限られるため、低めに評価する。
                return 0.65f;
            case PersonalityType.Headlong: // 猛進：攻撃・妨害・防御を切り替えるため、少し高めに評価する。
                return 1.1f;
            case PersonalityType.Obsessive: // 執念：状態異常中などの攻撃力上昇を考慮し、少し高めに評価する。
                return 1.1f;
            case PersonalityType.Sincere: // 誠実：強化後に魔法を使うため、少し低めに評価する。
                return 0.9f;
            case PersonalityType.Insidious: // 陰湿：弱体化を重ねる効果を考慮し、高めに評価する。
                return 1.15f;
            case PersonalityType.Adaptive: // 適応：スタンの成功率低下と物理・魔法・毒ダメージ軽減を考慮し、高めに評価する。
                return 1.2f;
            case PersonalityType.Flexible: // 柔軟：弱めのコピー技とコピー制限を考慮し、標準評価にする。
                return 1f;
            default: // 未定義の性格は基準値で扱う。
                return 1.0f;
        }
    }
}
