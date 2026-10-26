using UnityEngine;

// 技種別とは別にダメージの発生元を識別する。天罰・奇跡の直接攻撃・長期戦イベントは適応対象外。
public enum DamageSource
{
    Attack, Explosion, Recoil, Punishment, Miracle, TurnEvent
}

/// <summary>技選択以外の性格効果。強化・耐性・致死ダメージを共通の入口で処理する。</summary>
// 呼び出し順：試合開始時Reset→Refresh、各ターン開始時BeginTurn、技や状態変化後Refresh。
// ダメージ時はTakeDamageでHPを更新し、物理・魔法の命中後はOnAttackHitで次回の耐性を増やす。
// 適応はダメージを受けた「後」に成長するので、今回獲得した耐性は今回の一撃には遡って適用しない。
public static class PersonalityRules
{
    // 性格効果の共通パラメーター。バランス調整時は関連する耐性・成長処理も確認する。
    public const int AwakeningTurn = 10; // 無能が覚醒する戦闘全体のターン番号。個人の行動回数ではない。
    public const float StoicGrowthPerHit = 0.1f; // 克己の1回分は基本攻撃・防御・素早さの10%。現在値を毎回1.1倍する方式ではない。
    public const float AdversityStatMultiplier = 1.2f; // 逆境は基本攻撃・防御・素早さだけを強化する。
    public const int AdversityGrowthRate = 100;
    // 適応は種類別に蓄積する。数値は百分率の「ポイント」で、いずれも上限100。
    public const int NormalAttackAdaptation = 5; // 通常攻撃で実際にHPが減ると、物理軽減率を5ポイント増やす。
    public const int StrongAttackAdaptation = 10; // 強撃・絡め手の追加攻撃では物理軽減率を10ポイント増やす。
    public const int MagicHitAdaptation = 10; // 等倍相性では魔法軽減率を10ポイント増やす。
    public const int FavorableMagicHitAdaptation = 20; // 受ける側に有利な魔法なら20ポイント。
    public const int UnfavorableMagicHitAdaptation = 5; // 受ける側の弱点を突く魔法なら5ポイント。
    public const int AdditionalDamageAdaptation = 10; // 爆破・反動はそれぞれ独立して10ポイント増加。
    public const int PoisonHitAdaptation = 10; // 毒でHPが減ると、毒専用の軽減率を10ポイント増やす。
    public const int StunAppliedAdaptation = 10; // スタンが付与されると、次回の付与成功率を下げる値を10ポイント増やす。
    public const int AdaptationPerTurn = 1; // 一度受けた種類だけ、次のターンから毎ターン1ポイント増える。

    /// <summary>前の試合の一時状態を消し、モンスターを基本ステータスの状態に戻す。</summary>
    public static void Reset(MonsterData monster)
    {
        monster.PersonalityMultiplier = 1f; // 前の試合の覚醒による全能力倍率を戻す。
        monster.AdversityActive = false;
        monster.StoicStatMultiplier = 1f; // 克己の攻撃・防御・素早さの倍率を戻す。
        monster.PersonalityAttackMultiplier = 1f; // 執念の攻撃専用倍率を戻す。
        monster.ObsessionStartTurn = monster.ObsessionStunRecoveryTurn = 0; // 状態異常とスタン由来の強化予約を両方消す。
        monster.CurrentHp = monster.Stats.MaxHp; // 基本最大HPまで回復する。
        monster.AttackBuff = monster.DefenseBuff = monster.CriticalBuff = 0; // 加算バフと防御デバフを解除する。
        monster.GrowthAttackBonus = monster.GrowthDefenseBonus = monster.GrowthSpeedBonus = 0;
        monster.GrowthAccuracyBonus = monster.GrowthEvasionBonus = 0; // ターン成長も試合ごとにリセットする。
        monster.StatusAilment = StatusAilmentType.None; // 毒・スタンなどの状態異常を解除する。
        monster.IsSkillLocked = monster.IsInvincible = false; // 封印・無敵を解除する。
        // monster.IsStunnedThisTurn = false; // 未使用欄の初期化も休止する。
        // monster.IsPoisoned = false;
        monster.LastUsedSkill = null; // 柔軟用のコピー元履歴を消す。
        monster.LastUsedSkillType = SkillType.None; // 信仰用の種別履歴も「未行動」に戻す。
        monster.BattleTurn = monster.DamageTakenCount = monster.EndureCount = 0; // 経過ターン・被弾回数・踏みとどまり回数を初期化する。
        monster.PatienceWaitCount = 0;
        monster.PhysicalAdaptation = monster.MagicAdaptation = 0; // 物理・魔法の軽減率を0に戻す。
        monster.ExplosionAdaptation = monster.RecoilAdaptation = 0;
        monster.PoisonAdaptation = monster.StunAdaptation = 0; // 毒・スタンへの耐性を0に戻す。
        monster.Awakened = monster.DebuffRejected = false; // 覚醒済みと、相手の弱体無効を確認した記録を消す。
        // DebuffImmuneは固有の耐性設定なので維持する。
    }

