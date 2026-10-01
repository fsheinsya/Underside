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
    public PersonalityType Personality;
    public StatusAilmentType StatusAilment;

    // スキル一覧
    public List<SkillData> Skills = new List<SkillData>();

    // 戦闘中一時補正
    public int AttackBuff;
    public int DefenseBuff;
    public int CriticalBuff;

    // 現在行動不能か
    public bool IsStunnedThisTurn;

    public ActionType? LastAction; // 柔軟AI用
    public int BetCoin;            // ギャンブル用

    // 🔥 追加（ここが重要）
    public SkillType LastUsedSkillType = SkillType.None;

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