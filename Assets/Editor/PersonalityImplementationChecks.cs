using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>Unityの実際の乱数・計算処理を使う回帰チェック。Toolsメニューまたはbatchmodeで実行。</summary>
public static class PersonalityImplementationChecks
{
    private static int assertions;
    private static readonly ActionAI Ai = new ActionAI();
    private static readonly SkillExecutor Executor = new SkillExecutor();

    [MenuItem("Tools/UnderSide/Check Personalities and Elements")]
    public static void Run()
    {
        var randomState = Random.state;
        assertions = 0;
        try
        {
            Random.InitState(172026);
            CheckElements();
            CheckAttackBalance();
            CheckGrassWaterDirection();
            CheckTraits();
            CheckAdditionalDamageAdaptation();
            CheckAdaptationLogs();
            CheckSkills();
            FaithPrayerChecks.Run();
            BattleStatChecks.Run();
            ImpatientChargeChecks.Run();
            CheckGenerationAndMatches();
            BattleTurnEventChecks.Run();
            Debug.Log($"PERSONALITY_CHECKS_PASSED: {assertions} assertions; 400 personality matchups; 5 elements.");
        }
        finally { Random.state = randomState; }
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Personality check failed: " + message);
    }

    private static MonsterData Monster(PersonalityType personality = PersonalityType.Bold, ElementType element = ElementType.Fire)
    {
        return new MonsterData
        {
            Name = PersonalityNames.Get(personality), Personality = personality, Element = element,
            Stats = new MonsterStats { MaxHp = 100, Attack = 20, Defense = 10, Speed = 10, Accuracy = 80 },
            CurrentHp = 100,
            Skills = new List<SkillData>
            {
                new SkillData
                {
                    SkillName = "通常攻撃",
                    SkillType = SkillType.NormalAttack,
                    Power = 0,
                    SuccessRate = 100
                },
                new SkillData
                {
                    SkillName = "強撃",
                    SkillType = SkillType.StrongAttack,
                    Power = 12,
                    SuccessRate = 100
                }
            }
        };
    }

    private static void CheckElements()
    {
        var expected = new float[,]
        {
            {1, .75f, 1.5f, 1, 1}, {1.5f, 1, .75f, 1, 1}, {.75f, 1.5f, 1, 1, 1},
            {1, 1, 1, 1, 2}, {1, 1, 1, 2, 1}
        };
        for (int a = 0; a < 5; a++)
            for (int b = 0; b < 5; b++)
            {
                Assert(ElementCalculator.GetAttackMultiplier((ElementType)a, (ElementType)b) == expected[a, b], $"element {a}/{b}");
                Assert(ElementCalculator.IsDisadvantaged((ElementType)a, (ElementType)b) == (expected[b, a] > 1), "adversity relation");
            }
        var fire = Monster(element: ElementType.Fire);
        var grass = Monster(element: ElementType.Grass);
        Assert(BetOddsCalculator.CalculatePayoutMultiplier(fire, grass, PredictionSide.Left)
            < BetOddsCalculator.CalculatePayoutMultiplier(fire, grass, PredictionSide.Right), "favored element has lower payout");
        var light = Monster(element: ElementType.Light);
        var dark = Monster(element: ElementType.Dark);
        Assert(BetOddsCalculator.CalculatePayoutMultiplier(light, dark, PredictionSide.Left) == 2f, "mutual weaknesses symmetric");
    }

    private static void CheckAttackBalance()
    {
        var calculator = new DamageCalculator();
        var logger = new BattleLogger();
        // 全25相性を実際の技実行で確認。コピーで自分と違う属性の魔法を使う場合も含む。
        for (int element = 0; element < 5; element++)
        for (int targetElement = 0; targetElement < 5; targetElement++)
        {
            var mage = Monster(element: ElementType.Dark);
            mage.AttackBuff = 7;
            var target = Monster(element: (ElementType)targetElement);
            var magic = MonsterFactory.CreateElementSkill((ElementType)element);
            magic.SuccessRate = 100; // 成功時のダメージを抽選の成否から独立して確認する。
            bool favored = ElementCalculator.GetAttackMultiplier((ElementType)element, target.Element) > 1f;
            Random.InitState(20261019);
            int expected;
            if (favored)
                expected = mage.GetCurrentAttack() * 2 - target.GetCurrentDefense() / 2 + Random.Range(-2, 3);
            else
                expected = calculator.CalculateDamage(mage, target, 0, (ElementType)element);
            Random.InitState(20261019);
            Executor.Execute(mage, target, magic, logger, mage, target);
            Assert(100 - target.CurrentHp == expected, "magic doubles current attack only when favored: " + element + "/" + targetElement);
        }

        var attacker = Monster();
        var defender = Monster();
        var strong = MonsterFactory.CreateStrongAttack();
        strong.SuccessRate = 0; // 必ず失敗させ、反動の値と対象を確認する。
        attacker.AttackBuff = 7;
        attacker.Stats.Defense = 1000;
        attacker.Stats.CriticalRate = 100;
        strong.DamageMultiplier = .8f;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 94 && defender.CurrentHp == 100,
            "failed strong attack deals floor(current attack / 4) only to self, ignoring defense, critical and copy");
        Assert(logger.Logs.Any(log => log.Message.Contains("反動で 6 ダメージ")), "recoil is recorded in battle log");

