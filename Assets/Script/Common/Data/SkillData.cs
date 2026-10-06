/// <summary>
/// スキル1つ分のデータ
/// </summary>
[System.Serializable]
public class SkillData
{
    public string SkillName;
    public SkillType SkillType;
    public ElementType Element;

    public int Power;
    public int SuccessRate;

    public int HealAmount;
    public int BuffAmount;
    public int DebuffAmount;

    public StatusAilmentType InflictAilment;
    public int InflictRate;

    public bool IsHighRisk;
    public bool IsRandomSkill;
}
