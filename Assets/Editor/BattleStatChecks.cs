using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>先手・成長・命中回避を、実際の戦闘処理とUnity乱数で検証する。</summary>
public static class BattleStatChecks
{
    private static int assertions;

    [MenuItem("Tools/UnderSide/Check Battle Stats")]
    public static void Run()
    {
        var previous = Random.state;
        bool logging = Debug.unityLogger.logEnabled;
        assertions = 0;
        try
        {
            Random.InitState(20261022);
            CheckOrder();
            CheckGrowthIntegration();
            // 大量の成長抽選はConsoleを埋めない。単独の統合確認ではDebug.Logも検証済み。
            Debug.unityLogger.logEnabled = false;
            CheckGrowthDistribution();
            CheckHitRules();
            CheckGeneration();
        }
        finally
        {
            Random.state = previous;
            Debug.unityLogger.logEnabled = logging;
        }
        Debug.Log($"BATTLE_STAT_CHECKS_PASSED: {assertions} assertions.");
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Battle stat check failed: " + message);
    }

    private static MonsterData Monster(string name = "test") => new MonsterData
    {
        Name = name, Personality = PersonalityType.Inept, Element = ElementType.Fire,
        Stats = new MonsterStats { MaxHp = 100, Attack = 20, Defense = 10, Speed = 10 },
        CurrentHp = 100
    };

    private static int[] CurrentStats(MonsterData monster) => new[]
    {
        monster.GetCurrentAttack(), monster.GetCurrentDefense(), monster.GetCurrentSpeed(),
        monster.GetCurrentEvasion(), monster.GetCurrentAccuracy()
    };

    private static void CheckOrder()
    {
        var left = Monster("left");
        var right = Monster("right");
        right.Stats.Speed = 20;
        Assert(BattleStatRules.SelectFirstActor(left, right) == right, "faster right acts first");
        left.GrowthSpeedBonus = 11;
        Assert(BattleStatRules.SelectFirstActor(left, right) == left, "growth can reverse next turn order");
        left.GrowthSpeedBonus = 0;
        left.PersonalityMultiplier = 3;
        Assert(BattleStatRules.SelectFirstActor(left, right) == left, "current personality multiplier affects order");
        left.PersonalityMultiplier = 1;
        left.StoicStatMultiplier = 3;
        Assert(BattleStatRules.SelectFirstActor(left, right) == left, "stoic speed affects order");
        left.StoicStatMultiplier = 1;
        right.Stats.Speed = 10;
        int leftFirst = 0;
        for (int i = 0; i < 1000; i++)
            if (BattleStatRules.SelectFirstActor(left, right) == left) leftFirst++;
        Assert(leftFirst > 430 && leftFirst < 570, "equal speed has no fixed side preference");

        for (int fasterSide = 0; fasterSide < 2; fasterSide++)
        {
            left = Monster("left");
            right = Monster("right");
            left.Stats.Attack = right.Stats.Attack = 1000;
            left.Stats.Growth = right.Stats.Growth = 100;
            (fasterSide == 0 ? left : right).Stats.Speed = 20;
            var result = new BattleSimulator().Simulate(left, right, null, ModeType.ranking);
            var actions = result.BattleLogs.Where(log => log.IsAction).ToList();
            Assert(actions.Count == 1 && actions[0].IsLeftAction == (fasterSide == 0), "faster actor kills before retaliation");
            Assert(result.WinnerSide == (fasterSide == 0 ? PredictionSide.Left : PredictionSide.Right), "winner side stays correct");
            Assert(left.GrowthAttackBonus == 0 && right.GrowthAttackBonus == 0, "no growth at match start or after knockout");
            var last = result.BattleLogs.Last();
            Assert(last.LeftHp == left.CurrentHp && last.RightHp == right.CurrentHp, "HP log retains screen sides");
        }
    }

