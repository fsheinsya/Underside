using UnityEngine;

/// <summary>通常・強攻撃と属性魔法のダメージを計算する。</summary>
// 計算順：現在の攻防を取得 → 魔法の有利補正 → 攻撃－防御/2＋追加威力＋乱数 → 属性倍率。
// 会心・コピー倍率はSkillExecutor.ApplyDamage、適応の軽減・不屈・HP減少はPersonalityRules.TakeDamage。
// ここで相手のHPは書き換えない。AIが技を選ぶPower比較とも別の処理。
public class DamageCalculator
{
    /// <param name="attacker">使用者。性格倍率とバフを反映した現在の攻撃力を読む。</param>
    /// <param name="defender">対象。現在の防御力と防御側の属性を読む。</param>
    /// <param name="power">追加威力。魔法種別の場合は0に置き換え、AI比較用のPowerをダメージに加えない。</param>
    /// <param name="element">攻撃に使う属性。通常・強攻撃は使用者の属性、魔法は技種別に対応した属性。</param>
    /// <param name="attackType">魔法かどうかを判定する種別。省略時のNoneは魔法専用補正を行わない。</param>
    public int CalculateDamage(MonsterData attacker, MonsterData defender, int power, ElementType element,
        SkillType attackType = SkillType.None)
    {
        int attack = attacker.GetCurrentAttack(); // 生成時のStats.Attackではなく、現在の強化・弱体を含む攻撃力。
        int defense = defender.GetCurrentDefense(); // 現在の防御補正と性格倍率を含む防御力。
        float multiplier = ElementCalculator.GetAttackMultiplier(element, defender.Element); // 攻撃属性→相手属性の向きで相性を調べる。

        switch (attackType) // 魔法の5種だけ式を補正する。通常・強攻撃・発掘爆弾などはそのまま進む。
        {
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
                // 魔法のPowerはAIの選択優先度として残し、追加ダメージには使わない。
                power = 0; // 引数のローカル値だけを変更する。SkillData.Powerは変更しない。
                if (multiplier > 1f) // 1.5倍・2倍など、有利相性の場合。
                {
                    // 有利時は攻撃力を2倍にする。従来の有利倍率は重ねない。
                    attack *= 2; // 防御を引く前の攻撃力を2倍にする。
                    multiplier = 1f; // 後段で有利相性を二重に掛けない。不利相性の0.75倍は変更しない。
                }
                break;
        }

        int damage = Mathf.Max(1, attack - defense / 2 + power + Random.Range(-2, 3)); // 防御/2は整数除算。乱数は-2～+2の5通り。まず最低1にする。
        damage = Mathf.RoundToInt(damage * multiplier); // 相性倍率を1回掛け、小数を整数へ丸める。
        return Mathf.Max(1, damage); // 不利倍率で丸めて0になっても基礎ダメージは最低1。後段の耐性100%では0になり得る。
    }
}
