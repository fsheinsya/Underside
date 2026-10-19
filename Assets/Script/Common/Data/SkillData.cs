/// <summary>
/// スキル1つ分のデータ
/// </summary>
[System.Serializable]
public class SkillData
{
    public string SkillName; // 行動ログなどに表示する技名。ActionAI は名前では選ばない。
    public SkillType SkillType; // 効果の種類。ActionAI が防御・回復・無敵化を探すときや、信仰AIの種別一致判定に使う。
    public ElementType Element;

    public int Power; // AIの選択基準。物理攻撃では追加威力、回復では回復量（最低10）。属性魔法では選択優先度だけに使う。
    public float DamageMultiplier = 1f; // 攻撃ダメージ全体の倍率。通常は1倍、柔軟のコピーはコピー元の80％。
    public double SuccessRate; // 実行前の成功率（%）。挑戦寄りのAIはこの値が最小の技を選び、効果内の追加抽選は比較しない。

    public int HealAmount;
    public int BuffAmount;
    public int DebuffAmount;

    public StatusAilmentType InflictAilment;
    public int InflictRate;

    public bool IsHighRisk;
    public bool IsRandomSkill;
}
