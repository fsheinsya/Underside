using UnityEngine;

/// <summary>20ターン目以降、ターン終了時に4種類のイベントを独立して抽選する。</summary>
// BattleSimulatorが左右の行動と毒の後に呼ぶ。AIが選ぶ技とは独立した戦場全体のイベント。
// 抽選順と効果順は異なる。4つとも先に抽選し、弱体→強化→配当→ダメージの順で適用する。
public static class BattleTurnEvents
{
    public const int StartTurn = 20; // このターンの終了時からイベントを抽選する。表示メッセージのタイミングとは別。
    public const float EventChance = 0.25f; // 各イベントがそれぞれ25%。4つのうち1つを必ず選ぶ方式ではない。
    public const int PayoutDrawCount = 100000; // 配当倍率の抽選を10万通りに分け、0.001%の777倍を表現する。
    private static readonly int[] Damages = { 5, 10, 30, 50 }; // ダメージイベントの固定値候補。各25%で1つ。

    /// <summary>効果の対象はイベントごとに左右から1体。賭け情報自体は変更しない。</summary>
    public static double Apply(MonsterData left, MonsterData right, BattleLogger logger,
        double payoutMultiplier, bool hasBet)
    {
        if (left.CurrentHp <= 0 || right.CurrentHp <= 0) return payoutMultiplier; // イベント開始前に決着済みなら、倍率を変えず終了する。

        // else-ifでまとめず4回抽選する。効果内の抽選や致死ダメージで他の当選を取り消さない。
        bool damage = Random.value < EventChance; // ダメージの当選を記録する。まだ効果は適用しない。
        bool debuff = Random.value < EventChance; // 弱体の当選を別に抽選する。
        bool buff = Random.value < EventChance; // 強化も独立に抽選する。同じターンに弱体と両方当たることがある。
        bool payout = Random.value < EventChance; // 配当も独立に抽選する。賭けなしの場合でもこの乱数は消費する。

        if (debuff) ApplyDebuff(PickTarget(left, right), left, right, logger); // 当選時だけ対象を選ぶ。今回の毒処理は済んでいるので新しい毒は次ターン以降に効く。
        if (buff) ApplyBuff(PickTarget(left, right), left, right, logger); // 弱体とは別に対象を選ぶので、同じ側・違う側のどちらにもなり得る。
        if (payout && hasBet) // 配当当選かつ賭け情報があるときだけ、倍率を変更する。
        {
            double factor = GetPayoutFactor(Random.Range(0, PayoutDrawCount)); // 0～99999の整数を1つ引き、対応する上昇倍率へ変換する。
            payoutMultiplier *= factor; // 現在の倍率へ乗算する。元のBetDataのオッズや賭け金は変更しない。
            logger.Add($"配当イベント！勝利時の配当が{factor:0.###}倍に！現在の配当倍率は{payoutMultiplier:0.###}倍！", left, right);
        }
        // ダメージは最後に処理し、この一撃で決着しても同時当選した配当上昇を有効にする。
        if (damage) // 同時当選した他の効果が済んでから固定ダメージを適用する。
        {
            MonsterData target = PickTarget(left, right); // ダメージ対象も独立して左右から選ぶ。
            MonsterData opponent = target == left ? right : left; // 性格倍率の再評価などで必要な相手を特定する。
            int amount = Damages[Random.Range(0, Damages.Length)]; // 固定値候補から1つだけ等確率で選ぶ。
            int actual = PersonalityRules.TakeDamage(target, opponent, amount, logger, left, right, damageSource: DamageSource.TurnEvent); // 決着を促すダメージは全適応の対象外。不屈・克己は適用する。
            logger.Add($"ダメージイベント！{target.Name} に {actual} ダメージ！", left, right);
        }
        return payoutMultiplier; // 呼び出し元が保持する配当倍率を更新できるよう返す。
    }

    /// <summary>10万通りの等確率な抽選値を倍率に変換する。777倍は1通り（0.001%）。</summary>
    public static double GetPayoutFactor(int draw)
    {
        if (draw < 0 || draw >= PayoutDrawCount) // 抽選値は0～99999だけを受け付ける。
            throw new System.ArgumentOutOfRangeException(nameof(draw)); // 範囲外を誤った倍率として扱わず、呼び出し元の誤りを知らせる。
        if (draw < 50000) return 1.1; // 50%
        if (draw < 80000) return 1.5; // 30%
        if (draw < 95000) return 2.0; // 15%
        if (draw < 99999) return 3.0; // 4.999%
        return 777.0; // 0.001%
    }

