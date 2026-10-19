using TMPro;
using UnityEngine;

/// <summary>
/// Battleシーンで、1体分の簡易ステータスを表示するクラス
/// 左右の黒い枠に付けて使う
/// </summary>
public class BattleStatusView : MonoBehaviour
{
    [Header("表示テキスト")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text elementText;
    [SerializeField] private TMP_Text statusText;

    /// <summary>
    /// モンスター情報をまとめて表示する
    /// </summary>
    public void Show(MonsterData data)
    {
        // HP表示
        if (hpText != null)
        {
            hpText.text = $"HP:{data.CurrentHp}";
        }

        // 属性表示
        if (elementText != null)
        {
            elementText.text = $"属性：{GetElementName(data.Element)}";
        }

        // 状態異常表示
        SetStatus(data.StatusAilment, data.IsSkillLocked);
    }

    /// <summary>
    /// HPだけ更新する
    /// </summary>
    public void SetHp(int hp)
    {
        if (hpText != null)
        {
            hpText.text = $"HP:{hp}";
        }
    }

    /// <summary>
    /// 状態異常だけ更新する
    /// </summary>
    public void SetStatus(StatusAilmentType ailment, bool isSkillLocked = false)
    {
        if (statusText != null)
        {
            // 毒と封印が重なる場合も表示する。封印だけ残った場合は「なし」ではなく「封印」。
            string statusName = GetStatusName(ailment);
            if (isSkillLocked && ailment != StatusAilmentType.SkillSeal)
                statusName = ailment == StatusAilmentType.None ? "封印" : statusName + "・封印";
            statusText.text = $"状態：{statusName}";
        }
    }

    /// <summary>
    /// 属性名を日本語表示に変換する
    /// </summary>
    private string GetElementName(ElementType element)
    {
        switch (element)
        {
            //炎を設定をする
            case ElementType.Fire: return "炎";
            //水を設定をする
            case ElementType.Water: return "水";
            //草を設定をする
            case ElementType.Grass: return "草";
            case ElementType.Light: return "光";
            case ElementType.Dark: return "闇";
            //無を設定をする
            default: return "無";
        }
    }

    /// <summary>
    /// 状態異常名を日本語表示に変換する
    /// </summary>
    private string GetStatusName(StatusAilmentType ailment)
    {
        switch (ailment)
        {
            //行動を不能にする
            case StatusAilmentType.Stun: return "行動不能";
            //毒を設定をする
            case StatusAilmentType.Poison: return "毒";
            //封印を設定をする
            case StatusAilmentType.SkillSeal: return "封印";
            default: return "なし";
        }
    }
}
