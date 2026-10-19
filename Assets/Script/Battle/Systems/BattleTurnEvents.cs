using UnityEngine;

/// <summary>20ターン目以降、ターン終了時に4種類のイベントを独立して抽選する。</summary>
public static class BattleTurnEvents
{
    public const int StartTurn = 20;
    public const float EventChance = 0.25f;
    public const int PayoutDrawCount = 100000;
    private static readonly int[] Damages = { 5, 10, 30, 50 };

    /// <summary>効果の対象はイベントごとに左右から1体。賭け情報自体は変更しない。</summary>
    public static double Apply(MonsterData left, MonsterData right, BattleLogger logger,
        double payoutMultiplier, bool hasBet)
    {
        if (left.CurrentHp <= 0 || right.CurrentHp <= 0) return payoutMultiplier;

        // else-ifでまとめず4回抽選する。効果内の抽選や致死ダメージで他の当選を取り消さない。
        bool damage = Random.value < EventChance;
        bool debuff = Random.value < EventChance;
        bool buff = Random.value < EventChance;
        bool payout = Random.value < EventChance;

        if (debuff) ApplyDebuff(PickTarget(left, right), left, right, logger);
        if (buff) ApplyBuff(PickTarget(left, right), left, right, logger);
        if (payout && hasBet)
        {
            double factor = GetPayoutFactor(Random.Range(0, PayoutDrawCount));
            payoutMultiplier *= factor;
            logger.Add($"配当イベント！勝利時の配当が{factor:0.###}倍に！現在の配当倍率は{payoutMultiplier:0.###}倍！", left, right);
        }
        // ダメージは最後に処理し、この一撃で決着しても同時当選した配当上昇を有効にする。
        if (damage)
        {
            MonsterData target = PickTarget(left, right);
            MonsterData opponent = target == left ? right : left;
            int amount = Damages[Random.Range(0, Damages.Length)];
            int actual = PersonalityRules.TakeDamage(target, opponent, amount, logger, left, right);
            logger.Add($"ダメージイベント！{target.Name} に {actual} ダメージ！", left, right);
        }
        return payoutMultiplier;
    }

    /// <summary>10万通りの等確率な抽選値を倍率に変換する。777倍は1通り（0.001%）。</summary>
    public static double GetPayoutFactor(int draw)
    {
        if (draw < 0 || draw >= PayoutDrawCount)
            throw new System.ArgumentOutOfRangeException(nameof(draw));
        if (draw < 50000) return 1.1; // 50%
        if (draw < 80000) return 1.5; // 30%
        if (draw < 95000) return 2.0; // 15%
        if (draw < 99999) return 3.0; // 4.999%
        return 777.0; // 0.001%
    }

    private static MonsterData PickTarget(MonsterData left, MonsterData right)
    {
        return Random.Range(0, 2) == 0 ? left : right;
    }

    private static void ApplyBuff(MonsterData target, MonsterData left, MonsterData right, BattleLogger logger)
    {
        // 1回の当選で、既存の強化技と同じ効果を1つだけ付与する。
        switch (Random.Range(0, 3))
        {
            case 0:
                target.AttackBuff += 5;
                logger.Add($"バフイベント！{target.Name} の攻撃力+5！", left, right);
                break;
            case 1:
                target.DefenseBuff += 5;
                logger.Add($"バフイベント！{target.Name} の防御力+5！", left, right);
                break;
            case 2:
                target.CriticalBuff += 20;
                logger.Add($"バフイベント！{target.Name} の会心率+20！", left, right);
                break;
        }
    }

    private static void ApplyDebuff(MonsterData target, MonsterData left, MonsterData right, BattleLogger logger)
    {
        // 毒・スタン・封印・防御低下から1つだけ抽選。無効になっても別の効果へ引き直さない。
        int effect = Random.Range(0, 4);
        if (target.DebuffImmune || (effect == 0 && PersonalityRules.GetPoisonResistance(target) >= 1f))
        {
            logger.Add($"デバフイベント！{target.Name} にはデバフが効かなかった！", left, right);
            return;
        }

        switch (effect)
        {
            case 0:
                target.StatusAilment = StatusAilmentType.Poison;
                logger.AddStatusApplied($"デバフイベント！{target.Name} は毒状態！", target, left, right);
                break;
            case 1:
                int resistance = PersonalityRules.GetSuccessRateReduction(target, SkillType.StunDebuff);
                if (resistance >= 100 || Random.value * 100f < resistance)
                {
                    logger.Add($"デバフイベント！{target.Name} は適応でスタンを防いだ！", left, right);
                    return;
                }
                target.StatusAilment = StatusAilmentType.Stun;
                logger.AddStatusApplied($"デバフイベント！{target.Name} はスタン！（行動不能）", target, left, right);
                PersonalityRules.OnStunApplied(target, logger, left, right);
                break;
            case 2:
                target.IsSkillLocked = true;
                // 毒・スタンと封印は共存できるため、すでにある状態は消さない。
                if (target.StatusAilment == StatusAilmentType.None)
                    target.StatusAilment = StatusAilmentType.SkillSeal;
                logger.AddStatusApplied($"デバフイベント！{target.Name} はスキル封印！", target, left, right);
                break;
            case 3:
                target.DefenseBuff -= 5;
                logger.Add($"デバフイベント！{target.Name} の防御力-5！", left, right);
                break;
        }
        // 執念の次ターン強化など、状態異常に連動する性格効果へ通知する。
        PersonalityRules.Refresh(target, target == left ? right : left, logger, left, right);
    }
}
