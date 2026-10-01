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
        if (statusText != null)
        {
            statusText.text = $"状態：{GetStatusName(data.StatusAilment)}";
        }
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
    public void SetStatus(StatusAilmentType ailment)
    {
        if (statusText != null)
        {
            statusText.text = $"状態：{GetStatusName(ailment)}";
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