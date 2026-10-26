/// <summary>
/// 攻撃側の属性→防御側の属性の相性表。戦闘ダメージ・逆境・オッズが共通で参照する。
/// 属性魔法の有利倍率はDamageCalculatorで「攻撃力2倍」に置き換えるので、この戻り値を重ねて掛けない。
/// </summary>
public static class ElementCalculator
{
    public static float GetAttackMultiplier(ElementType attacker, ElementType defender)
    {
        // 有利属性
        if (attacker == ElementType.Water && defender == ElementType.Fire) return 1.5f; // 水→炎は有利。
        if (attacker == ElementType.Fire && defender == ElementType.Grass) return 1.5f; // 炎→草は有利。
        if (attacker == ElementType.Grass && defender == ElementType.Water) return 1.5f; // 草→水は有利。
        if (attacker == ElementType.Light && defender == ElementType.Dark) return 2.0f; // 光→闇は有利。
        if (attacker == ElementType.Dark && defender == ElementType.Light) return 2.0f; // 闇→光も有利。互いに弱点を突く。

        if (attacker == ElementType.Fire && defender == ElementType.Water) return 0.75f; // 炎→水は不利。
        if (attacker == ElementType.Water && defender == ElementType.Grass) return 0.75f; // 水→草は不利。
        if (attacker == ElementType.Grass && defender == ElementType.Fire) return 0.75f; // 草→炎は不利。

        return 1.0f; // 同属性など、上に列挙しなかった組み合わせは等倍。
    }

    public static float GetDefenseMultiplier(ElementType attacker, ElementType defender)
    {
        // 同じ相性表の被ダメージ倍率。攻撃倍率との二重適用はしない。
        return GetAttackMultiplier(attacker, defender); // 防御用という名前でも引数の向きは「攻撃側→防御側」のまま。
    }

    public static bool IsDisadvantaged(ElementType self, ElementType opponent)
    {
        // 光・闇は双方が弱点を突かれるので、どちらも逆境の対象。
        return GetAttackMultiplier(opponent, self) > 1f; // 「自分の攻撃が不利」ではなく「相手からの攻撃が有利」を調べる。
    }
}
