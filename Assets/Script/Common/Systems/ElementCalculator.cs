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

        return 1.0f;
    }

    public static float GetDefenseMultiplier(ElementType attacker, ElementType defender)
    {
        // 有利を受ける側はダメージ軽減
        if (attacker == ElementType.Water && defender == ElementType.Fire) return 0.75f;
        if (attacker == ElementType.Fire && defender == ElementType.Grass) return 0.75f;
        if (attacker == ElementType.Grass && defender == ElementType.Water) return 0.75f;

        return 1.0f;
    }
}