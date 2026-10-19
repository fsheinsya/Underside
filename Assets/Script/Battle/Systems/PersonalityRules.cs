using UnityEngine;

/// <summary>技選択以外の性格効果。強化・耐性・致死ダメージを共通の入口で処理する。</summary>
public static class PersonalityRules
{
    // 性格効果の共通パラメーター。バランス調整時は関連する耐性・成長処理も確認する。
    public const int AwakeningTurn = 10;
    public const float StoicGrowthPerHit = 0.1f;
    // 適応は種類別に蓄積する。数値は百分率の「ポイント」で、いずれも上限100。
    public const int NormalAttackAdaptation = 5;
    public const int StrongAttackAdaptation = 10;
    public const int MagicHitAdaptation = 20;
    public const int PoisonHitAdaptation = 10;
    public const int StunAppliedAdaptation = 10;
    public const int AdaptationPerTurn = 1; // 一度受けた種類だけ、次のターンから毎ターン1ポイント増える。

    /// <summary>前の試合の一時状態を消し、モンスターを基本ステータスの状態に戻す。</summary>
    public static void Reset(MonsterData monster)
    {
        monster.PersonalityMultiplier = 1f;
        monster.PersonalityAttackMultiplier = 1f;
        monster.ObsessionStartTurn = monster.ObsessionStunRecoveryTurn = 0;
        monster.CurrentHp = monster.Stats.MaxHp;
        monster.AttackBuff = monster.DefenseBuff = monster.CriticalBuff = 0;
        monster.StatusAilment = StatusAilmentType.None;
        monster.IsSkillLocked = monster.IsInvincible = monster.IsStunnedThisTurn = false;
        monster.IsPoisoned = false;
        monster.LastUsedSkill = null;
        monster.LastUsedSkillType = SkillType.None;
        monster.BattleTurn = monster.DamageTakenCount = monster.EndureCount = 0;
        monster.PhysicalAdaptation = monster.MagicAdaptation = 0;
        monster.PoisonAdaptation = monster.StunAdaptation = 0;
        monster.Awakened = monster.DebuffRejected = false;
        // DebuffImmuneは固有の耐性設定なので維持する。
    }

    /// <summary>スタンで行動を休んだとき、執念の攻撃強化を次のターンに予約する。</summary>
    public static void OnStunRecovered(MonsterData monster)
    {
        if (monster.Personality == PersonalityType.Obsessive)
            monster.ObsessionStunRecoveryTurn = monster.BattleTurn + 1;
    }

    /// <summary>ターン開始時の性格効果を反映し、必要なら覚醒や倍率変化をログに記録する。</summary>
    public static void BeginTurn(MonsterData monster, MonsterData opponent, int turn,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 同じターンを再評価しても二重に増やさない。前回から経過したターン数だけ進める。
        int elapsedTurns = Mathf.Max(0, turn - monster.BattleTurn);
        AdvanceAdaptation(monster, elapsedTurns, logger, left, right);
        monster.BattleTurn = turn;
        if (monster.Personality == PersonalityType.Inept && turn >= AwakeningTurn && !monster.Awakened)
        {
            // 初回だけ覚醒させ、倍率を更新してから新しい最大HPまで回復する。
            monster.Awakened = true;
            Refresh(monster, opponent, logger, left, right);
            monster.CurrentHp = monster.GetCurrentMaxHp();
            logger.Add($"{monster.Name} の性格は勇敢に転生した！全能力4倍、HP全回復！", left, right);
        }
        Refresh(monster, opponent, logger, left, right);
    }

    /// <summary>性格の攻撃専用倍率と全能力倍率を更新する。全能力倍率を変える場合はHP割合を保つ。</summary>
    public static void Refresh(MonsterData monster, MonsterData opponent,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        // 執念は攻撃力だけを更新する。全能力倍率が同じでも状態異常の変化は確認する。
        RefreshObsession(monster, logger, left, right);
        float multiplier = GetMultiplier(monster, opponent);
        if (Mathf.Approximately(multiplier, monster.PersonalityMultiplier)) return;
        int previousMax = monster.GetCurrentMaxHp();
        monster.PersonalityMultiplier = multiplier;
        // 倍率の切り替えでHP割合を保つ。瀕死・死亡を強化によって取り消さない。
        if (monster.CurrentHp > 0)
            monster.CurrentHp = Mathf.Clamp(Mathf.FloorToInt((float)monster.CurrentHp / previousMax * monster.GetCurrentMaxHp()), 1, monster.GetCurrentMaxHp());
        logger.Add($"{monster.Name} の性格効果：能力倍率 {multiplier:0.0}倍！", left, right);
    }

