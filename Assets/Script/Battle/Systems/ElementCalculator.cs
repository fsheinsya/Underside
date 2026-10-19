/// <summary>
/// 属性相性の倍率を返す
/// </summary>
public static class ElementCalculator
{
    public static float GetAttackMultiplier(ElementType attacker, ElementType defender)
    {
        // 有利属性
        if (attacker == ElementType.Water && defender == ElementType.Fire) return 1.5f;
        if (attacker == ElementType.Fire && defender == ElementType.Grass) return 1.5f;
        if (attacker == ElementType.Grass && defender == ElementType.Water) return 1.5f;
        if (attacker == ElementType.Light && defender == ElementType.Dark) return 2.0f;
        if (attacker == ElementType.Dark && defender == ElementType.Light) return 2.0f;

        if (attacker == ElementType.Fire && defender == ElementType.Water) return 0.75f;
        if (attacker == ElementType.Water && defender == ElementType.Grass) return 0.75f;
        if (attacker == ElementType.Grass && defender == ElementType.Fire) return 0.75f;

        return 1.0f;
    }

    public static float GetDefenseMultiplier(ElementType attacker, ElementType defender)
    {
        // 同じ相性表の被ダメージ倍率。攻撃倍率との二重適用はしない。
        return GetAttackMultiplier(attacker, defender);
    }

    public static bool IsDisadvantaged(ElementType self, ElementType opponent)
    {
        // 光・闇は双方が弱点を突かれるので、どちらも逆境の対象。
        return GetAttackMultiplier(opponent, self) > 1f;
    }
}
