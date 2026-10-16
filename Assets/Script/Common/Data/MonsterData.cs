using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// スライムの情報
/// </summary>
[System.Serializable]
public class MonsterData
{
    public string Name;
    public Sprite Icon;
    public MonsterStats Stats;
    public int CurrentHp;

    // 状態管理
    public bool IsInvincible;
    public bool IsSkillLocked;
    public int BuffAttack;
    public int BuffDefense;
    public int CritRateBonus;
    public bool IsPoisoned;

    // 既存
    public SpecialAbilityType SpecialAbility;

    // 追加パラメータ
    public ElementType Element;
    public PersonalityType Personality; // ActionAI が行動を選ぶときの性格。MonsterFactory はこの性格に応じた所持スキルも生成する。
    public StatusAilmentType StatusAilment;

    // スキル一覧。ActionAI は原則ここから選び、威力・成功率が同値の場合や種別検索では一覧の順番を優先する。
    public List<SkillData> Skills = new List<SkillData>();

    // 戦闘中一時補正
    public int AttackBuff;
    public int DefenseBuff;
    public int CriticalBuff;

    // 現在行動不能か
    public bool IsStunnedThisTurn;

    public ActionType? LastAction; // 現在の信仰AIはこの値を使わず、下の LastUsedSkillType を参照する。
    public int BetCoin;            // ギャンブル用

    // 本人が直近に選んだスキル種別。相手の信仰AIがまねする種別として参照し、失敗・封印時も記録される。
    public SkillType LastUsedSkillType = SkillType.None; // 生成直後の None は行動履歴なしを表し、ActionAI.RecordAction が更新する。

    /// <summary>
    /// 実際に戦闘で使う攻撃力
    /// </summary>
    public int GetCurrentAttack()
    {
        return Stats.Attack + AttackBuff;
    }

    /// <summary>
    /// 実際に戦闘で使う防御力
    /// </summary>
    public int GetCurrentDefense()
    {
        return Stats.Defense + DefenseBuff;
    }

    /// <summary>
    /// 実際に戦闘で使う会心率
    /// </summary>
    public int GetCurrentCriticalRate()
    {
        return Stats.CriticalRate + CriticalBuff;
    }
}