    // 能力倍率が状態に応じて変わる性格だけをここで評価する。
    private static float GetMultiplier(MonsterData monster, MonsterData opponent)
    {
        switch (monster.Personality)
        {
            case PersonalityType.Inept: return monster.Awakened ? 4f : 1f;
            case PersonalityType.Stoic: return 1f + monster.DamageTakenCount * StoicGrowthPerHit;
            case PersonalityType.Adversity: return ElementCalculator.IsDisadvantaged(monster.Element, opponent.Element) ? 2f : 1f;
            default: return 1f;
        }
    }

    // 毒・封印は次のターンから状態異常が続く間、スタンは行動を休んだ次の1ターンだけ強化する。
    private static void RefreshObsession(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Obsessive) return;

        bool hasStatus = monster.IsSkillLocked
            || (monster.StatusAilment != StatusAilmentType.None && monster.StatusAilment != StatusAilmentType.Stun);
        if (hasStatus)
        {
            // 再付与や被弾のたびに開始ターンを延ばさない。最初に受けたときだけ予約する。
            if (monster.ObsessionStartTurn == 0)
                monster.ObsessionStartTurn = monster.BattleTurn + 1;
        }
        else monster.ObsessionStartTurn = 0;

        bool statusActive = hasStatus && monster.BattleTurn >= monster.ObsessionStartTurn;
        bool afterStun = monster.ObsessionStunRecoveryTurn > 0
            && monster.BattleTurn == monster.ObsessionStunRecoveryTurn;
        float attackMultiplier = statusActive || afterStun ? 2f : 1f;
        if (Mathf.Approximately(attackMultiplier, monster.PersonalityAttackMultiplier)) return;

