using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 1体分のモンスター情報表示を担当するクラス
/// LeftStatusPanel と RightStatusPanel の両方に付ける
/// 主な役割:
/// ・未開示表示
/// ・基本情報表示
/// ・完全情報表示
/// </summary>
public class StatusPanelView : MonoBehaviour
{
    [Header("テキスト参照")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text growthText;
    [SerializeField] private TMP_Text abilityText;

    [Header("画像参照")]
    [SerializeField] private Image iconImage;

    /// <summary>
    /// 最低限の情報だけを表示する
    /// ステータスは隠して表示する
    /// </summary>
    public void SetupHidden(MonsterData data)
    {
        // 名前と画像だけは見せる
        nameText.text = data.Name;

        // 画像が設定されている場合だけ差し替える
        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
        }

        // 各能力値は未開示状態にする
        hpText.text = "HP : ????";
        atkText.text = "ATK : ????";
        defText.text = "DEF : ????";
        growthText.text = "Growth : ????";
        abilityText.text = "Ability : ????";
    }

    /// <summary>
    /// 基本情報だけを表示する
    /// HP / ATK / DEF は見えるが Growth と Ability は隠す
    /// </summary>
    public void ShowBasic(MonsterData data)
    {
        // 名前と画像を反映
        nameText.text = data.Name;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
        }

        // 基本ステータスだけ表示
        hpText.text = $"HP : {data.Stats.MaxHp}";
        atkText.text = $"ATK : {data.Stats.Attack}";
        defText.text = $"DEF : {data.Stats.Defense}";

        // 追加情報はまだ伏せる
        growthText.text = "Growth : ????";
        abilityText.text = "Ability : ????";
    }

    /// <summary>
    /// すべての情報を表示する
    /// </summary>
    public void ShowFull(MonsterData data)
    {
        // 名前と画像を反映
        nameText.text = data.Name;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
        }

        // すべてのステータスを表示
        hpText.text = $"HP : {data.Stats.MaxHp}";
        atkText.text = $"ATK : {data.Stats.Attack}";
        defText.text = $"DEF : {data.Stats.Defense}";
        growthText.text = $"Growth : {data.Stats.Growth}";
        abilityText.text = $"Ability : {GetAbilityDisplayName(data.SpecialAbility)}";
    }

    /// <summary>
    /// enumで管理している特殊能力名を、画面表示用の文字列に変換する
    /// </summary>
    private string GetAbilityDisplayName(SpecialAbilityType type)
    {
        switch (type)
        {
            case SpecialAbilityType.None:
                return "None";

            case SpecialAbilityType.PowerStrike:
                return "Power Strike";

            case SpecialAbilityType.IronWall:
                return "Iron Wall";

            case SpecialAbilityType.Quick:
                return "Quick";

            case SpecialAbilityType.Regenerate:
                return "Regenerate";

            case SpecialAbilityType.Berserk:
                return "Berserk";

            case SpecialAbilityType.Lucky:
                return "Lucky";

            default:
                return "Unknown";
        }
    }
}