    /// <summary>スタンで行動を休んだとき、執念の攻撃強化を次のターンに予約する。</summary>
    public static void OnStunRecovered(MonsterData monster)
    {
        if (monster.Personality == PersonalityType.Obsessive) // 執念以外では予約を作らない。
            monster.ObsessionStunRecoveryTurn = monster.BattleTurn + 1; // スタンで行動を休んだ次の戦闘ターンだけ強化する。
    }

    /// <summary>ターン開始時の性格効果を反映し、必要なら覚醒や倍率変化をログに記録する。</summary>
    public static void BeginTurn(MonsterData monster, MonsterData opponent, int turn,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 同じターンを再評価しても二重に増やさない。前回から経過したターン数だけ進める。
        int elapsedTurns = Mathf.Max(0, turn - monster.BattleTurn); // 保存済みターンとの差。初期状態の0→1は1、同ターン再呼び出しなら0。
        AdvanceAdaptation(monster, elapsedTurns, logger, left, right); // すでに経験した攻撃種別の耐性だけを経過分成長させる。
        monster.BattleTurn = turn; // 執念の予約判定や、次回の経過ターン計算の基準を更新する。
        if (monster.Personality == PersonalityType.Inept && turn >= AwakeningTurn && !monster.Awakened) // 10ターン目以降、まだ覚醒していない無能だけ。
        {
            // 初回だけ覚醒させ、倍率を更新してから新しい最大HPまで回復する。
            monster.Awakened = true; // 先にフラグを立てることで、Refreshが4倍の能力倍率を返す。
            Refresh(monster, opponent, logger, left, right); // 最大HPなどの能力倍率を更新する。
            monster.CurrentHp = monster.GetCurrentMaxHp(); // 更新後の最大HPで全回復する。順序を逆にすると回復量が変わる。
            logger.Add($"{monster.Name} の性格は勇敢に転生した！全能力4倍、HP全回復！", left, right);
        }
        Refresh(monster, opponent, logger, left, right); // 覚醒以外の性格も毎ターン再評価する。倍率が同じなら重複ログを出さない。
    }

    /// <summary>性格の対象能力別倍率と全能力倍率を更新する。全能力倍率を変える場合はHP割合を保つ。</summary>
    public static void Refresh(MonsterData monster, MonsterData opponent,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 克己は攻撃・防御・素早さ、執念は攻撃だけを更新する。全能力倍率が同じでも再評価する。
        RefreshStoic(monster, logger, left, right);
        RefreshAdversity(monster, opponent, logger, left, right);
        RefreshObsession(monster, logger, left, right); // 全能力倍率の比較より先に、攻撃専用倍率の開始・終了を判定する。
        float multiplier = GetMultiplier(monster); // 覚醒による全能力倍率を計算する。
        if (Mathf.Approximately(multiplier, monster.PersonalityMultiplier)) return; // 全能力倍率に変化がなければHP換算は不要。
        int previousMax = monster.GetCurrentMaxHp(); // 倍率変更前の最大HPを保存する。HP割合の分母として使う。
        monster.PersonalityMultiplier = multiplier; // 以降のGetCurrent...は新しい倍率を使う。
        // 倍率の切り替えでHP割合を保つ。瀕死・死亡を強化によって取り消さない。
        if (monster.CurrentHp > 0) // 死亡している場合はHP0を維持する。
            monster.CurrentHp = Mathf.Clamp(Mathf.FloorToInt((float)monster.CurrentHp / previousMax * monster.GetCurrentMaxHp()), 1, monster.GetCurrentMaxHp()); // 旧HP割合×新最大HPを切り捨て、生存中は最低1にする。
        logger.Add($"{monster.Name} の性格効果：能力倍率 {multiplier:0.0}倍！", left, right);
    }

