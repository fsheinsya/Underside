/// <summary>
/// 性格 enum を画面表示用の日本語名へ変換する。
/// </summary>
public static class PersonalityNames
{
    /// <summary>性格名を返す。未定義値は「不明」として扱う。</summary>
    public static string Get(PersonalityType type)
    {
        switch (type)
        {
            case PersonalityType.Bold: return "大胆";
            case PersonalityType.Calm: return "冷静";
            case PersonalityType.faith: return "信仰";
            case PersonalityType.Taunt: return "挑発";
            case PersonalityType.Aggressive: return "挑戦";
            case PersonalityType.Timid: return "臆病";
            case PersonalityType.Delusional: return "中二";
            case PersonalityType.Inept: return "無能";
            case PersonalityType.Stoic: return "克己";
            case PersonalityType.Explorer: return "探求";
            case PersonalityType.Devoted: return "献身";
            case PersonalityType.Adversity: return "逆境";
            case PersonalityType.Unyielding: return "不屈";
            case PersonalityType.Impatient: return "短気";
            case PersonalityType.Headlong: return "猛進";
            case PersonalityType.Obsessive: return "執念";
            case PersonalityType.Sincere: return "誠実";
            case PersonalityType.Insidious: return "陰湿";
            case PersonalityType.Adaptive: return "適応";
            case PersonalityType.Flexible: return "柔軟";
            default: return "不明";
        }
    }
}