    private static void CheckGrowthIntegration()
    {
        var left = Monster("growth-left");
        var right = Monster("growth-right");
        foreach (var monster in new[] { left, right })
        {
            monster.Personality = PersonalityType.Delusional;
            monster.Stats.MaxHp = 100000;
            monster.Stats.Growth = 100;
            monster.DebuffImmune = true;
            monster.Skills.Add(new SkillData { SkillName = "自称", SkillType = SkillType.Delusion, SuccessRate = 100 });
        }
        var debugMessages = new List<string>();
        Application.LogCallback capture = (message, stack, type) =>
        {
            if (message.StartsWith("[成長]")) debugMessages.Add(message);
        };
        Application.logMessageReceived += capture;
        MatchResultData result;
        try { result = new BattleSimulator().Simulate(left, right, null, ModeType.ranking); }
        finally { Application.logMessageReceived -= capture; }
        Assert(debugMessages.Count == BattleSimulator.MaxTurns * 2, "one growth per survivor per completed turn at 100%");
        Assert(debugMessages.Count(message => message.StartsWith("[成長] Turn 1 /")) == 2, "first growth follows turn one");
        Assert(debugMessages.All(message => message.Contains("→") && message.Contains("成長率100%")), "Debug.Log shows rate and before/after stats");
        Assert(result.BattleLogs.All(log => !log.Message.Contains("成長")), "growth is absent from game battle log");
        Assert(left.Stats.Attack == 20 && left.Stats.Defense == 10 && left.Stats.Speed == 10
            && left.Stats.Accuracy == 0 && left.Stats.Evasion == 0, "growth never changes base stats");
        Assert(left.GrowthAttackBonus > 0 && left.GrowthDefenseBonus > 0 && left.GrowthSpeedBonus > 0, "growth is applied during simulation");
        PersonalityRules.Reset(left);
        Assert(left.GrowthAttackBonus == 0 && left.GrowthDefenseBonus == 0 && left.GrowthSpeedBonus == 0
            && left.GrowthAccuracyBonus == 0 && left.GrowthEvasionBonus == 0, "new match resets every growth bonus");
    }

    private static void CheckGrowthDistribution()
    {
        var monster = Monster();
        monster.Stats.Growth = -10;
        Assert(monster.GetCurrentGrowth() == 0, "negative growth clamps to zero");
        for (int i = 0; i < 100; i++) BattleStatRules.ApplyEndTurnGrowth(monster, i + 1);
        Assert(CurrentStats(monster).SequenceEqual(new[] { 20, 10, 10, 0, 0 }), "zero growth never increases stats");
        monster.Stats.Growth = 1000;
        Assert(monster.GetCurrentGrowth() == 100, "growth is capped at 100%");
        monster.CurrentHp = 0;
        BattleStatRules.ApplyEndTurnGrowth(monster, 1);
        Assert(monster.GrowthAttackBonus == 0, "defeated monsters cannot grow");

        var counts = new HashSet<int>();
        var amounts = new HashSet<int>();
        for (int i = 0; i < 500; i++)
        {
            PersonalityRules.Reset(monster);
            monster.PersonalityMultiplier = 2;
            monster.PersonalityAttackMultiplier = 2;
            int[] before = CurrentStats(monster);
            BattleStatRules.ApplyEndTurnGrowth(monster, 1);
            int[] delta = CurrentStats(monster).Zip(before, (after, prior) => after - prior).ToArray();
            Assert(delta.All(value => value >= 0 && value <= 5), "each selected stat grows at most five, even with personality multipliers");
            int changed = delta.Count(value => value > 0);
            Assert(changed >= 1 && changed <= 5, "one to five distinct stats grow");
            counts.Add(changed);
            foreach (int value in delta.Where(value => value > 0)) amounts.Add(value);
        }
        Assert(counts.Count == 5 && amounts.Count == 5, "all subset sizes and increments are reachable");

        int successes = 0;
        monster.Stats.Growth = 30;
        for (int i = 0; i < 4000; i++)
        {
            PersonalityRules.Reset(monster);
            BattleStatRules.ApplyEndTurnGrowth(monster, 1);
            if (monster.GrowthAttackBonus + monster.GrowthDefenseBonus + monster.GrowthSpeedBonus
                + monster.GrowthEvasionBonus + monster.GrowthAccuracyBonus > 0) successes++;
        }
        Assert(successes > 1080 && successes < 1320, "growth 30 produces approximately 30% successes");
        monster.Stats.Growth = 100;
        monster.Stats.Accuracy = monster.Stats.Evasion = 99;
        for (int i = 0; i < 30; i++) BattleStatRules.ApplyEndTurnGrowth(monster, i + 1);
        Assert(monster.GetCurrentAccuracy() == 100 && monster.GetCurrentEvasion() == 100, "grown accuracy/evasion cap at 100, not initial 30");
    }

