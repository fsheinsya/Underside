using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>実際の祈りを実行し、効果の排他性・確率・左右の対象・耐性との連携を確認する。</summary>
public static class FaithPrayerChecks
{
    private static int assertions;
    private static readonly SkillExecutor Executor = new SkillExecutor();
    private static readonly SkillData Prayer = new SkillData
    {
        SkillName = "祈り", SkillType = SkillType.RandomSkill, SuccessRate = 100
    };

    [MenuItem("Tools/UnderSide/Check Faith Prayer")]
    public static void Run()
    {
        var previous = Random.state;
        assertions = 0;
        try
        {
            var counts = new Dictionary<string, int>();
            var seeds = new Dictionary<string, int>();
            const int trials = 20000;
            for (int seed = 0; seed < trials; seed++)
            {
                var actor = Monster();
                var target = Monster();
                // 攻防・会心・属性・無敵に左右されず、指定の固定値になることも確認する。
                actor.Stats.Attack = target.Stats.Defense = 9999;
                actor.Stats.CriticalRate = 100;
                target.Element = ElementType.Water;
                target.IsInvincible = true;
                var logger = Execute(seed, actor, target, seed % 2 == 0);
                bool miracle = logger.Logs.Any(x => x.Message == "奇跡が起きた！");
                int damage = 100 - target.CurrentHp;
                string outcome;
                if (miracle)
                {
                    Assert(actor.CurrentHp == 100, "miracle never harms actor");
                    int effects = (damage > 0 ? 1 : 0) + (target.StatusAilment != StatusAilmentType.None ? 1 : 0)
                        + (target.DefenseBuff != 0 ? 1 : 0);
                    Assert(effects == 1, "exactly one miracle effect");
                    if (target.CurrentHp == 0) outcome = "enemy death";
                    else if (damage > 0)
                    {
                        Assert(new[] { 5, 10, 20, 40 }.Contains(damage), "miracle uses exact fixed damage");
                        outcome = "damage " + damage;
                    }
                    else if (target.DefenseBuff != 0)
                    {
                        Assert(target.DefenseBuff == -5, "defense debuff is minus five");
                        outcome = "defense";
                    }
                    else outcome = target.StatusAilment.ToString();
                }
                else
                {
                    Assert(target.CurrentHp == 100 && target.StatusAilment == StatusAilmentType.None
                        && target.DefenseBuff == 0 && !target.IsSkillLocked, "non-miracle never affects opponent");
                    outcome = actor.CurrentHp == 0 ? "self death" : actor.CurrentHp < 100 ? "self damage" : "nothing";
                    if (outcome == "self damage")
                        Assert(actor.CurrentHp >= 20 && actor.CurrentHp <= 95, "existing punishment damage range");
                }
                var last = logger.Logs.Last();
                Assert((seed % 2 == 0 ? last.LeftHp : last.RightHp) == actor.CurrentHp
                    && (seed % 2 == 0 ? last.RightHp : last.LeftHp) == target.CurrentHp, "log stores correct sides");
                counts[outcome] = counts.TryGetValue(outcome, out int count) ? count + 1 : 1;
                seeds[outcome] = seed;
            }

            foreach (string outcome in new[] { "damage 5", "damage 10", "damage 20", "damage 40",
                "Poison", "Stun", "SkillSeal", "defense", "enemy death", "self death", "self damage", "nothing" })
            {
                double expected = outcome == "nothing" ? .20 : outcome == "self damage" ? .36
                    : outcome.EndsWith("death") ? .04 : .045;
                Assert(counts.ContainsKey(outcome) && Math.Abs(counts[outcome] / (double)trials - expected) < .012,
                    "prayer distribution: " + outcome);
            }

            foreach (string outcome in new[] { "Poison", "Stun", "SkillSeal", "defense" })
            {
                var actor = Monster();
                var immune = Monster();
                immune.DebuffImmune = true;
                Execute(seeds[outcome], actor, immune);
                Assert(immune.CurrentHp == 100 && immune.StatusAilment == StatusAilmentType.None
                    && !immune.IsSkillLocked && immune.DefenseBuff == 0, "immune debuff is not rerolled: " + outcome);
            }
            foreach (string outcome in new[] { "Poison", "Stun" })
            {
                var adaptive = Monster(PersonalityType.Adaptive);
                adaptive.PoisonAdaptation = adaptive.StunAdaptation = 100;
                Execute(seeds[outcome], Monster(), adaptive);
                Assert(adaptive.StatusAilment == StatusAilmentType.None, "full adaptation blocks " + outcome);
            }
            var stunTarget = Monster(PersonalityType.Adaptive);
            Execute(seeds["Stun"], Monster(), stunTarget);
            Assert(stunTarget.StatusAilment == StatusAilmentType.Stun && stunTarget.StunAdaptation == 10,
                "successful stun increases adaptation once");

            var poisoned = Monster();
            poisoned.StatusAilment = StatusAilmentType.Poison;
            Execute(seeds["SkillSeal"], Monster(), poisoned);
            Assert(poisoned.IsSkillLocked && poisoned.StatusAilment == StatusAilmentType.Poison, "seal preserves poison");

            var enduring = Monster(PersonalityType.Unyielding);
            Execute(seeds["enemy death"], Monster(), enduring);
            Assert(enduring.CurrentHp == 1 && enduring.EndureCount == 1, "enemy can endure miracle death");
            enduring = Monster(PersonalityType.Unyielding);
            Execute(seeds["self death"], enduring, Monster());
            Assert(enduring.CurrentHp == 1 && enduring.EndureCount == 1, "actor can endure punishment death");

            var resistant = Monster(PersonalityType.Adaptive);
            resistant.PhysicalAdaptation = resistant.MagicAdaptation = 100;
            Execute(seeds["damage 40"], Monster(), resistant);
            Assert(resistant.CurrentHp == 60 && resistant.PhysicalAdaptation == 100 && resistant.MagicAdaptation == 100,
                "fixed damage ignores physical and magic resistance");
            var stoic = Monster(PersonalityType.Stoic);
            Execute(seeds["damage 5"], Monster(), stoic);
            Assert(stoic.DamageTakenCount == 1 && Mathf.Approximately(stoic.PersonalityMultiplier, 1.1f), "miracle triggers stoic growth");

            var sealedActor = Monster();
            sealedActor.IsSkillLocked = true;
            var opponent = Monster();
            Execute(seeds["enemy death"], sealedActor, opponent);
            Assert(sealedActor.CurrentHp == 100 && opponent.CurrentHp == 100, "sealed prayer has no effect");
            var noSkills = Monster();
            noSkills.Skills = null;
            opponent = Monster();
            Execute(seeds["damage 20"], noSkills, opponent);
            Assert(opponent.CurrentHp == 80, "miracle does not require a skill list");
            Debug.Log($"FAITH_PRAYER_CHECKS_PASSED: {assertions} assertions; {trials} prayers; enemy death {counts["enemy death"]}, self death {counts["self death"]}.");
        }
        finally { Random.state = previous; }
    }

    private static MonsterData Monster(PersonalityType personality = PersonalityType.faith)
    {
        return new MonsterData
        {
            Name = personality.ToString(), Personality = personality, Element = ElementType.Fire,
            Stats = new MonsterStats { MaxHp = 100, Attack = 20, Defense = 10 }, CurrentHp = 100,
            Skills = new List<SkillData> { Prayer }
        };
    }

    private static BattleLogger Execute(int seed, MonsterData actor, MonsterData target, bool actorLeft = true)
    {
        Random.InitState(seed);
        var logger = new BattleLogger();
        Executor.Execute(actor, target, Prayer, logger, actorLeft ? actor : target, actorLeft ? target : actor);
        return logger;
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Faith prayer check failed: " + message);
    }
}