        // HPと最大HPには触れず、攻撃力の倍率だけ切り替える。両条件を満たしても4倍にはしない。
        monster.PersonalityAttackMultiplier = attackMultiplier;
        logger.Add(attackMultiplier > 1f
            ? $"{monster.Name} は執念で攻撃力2倍！"
            : $"{monster.Name} の執念による攻撃強化が終了した。", left, right);
    }

    // 耐性が0より大きければ、その試合ですでに経験した種類。未経験の種類は0のままにする。
    private static void AdvanceAdaptation(MonsterData monster, int elapsedTurns,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive || monster.CurrentHp <= 0 || elapsedTurns <= 0) return;
        int before = monster.PhysicalAdaptation + monster.MagicAdaptation + monster.PoisonAdaptation + monster.StunAdaptation;
        int increase = elapsedTurns * AdaptationPerTurn;
        if (monster.PhysicalAdaptation > 0) monster.PhysicalAdaptation = Mathf.Min(100, monster.PhysicalAdaptation + increase);
        if (monster.MagicAdaptation > 0) monster.MagicAdaptation = Mathf.Min(100, monster.MagicAdaptation + increase);
        if (monster.PoisonAdaptation > 0) monster.PoisonAdaptation = Mathf.Min(100, monster.PoisonAdaptation + increase);
        if (monster.StunAdaptation > 0) monster.StunAdaptation = Mathf.Min(100, monster.StunAdaptation + increase);
        int after = monster.PhysicalAdaptation + monster.MagicAdaptation + monster.PoisonAdaptation + monster.StunAdaptation;
        if (after > before)
            logger.Add($"{monster.Name} は経験した攻撃にさらに適応！物理{monster.PhysicalAdaptation}／魔法{monster.MagicAdaptation}／毒{monster.PoisonAdaptation}／スタン{monster.StunAdaptation}％", left, right);
    }

    /// <summary>今回の技の成功率から差し引くポイント数。スタンだけに適用する。</summary>
    public static int GetSuccessRateReduction(MonsterData monster, SkillType skillType)
    {
        if (monster.Personality != PersonalityType.Adaptive) return 0;
        switch (skillType)
        {
            case SkillType.StunDebuff:
                return monster.StunAdaptation;
            default:
                return 0; // 回復・強化・毒付与・即死などの成功率には適用しない。
        }
    }

    /// <summary>実際に命中してダメージを受けた後、該当する攻撃の耐性だけを増やす。</summary>
    public static void OnAttackHit(MonsterData monster, SkillType skillType,
        BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive || monster.CurrentHp <= 0) return;
        switch (skillType)
        {
            case SkillType.NormalAttack:
                monster.PhysicalAdaptation = Mathf.Min(100, monster.PhysicalAdaptation + NormalAttackAdaptation);
                logger.Add($"{monster.Name} は通常攻撃に適応！次回から物理ダメージを{monster.PhysicalAdaptation}％軽減！", left, right);
                break;
            case SkillType.StrongAttack:
            case SkillType.AllOutAttack:
                monster.PhysicalAdaptation = Mathf.Min(100, monster.PhysicalAdaptation + StrongAttackAdaptation);
                logger.Add($"{monster.Name} は強攻撃に適応！次回から物理ダメージを{monster.PhysicalAdaptation}％軽減！", left, right);
                break;
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
                monster.MagicAdaptation = Mathf.Min(100, monster.MagicAdaptation + MagicHitAdaptation);
                logger.Add($"{monster.Name} は魔法に適応！次回から魔法ダメージを{monster.MagicAdaptation}％軽減！", left, right);
                break;
        }
    }

    /// <summary>スタンが実際に付与された後に増やす。今回の付与を取り消さず、次回から効く。</summary>
    public static void OnStunApplied(MonsterData monster, BattleLogger logger, MonsterData left, MonsterData right)
    {
        if (monster.Personality != PersonalityType.Adaptive || monster.CurrentHp <= 0) return;
        monster.StunAdaptation = Mathf.Min(100, monster.StunAdaptation + StunAppliedAdaptation);
        logger.Add($"{monster.Name} はスタンに適応！スタンの成功率を{monster.StunAdaptation}ポイント低下！", left, right);
    }

    /// <summary>技の種類に応じたダメージ軽減率。物理と魔法は独立し、魔法は全属性で共有する。</summary>
    public static float GetAttackResistance(MonsterData monster, SkillType skillType)
    {
        if (monster.Personality != PersonalityType.Adaptive) return 0f;
        switch (skillType)
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
            case SkillType.AllOutAttack:
                return Mathf.Clamp01(monster.PhysicalAdaptation / 100f);
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
                return Mathf.Clamp01(monster.MagicAdaptation / 100f);
            default:
                return 0f;
        }
    }

    /// <summary>毒専用のダメージ軽減率。物理・魔法・スタンの耐性は混ぜない。</summary>
    public static float GetPoisonResistance(MonsterData monster)
    {
        return monster.Personality == PersonalityType.Adaptive ? monster.PoisonAdaptation / 100f : 0f;
    }

    /// <summary>
    /// 通常攻撃・毒・即死・反動・決着ダメージを共通処理する。
    /// 不屈の踏みとどまりと適応の物理・魔法・毒ダメージ軽減をここで処理する。
    /// </summary>
    public static int TakeDamage(MonsterData target, MonsterData opponent, int amount,
        BattleLogger logger, MonsterData left, MonsterData right, bool poison = false, bool instantDeath = false,
        SkillType attackType = SkillType.None)
    {
        // すでに戦闘不能の対象や、無効なダメージでは耐性・被弾回数を更新しない。
        if (target.CurrentHp <= 0 || amount <= 0) return 0;
        // 物理・魔法は会心・コピー倍率を反映したダメージから軽減。スタンは成功判定で扱う。
        float resistance = poison ? GetPoisonResistance(target) : GetAttackResistance(target, attackType);
        int damage = instantDeath ? target.CurrentHp : Mathf.RoundToInt(amount * (1f - resistance));
        if (!instantDeath && resistance < 1f) damage = Mathf.Max(1, damage);
        if (damage <= 0) return 0;
        int before = target.CurrentHp;
        bool lethal = damage >= before;
        if (lethal && target.Personality == PersonalityType.Unyielding
            && (target.EndureCount == 0 || Random.value < 0.5f))
        {
            // 不屈は最初の致死攻撃を必ず耐え、その後の致死攻撃は半分の確率で耐える。
            target.EndureCount++;
            target.CurrentHp = 1;
            logger.Add($"{target.Name} は不屈の意志でHP1に踏みとどまった！", left, right);
        }
        else target.CurrentHp = Mathf.Max(0, before - damage);

        int actual = before - target.CurrentHp;
        if (actual > 0 && target.CurrentHp > 0)
        {
            target.DamageTakenCount++;
            // 今回の毒ダメージを与えた後で耐性を増やす。次回は元の毒ダメージから10％分多く減らす。
            if (target.Personality == PersonalityType.Adaptive && poison && !instantDeath)
            {
                target.PoisonAdaptation = Mathf.Min(100, target.PoisonAdaptation + PoisonHitAdaptation);
                logger.Add($"{target.Name} は毒に適応！次回から毒ダメージを{target.PoisonAdaptation}％軽減！", left, right);
            }
            Refresh(target, opponent, logger, left, right);
        }
        return actual;
    }
}
