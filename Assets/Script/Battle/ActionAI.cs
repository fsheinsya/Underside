using UnityEngine;

//性格のパターンのステートメントAI

public class ActionAI
{
    public SkillData ChooseSkill(MonsterData attacker, MonsterData defender, int turn)
    {

        if (attacker.Skills == null || attacker.Skills.Count == 0)
            return CreateNormalAttack();

        //性格の行動パターンの変更
        switch (attacker.Personality)
        {
            case PersonalityType.Bold: // 大胆
                return GetStrongest(attacker);

            case PersonalityType.Calm: // 冷静
                //2ターン以内の行動
                if (turn <= 2)

                    return GetDefensive(attacker);
                return GetStrongest(attacker);

            case PersonalityType.Flexible: // 柔軟
                var copied = FindSameType(attacker, defender);
                if (copied != null) return copied;
                break;

            case PersonalityType.Taunt: // 挑発
                return turn % 2 == 1 ? GetWeak(attacker) : GetStrongest(attacker);

            case PersonalityType.Aggressive: // 挑戦寄り
                return GetLowSuccess(attacker);

            case PersonalityType.Timid: // 臆病
                if (attacker.CurrentHp <= attacker.Stats.MaxHp / 2)
                {
                    var heal = FindSkill(attacker, SkillType.Heal);
                    if (heal != null) return heal;
                }
                return GetEvasion(attacker);
        }

        return attacker.Skills[Random.Range(0, attacker.Skills.Count)];
    }

    // =========================
    // 🔽 ここ全部「既存スキルから選ぶ」
    // =========================

    //ガンガン攻撃を繰り出す
    private SkillData GetStrongest(MonsterData m)
    {
        SkillData best = m.Skills[0];
        foreach (var s in m.Skills)
            if (s.Power > best.Power)
                best = s;
        return best;
    }

    //弱い
    private SkillData GetWeak(MonsterData m)
    {
        SkillData weak = m.Skills[0];
        foreach (var s in m.Skills)
            if (s.Power < weak.Power)
                weak = s;
        return weak;
    }

    //
    private SkillData GetLowSuccess(MonsterData m)
    {
        SkillData risky = m.Skills[0];
        foreach (var s in m.Skills)
            if (s.SuccessRate < risky.SuccessRate)
                risky = s;
        return risky;
    }

    private SkillData GetDefensive(MonsterData m)
    {
        foreach (var s in m.Skills)
        {
            if (s.SkillType == SkillType.DefenseBuff ||
                s.SkillType == SkillType.Heal)
                return s;
        }
        return m.Skills[0];
    }

    private SkillData GetEvasion(MonsterData m)
    {
        foreach (var s in m.Skills)
        {
            if (s.SkillType == SkillType.Invincible)
                return s;
        }
        return m.Skills[Random.Range(0, m.Skills.Count)];
    }

    private SkillData FindSkill(MonsterData m, SkillType type)
    {
        foreach (var s in m.Skills)
            if (s.SkillType == type)
                return s;
        return null;
    }

    // 🔥 柔軟AI用（相手のタイプをコピー）
    private SkillData FindSameType(MonsterData attacker, MonsterData defender)
    {
        if (defender.LastUsedSkillType == SkillType.None) return null;

        foreach (var s in attacker.Skills)
        {
            if (s.SkillType == defender.LastUsedSkillType)
                return s;
        }

        return null;
    }

    private SkillData CreateNormalAttack()
    {
        return new SkillData
        {
            SkillName = "通常攻撃",
            SkillType = SkillType.NormalAttack,
            Power = 0,
            SuccessRate = 100
        };
    }

    public void RecordAction(MonsterData attacker, SkillData skill)
    {
        attacker.LastUsedSkillType = skill.SkillType;
    }
}