using UnityEngine;

/// <summary>通常・強攻撃と属性魔法のダメージを計算する。</summary>
public class DamageCalculator
{
    public int CalculateDamage(MonsterData attacker, MonsterData defender, int power, ElementType element,
        SkillType attackType = SkillType.None)
    {
        int attack = attacker.GetCurrentAttack();
        int defense = defender.GetCurrentDefense();
        float multiplier = ElementCalculator.GetAttackMultiplier(element, defender.Element);

        switch (attackType)
        {
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
                // 魔法のPowerはAIの選択優先度として残し、追加ダメージには使わない。
                power = 0;
                if (multiplier > 1f)
                {
                    // 有利時は攻撃力を2倍にする。従来の有利倍率は重ねない。
                    attack *= 2;
                    multiplier = 1f;
                }
                break;
        }

        int damage = Mathf.Max(1, attack - defense / 2 + power + Random.Range(-2, 3));
        damage = Mathf.RoundToInt(damage * multiplier);
        return Mathf.Max(1, damage);
    }
}
