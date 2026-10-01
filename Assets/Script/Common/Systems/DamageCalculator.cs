using UnityEngine;

//ダメージの計算を管理をする
public class DamageCalculator
{
    //ダメージ計算を行う
    public int CalculateDamage(MonsterData attacker, MonsterData defender, int power, ElementType element)
    {
        //攻撃側の攻撃ステータス設定
        int attack = attacker.GetCurrentAttack();
        //防御側の防御ステータス設定
        int defense = defender.GetCurrentDefense();

        int damage = Mathf.Max(1, attack - defense / 2 + power + Random.Range(-2, 3));

        float multiplier = GetElementMultiplier(element, defender.Element);
        damage = Mathf.RoundToInt(damage * multiplier);

        return Mathf.Max(1, damage);
    }

    //属性同士のダメージを計算をする
    private float GetElementMultiplier(ElementType attacker, ElementType defender)
    {
        if (attacker == ElementType.Water && defender == ElementType.Fire) return 1.5f;
        if (attacker == ElementType.Fire && defender == ElementType.Grass) return 1.5f;
        if (attacker == ElementType.Grass && defender == ElementType.Water) return 1.5f;

        return 1.0f;
    }
}