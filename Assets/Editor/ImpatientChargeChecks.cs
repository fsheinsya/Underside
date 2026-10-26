using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public static class ImpatientChargeChecks
{
    private static int assertions;
    private static readonly SkillExecutor Executor = new SkillExecutor();
    private static readonly SkillData Wait = new SkillData { SkillType = SkillType.Wait, SuccessRate = 100 };

    [MenuItem("Tools/UnderSide/Check Impatient Charge")]
    public static void Run()
    {
        var previous = Random.state;
        assertions = 0;
        try
        {
            Random.InitState(20261022);
            CheckDamageAndReset();
            CheckOtherActions();
            CheckBlockedAttacks();
            CheckCopy();
            Debug.Log($"IMPATIENT_CHARGE_CHECKS_PASSED: {assertions} assertions.");
        }
        finally { Random.state = previous; }
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Impatient charge check failed: " + message);
    }

    private static MonsterData Monster()
    {
        var factory = new MonsterFactory();
        for (int i = 0; i < 2000; i++)
        {
            var monster = factory.CreateRandom("短気チェック", ModeType.ranking);
            if (monster.Personality != PersonalityType.Impatient) continue;
            monster.Stats = new MonsterStats { MaxHp = 5000, Attack = 100, Defense = 20, Speed = 10, Accuracy = 30 };
            monster.Element = ElementType.Fire;
            monster.CurrentHp = 5000;
            return monster;
        }
        throw new Exception("Could not generate impatient fixture");
    }

    private static SkillData Release(MonsterData monster) => monster.Skills.Single(skill => skill.UsesPatienceCharge);

    private static void Execute(MonsterData actor, MonsterData target, SkillData skill)
    {
        Executor.Execute(actor, target, skill, new BattleLogger(), actor, target);
    }

    private static void CheckDamageAndReset()
    {
        foreach (int count in new[] { 0, 1, 2, 3, 10 })
        foreach (int critical in new[] { 0, 100 })
        foreach (int resistance in new[] { 0, 50 })
        {
            var actor = Monster();
            var target = Monster();
            target.Personality = PersonalityType.Adaptive;
            target.PhysicalAdaptation = resistance;
            actor.Stats.CriticalRate = critical;
            var release = Release(actor);
            for (int i = 0; i < count; i++) Execute(actor, target, actor.Skills.Single(skill => skill.SkillType == SkillType.Wait));
            Assert(actor.PatienceWaitCount == count, "each executed wait stores exactly one charge");
            Random.InitState(601);
            int baseDamage = new DamageCalculator().CalculateDamage(actor, target, release.Power, actor.Element, release.SkillType);
            if (critical == 100) baseDamage *= 2;
            int scaled = Mathf.Max(1, Mathf.FloorToInt(baseDamage * Mathf.Pow(1.1f, count)));
            int expected = Mathf.Max(1, Mathf.RoundToInt(scaled * (1f - resistance / 100f)));
            Random.InitState(601);
            Execute(actor, target, release);
            Assert(target.Stats.MaxHp - target.CurrentHp == expected, "compound multiplier applies to full damage, before resistance");
            Assert(actor.PatienceWaitCount == 0, "release consumes all waits");
            Assert(release.DamageMultiplier == 1f && release.Power == 18 && actor.GetCurrentAttack() == 100,
                "charge does not mutate shared skill or attack stat");

            target = Monster();
            Random.InitState(601);
            Execute(actor, target, release);
            Assert(target.Stats.MaxHp - target.CurrentHp == baseDamage, "following release is back to one times damage");
            Execute(actor, target, Wait);
            Assert(actor.PatienceWaitCount == 1, "can charge again after release");
            PersonalityRules.Reset(actor);
            Assert(actor.PatienceWaitCount == 0, "next match clears charge");
        }
    }

    private static void CheckOtherActions()
    {
        var actor = Monster();
        var target = Monster();
        Execute(actor, target, Wait);
        Execute(actor, target, Wait);
        foreach (var skill in new[] { MonsterFactory.CreateNormalAttack(), MonsterFactory.CreateStrongAttack(),
            MonsterFactory.CreateElementSkill(actor.Element) })
        {
            skill.SuccessRate = 100;
            Random.InitState(302);
            int expected = new DamageCalculator().CalculateDamage(actor, target, skill.Power, actor.Element, skill.SkillType);
            target.CurrentHp = target.Stats.MaxHp;
            Random.InitState(302);
            Execute(actor, target, skill);
            Assert(target.Stats.MaxHp - target.CurrentHp == expected && actor.PatienceWaitCount == 2,
                "ordinary attacks neither benefit from nor consume stored charge");
        }
        actor.StatusAilment = StatusAilmentType.Stun;
        Assert(!new StatusSystem().CanAct(actor) && actor.PatienceWaitCount == 2, "stun neither charges nor consumes");
        actor.Personality = PersonalityType.Delusional;
        actor.PatienceWaitCount = 0;
        Execute(actor, target, Wait);
        Assert(actor.PatienceWaitCount == 0, "other personalities do not charge when waiting");
    }

    private static void CheckBlockedAttacks()
    {
        foreach (string condition in new[] { "sealed", "evaded", "invincible", "failed" })
        {
            var actor = Monster();
            var target = Monster();
            var release = Release(actor);
            Execute(actor, target, Wait);
            Execute(actor, target, Wait);
            if (condition == "sealed") actor.IsSkillLocked = true;
            if (condition == "evaded") { actor.Stats.Accuracy = 0; target.Stats.Evasion = 100; }
            if (condition == "invincible") target.IsInvincible = true;
            if (condition == "failed") release.SuccessRate = 0;
            Execute(actor, target, release);
            Assert(actor.PatienceWaitCount == (condition == "sealed" ? 2 : 0), "charge consumption for " + condition);
            Assert(target.CurrentHp == target.Stats.MaxHp, "no damage when " + condition);
        }
    }

    private static void CheckCopy()
    {
        var source = Monster();
        var copier = Monster();
        copier.Personality = PersonalityType.Flexible;
        Execute(source, copier, Wait);
        Execute(source, copier, Wait);
        var ai = new ActionAI();
        ai.RecordAction(source, Release(source));
        var copy = ai.ChoosePersonalitySkill(copier, source, 1);
        Assert(!copy.UsesPatienceCharge && Mathf.Approximately(copy.DamageMultiplier, .8f), "copy does not inherit personality charge");
        Random.InitState(81);
        int expected = Mathf.FloorToInt(new DamageCalculator().CalculateDamage(copier, source, copy.Power, copier.Element, copy.SkillType) * .8f);
        Random.InitState(81);
        Execute(copier, source, copy);
        Assert(source.Stats.MaxHp - source.CurrentHp == expected && source.PatienceWaitCount == 2,
            "copy deals ordinary copy damage and preserves opponent's charge");
    }
}