    // 能力倍率が状態に応じて変わる性格だけをここで評価する。
    private static float GetMultiplier(MonsterData monster)
    {
        switch (monster.Personality) // 毎回基礎状態から計算し直す。Refreshを複数回呼んでも倍率を累乗しない。
        {
            case PersonalityType.Inept: return monster.Awakened ? 4f : 1f; // 無能は覚醒済みなら4倍、覚醒前なら等倍。
            default: return 1f; // その他の性格には全能力倍率を付けない。克己・執念は対象能力別の倍率。
        }
    }

    // 戦闘とオッズで同じ発動条件を使う。
    public static bool ShouldActivateAdversity(MonsterData monster, MonsterData opponent)
        => monster.Personality == PersonalityType.Adversity
            && ElementCalculator.IsDisadvantaged(monster.Element, opponent.Element);

    private static void RefreshAdversity(MonsterData monster, MonsterData opponent,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        bool active = ShouldActivateAdversity(monster, opponent);
        if (monster.AdversityActive == active) return;
        monster.AdversityActive = active;
        logger.Add(active
            ? $"{monster.Name} の逆境が発動！攻撃・防御・素早さ{AdversityStatMultiplier:0.0}倍、成長率{AdversityGrowthRate}%！"
            : $"{monster.Name} の逆境による強化が終了した。", left, right);
    }