        attacker = Monster();
        attacker.Stats.Attack = 1;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 99, "recoil has minimum one damage");
        attacker = Monster();
        attacker.CurrentHp = 4;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 0, "recoil can defeat its user");

        attacker = Monster(PersonalityType.Unyielding);
        attacker.CurrentHp = 4;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 1 && attacker.EndureCount == 1, "unyielding survives lethal recoil");
        attacker = Monster(PersonalityType.Stoic);
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.DamageTakenCount == 1 && attacker.GetCurrentAttack() == 22
            && attacker.GetCurrentDefense() == 11 && attacker.GetCurrentSpeed() == 11
            && attacker.CurrentHp == 95 && attacker.GetCurrentMaxHp() == 100, "stoic grows three stats after surviving recoil");
        attacker = Monster(PersonalityType.Adaptive);
        attacker.PhysicalAdaptation = 100;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 95 && attacker.PhysicalAdaptation == 100 && attacker.RecoilAdaptation == 10,
            "recoil uses its own adaptation rather than physical adaptation");

        attacker = Monster();
        attacker.IsSkillLocked = true;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 100, "seal prevents a strong attack without recoil");
        attacker.IsSkillLocked = false;
        strong.SuccessRate = 100;
        Executor.Execute(attacker, defender, strong, logger, attacker, defender);
        Assert(attacker.CurrentHp == 100 && defender.CurrentHp < 100, "successful strong attack has no recoil");
        var failedMagic = MonsterFactory.CreateElementSkill(ElementType.Fire);
        failedMagic.SuccessRate = 0;
        Executor.Execute(attacker, defender, failedMagic, logger, attacker, defender);
        Assert(attacker.CurrentHp == 100, "failed magic has no recoil");

        // 成功率そのものは変更しない。通常・強撃の追加威力も保つ。
        Assert(MonsterFactory.CreateElementSkill(ElementType.Fire).SuccessRate == 90
            && MonsterFactory.CreateStrongAttack().SuccessRate == 65, "attack success rates are unchanged");
        Random.InitState(20261019);
        int normalDamage = calculator.CalculateDamage(attacker, defender, 0, ElementType.Fire, SkillType.NormalAttack);
        Random.InitState(20261019);
        int strongDamage = calculator.CalculateDamage(attacker, defender, 12, ElementType.Fire, SkillType.StrongAttack);
        Assert(strongDamage == normalDamage + 12, "strong attack keeps its additional power");
    }

    private static void CheckTraits()
    {
        var foe = Monster(element: ElementType.Water);
        var logger = new BattleLogger();
        var inept = Monster(PersonalityType.Inept);
        PersonalityRules.BeginTurn(inept, foe, 9, logger, inept, foe);
        Assert(!inept.Awakened && Ai.ChoosePersonalitySkill(inept, foe, 9).SkillType == SkillType.NormalAttack, "normal attack before turn 10");
        inept.CurrentHp = 5;
        PersonalityRules.BeginTurn(inept, foe, 10, logger, inept, foe);
        Assert(inept.Awakened && inept.GetCurrentAttack() == 80 && inept.CurrentHp == 400, "turn 10 awakening");
        Assert(Ai.ChoosePersonalitySkill(inept, foe, 10).SkillType == SkillType.StrongAttack, "awakening attack on turn 10");
        inept.CurrentHp = 200;
        PersonalityRules.BeginTurn(inept, foe, 11, logger, inept, foe);
        Assert(inept.CurrentHp == 200, "awakening happens once");

        CheckAdversity();

        CheckStoic();

        CheckObsession();

        var endure = Monster(PersonalityType.Unyielding);
        PersonalityRules.TakeDamage(endure, foe, 1000, logger, endure, foe);
        Assert(endure.CurrentHp == 1 && endure.EndureCount == 1, "first fatal hit survives");
        int survives = 0;
        for (int i = 0; i < 2000; i++)
        {
            endure.CurrentHp = 1;
            PersonalityRules.TakeDamage(endure, foe, 10, logger, endure, foe);
            if (endure.CurrentHp == 1) survives++;
        }
        Assert(survives > 850 && survives < 1150, "subsequent survival approximately 50 percent, including HP1");
        PersonalityRules.Reset(endure);
        PersonalityRules.TakeDamage(endure, foe, 100, logger, endure, foe, instantDeath: true);
        Assert(endure.CurrentHp == 1, "instant death also respects unyielding");
        PersonalityRules.Reset(endure);
        endure.CurrentHp = 1;
        PersonalityRules.TakeDamage(endure, foe, 10, logger, endure, foe, poison: true);
        Assert(endure.CurrentHp == 1, "poison respects unyielding");

        CheckAdaptation();
    }

    private static void CheckAdversity()
    {
        for (int selfElement = 0; selfElement < 5; selfElement++)
        for (int opponentElement = 0; opponentElement < 5; opponentElement++)
        {
            var monster = Monster(PersonalityType.Adversity, (ElementType)selfElement);
            var foe = Monster(element: (ElementType)opponentElement);
            monster.Stats.Growth = 25;
            monster.Stats.Accuracy = 20;
            monster.Stats.Evasion = 15;
            monster.Stats.CriticalRate = 10;
            monster.CurrentHp = 37;
            var logger = new BattleLogger();
            bool active = ElementCalculator.IsDisadvantaged(monster.Element, foe.Element);
            PersonalityRules.Refresh(monster, foe, logger, monster, foe);
            Assert(monster.AdversityActive == active && monster.PersonalityMultiplier == 1f,
                "adversity follows all elemental matchups without a global multiplier");
            Assert(monster.CurrentHp == 37 && monster.GetCurrentMaxHp() == 100,
                "activating adversity preserves current and maximum HP");
            Assert(monster.GetCurrentAttack() == (active ? 24 : 20)
                && monster.GetCurrentDefense() == (active ? 12 : 10)
                && monster.GetCurrentSpeed() == (active ? 12 : 10),
                "adversity multiplies only attack, defense and speed by 1.2");
            Assert(monster.GetCurrentGrowth() == (active ? 100 : 25)
                && monster.GetCurrentAccuracy() == 20 && monster.GetCurrentEvasion() == 15
                && monster.GetCurrentCriticalRate() == 10, "adversity fixes growth at 100 and preserves accuracy, evasion and critical");
            Assert(monster.Stats.MaxHp == 100 && monster.Stats.Attack == 20 && monster.Stats.Defense == 10
                && monster.Stats.Speed == 10 && monster.Stats.Growth == 25, "adversity does not mutate base stats");
            PersonalityRules.Refresh(monster, foe, logger, monster, foe);
            Assert(logger.Logs.Count == (active ? 1 : 0) && monster.GetCurrentAttack() == (active ? 24 : 20),
                "refresh neither stacks adversity nor repeats its activation log");
            if (active)
            {
                Assert(logger.Logs[0].Message == "逆境 の逆境が発動！攻撃・防御・素早さ1.2倍、成長率100%！"
                    && logger.Logs[0].LeftHp == 37, "adversity log describes the new effect with unchanged HP");
                monster.AttackBuff = 3;
                monster.DefenseBuff = 4;
                monster.GrowthAttackBonus = monster.GrowthDefenseBonus = monster.GrowthSpeedBonus = 5;
                Assert(monster.GetCurrentAttack() == 32 && monster.GetCurrentDefense() == 21 && monster.GetCurrentSpeed() == 17,
                    "additive buffs and growth are added after the adversity multiplier");
                // 成長率が元は0でも、逆境中は毎回成長する。
                monster.Stats.Growth = 0;
                for (int turn = 1; turn <= 10; turn++)
                {
                    int before = monster.GrowthAttackBonus + monster.GrowthDefenseBonus + monster.GrowthSpeedBonus
                        + monster.GrowthAccuracyBonus + monster.GrowthEvasionBonus;
                    BattleStatRules.ApplyEndTurnGrowth(monster, turn);
                    int after = monster.GrowthAttackBonus + monster.GrowthDefenseBonus + monster.GrowthSpeedBonus
                        + monster.GrowthAccuracyBonus + monster.GrowthEvasionBonus;
                    Assert(after > before, "active adversity guarantees an end-turn growth roll");
                }
            }
            foe.Element = monster.Element;
            PersonalityRules.Refresh(monster, foe, logger, monster, foe);
            Assert(!monster.AdversityActive && monster.GetCurrentGrowth() == monster.Stats.Growth
                && monster.CurrentHp == 37 && monster.GetCurrentMaxHp() == 100,
                "losing elemental disadvantage removes adversity without changing HP");
            foe.Element = (ElementType)opponentElement;
            PersonalityRules.Refresh(monster, foe, logger, monster, foe);
            PersonalityRules.Reset(monster);
            Assert(!monster.AdversityActive && monster.GetCurrentAttack() == 20
                && monster.GetCurrentDefense() == 10 && monster.GetCurrentSpeed() == 10,
                "a new match resets adversity and temporary bonuses");
        }

        var priced = Monster(PersonalityType.Adversity);
        var opponent = Monster(element: ElementType.Water);
        // 基本値はHP100、攻撃20、防御10、素早さ10、命中80、他0、技数2。
        float activeScore = (20f + 24f * .7f + 12f * .9f + 100f * .5f + 12f * .9f + 80f * .9f)
            * Mathf.Sqrt(.75f / 1.5f) + 16f;
        float opponentScore = (20f + 20f * .7f + 10f * .9f + 10f * .9f + 80f * .9f)
            * Mathf.Sqrt(1.5f / .75f) * 1.4f + 16f;
        float payout = BetOddsCalculator.CalculatePayoutMultiplier(priced, opponent, PredictionSide.Left);
        Assert(Mathf.Abs(payout - (1f + opponentScore / activeScore)) < .0001f,
            "odds value the new individual adversity bonuses instead of doubling the whole score");
        PersonalityRules.Refresh(priced, opponent, new BattleLogger(), priced, opponent);
        Assert(BetOddsCalculator.CalculatePayoutMultiplier(priced, opponent, PredictionSide.Left) == payout,
            "adversity odds are independent of whether battle state has been initialized");
    }

    private static void CheckStoic()
    {
        var stoic = Monster(PersonalityType.Stoic);
        var foe = Monster();
        var logger = new BattleLogger();
        stoic.Stats.Growth = 7;
        stoic.Stats.Evasion = 12;
        stoic.Stats.CriticalRate = 15;
        stoic.Stats.MaxHp = stoic.CurrentHp = 1000;
        for (int hit = 1; hit <= 10; hit++)
        {
            Assert(PersonalityRules.TakeDamage(stoic, foe, 10, logger, stoic, foe) == 10, "stoic reports actual HP loss");
            Assert(stoic.GetCurrentAttack() == 20 + 2 * hit && stoic.GetCurrentDefense() == 10 + hit
                && stoic.GetCurrentSpeed() == 10 + hit && stoic.DamageTakenCount == hit,
                "stoic adds one tenth of base attack, defense and speed per hit, without compounding");
            Assert(logger.Logs.Count == hit && logger.Logs[hit - 1].Message == "ダメージを乗り越えて強くなった！",
                "each surviving hit emits the requested growth message exactly once");
            Assert(stoic.CurrentHp == 1000 - 10 * hit && stoic.GetCurrentMaxHp() == 1000
                && stoic.GetCurrentGrowth() == 7 && stoic.GetCurrentAccuracy() == 80
                && stoic.GetCurrentEvasion() == 12 && stoic.GetCurrentCriticalRate() == 15,
                "stoic leaves HP, growth, accuracy, evasion and critical rate unchanged");
        }
        stoic.AttackBuff = 5;
        stoic.DefenseBuff = -3;
        PersonalityRules.Refresh(stoic, foe, logger, stoic, foe);
        Assert(stoic.GetCurrentAttack() == 45 && stoic.GetCurrentDefense() == 17 && stoic.GetCurrentSpeed() == 20
            && stoic.Stats.Attack == 20 && stoic.Stats.Defense == 10 && stoic.Stats.Speed == 10
            && stoic.PersonalityMultiplier == 1f && logger.Logs.Count == 10,
            "refresh does not stack growth or multiply additive buffs or mutate base stats");
        PersonalityRules.TakeDamage(stoic, foe, 0, logger, stoic, foe);
        Assert(stoic.DamageTakenCount == 10 && stoic.GetCurrentAttack() == 45 && logger.Logs.Count == 10, "zero damage grants no growth or log");
        PersonalityRules.TakeDamage(stoic, foe, 10, logger, stoic, foe, poison: true);
        Assert(stoic.DamageTakenCount == 11 && stoic.GetCurrentAttack() == 47 && stoic.GetCurrentDefense() == 18
            && stoic.GetCurrentSpeed() == 21 && stoic.CurrentHp == 890 && logger.Logs.Count == 11,
            "poison increases the three stats after HP loss");
        PersonalityRules.TakeDamage(stoic, foe, 1000, logger, stoic, foe);
        PersonalityRules.TakeDamage(stoic, foe, 10, logger, stoic, foe);
        Assert(stoic.CurrentHp == 0 && stoic.DamageTakenCount == 11 && logger.Logs.Count == 11, "lethal and postmortem damage grant no growth or log");
        PersonalityRules.Reset(stoic);
        Assert(stoic.GetCurrentAttack() == 20 && stoic.CurrentHp == 1000 && stoic.DamageTakenCount == 0
            && stoic.GetCurrentDefense() == 10 && stoic.GetCurrentSpeed() == 10
            && stoic.StoicStatMultiplier == 1f && stoic.PersonalityAttackMultiplier == 1f, "new match resets all three stoic stats");
    }

    // 受ける側の5属性×魔法5属性。使用者と魔法の属性を変え、コピー時も技属性で判定する。
    private static void CheckMagicAdaptationAffinity()
    {
        var expectedGrowth = new int[,]
        {
            {10, 5, 20, 10, 10}, {20, 10, 5, 10, 10}, {5, 20, 10, 10, 10},
            {10, 10, 10, 10, 5}, {10, 10, 10, 5, 10}
        };
        var logger = new BattleLogger();
        for (int defenderElement = 0; defenderElement < 5; defenderElement++)
        for (int spellElement = 0; spellElement < 5; spellElement++)
        {
            var adaptive = Monster(PersonalityType.Adaptive, (ElementType)defenderElement);
            var baseline = Monster(element: (ElementType)defenderElement);
            var caster = Monster(element: (ElementType)((spellElement + 1) % 5));
            adaptive.Stats.MaxHp = adaptive.CurrentHp = baseline.Stats.MaxHp = baseline.CurrentHp = 1000;
            var magic = MonsterFactory.CreateElementSkill((ElementType)spellElement);
            magic.SuccessRate = 100;
            int growth = expectedGrowth[defenderElement, spellElement];
            Random.InitState(20261021);
            Executor.Execute(caster, baseline, magic, logger, caster, baseline);
            int baseDamage = 1000 - baseline.CurrentHp;
            Random.InitState(20261021);
            Executor.Execute(caster, adaptive, magic, logger, caster, adaptive);
            Assert(adaptive.MagicAdaptation == growth && adaptive.CurrentHp == baseline.CurrentHp,
                $"magic growth uses defender affinity only after damage: {defenderElement}/{spellElement}");
            int hp = adaptive.CurrentHp;
            Random.InitState(20261021);
            Executor.Execute(caster, adaptive, magic, logger, caster, adaptive);
            Assert(hp - adaptive.CurrentHp == Mathf.Max(1, Mathf.RoundToInt(baseDamage * (1f - growth / 100f)))
                && adaptive.MagicAdaptation == growth * 2, "new resistance applies from the next magic hit");
            adaptive.MagicAdaptation = 97;
            Executor.Execute(caster, adaptive, magic, logger, caster, adaptive);
            Assert(adaptive.MagicAdaptation == 100, "every affinity caps at 100");
            hp = adaptive.CurrentHp;
            Executor.Execute(caster, adaptive, magic, logger, caster, adaptive);
            Assert(adaptive.CurrentHp == hp && adaptive.MagicAdaptation == 100, "full magic adaptation prevents damage");
            adaptive.MagicAdaptation = 0;
            adaptive.IsInvincible = true;
            Executor.Execute(caster, adaptive, magic, logger, caster, adaptive);
            Assert(adaptive.CurrentHp == hp && adaptive.MagicAdaptation == 0, "invincible magic hit grants no adaptation");
            adaptive.IsInvincible = false;
            adaptive.CurrentHp = 1;
            Executor.Execute(caster, adaptive, magic, logger, caster, adaptive);
            Assert(adaptive.CurrentHp == 0 && adaptive.MagicAdaptation == 0, "lethal magic grants no adaptation");
        }
    }

    // 実際の技実行で、命中・失敗・無敵・種類別蓄積とターン経過を確認する。
    private static void CheckAdaptation()
    {
        CheckMagicAdaptationAffinity();
        CheckAttackAdaptationDamage();
        var adaptive = Monster(PersonalityType.Adaptive);
        adaptive.Stats.MaxHp = adaptive.CurrentHp = 1000;
        var foe = Monster();
        var logger = new BattleLogger();
        var normal = MonsterFactory.CreateNormalAttack();
        var strong = MonsterFactory.CreateStrongAttack();
        var magic = MonsterFactory.CreateElementSkill(ElementType.Water);
        var stun = new SkillData { SkillName = "スタン", SkillType = SkillType.StunDebuff, SuccessRate = 100 };

        PersonalityRules.BeginTurn(adaptive, foe, 10, logger, adaptive, foe);
        Assert(adaptive.PhysicalAdaptation == 0 && adaptive.MagicAdaptation == 0
            && adaptive.PoisonAdaptation == 0 && adaptive.StunAdaptation == 0, "unexperienced categories never grow with turns");
        Executor.Execute(foe, adaptive, normal, logger, foe, adaptive);
        Assert(adaptive.CurrentHp < 1000 && adaptive.PhysicalAdaptation == 5, "normal hit starts physical adaptation at five points");
        int hp = adaptive.CurrentHp;
        SetNextSuccessRoll(.97f, .99f);
        Executor.Execute(foe, adaptive, strong, logger, foe, adaptive);
        Assert(adaptive.CurrentHp == hp && adaptive.PhysicalAdaptation == 5, "failed strong attack adds no physical adaptation");
        PersonalityRules.BeginTurn(adaptive, foe, 11, logger, adaptive, foe);
        PersonalityRules.BeginTurn(adaptive, foe, 11, logger, adaptive, foe);
        Assert(adaptive.PhysicalAdaptation == 6 && adaptive.MagicAdaptation == 0
            && adaptive.PoisonAdaptation == 0 && adaptive.StunAdaptation == 0, "only experienced physical category grows once per turn");
        SetNextSuccessRoll(.1f, .2f);
        Executor.Execute(foe, adaptive, strong, logger, foe, adaptive);
        Assert(adaptive.PhysicalAdaptation == 16, "strong hit adds ten to shared physical adaptation");
        SetNextSuccessRoll(.1f, .2f);
        Executor.Execute(foe, adaptive, magic, logger, foe, adaptive);
        Assert(adaptive.MagicAdaptation == 5 && adaptive.PhysicalAdaptation == 16, "magic hit changes only magic adaptation");
        hp = adaptive.CurrentHp;
        SetNextSuccessRoll(.95f, .99f);
        Executor.Execute(foe, adaptive, magic, logger, foe, adaptive);
        Assert(adaptive.CurrentHp == hp && adaptive.MagicAdaptation == 5, "failed magic adds no adaptation");
        Assert(PersonalityRules.GetAttackResistance(adaptive, SkillType.FireMagic) == .05f
            && PersonalityRules.GetAttackResistance(adaptive, SkillType.DarkMagic) == .05f, "all five magic elements share magic damage resistance");
        Assert(normal.SuccessRate == 100 && strong.SuccessRate == 65 && magic.SuccessRate == 90, "adaptation does not mutate shared skill definitions");

        // 無敵でダメージを防いだ場合は、新しい種類の学習を開始しない。
        var protectedMonster = Monster(PersonalityType.Adaptive);
        protectedMonster.IsInvincible = true;
        Executor.Execute(foe, protectedMonster, normal, logger, foe, protectedMonster);
        PersonalityRules.BeginTurn(protectedMonster, foe, 5, logger, protectedMonster, foe);
        Assert(protectedMonster.CurrentHp == 100 && protectedMonster.PhysicalAdaptation == 0, "invincible hit does not unlock passive adaptation");

        Executor.Execute(foe, adaptive, stun, logger, foe, adaptive);
        Assert(adaptive.StatusAilment == StatusAilmentType.Stun && adaptive.StunAdaptation == 10, "successful stun adds ten independently");
        Assert(!new StatusSystem().CanAct(adaptive) && adaptive.StatusAilment == StatusAilmentType.None, "stun recovery preserves adaptation");
        SetNextSuccessRoll(.95f, .99f);
        Executor.Execute(foe, adaptive, stun, logger, foe, adaptive);
        Assert(adaptive.StatusAilment == StatusAilmentType.None && adaptive.StunAdaptation == 10, "failed stun adds no adaptation");
        SetNextSuccessRoll(.1f, .2f);
        Executor.Execute(foe, adaptive, stun, logger, foe, adaptive);
        Assert(adaptive.StunAdaptation == 20, "second successful stun adds another ten");
        var immune = Monster(PersonalityType.Adaptive);
        immune.DebuffImmune = true;
        Executor.Execute(foe, immune, stun, logger, foe, immune);
        Assert(immune.StunAdaptation == 0 && immune.StatusAilment == StatusAilmentType.None, "rejected stun does not teach adaptation");

        var poisoned = Monster(PersonalityType.Adaptive);
        var poison = new SkillData { SkillName = "毒", SkillType = SkillType.PoisonDebuff, SuccessRate = 100 };
        Executor.Execute(foe, poisoned, poison, logger, foe, poisoned);
        Assert(poisoned.PoisonAdaptation == 0, "poison application alone does not teach damage adaptation");
        var status = new StatusSystem();
        for (int i = 0; i < 10; i++)
        {
            hp = poisoned.CurrentHp;
            status.ApplyEndTurn(poisoned, logger, foe, poisoned);
            Assert(hp - poisoned.CurrentHp == 10 - i && poisoned.PoisonAdaptation == (i + 1) * 10,
                "poison tick decreases by ten percent of base damage " + i);
        }
        hp = poisoned.CurrentHp;
        status.ApplyEndTurn(poisoned, logger, foe, poisoned);
        Assert(poisoned.CurrentHp == hp && poisoned.PoisonAdaptation == 100, "fully adapted poison deals zero");
        Assert(poisoned.PhysicalAdaptation == 0 && poisoned.MagicAdaptation == 0 && poisoned.StunAdaptation == 0,
            "poison adaptation is isolated from other resistances");
        PersonalityRules.TakeDamage(adaptive, foe, 10, logger, adaptive, foe, poison: true);
        PersonalityRules.BeginTurn(adaptive, foe, 12, logger, adaptive, foe);
        Assert(adaptive.PhysicalAdaptation == 17 && adaptive.MagicAdaptation == 6
            && adaptive.PoisonAdaptation == 11 && adaptive.StunAdaptation == 21, "each experienced category grows by one per turn");
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 20, logger, adaptive, foe) == 20, "untyped damage is not reduced by physical adaptation");
        PersonalityRules.BeginTurn(adaptive, foe, 200, logger, adaptive, foe);
        Assert(adaptive.PhysicalAdaptation == 100 && adaptive.MagicAdaptation == 100
            && adaptive.PoisonAdaptation == 100 && adaptive.StunAdaptation == 100, "all adaptation values cap at 100");
        adaptive.StatusAilment = StatusAilmentType.None;
        hp = adaptive.CurrentHp;
        Executor.Execute(foe, adaptive, normal, logger, foe, adaptive);
        Executor.Execute(foe, adaptive, magic, logger, foe, adaptive);
        Executor.Execute(foe, adaptive, stun, logger, foe, adaptive);
        Assert(adaptive.CurrentHp == hp && adaptive.StatusAilment == StatusAilmentType.None, "full adaptation blocks physical and magic damage and stun");
        Assert(PersonalityRules.GetSuccessRateReduction(adaptive, SkillType.Heal) == 0
            && PersonalityRules.GetSuccessRateReduction(adaptive, SkillType.InstantDeath) == 0, "unrelated skills are not affected");

        PersonalityRules.Reset(adaptive);
        PersonalityRules.BeginTurn(adaptive, foe, 5, logger, adaptive, foe);
        Assert(adaptive.CurrentHp == 1000 && adaptive.DamageTakenCount == 0 && adaptive.PhysicalAdaptation == 0
            && adaptive.MagicAdaptation == 0 && adaptive.PoisonAdaptation == 0 && adaptive.StunAdaptation == 0,
            "reset forgets all learned categories and their passive growth");
    }

    private static MonsterData DamageTestMonster(PersonalityType personality = PersonalityType.Adaptive)
    {
        var monster = Monster(personality);
        monster.Stats.MaxHp = monster.CurrentHp = 10000;
        return monster;
    }

    private static void SetAllAdaptation(MonsterData monster, int value)
    {
        monster.PhysicalAdaptation = monster.MagicAdaptation = monster.PoisonAdaptation = monster.StunAdaptation = value;
        monster.ExplosionAdaptation = monster.RecoilAdaptation = value;
    }

    private static int AdditionalAdaptation(MonsterData monster, DamageSource source)
    {
        return source == DamageSource.Explosion ? monster.ExplosionAdaptation
            : monster.RecoilAdaptation;
    }

    private static void CheckAdditionalDamageAdaptation()
    {
        var foe = Monster();
        var logger = new BattleLogger();
        foreach (var source in new[] { DamageSource.Explosion, DamageSource.Recoil })
        {
            var adaptive = DamageTestMonster();
            PersonalityRules.BeginTurn(adaptive, foe, 1, logger, adaptive, foe);
            for (int hit = 0; hit < 10; hit++)
            {
                int actual = PersonalityRules.TakeDamage(adaptive, foe, 100, logger, adaptive, foe, damageSource: source);
                Assert(actual == 100 - hit * 10 && AdditionalAdaptation(adaptive, source) == (hit + 1) * 10,
                    "special damage learns ten points after each surviving hit: " + source);
            }
            int count = adaptive.DamageTakenCount;
            Assert(PersonalityRules.TakeDamage(adaptive, foe, 100, logger, adaptive, foe, damageSource: source) == 0
                && adaptive.DamageTakenCount == count, "fully adapted damage grants no extra hit");
            Assert(adaptive.PhysicalAdaptation == 0 && adaptive.MagicAdaptation == 0 && adaptive.PoisonAdaptation == 0
                && adaptive.StunAdaptation == 0 && adaptive.ExplosionAdaptation + adaptive.RecoilAdaptation == 100, "special damage categories are independent");
            PersonalityRules.Reset(adaptive);
            Assert(adaptive.ExplosionAdaptation + adaptive.RecoilAdaptation == 0,
                "new match resets all special adaptation");
            PersonalityRules.BeginTurn(adaptive, foe, 1, logger, adaptive, foe);
            PersonalityRules.TakeDamage(adaptive, foe, 1, logger, adaptive, foe, damageSource: source);
            PersonalityRules.BeginTurn(adaptive, foe, 2, logger, adaptive, foe);
            PersonalityRules.BeginTurn(adaptive, foe, 2, logger, adaptive, foe);
            Assert(AdditionalAdaptation(adaptive, source) == 11, "experienced special type grows once per turn");
            PersonalityRules.BeginTurn(adaptive, foe, 500, logger, adaptive, foe);
            Assert(AdditionalAdaptation(adaptive, source) == 100, "passive special adaptation caps at 100");
            PersonalityRules.Reset(adaptive);
            PersonalityRules.TakeDamage(adaptive, foe, 0, logger, adaptive, foe, damageSource: source);
            adaptive.CurrentHp = 1;
            PersonalityRules.TakeDamage(adaptive, foe, 100, logger, adaptive, foe, damageSource: source);
            Assert(adaptive.CurrentHp == 0 && AdditionalAdaptation(adaptive, source) == 0,
                "zero and lethal special damage teach nothing");
            var ordinary = DamageTestMonster(PersonalityType.Bold);
            SetAllAdaptation(ordinary, 100);
            Assert(PersonalityRules.TakeDamage(ordinary, foe, 100, logger, ordinary, foe, damageSource: source) == 100,
                "non-adaptive personality cannot use stored resistances");
        }

        foreach (var source in new[] { DamageSource.Punishment, DamageSource.Miracle, DamageSource.TurnEvent })
        foreach (int resistance in new[] { 0, 50, 100 })
        {
            var adaptive = DamageTestMonster();
            SetAllAdaptation(adaptive, resistance);
            Assert(PersonalityRules.TakeDamage(adaptive, foe, 40, logger, adaptive, foe, damageSource: source) == 40,
                "excluded sources ignore every resistance");
            Assert(adaptive.PhysicalAdaptation == resistance && adaptive.MagicAdaptation == resistance
                && adaptive.ExplosionAdaptation == resistance && adaptive.RecoilAdaptation == resistance, "excluded source grants no adaptation");
        }
        var deathTarget = DamageTestMonster();
        SetAllAdaptation(deathTarget, 100);
        Executor.Execute(foe, deathTarget, new SkillData { SkillType = SkillType.InstantDeath, SuccessRate = 100 }, logger, foe, deathTarget);
        Assert(deathTarget.CurrentHp == 0, "instant death bypasses even complete adaptation");

        var recoilTarget = Monster(PersonalityType.Adaptive);
        recoilTarget.Stats.Attack = 100;
        recoilTarget.RecoilAdaptation = 50;
        var failedStrong = MonsterFactory.CreateStrongAttack();
        failedStrong.SuccessRate = 0;
        Executor.Execute(recoilTarget, foe, failedStrong, logger, recoilTarget, foe);
        Assert(recoilTarget.CurrentHp == 100 - Mathf.RoundToInt(25 * .5f) && recoilTarget.RecoilAdaptation == 60,
            "actual failed strong attack reduces recoil and learns exactly once");

        var comboTarget = DamageTestMonster();
        comboTarget.StatusAilment = StatusAilmentType.Poison;
        comboTarget.DefenseBuff = -10;
        comboTarget.IsSkillLocked = true;
        comboTarget.PhysicalAdaptation = 50;
        var combo = new SkillData { SkillType = SkillType.DebuffCombo, SuccessRate = 100 };
        Random.InitState(20261022);
        int comboBase = new DamageCalculator().CalculateDamage(foe, comboTarget, 0, foe.Element, SkillType.DebuffCombo);
        Random.InitState(20261022);
        Executor.Execute(foe, comboTarget, combo, logger, foe, comboTarget);
        Assert(10000 - comboTarget.CurrentHp == Mathf.RoundToInt(comboBase * .5f) && comboTarget.PhysicalAdaptation == 60,
            "combo's damage stage uses and learns physical adaptation");

        CheckExplosionAdaptation();
        CheckAllOutAdaptation();
        int eventHits = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var left = DamageTestMonster();
            var right = DamageTestMonster();
            SetAllAdaptation(left, 100);
            SetAllAdaptation(right, 100);
            Random.InitState(seed);
            BattleTurnEvents.Apply(left, right, new BattleLogger(), 1, false);
            int loss = 20000 - left.CurrentHp - right.CurrentHp;
            Assert(loss == 0 || new[] { 5, 10, 30, 50 }.Contains(loss), "late battle events keep full fixed damage");
            if (loss > 0) eventHits++;
        }
        Assert(eventHits > 0, "actual excluded damage events exercised");
    }

    private static void CheckExplosionAdaptation()
    {
        var dig = new SkillData { SkillType = SkillType.Dig, SuccessRate = 100 };
        int explosionSeed = -1;
        int baseTargetDamage = 0, baseSelfDamage = 0;
        for (int seed = 0; seed < 1000; seed++)
        {
            var actor = DamageTestMonster();
            var target = DamageTestMonster();
            Random.InitState(seed);
            Executor.Execute(actor, target, dig, new BattleLogger(), actor, target);
            if (actor.ExplosionAdaptation != 10 || target.ExplosionAdaptation != 10) continue;
            explosionSeed = seed;
            baseTargetDamage = 10000 - target.CurrentHp;
            baseSelfDamage = 10000 - actor.CurrentHp;
            break;
        }
        Assert(explosionSeed >= 0 && baseTargetDamage > 0 && baseSelfDamage > 0, "actual explosion and self-blast both learn ten");
        var adaptedActor = DamageTestMonster();
        var adaptedTarget = DamageTestMonster();
        SetAllAdaptation(adaptedActor, 100);
        SetAllAdaptation(adaptedTarget, 100);
        adaptedActor.ExplosionAdaptation = adaptedTarget.ExplosionAdaptation = 50;
        Random.InitState(explosionSeed);
        Executor.Execute(adaptedActor, adaptedTarget, dig, new BattleLogger(), adaptedActor, adaptedTarget);
        Assert(10000 - adaptedTarget.CurrentHp == Mathf.RoundToInt(baseTargetDamage * .5f)
            && 10000 - adaptedActor.CurrentHp == Mathf.RoundToInt(baseSelfDamage * .5f)
            && adaptedActor.ExplosionAdaptation == 60 && adaptedTarget.ExplosionAdaptation == 60,
            "explosion and self-blast use only explosion resistance and grow once");
        adaptedActor = DamageTestMonster();
        adaptedTarget = DamageTestMonster();
        adaptedActor.IsInvincible = adaptedTarget.IsInvincible = true;
        Random.InitState(explosionSeed);
        Executor.Execute(adaptedActor, adaptedTarget, dig, new BattleLogger(), adaptedActor, adaptedTarget);
        Assert(adaptedActor.CurrentHp == 10000 && adaptedTarget.CurrentHp == 10000
            && adaptedActor.ExplosionAdaptation == 0 && adaptedTarget.ExplosionAdaptation == 0,
            "invincibility prevents explosion damage and learning");
    }

    private static void CheckAllOutAdaptation()
    {
        var skill = new SkillData { SkillType = SkillType.AllOutAttack, SuccessRate = 100 };
        var actor = Monster();
        var target = DamageTestMonster();
        var logger = new BattleLogger();
        Executor.Execute(actor, target, skill, logger, actor, target);
        Assert(target.CurrentHp > 0 && target.CurrentHp < 10000 && target.PhysicalAdaptation == 100
            && target.MagicAdaptation == 100 && actor.CurrentHp == 1,
            "surviving all-out damage completes both defenses while attacker pays HP cost");
        Assert(target.ExplosionAdaptation == 0 && target.RecoilAdaptation == 0,
            "all-out survival completes only physical and magic adaptation");
        int hp = target.CurrentHp;
        Executor.Execute(actor, target, MonsterFactory.CreateNormalAttack(), logger, actor, target);
        var magic = MonsterFactory.CreateElementSkill(ElementType.Water);
        magic.SuccessRate = 100;
        Executor.Execute(actor, target, magic, logger, actor, target);
        Assert(target.CurrentHp == hp, "after surviving all-out, physical and magic attacks deal zero");
        foreach (int scenario in new[] { 0, 1, 2 })
        {
            actor = Monster();
            target = DamageTestMonster();
            skill.SuccessRate = scenario == 0 ? 0 : 100;
            target.IsInvincible = scenario == 1;
            if (scenario == 2) target.CurrentHp = 1;
            Executor.Execute(actor, target, skill, logger, actor, target);
            Assert(target.PhysicalAdaptation == 0 && target.MagicAdaptation == 0,
                "failed, invincible and lethal all-out attacks do not teach adaptation");
        }
    }

    private static void CheckAdaptationLogs()
    {
        var foe = Monster();
        var gains = new Action<MonsterData, BattleLogger>[]
        {
            (m, log) => PersonalityRules.OnAttackHit(m, SkillType.NormalAttack, ElementType.Fire, log, m, foe),
            (m, log) => PersonalityRules.OnAttackHit(m, SkillType.FireMagic, ElementType.Fire, log, m, foe),
            (m, log) => PersonalityRules.TakeDamage(m, foe, 100, log, m, foe, poison: true),
            (m, log) => PersonalityRules.OnStunApplied(m, log, m, foe),
            (m, log) => PersonalityRules.TakeDamage(m, foe, 100, log, m, foe, damageSource: DamageSource.Explosion),
            (m, log) => PersonalityRules.TakeDamage(m, foe, 100, log, m, foe, damageSource: DamageSource.Recoil)
        };
        var categories = new[] { "物理攻撃", "魔法攻撃", "毒", "スタン", "爆破", "反動" };
        var expectedMessages = categories.Select(x => $"{x}の適応が完了した！次からの{x}は無効化される！").ToArray();
        for (int kind = 0; kind < gains.Length; kind++)
        {
            var gain = gains[kind];
            var adaptive = DamageTestMonster();
            adaptive.Name = "検証スライム";
            var logger = new BattleLogger();
            gain(adaptive, logger);
            Assert(logger.Logs.Count == 1
                && logger.Logs[0].Message == $"{categories[kind]}の適応が始まった...",
                "first gain announces the start of adaptation for its category");
            Assert(!logger.Logs[0].IsAction && !logger.Logs[0].PlayMotion
                && logger.Logs[0].LeftHp == adaptive.CurrentHp && logger.Logs[0].RightHp == foe.CurrentHp,
                "adaptation progress preserves HP snapshots without replaying an attack");
            gain(adaptive, logger);
            Assert(logger.Logs.Count == 2 && logger.Logs[1].Message == $"{categories[kind]}の適応がすすんだ！",
                "later hits announce progress instead of repeating the start message");
            SetAllAdaptation(adaptive, 99);
            gain(adaptive, logger);
            Assert(logger.Logs.Count == 3 && logger.Logs[2].Message == expectedMessages[kind],
                "each resistance uses its exact category completion message");
            gain(adaptive, logger);
            Assert(logger.Logs.Count == 3, "already complete resistance does not repeat its message");
        }

        var passive = DamageTestMonster();
        passive.Name = "成長スライム";
        var passiveLog = new BattleLogger();
        SetAllAdaptation(passive, 98);
        PersonalityRules.BeginTurn(passive, foe, 1, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Select(x => x.Message).SequenceEqual(categories.Select(x => $"{x}を解析している。。")),
            "turn growth uses the analysis message for each progressing category");
        PersonalityRules.BeginTurn(passive, foe, 1, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Count == 6, "re-evaluating the same turn does not repeat progress");
        PersonalityRules.BeginTurn(passive, foe, 2, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Skip(6).Select(x => x.Message).SequenceEqual(expectedMessages),
            "simultaneous passive completions identify every completed category");
        PersonalityRules.BeginTurn(passive, foe, 2, passiveLog, passive, foe);
        PersonalityRules.BeginTurn(passive, foe, 3, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Count == 12, "same and later turns do not repeat completed status");
        PersonalityRules.Reset(passive);
        SetAllAdaptation(passive, 99);
        PersonalityRules.BeginTurn(passive, foe, 1, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Count == 18, "new match can announce completion again");

        PersonalityRules.Reset(passive);
        passiveLog = new BattleLogger();
        PersonalityRules.BeginTurn(passive, foe, 1, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Count == 0, "unexperienced categories neither grow nor emit progress");
        passive.PhysicalAdaptation = 99;
        passive.MagicAdaptation = 10;
        PersonalityRules.BeginTurn(passive, foe, 2, passiveLog, passive, foe);
        Assert(passiveLog.Logs.Select(x => x.Message).SequenceEqual(new[]
            { expectedMessages[0], "魔法攻撃を解析している。。" }),
            "completion and progress can coexist without reporting unchanged categories");

        for (int scenario = 0; scenario < 4; scenario++)
        {
            var damageTarget = DamageTestMonster();
            damageTarget.Name = "被弾対象";
            var damageLog = new BattleLogger();
            foe.Stats.Accuracy = 100;
            damageTarget.Stats.Evasion = 0;
            damageTarget.IsInvincible = scenario == 1;
            if (scenario == 2) damageTarget.CurrentHp = 1;
            Executor.Execute(foe, damageTarget,
                new SkillData { SkillType = SkillType.NormalAttack, SuccessRate = scenario == 3 ? 0 : 100 },
                damageLog, foe, damageTarget);
            var progressLogs = damageLog.Logs.Where(x => x.Message.Contains("適応")).ToList();
            Assert(scenario == 0
                ? progressLogs.Count == 1 && progressLogs[0].Message == "物理攻撃の適応が始まった..."
                    && progressLogs[0].RightHp == damageTarget.CurrentHp
                : progressLogs.Count == 0,
                "skill execution logs actual adaptation gains, excluding invincible, lethal and failed hits");
        }

        var target = DamageTestMonster();
        target.Name = "全身全霊を耐えるスライム";
        var allOutLog = new BattleLogger();
        var actor = Monster();
        Executor.Execute(actor, target, new SkillData { SkillType = SkillType.AllOutAttack, SuccessRate = 100 },
            allOutLog, actor, target);
        var notifications = allOutLog.Logs.Where(x => x.Message.Contains("適応")).ToList();
        Assert(target.PhysicalAdaptation == 100 && target.MagicAdaptation == 100
            && notifications.Select(x => x.Message).SequenceEqual(expectedMessages.Take(2)),
            "all-out survival identifies physical and magic completion without numeric stats");
        PersonalityRules.OnAttackHit(target, SkillType.AllOutAttack, ElementType.Fire, allOutLog, actor, target);
        Assert(allOutLog.Logs.Count(x => x.Message.Contains("適応")) == 2, "all-out completion cannot repeat");

        var stunTarget = DamageTestMonster();
        stunTarget.Name = "スタン対象";
        stunTarget.StunAdaptation = 50;
        var failureLog = new BattleLogger();
        Executor.Execute(foe, stunTarget, new SkillData { SkillType = SkillType.StunDebuff, SuccessRate = 0 },
            failureLog, foe, stunTarget);
        Assert(failureLog.Logs.Count == 1 && failureLog.Logs[0].Message == "しかし失敗した！",
            "resisted skill failure does not reveal adaptation or success-rate stats");
    }

    private static void CheckAttackAdaptationDamage()
    {
        var logger = new BattleLogger();
        var foe = Monster();
        var adaptive = Monster(PersonalityType.Adaptive);
        adaptive.PhysicalAdaptation = 20;
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 10, logger, foe, adaptive,
            attackType: SkillType.NormalAttack) == 8, "20 percent physical adaptation reduces 10 damage to 8");
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 1, logger, foe, adaptive,
            attackType: SkillType.NormalAttack) == 1, "partial physical resistance preserves minimum one damage");
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 10, logger, foe, adaptive,
            attackType: SkillType.FireMagic) == 10, "physical resistance does not reduce magic damage");
        adaptive.PhysicalAdaptation = 0;
        adaptive.MagicAdaptation = 20;
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 10, logger, foe, adaptive,
            attackType: SkillType.FireMagic) == 8, "20 percent magic adaptation reduces 10 damage to 8");
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 1, logger, foe, adaptive,
            attackType: SkillType.FireMagic) == 1, "partial magic resistance preserves minimum one damage");
        Assert(PersonalityRules.TakeDamage(adaptive, foe, 10, logger, foe, adaptive,
            attackType: SkillType.NormalAttack) == 10, "magic resistance does not reduce physical damage");

        // 同じ乱数で比較し、会心・コピー倍率の後に軽減し、今回の被弾分は次回から効くことを確認。
        foreach (var type in new[] { SkillType.NormalAttack, SkillType.StrongAttack, SkillType.AllOutAttack,
            SkillType.FireMagic, SkillType.WaterMagic, SkillType.GrassMagic, SkillType.LightMagic, SkillType.DarkMagic })
        foreach (int adaptation in new[] { 0, 20, 100 })
        foreach (float multiplier in new[] { 1f, .8f })
        foreach (int critical in new[] { 0, 100 })
        {
            var skill = new SkillData { SkillType = type, SuccessRate = 100, Power = 12, DamageMultiplier = multiplier };
            var baseline = Monster();
            adaptive = Monster(PersonalityType.Adaptive);
            baseline.Stats.MaxHp = baseline.CurrentHp = adaptive.Stats.MaxHp = adaptive.CurrentHp = 1000;
            bool isPhysical = type == SkillType.NormalAttack || type == SkillType.StrongAttack || type == SkillType.AllOutAttack;
            if (isPhysical) adaptive.PhysicalAdaptation = adaptation;
            else adaptive.MagicAdaptation = adaptation;
            foe.Stats.CriticalRate = critical;
            foe.CurrentHp = 100;
            Random.InitState(172026);
            Executor.Execute(foe, baseline, skill, logger, foe, baseline);
            int baseDamage = 1000 - baseline.CurrentHp;
            foe.CurrentHp = 100;
            Random.InitState(172026);
            Executor.Execute(foe, adaptive, skill, logger, foe, adaptive);
            int expected = adaptation == 100 ? 0 : Mathf.Max(1, Mathf.RoundToInt(baseDamage * (100 - adaptation) / 100f));
            Assert(1000 - adaptive.CurrentHp == expected, "damage reduction after critical and copy: " + type);
            Assert(PersonalityRules.GetSuccessRateReduction(adaptive, type) == 0, "attack adaptation never changes success rate");
            int growth = isPhysical ? (type == SkillType.NormalAttack ? 5 : 10)
                : type == SkillType.WaterMagic ? 5 : type == SkillType.GrassMagic ? 20 : 10;
            int actualAdaptation = isPhysical ? adaptive.PhysicalAdaptation : adaptive.MagicAdaptation;
            Assert(actualAdaptation == (type == SkillType.AllOutAttack && expected > 0 ? 100
                : Mathf.Min(100, adaptation + (expected > 0 ? growth : 0))),
                "adaptation grows only after positive damage");
            Assert((isPhysical ? adaptive.MagicAdaptation : adaptive.PhysicalAdaptation)
                == (type == SkillType.AllOutAttack && expected > 0 ? 100 : 0),
                "only surviving all-out damage also completes magic adaptation");
            Assert(adaptive.DamageTakenCount == (expected > 0 ? 1 : 0), "full resistance prevents damage events");
        }

        // 強撃は物理適応20％でも元の65％で成功する（成功率45％にはならない）。
        adaptive = Monster(PersonalityType.Adaptive);
        adaptive.PhysicalAdaptation = 20;
        foe.Stats.CriticalRate = 0;
        SetNextSuccessRoll(.5f, .6f);
        Executor.Execute(foe, adaptive, MonsterFactory.CreateStrongAttack(), logger, foe, adaptive);
        Assert(adaptive.CurrentHp < 100 && adaptive.PhysicalAdaptation == 30, "strong attack retains original success rate");

        // 魔法は適応20％でも元の90％で成功する（成功率70％にはならない）。
        adaptive = Monster(PersonalityType.Adaptive);
        adaptive.MagicAdaptation = 20;
        SetNextSuccessRoll(.75f, .8f);
        Executor.Execute(foe, adaptive, MonsterFactory.CreateElementSkill(ElementType.Water), logger, foe, adaptive);
        Assert(adaptive.CurrentHp < 100 && adaptive.MagicAdaptation == 25, "magic retains original success rate");
    }

    // 成功判定に使う次の乱数を範囲内に固定し、運で検証結果が変わることを防ぐ。
    private static void SetNextSuccessRoll(float minimum, float maximum)
    {
        for (int seed = 0; seed < 10000; seed++)
        {
            Random.InitState(seed);
            var state = Random.state;
            float roll = Random.value;
            if (roll < minimum || roll >= maximum) continue;
            Random.state = state;
            return;
        }
        throw new Exception("Could not find a deterministic success roll");
    }

    private static void CheckObsession()
    {
        var obsession = Monster(PersonalityType.Obsessive);
        var foe = Monster();
        var logger = new BattleLogger();
        var status = new StatusSystem();
        var poison = new SkillData
        {
            SkillName = "毒霧",
            SkillType = SkillType.PoisonDebuff,
            Power = 0,
            SuccessRate = 100
        };
        var stun = new SkillData
        {
            SkillName = "スタン",
            SkillType = SkillType.StunDebuff,
            Power = 0,
            SuccessRate = 100
        };

        // 実際の技付与から追い、付与直後と次ターンの境界・HPの不変を確認する。
        obsession.CurrentHp = 37;
        PersonalityRules.BeginTurn(obsession, foe, 1, logger, obsession, foe);
        Executor.Execute(foe, obsession, poison, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20, "obsession waits until next turn after poison");
        PersonalityRules.BeginTurn(obsession, foe, 2, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 40, "obsession doubles attack on following turn");
        Assert(obsession.CurrentHp == 37 && obsession.GetCurrentMaxHp() == 100
            && obsession.GetCurrentDefense() == 10 && obsession.GetCurrentSpeed() == 10
            && obsession.GetCurrentAccuracy() == 80, "obsession changes attack only, including wounded HP");
        Executor.Execute(foe, obsession, poison, logger, obsession, foe);
        PersonalityRules.BeginTurn(obsession, foe, 3, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 40, "poison reapplication neither postpones nor stacks obsession");
        obsession.StatusAilment = StatusAilmentType.None;
        PersonalityRules.Refresh(obsession, foe, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20 && obsession.CurrentHp == 37, "recovery ends attack buff without changing HP");
        obsession.DefenseBuff = -5;
        PersonalityRules.BeginTurn(obsession, foe, 4, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20, "stat reductions alone do not trigger obsession");
        obsession.IsSkillLocked = true;
        obsession.AttackBuff = 5;
        PersonalityRules.Refresh(obsession, foe, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 25, "skill seal also waits one turn");
        PersonalityRules.BeginTurn(obsession, foe, 5, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 50, "skill seal doubles current attack including attack buffs");
        PersonalityRules.Reset(obsession);
        Assert(obsession.GetCurrentAttack() == 20 && obsession.ObsessionStartTurn == 0
            && obsession.ObsessionStunRecoveryTurn == 0, "new match clears all obsession state");

        // 自分の行動前にスタンを受けた場合、休んだ次の1ターンだけ反撃する。
        PersonalityRules.BeginTurn(obsession, foe, 1, logger, obsession, foe);
        Executor.Execute(foe, obsession, stun, logger, obsession, foe);
        Assert(!status.CanAct(obsession), "stun before action consumes that action");
        PersonalityRules.Refresh(obsession, foe, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20, "stun recovery does not immediately double attack");
        PersonalityRules.BeginTurn(obsession, foe, 2, logger, obsession, foe);
        Assert(status.CanAct(obsession) && obsession.GetCurrentAttack() == 40
            && obsession.GetCurrentMaxHp() == 100, "turn after stun has doubled attack and normal HP");
        PersonalityRules.BeginTurn(obsession, foe, 3, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20, "stun bonus expires after one turn");

        // 自分の行動後に受けたスタンは、次の行動を休み、その次のターンに反撃する。
        PersonalityRules.Reset(obsession);
        PersonalityRules.BeginTurn(obsession, foe, 1, logger, obsession, foe);
        Assert(status.CanAct(obsession), "first action finishes before late stun");
        Executor.Execute(foe, obsession, stun, logger, obsession, foe);
        PersonalityRules.BeginTurn(obsession, foe, 2, logger, obsession, foe);
        Assert(!status.CanAct(obsession), "late stun consumes following turn action");
        PersonalityRules.Refresh(obsession, foe, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20, "no attack bonus on skipped turn");
        PersonalityRules.BeginTurn(obsession, foe, 3, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 40, "late stun grants bonus on actual recovery turn");
        PersonalityRules.BeginTurn(obsession, foe, 4, logger, obsession, foe);
        Assert(obsession.GetCurrentAttack() == 20, "late stun bonus also lasts one turn");
    }

    private static void CheckSkills()
    {
        var foe = Monster();
        foe.Stats.MaxHp = foe.CurrentHp = 1000;
        var logger = new BattleLogger();
        var devotion = Monster(PersonalityType.Devoted);
        Executor.Execute(devotion, foe, new SkillData
        {
            SkillName = "全身全霊",
            SkillType = SkillType.AllOutAttack,
            Power = 0,
            SuccessRate = 100
        }, logger, devotion, foe);
        Assert(devotion.CurrentHp == 1 && foe.CurrentHp < 900, "devotion uses HP as attack energy");

        var sincere = Monster(PersonalityType.Sincere);
        sincere.Skills[0] = new SkillData
        {
            SkillName = "鍛錬",
            SkillType = SkillType.AttackBuff,
            Power = 0,
            SuccessRate = 100
        };
        Assert(Ai.ChoosePersonalitySkill(sincere, foe, 1).SkillType == SkillType.AttackBuff, "sincere buffs above half");
        sincere.CurrentHp = 50;
        Assert(Ai.ChoosePersonalitySkill(sincere, foe, 2).SkillType == SkillType.StrongAttack, "sincere attacks at half");

        var headlong = Monster(PersonalityType.Headlong);
        Assert(Ai.ChoosePersonalitySkill(headlong, foe, 1).SkillType == SkillType.StrongAttack, "headlong attack phase");
        Assert(Ai.ChoosePersonalitySkill(headlong, foe, 3).SkillType == SkillType.DebuffCombo, "headlong debuff phase");
        Assert(Ai.ChoosePersonalitySkill(headlong, foe, 5).SkillType == SkillType.DefenseBuff, "headlong defense phase");
        Assert(Ai.ChoosePersonalitySkill(headlong, foe, 7).SkillType == SkillType.StrongAttack, "headlong repeats");

        var insidious = Monster(PersonalityType.Insidious);
        insidious.Skills[1] = new SkillData
        {
            SkillName = "絡め手",
            SkillType = SkillType.DebuffCombo,
            Power = 0,
            SuccessRate = 100
        };
        for (int i = 0; i < 4; i++) Executor.Execute(insidious, foe, Ai.ChoosePersonalitySkill(insidious, foe, i + 1), logger, insidious, foe);
        Assert(foe.StatusAilment == StatusAilmentType.Poison && foe.DefenseBuff == -10 && foe.IsSkillLocked, "debuff sequence");
        Assert(Ai.ChoosePersonalitySkill(insidious, foe, 5).SkillType == SkillType.NormalAttack, "attack after weakening");
        foe = Monster();
        foe.DebuffImmune = true;
        Executor.Execute(insidious, foe, insidious.Skills[1], logger, insidious, foe);
        Assert(insidious.DebuffRejected && Ai.ChoosePersonalitySkill(insidious, foe, 1).SkillType == SkillType.NormalAttack, "recognizes immunity");
        Assert(foe.StatusAilment == StatusAilmentType.None, "immune target has no poison");

        var flexible = Monster(PersonalityType.Flexible);
        var fallback = Ai.ChoosePersonalitySkill(flexible, foe, 1);
        Assert(fallback.SkillType == SkillType.NormalAttack || fallback.SkillType == SkillType.StrongAttack
            || fallback.SkillType == SkillType.FireMagic, "mimic without history uses a common attack");
        Ai.RecordAction(foe, new SkillData
        {
            SkillName = "光魔法",
            SkillType = SkillType.LightMagic,
            Power = 10,
            SuccessRate = 90
        });
        var copied = Ai.ChoosePersonalitySkill(flexible, foe, 2);
        Assert(copied.SkillType == SkillType.LightMagic && copied.Power == 10 && copied.SuccessRate == 90 && !ReferenceEquals(copied, foe.LastUsedSkill), "mimic unowned skill safely");

        var impatient = Monster(PersonalityType.Impatient);
        impatient.Skills[0] = new SkillData
        {
            SkillName = "我慢",
            SkillType = SkillType.Wait,
            Power = 0,
            SuccessRate = 100
        };
        int attacks = 0;
        for (int i = 0; i < 2000; i++) if (Ai.ChoosePersonalitySkill(impatient, foe, 1).SkillType == SkillType.StrongAttack) attacks++;
        Assert(attacks > 580 && attacks < 820, "impatient attacks approximately 35 percent");

        var explorer = Monster(PersonalityType.Explorer);
        for (int i = 0; i < 100; i++)
        {
            foe.CurrentHp = 100000;
            Executor.Execute(explorer, foe, new SkillData
            {
                SkillName = "発掘",
                SkillType = SkillType.Dig,
                Power = 0,
                SuccessRate = 100
            }, logger, explorer, foe);
        }
        foreach (string item in new[] { "回復薬", "攻撃増強薬", "防御増強薬", "爆弾", "石ころ" })
            Assert(logger.Logs.Any(log => log.Message.Contains(item)), "exploration produces " + item);

        var delusional = Monster(PersonalityType.Delusional);
        int before = foe.CurrentHp;
        Executor.Execute(delusional, foe, new SkillData
        {
            SkillName = "自称",
            SkillType = SkillType.Delusion,
            Power = 0,
            SuccessRate = 100
        }, logger, delusional, foe);
        Assert(foe.CurrentHp == before && delusional.AttackBuff == 0, "delusion has no effect");
        delusional.IsSkillLocked = true;
        Assert(Ai.ChooseSkill(delusional, foe, 1).SkillType == SkillType.Wait, "sealed delusional waits without attacking");

        // 2.5%を整数乱数で3%へ丸めない。内部に追加の即死抽選も置かない。
        int instantKills = 0;
        var chanceLogger = new BattleLogger();
        var instant = new SkillData
        {
            SkillName = "即死",
            SkillType = SkillType.InstantDeath,
            Power = 0,
            SuccessRate = 2.5
        };
        for (int i = 0; i < 20000; i++)
        {
            foe.CurrentHp = 100;
            Executor.Execute(explorer, foe, instant, chanceLogger, explorer, foe);
            if (foe.CurrentHp == 0) instantKills++;
        }
        Assert(instantKills > 400 && instantKills < 580, "fractional instant-death chance approximately 2.5 percent");

        // 5属性の魔法が実際に効果を持ち、魔法用の計算が適用されることを確認。
        var magics = new[] { SkillType.FireMagic, SkillType.WaterMagic, SkillType.GrassMagic, SkillType.LightMagic, SkillType.DarkMagic };
        for (int element = 0; element < 5; element++)
        {
            var mage = Monster();
            var target = Monster(element: ElementType.Dark);
            Random.InitState(2026);
            int expectedDamage = new DamageCalculator().CalculateDamage(mage, target, 10, (ElementType)element, magics[element]);
            Random.InitState(2026);
            Executor.Execute(mage, target, new SkillData
            {
                SkillName = "魔法",
                SkillType = magics[element],
                Power = 10,
                SuccessRate = 100
            }, logger, mage, target);
            Assert(target.CurrentHp == 100 - expectedDamage, "magic applies expected element: " + element);
        }
    }

    private static void CheckGenerationAndMatches()
    {
        var factory = new MonsterFactory();
        var catalog = new Dictionary<PersonalityType, MonsterData>();
        var elements = new HashSet<ElementType>();
        for (int i = 0; i < 2000; i++)
        {
            var story = factory.CreateRandom("story", ModeType.story);
            Assert((int)story.Personality <= 5 && (int)story.Element <= 2, "story restrictions");
            var ranked = factory.CreateRandom("ranked", ModeType.ranking);
            catalog[ranked.Personality] = ranked;
            elements.Add(ranked.Element);
            Assert(ranked.Skills.Count == 2 && ranked.Skills.All(s => s != null), "two valid skills");
            if (ranked.Personality == PersonalityType.Delusional || ranked.Personality == PersonalityType.Inept)
            {
                var stats = ranked.Stats;
                Assert(stats.MaxHp >= 45 && stats.MaxHp <= 90 && stats.Attack >= 8 && stats.Attack <= 25
                    && stats.Defense >= 8 && stats.Defense <= 25 && stats.Growth >= 5 && stats.Growth <= 100
                    && stats.Speed >= 5 && stats.Speed <= 30 && stats.Accuracy >= 5 && stats.Accuracy <= 30
                    && stats.Evasion >= 5 && stats.Evasion <= 30 && stats.CriticalRate >= 5 && stats.CriticalRate <= 20,
                    "normal starting stats for " + ranked.Personality);
            }
        }
        Assert(catalog.Count == 20 && elements.Count == 5, "all ranked personalities and elements spawn");
        foreach (var personality in catalog.Keys)
            Assert(PersonalityNames.Get(personality) != "不明", "display name for " + personality);

        CheckActionMix(catalog);
        CheckActionMotions(catalog);

        // 猛進が別のスライムと同じ技名・威力・成功率を使い、個体間で技データを共有しないことを確認する。
        var headlong = catalog[PersonalityType.Headlong];
        var opponent = catalog[PersonalityType.Bold];
        AssertSameSkill(Ai.ChoosePersonalitySkill(headlong, opponent, 1), opponent.Skills[1], "headlong reuses bold strong attack");
        AssertSameSkill(Ai.ChoosePersonalitySkill(headlong, opponent, 3), catalog[PersonalityType.Insidious].Skills[1], "headlong reuses insidious debuff");
        AssertSameSkill(Ai.ChoosePersonalitySkill(headlong, opponent, 5), catalog[PersonalityType.Calm].Skills[0], "headlong reuses calm defense");
        headlong.CurrentHp--;
        AssertSameSkill(Ai.ChoosePersonalitySkill(headlong, opponent, 6), catalog[PersonalityType.Timid].Skills[0], "headlong reuses timid heal");

        var simulator = new BattleSimulator();
        foreach (var a in catalog.Values)
            foreach (var b in catalog.Values)
            {
                // 同じ性格同士でも個体データを共有させない。
                var left = Clone(a);
                var right = Clone(b);
                var result = simulator.Simulate(left, right, new BetData { Amount = 10, PayoutMultiplier = 2, Side = PredictionSide.Left }, ModeType.ranking);
                Assert(result.BattleLogs.Count > 0 && result.BattleLogs.Count < 2000, "bounded match " + a.Personality + "/" + b.Personality);
                Assert(left.CurrentHp >= 0 && right.CurrentHp >= 0 && left.CurrentHp <= left.GetCurrentMaxHp() && right.CurrentHp <= right.GetCurrentMaxHp(), "valid final HP");
                Assert(result.RewardCoin == (result.IsPredictionSuccess
                    ? BattleSimulator.CalculateReward(10, result.FinalPayoutMultiplier) : 0), "event-adjusted payout");
            }
        var harmless = Monster(PersonalityType.Delusional);
        harmless.Skills[1] = new SkillData
        {
            SkillName = "自称",
            SkillType = SkillType.Delusion,
            Power = 0,
            SuccessRate = 100
        };
        var other = Clone(harmless);
        harmless.Stats.MaxHp = other.Stats.MaxHp = 10000;
        harmless.DebuffImmune = other.DebuffImmune = true;
        var timedOut = simulator.Simulate(harmless, other, null, ModeType.ranking);
        Assert(timedOut.BattleLogs.Any(log => log.Message.Contains("40ターン経過")), "stalemate terminates");
        var firstLogs = timedOut.BattleLogs;
        int firstCount = firstLogs.Count;
        simulator.Simulate(Clone(harmless), Clone(other), null, ModeType.ranking);
        Assert(firstLogs.Count == firstCount, "reusing simulator preserves previous result logs");
    }

    private static void AssertSameSkill(SkillData actual, SkillData expected, string message)
    {
        Assert(actual.SkillName == expected.SkillName && actual.SkillType == expected.SkillType
            && actual.Power == expected.Power && actual.SuccessRate == expected.SuccessRate
            && !ReferenceEquals(actual, expected), message);
    }

    private static void CheckActionMix(Dictionary<PersonalityType, MonsterData> catalog)
    {
        var foe = Monster();
        Ai.RecordAction(foe, MonsterFactory.CreateStrongAttack());
        var elements = new[] { ElementType.Fire, ElementType.Water, ElementType.Grass, ElementType.Light, ElementType.Dark };
        foreach (var source in catalog.Values)
        {
            var attacker = Clone(source);
            var originalSkills = attacker.Skills.ToArray();
            var routes = new int[3];
            // 無能はここでは覚醒後の基本抽選を検証し、覚醒前は別途100%通常攻撃を確認する。
            attacker.Awakened = attacker.Personality == PersonalityType.Inept;
            bool specialOnly = attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional;
            for (int i = 0; i < 2000; i++)
            {
                attacker.Element = elements[i % elements.Length];
                attacker.CurrentHp = i % 2 == 0 ? attacker.GetCurrentMaxHp() : 1;
                int turn = i % 12 + (attacker.Awakened ? 10 : 1);
                var before = Random.state;
                int roll = Random.Range(0, 100);
                SkillData expected;
                int route;
                if (specialOnly)
                {
                    route = 0;
                    expected = Ai.ChoosePersonalitySkill(attacker, foe, turn);
                }
                else if (attacker.Personality == PersonalityType.Bold)
                {
                    route = roll < 70 ? 0 : 1;
                    expected = route == 0 ? MonsterFactory.CreateStrongAttack() : MonsterFactory.CreateNormalAttack();
                }
                else if (roll < 70)
                {
                    route = 0;
                    expected = Ai.ChoosePersonalitySkill(attacker, foe, turn);
                }
                else if (roll < 85)
                {
                    route = 1;
                    expected = MonsterFactory.CreateNormalAttack();
                }
                else
                {
                    route = 2;
                    expected = MonsterFactory.CreateElementSkill(attacker.Element);
                }
                Random.state = before;
                SkillData actual = Ai.ChooseSkill(attacker, foe, turn);
                routes[route]++;
                Assert(actual.SkillName == expected.SkillName && actual.SkillType == expected.SkillType
                    && actual.Element == expected.Element && actual.Power == expected.Power
                    && actual.SuccessRate == expected.SuccessRate && actual.DamageMultiplier == expected.DamageMultiplier,
                    "70/15/15 action selection: " + source.Personality);
                if (route != 0)
                    Assert(!attacker.Skills.Contains(actual), "common attacks are created without replacing owned skills");
                // 探求の70%枠には追加の抽選を重ねず、必ず発掘を選ぶ。
                if (source.Personality == PersonalityType.Explorer && route == 0)
                    Assert(actual.SkillType == SkillType.Dig, "explorer intrinsic slot always digs");
            }
            if (specialOnly)
                Assert(routes[0] == 2000 && routes[1] == 0 && routes[2] == 0, "special-only personalities never choose common attacks");
            else if (attacker.Personality == PersonalityType.Bold)
                Assert(routes[0] > 1250 && routes[0] < 1550 && routes[1] > 450 && routes[1] < 750 && routes[2] == 0,
                    "bold chooses strong attack 70%, normal 30%, no magic");
            else
                Assert(routes[0] > 1250 && routes[0] < 1550 && routes[1] > 200 && routes[1] < 400
                    && routes[2] > 200 && routes[2] < 400, "base action routes are intrinsic 70%, normal 15%, magic 15%");
            Assert(attacker.Skills.SequenceEqual(originalSkills), "action selection preserves the status-screen skill list");
            if (attacker.Personality == PersonalityType.Inept)
            {
                attacker.Awakened = false;
                for (int turn = 1; turn < PersonalityRules.AwakeningTurn; turn++)
                    for (int sample = 0; sample < 100; sample++)
                        Assert(Ai.ChooseSkill(attacker, foe, turn).SkillType == SkillType.NormalAttack,
                            "inept uses only normal attacks before awakening");
            }
            attacker.IsSkillLocked = true;
            for (int i = 0; i < 20; i++)
                Assert(Ai.ChooseSkill(attacker, foe, i + 1).SkillType == (specialOnly ? SkillType.Wait : SkillType.NormalAttack),
                    "seal suppresses skills without granting attacks to special-only personalities");
            if (specialOnly)
            {
                var logger = new BattleLogger();
                int before = foe.CurrentHp;
                Executor.Execute(attacker, foe, Ai.ChooseSkill(attacker, foe, 1), logger, attacker, foe);
                Assert(foe.CurrentHp == before && logger.Logs.Any(x => x.Message.Contains("我慢")), "sealed special-only actor can wait without damage");
                attacker.IsSkillLocked = false;
                attacker.Skills.Clear();
                Assert(Ai.ChooseSkill(attacker, foe, 1).SkillType == SkillType.Wait
                    && ActionAI.ChooseFallbackAttack(attacker).SkillType == SkillType.Wait, "missing special skills never fall back to attacks");
            }
        }
    }

    private static void CheckActionMotions(Dictionary<PersonalityType, MonsterData> catalog)
    {
        var simulator = new BattleSimulator();
        foreach (var personality in new[] { PersonalityType.Delusional, PersonalityType.Aggressive })
            foreach (bool isLeft in new[] { true, false })
            {
                var selected = new HashSet<SkillType>();
                for (int seed = 0; seed < 8; seed++)
                {
                    var actor = Clone(catalog[personality]);
                    var opponent = Clone(catalog[PersonalityType.Bold]);
                    foreach (var monster in new[] { actor, opponent })
                    {
                        monster.Element = ElementType.Fire;
                        monster.Stats.MaxHp = 10000;
                        monster.Stats.Attack = 5;
                        monster.Stats.Defense = 500;
                    }
                    Random.InitState(seed);
                    var result = simulator.Simulate(isLeft ? actor : opponent, isLeft ? opponent : actor, null, ModeType.ranking);
                    foreach (var log in result.BattleLogs.Where(x => x.IsAction))
                    {
                        if (log.IsLeftAction == isLeft)
                        {
                            selected.Add(log.UsedSkillType);
                            Assert(!log.PlayMotion, "delusional and aggressive suppress motions for all their actions on either side");
                            Assert(log.UsedSkillType == SkillType.Wait || log.UsedSkillType ==
                                (personality == PersonalityType.Aggressive ? SkillType.InstantDeath : SkillType.Delusion),
                                "special-only actors never use normal, strong or magic attacks");
                        }
                        else
                            Assert(log.PlayMotion, "other actors keep attack and hit motions");
                    }
                }
                Assert(selected.Contains(personality == PersonalityType.Aggressive ? SkillType.InstantDeath : SkillType.Delusion),
                    "special-only actors still use their own special skill");
            }
    }

    private static void CheckGrassWaterDirection()
    {
        foreach (bool grassOnLeft in new[] { true, false })
            foreach (var type in new[] { SkillType.NormalAttack, SkillType.StrongAttack, SkillType.GrassMagic })
                for (int seed = 0; seed < 32; seed++)
                {
                    var grass = Monster(element: ElementType.Grass);
                    var water = Monster(element: ElementType.Water);
                    var left = grassOnLeft ? grass : water;
                    var right = grassOnLeft ? water : grass;
                    var grassSkill = type == SkillType.GrassMagic ? MonsterFactory.CreateElementSkill(ElementType.Grass)
                        : type == SkillType.StrongAttack ? MonsterFactory.CreateStrongAttack() : MonsterFactory.CreateNormalAttack();
                    var waterSkill = type == SkillType.GrassMagic ? MonsterFactory.CreateElementSkill(ElementType.Water)
                        : type == SkillType.StrongAttack ? MonsterFactory.CreateStrongAttack() : MonsterFactory.CreateNormalAttack();
                    grassSkill.SuccessRate = waterSkill.SuccessRate = 100;
                    Random.InitState(seed);
                    var grassLogger = new BattleLogger();
                    Executor.Execute(grass, water, grassSkill, grassLogger, left, right);
                    int grassDamage = 100 - water.CurrentHp;
                    water.CurrentHp = 100;
                    Random.InitState(seed);
                    var waterLogger = new BattleLogger();
                    Executor.Execute(water, grass, waterSkill, waterLogger, left, right);
                    int waterDamage = 100 - grass.CurrentHp;
                    Assert(grassDamage > waterDamage, "grass deals more damage to water with identical stats, skill and RNG on either side");
                    Assert((grassOnLeft ? grassLogger.Logs.Last().RightHp : grassLogger.Logs.Last().LeftHp) == 100 - grassDamage,
                        "grass hit updates water HP on the correct display side");
                    Assert((grassOnLeft ? waterLogger.Logs.Last().LeftHp : waterLogger.Logs.Last().RightHp) == 100 - waterDamage,
                        "water hit updates grass HP on the correct display side");
                }

        var favored = Monster(element: ElementType.Grass);
        var unfavored = Monster(element: ElementType.Water);
        Assert(BetOddsCalculator.CalculatePayoutMultiplier(favored, unfavored, PredictionSide.Left)
            < BetOddsCalculator.CalculatePayoutMultiplier(favored, unfavored, PredictionSide.Right), "grass has lower payout than equally strong water");
        Assert(BetOddsCalculator.CalculatePayoutMultiplier(unfavored, favored, PredictionSide.Right)
            < BetOddsCalculator.CalculatePayoutMultiplier(unfavored, favored, PredictionSide.Left), "grass odds remain favored after swapping sides");

        const int trials = 256;
        int grassLeftWins = 0, grassRightWins = 0;
        var simulator = new BattleSimulator();
        for (int seed = 0; seed < trials; seed++)
            foreach (bool grassOnLeft in new[] { true, false })
            {
                var grass = Monster(element: ElementType.Grass);
                var water = Monster(element: ElementType.Water);
                Random.InitState(seed);
                var result = simulator.Simulate(grassOnLeft ? grass : water, grassOnLeft ? water : grass, null, ModeType.ranking);
                bool grassWon = result.WinnerSide == (grassOnLeft ? PredictionSide.Left : PredictionSide.Right);
                if (grassWon)
                {
                    if (grassOnLeft) grassLeftWins++;
                    else grassRightWins++;
                }
            }
        Assert(grassLeftWins > trials * .6f && grassRightWins > trials * .6f, "grass is favored in equal-stat matches on either side");
        Debug.Log($"GRASS_WATER_CHECKS: grass wins {grassLeftWins}/{trials} on left, {grassRightWins}/{trials} on right (equal stats/personality).");
    }

    private static MonsterData Clone(MonsterData source)
    {
        return JsonUtility.FromJson<MonsterData>(JsonUtility.ToJson(source));
    }
}