    private static MonsterData PickTarget(MonsterData left, MonsterData right)
    {
        return Random.Range(0, 2) == 0 ? left : right; // 整数0なら左、1なら右。それぞれ50%。
    }

    private static void ApplyBuff(MonsterData target, MonsterData left, MonsterData right, BattleLogger logger)
    {
        // 1回の当選で、既存の強化技と同じ効果を1つだけ付与する。
        switch (Random.Range(0, 3)) // 攻撃・防御・会心から各1/3で選ぶ。
        {
            case 0:
                target.AttackBuff += 5; // 攻撃補正を5加算。試合中は重ね掛けされる。
                logger.Add($"バフイベント！{target.Name} の攻撃力+5！", left, right);
                break;
            case 1:
                target.DefenseBuff += 5; // 防御補正を5加算。負の補正があればその分を相殺する。
                logger.Add($"バフイベント！{target.Name} の防御力+5！", left, right);
                break;
            case 2:
                target.CriticalBuff += 20; // 会心率に20ポイント加算。最終率の上限はMonsterData側で適用する。
                logger.Add($"バフイベント！{target.Name} の会心率+20！", left, right);
                break;
        }
    }

    private static void ApplyDebuff(MonsterData target, MonsterData left, MonsterData right, BattleLogger logger)
    {
        // 毒・スタン・封印・防御低下から1つだけ抽選。無効になっても別の効果へ引き直さない。
        int effect = Random.Range(0, 4); // 0=毒、1=スタン、2=封印、3=防御低下。
        if (target.DebuffImmune || (effect == 0 && PersonalityRules.GetPoisonResistance(target) >= 1f)) // 固有弱体無効、または毒を選んだときの毒耐性100%。
        {
            logger.Add($"デバフイベント！{target.Name} にはデバフが効かなかった！", left, right);
            return; // 無効だった場合も他の弱体へ引き直さない。
        }

        switch (effect)
        {
            case 0:
                target.StatusAilment = StatusAilmentType.Poison; // 毒を付与。既存のスタンは上書きし、封印フラグは残す。
                logger.AddStatusApplied($"デバフイベント！{target.Name} は毒状態！", target, left, right);
                break;
            case 1:
                int resistance = PersonalityRules.GetSuccessRateReduction(target, SkillType.StunDebuff); // 適応が持つスタン拒否率のポイント数を読む。
                if (resistance >= 100 || Random.value * 100f < resistance) // 耐性100なら必ず拒否。それ以外は耐性分の確率で拒否する。
                {
                    logger.Add($"デバフイベント！{target.Name} はスタンを防いだ！", left, right);
                    return; // スタンを防いだ場合は付与後の耐性増加も行わない。
                }
                target.StatusAilment = StatusAilmentType.Stun; // 次の行動機会で1回休む。既存の毒は上書きされる。
                logger.AddStatusApplied($"デバフイベント！{target.Name} はスタン！（行動不能）", target, left, right);
                PersonalityRules.OnStunApplied(target, logger, left, right); // 今回付与できた後で、次回のスタン耐性を増やす。
                break;
            case 2:
                target.IsSkillLocked = true; // 通常攻撃・待機以外を止める封印フラグを設定する。
                // 毒・スタンと封印は共存できるため、すでにある状態は消さない。
                if (target.StatusAilment == StatusAilmentType.None) // すでに毒・スタンがあれば状態欄は維持する。
                    target.StatusAilment = StatusAilmentType.SkillSeal; // 状態欄が空のときだけ封印を記録する。
                logger.AddStatusApplied($"デバフイベント！{target.Name} はスキル封印！", target, left, right);
                break;
            case 3:
                target.DefenseBuff -= 5; // 防御補正を5減らす。最終的な防御値の下限0はMonsterDataが守る。
                logger.Add($"デバフイベント！{target.Name} の防御力-5！", left, right);
                break;
        }
        // 執念の次ターン強化など、状態異常に連動する性格効果へ通知する。
        PersonalityRules.Refresh(target, target == left ? right : left, logger, left, right); // 対象と相手を渡し、執念の開始予約などを更新する。
    }
}