    private static void CheckHitRules()
    {
        var actor = Monster();
        var target = Monster();
        actor.Stats.Accuracy = 10;
        target.Stats.Evasion = 30;
        Assert(BattleStatRules.GetHitChance(actor, target) == 80, "10 accuracy versus 30 evasion gives 80%");
        actor.Stats.Accuracy = 30;
        Assert(BattleStatRules.GetHitChance(actor, target) == 100, "matching accuracy cancels evasion");
        target.Stats.Evasion = 0;
        Assert(BattleStatRules.GetHitChance(actor, target) == 100, "hit chance never exceeds 100%");

        var executor = new SkillExecutor();
        var logger = new BattleLogger();
        foreach (SkillType type in new[] { SkillType.NormalAttack, SkillType.StrongAttack, SkillType.FireMagic,
            SkillType.WaterMagic, SkillType.GrassMagic, SkillType.LightMagic, SkillType.DarkMagic,
            SkillType.AllOutAttack, SkillType.PoisonDebuff, SkillType.StunDebuff, SkillType.SkillSealDebuff,
            SkillType.DefenseDown, SkillType.DebuffCombo, SkillType.InstantDeath })
        {
            actor = Monster();
            target = Monster();
            target.Personality = PersonalityType.Adaptive;
            target.Stats.Evasion = 100;
            executor.Execute(actor, target, new SkillData { SkillType = type, SuccessRate = 100 }, logger, actor, target);
            Assert(target.CurrentHp == 100 && target.StatusAilment == StatusAilmentType.None
                && target.DefenseBuff == 0 && !target.IsSkillLocked, "zero hit chance prevents target effect: " + type);
            Assert(target.PhysicalAdaptation == 0 && target.MagicAdaptation == 0 && target.StunAdaptation == 0
                && target.DamageTakenCount == 0, "dodged attacks cannot grant adaptation: " + type);
            Assert(actor.CurrentHp == (type == SkillType.AllOutAttack ? 1 : 100), "dodging preserves all-out cost but causes no strong-attack recoil");
        }
        actor = Monster();
        actor.CurrentHp = 50;
        executor.Execute(actor, target, new SkillData { SkillType = SkillType.Heal, Power = 20, SuccessRate = 100 }, logger, actor, target);
        Assert(actor.CurrentHp == 70, "enemy evasion cannot prevent self healing");
        executor.Execute(actor, target, new SkillData { SkillType = SkillType.AttackBuff, SuccessRate = 100 }, logger, actor, target);
        Assert(actor.AttackBuff == 5, "enemy evasion cannot prevent self buffs");
        executor.Execute(actor, target, new SkillData { SkillType = SkillType.StrongAttack, SuccessRate = 0 }, logger, actor, target);
        Assert(actor.CurrentHp == 64, "skill failure still causes recoil independently of evasion");

        foreach (bool combo in new[] { false, true })
        {
            int hits = 0;
            var skill = new SkillData { SkillType = combo ? SkillType.DebuffCombo : SkillType.StrongAttack,
                SuccessRate = combo ? 100 : 65, Power = 12 };
            for (int i = 0; i < 4000; i++)
            {
                actor = Monster();
                target = Monster();
                actor.Stats.Accuracy = 10;
                target.Stats.Evasion = 30;
                if (combo)
                {
                    target.StatusAilment = StatusAilmentType.Poison;
                    target.DefenseBuff = -10;
                    target.IsSkillLocked = true;
                }
                executor.Execute(actor, target, skill, new BattleLogger(), actor, target);
                if (target.CurrentHp < 100) hits++;
            }
            float rate = hits / 4000f;
            Assert(Mathf.Abs(rate - (combo ? .8f : .65f * .8f)) < .04f,
                combo ? "combo checks hit only once" : "skill success and hit chance multiply without inflating skill success");
        }
    }

    private static void CheckGeneration()
    {
        var factory = new MonsterFactory();
        bool fullGrowth = false;
        bool fullAccuracy = false;
        bool fullEvasion = false;
        for (int i = 0; i < 2000; i++)
        {
            var stats = factory.CreateRandom("generated", i % 2 == 0 ? ModeType.story : ModeType.ranking).Stats;
            Assert(stats.Growth >= 5 && stats.Growth <= 100 && stats.Accuracy >= 5 && stats.Accuracy <= 30
                && stats.Evasion >= 5 && stats.Evasion <= 30, "generation respects the new initial ranges");
            fullGrowth |= stats.Growth == 100;
            fullAccuracy |= stats.Accuracy == 30;
            fullEvasion |= stats.Evasion == 30;
        }
        Assert(fullGrowth && fullAccuracy && fullEvasion, "generation includes the requested maxima");
    }
}