    // 克己は攻撃・防御・素早さを1.0→1.1→1.2倍と加算する。HPと他の能力は変更しない。
    private static void RefreshStoic(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Stoic) return;
        float statMultiplier = 1f + monster.DamageTakenCount * StoicGrowthPerHit;
        if (Mathf.Approximately(statMultiplier, monster.StoicStatMultiplier)) return;
        monster.StoicStatMultiplier = statMultiplier;
        logger.Add("ダメージを乗り越えて強くなった！", left, right);
    }

    // 毒・封印は次のターンから状態異常が続く間、スタンは行動を休んだ次の1ターンだけ強化する。
    private static void RefreshObsession(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Obsessive) return; // 執念以外は状態異常による攻撃強化を行わない。

        bool hasStatus = monster.IsSkillLocked // 封印は専用フラグでも確認する。
            || (monster.StatusAilment != StatusAilmentType.None && monster.StatusAilment != StatusAilmentType.Stun); // スタン以外の状態異常を継続条件にする。
        if (hasStatus) // 毒・封印などの継続状態がある場合。
        {
            // 再付与や被弾のたびに開始ターンを延ばさない。最初に受けたときだけ予約する。
            if (monster.ObsessionStartTurn == 0) // まだ予約がないときだけ開始ターンを記録する。
                monster.ObsessionStartTurn = monster.BattleTurn + 1; // 付与されたそのターンには発動させない。
        }
        else monster.ObsessionStartTurn = 0; // 状態異常がなくなったら予約も消す。次の付与は改めて翌ターンから。

        bool statusActive = hasStatus && monster.BattleTurn >= monster.ObsessionStartTurn; // 状態異常が継続し、予約した開始ターンに達している。
        bool afterStun = monster.ObsessionStunRecoveryTurn > 0 // スタンからの反撃予約があることを確認する。
            && monster.BattleTurn == monster.ObsessionStunRecoveryTurn; // スタン側の強化は予約した1ターンだけ。
        float attackMultiplier = statusActive || afterStun ? 2f : 1f; // どちらか成立で2倍。両方成立しても重複して4倍にはしない。
        if (Mathf.Approximately(attackMultiplier, monster.PersonalityAttackMultiplier)) return; // 同じ倍率なら更新とログを省略する。

        // HPと最大HPには触れず、攻撃力の倍率だけ切り替える。両条件を満たしても4倍にはしない。
        monster.PersonalityAttackMultiplier = attackMultiplier; // MonsterData.GetCurrentAttackだけがこの倍率を使用する。
        logger.Add(attackMultiplier > 1f
            ? $"{monster.Name} は執念で攻撃力2倍！"
            : $"{monster.Name} の執念による攻撃強化が終了した。", left, right);
    }

    // 経験済みの種類だけ進め、ターン経過による解析のログを出す。
    private static void AdvanceAdaptation(MonsterData monster, int elapsedTurns,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive || monster.CurrentHp <= 0 || elapsedTurns <= 0) return;
        int[] adaptationsBefore = GetAdaptations(monster);
        int increase = elapsedTurns * AdaptationPerTurn;
        if (monster.PhysicalAdaptation > 0) monster.PhysicalAdaptation = Mathf.Min(100, monster.PhysicalAdaptation + increase);
        if (monster.MagicAdaptation > 0) monster.MagicAdaptation = Mathf.Min(100, monster.MagicAdaptation + increase);
        if (monster.PoisonAdaptation > 0) monster.PoisonAdaptation = Mathf.Min(100, monster.PoisonAdaptation + increase);
        if (monster.StunAdaptation > 0) monster.StunAdaptation = Mathf.Min(100, monster.StunAdaptation + increase);
        if (monster.ExplosionAdaptation > 0) monster.ExplosionAdaptation = Mathf.Min(100, monster.ExplosionAdaptation + increase);
        if (monster.RecoilAdaptation > 0) monster.RecoilAdaptation = Mathf.Min(100, monster.RecoilAdaptation + increase);
        LogAdaptationChanges(monster, adaptationsBefore, logger, left, right, isTurnProgress: true);
    }

    // 下の種類名と同じ順序で、増加前後の耐性値を保存する。
    private static int[] GetAdaptations(MonsterData monster) => new[]
    {
        monster.PhysicalAdaptation, monster.MagicAdaptation, monster.PoisonAdaptation,
        monster.StunAdaptation, monster.ExplosionAdaptation, monster.RecoilAdaptation
    };

    private static readonly string[] AdaptationNames =
    {
        "物理攻撃", "魔法攻撃", "毒", "スタン", "爆破", "反動"
    };

    // 増加した種類だけ、開始・解析・被弾による進行・完了を通知する。
    // 一度の被弾で100％になる場合は完了を優先し、開始や進捗を重複させない。
    private static void LogAdaptationChanges(MonsterData monster, int[] adaptationsBefore,
        BattleLogger logger, MonsterData left, MonsterData right, bool isTurnProgress = false)
    {
        int[] adaptationsAfter = GetAdaptations(monster);
        for (int i = 0; i < AdaptationNames.Length; i++)
        {
            if (adaptationsAfter[i] <= adaptationsBefore[i]) continue;
            string category = AdaptationNames[i];
            string message;
            if (adaptationsAfter[i] >= 100)
                message = $"{category}の適応が完了した！次からの{category}は無効化される！";
            else if (adaptationsBefore[i] == 0)
                message = $"{category}の適応が始まった...";
            else if (isTurnProgress)
                message = $"{category}を解析している。。";
            else
                message = $"{category}の適応がすすんだ！";
            logger.Add(message, left, right);
        }
    }

    /// <summary>今回の技の成功率から差し引くポイント数。スタンだけに適用する。</summary>
    public static int GetSuccessRateReduction(MonsterData monster, SkillType skillType)
    {
        if (monster.Personality != PersonalityType.Adaptive) return 0; // 適応以外は成功率を下げない。
        switch (skillType)
        {
            case SkillType.StunDebuff:
                return monster.StunAdaptation; // 20なら成功率から20ポイントを引く。元の成功率に0.8を掛ける方式ではない。
            default:
                return 0; // 回復・強化・毒付与・即死などの成功率には適用しない。
        }
    }

    /// <summary>実際に命中してダメージを受けた後、該当する攻撃の耐性だけを増やす。</summary>
    public static void OnAttackHit(MonsterData monster, SkillType skillType, ElementType attackElement,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive || monster.CurrentHp <= 0) return; // 適応以外・戦闘不能なら今回の被弾から耐性を獲得しない。
        int[] adaptationsBefore = GetAdaptations(monster);
        switch (skillType)
        {
            case SkillType.NormalAttack:
                monster.PhysicalAdaptation = Mathf.Min(100, monster.PhysicalAdaptation + NormalAttackAdaptation); // 通常攻撃は物理耐性+5、上限100。
                break;
            case SkillType.StrongAttack:
            case SkillType.DebuffCombo:
                monster.PhysicalAdaptation = Mathf.Min(100, monster.PhysicalAdaptation + StrongAttackAdaptation); // 強撃・絡め手の追加攻撃は同じ物理耐性に+10。
                break;
            case SkillType.AllOutAttack:
                monster.PhysicalAdaptation = monster.MagicAdaptation = 100;
                break;
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
                // 技の属性→受ける側の属性で判定する。コピー元・使用者の属性は使わない。
                float incomingMultiplier = ElementCalculator.GetAttackMultiplier(attackElement, monster.Element);
                int increase = incomingMultiplier < 1f ? FavorableMagicHitAdaptation
                    : incomingMultiplier > 1f ? UnfavorableMagicHitAdaptation : MagicHitAdaptation;
                monster.MagicAdaptation = Mathf.Min(100, monster.MagicAdaptation + increase); // 全属性で共有、上限100。
                break;
        }
        LogAdaptationChanges(monster, adaptationsBefore, logger, left, right);
    }

    /// <summary>スタンが実際に付与された後に増やす。今回の付与を取り消さず、次回から効く。</summary>
    public static void OnStunApplied(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive || monster.CurrentHp <= 0) return; // 適応で生存中の対象だけを更新する。
        int[] adaptationsBefore = GetAdaptations(monster);
        monster.StunAdaptation = Mathf.Min(100, monster.StunAdaptation + StunAppliedAdaptation); // スタン付与後に+10。すでに付与した今回のスタンは消さない。
        LogAdaptationChanges(monster, adaptationsBefore, logger, left, right);
    }

    /// <summary>技の種類に応じたダメージ軽減率。物理と魔法は独立し、魔法は全属性で共有する。</summary>
    public static float GetAttackResistance(MonsterData monster, SkillType skillType)
    {
        if (monster.Personality != PersonalityType.Adaptive) return 0f; // 適応以外の軽減率は0。
        switch (skillType)
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
            case SkillType.AllOutAttack:
            case SkillType.DebuffCombo:
                return Mathf.Clamp01(monster.PhysicalAdaptation / 100f); // 物理耐性20ポイントを0.2へ変換し、0～1に収める。
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
                return Mathf.Clamp01(monster.MagicAdaptation / 100f); // 5属性魔法は同じ軽減率を使用する。
            default:
                return 0f; // 技種別がない爆破・反動はDamageSourceごとの耐性で扱う。
        }
    }

    // 既存の物理・魔法・毒に加え、爆破・反動を発生元ごとに軽減する。
    private static float GetDamageResistance(MonsterData monster, SkillType attackType, bool poison, DamageSource source)
    {
        if (monster.Personality != PersonalityType.Adaptive) return 0f;
        switch (source)
        {
            case DamageSource.Punishment:
            case DamageSource.Miracle:
            case DamageSource.TurnEvent: return 0f;
            case DamageSource.Explosion: return Mathf.Clamp01(monster.ExplosionAdaptation / 100f);
            case DamageSource.Recoil: return Mathf.Clamp01(monster.RecoilAdaptation / 100f);
            default: return poison ? GetPoisonResistance(monster) : GetAttackResistance(monster, attackType);
        }
    }

    // 実ダメージを受けて生存した後にだけ呼ぶ。今回増えた耐性は次回から有効。
    private static void GrowDamageAdaptation(MonsterData monster, bool poison, DamageSource source,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive) return;
        int[] adaptationsBefore = GetAdaptations(monster);
        switch (source)
        {
            case DamageSource.Explosion:
                monster.ExplosionAdaptation = Mathf.Min(100, monster.ExplosionAdaptation + AdditionalDamageAdaptation);
                break;
            case DamageSource.Recoil:
                monster.RecoilAdaptation = Mathf.Min(100, monster.RecoilAdaptation + AdditionalDamageAdaptation);
                break;
            case DamageSource.Attack:
                if (!poison) return; // 物理・魔法はOnAttackHitで増やす。
                monster.PoisonAdaptation = Mathf.Min(100, monster.PoisonAdaptation + PoisonHitAdaptation);
                break;
            default: return; // 天罰・奇跡・決着イベントは耐性を獲得しない。
        }
        LogAdaptationChanges(monster, adaptationsBefore, logger, left, right);
    }

    /// <summary>毒専用のダメージ軽減率。物理・魔法・スタンの耐性は混ぜない。</summary>
    public static float GetPoisonResistance(MonsterData monster)
    {
        return monster.Personality == PersonalityType.Adaptive ? monster.PoisonAdaptation / 100f : 0f; // 適応なら毒耐性を0～1の割合へ変換。通常の更新処理が上限100を守る。
    }

    /// <summary>
    /// 通常攻撃・毒・即死・反動・決着ダメージを共通処理する。
    /// 不屈の踏みとどまりと適応の種類別ダメージ軽減をここで処理する。
    /// 戻り値はこのHP減算で失った量。生存後の克己は攻撃・防御・素早さだけを更新する。
    /// 無敵・攻防・属性・会心はここでは確認しない。通常攻撃はSkillExecutor.ApplyDamageを経由する。
    /// </summary>
    // poison=trueは毒耐性、instantDeath=trueは軽減を無視した現在HP分のダメージ。
    // attackTypeは通常攻撃系、damageSourceは爆破・自傷・対象外の発生元を指定する。不屈・克己は共通で適用。
    public static int TakeDamage(MonsterData target, MonsterData opponent, int amount,
        BattleLogger logger, MonsterData left, MonsterData right, bool poison = false, bool instantDeath = false,
        SkillType attackType = SkillType.None, DamageSource damageSource = DamageSource.Attack)
    {
        // すでに戦闘不能の対象や、無効なダメージでは耐性・被弾回数を更新しない。
        if (target.CurrentHp <= 0 || amount <= 0) return 0; // 倒れている・入力ダメージがない場合は何も変更しない。
        // 物理・魔法は会心・コピー倍率を反映したダメージから軽減。スタンは成功判定で扱う。
        float resistance = GetDamageResistance(target, attackType, poison, damageSource); // 種類別の耐性は重ねない。
        int damage = instantDeath ? target.CurrentHp : Mathf.RoundToInt(amount * (1f - resistance)); // 即死は残HP全量、それ以外は耐性で軽減して丸める。
        if (!instantDeath && resistance < 1f) damage = Mathf.Max(1, damage); // 耐性100%未満は最低1、100%なら0まで軽減できる。
        if (damage <= 0) return 0; // 完全に防いだ場合は被弾回数・耐性を増やさない。
        int before = target.CurrentHp; // 減算前のHPを保存し、最後に実際の減少量を求める。
        bool lethal = damage >= before; // 今回のダメージだけでHPが0以下になるかを確認する。
        if (lethal && target.Personality == PersonalityType.Unyielding // 不屈の致死ダメージ時だけ踏みとどまり判定を行う。
            && (target.EndureCount == 0 || Random.value < 0.5f)) // 初回は抽選なしで成功。2回目以降は50%抽選。
        {
            // 不屈は最初の致死攻撃を必ず耐え、その後の致死攻撃は半分の確率で耐える。
            target.EndureCount++; // 耐えた回数を増やし、次回以降は初回保証を使わない。
            target.CurrentHp = 1; // 元からHP1でも耐えることができ、その場合の実HP減少は0。
            logger.Add($"{target.Name} は不屈の意志でHP1に踏みとどまった！", left, right);
        }
        else target.CurrentHp = Mathf.Max(0, before - damage); // 不屈が発動しない場合は通常の減算。HPを負数にしない。

        int actual = before - target.CurrentHp; // オーバーキル分を除き、ここまでで実際に失ったHPだけを計算する。
        if (actual > 0 && target.CurrentHp > 0) // ダメージを受けて生存した場合だけ、被弾後の性格効果を更新する。
        {
            target.DamageTakenCount++; // 克己の倍率の基になる回数。即死・反動などもこの入口で生存条件を満たせば数える。
            if (!instantDeath) // 即死は軽減・学習とも対象外。
                GrowDamageAdaptation(target, poison, damageSource, logger, left, right);
            Refresh(target, opponent, logger, left, right); // 克己の攻撃・防御・素早さの倍率、執念などを更新する。
        }
        return actual; // ログやOnAttackHitの実ダメージ有無の判定に使用する。
    }
}
