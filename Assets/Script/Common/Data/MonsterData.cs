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

    // 性格の一時状態。試合開始時にリセットし、元のStatsは変更しない。
    // NonSerialized により保存データへ混ぜず、対戦ごとの計算値として扱う。
    [System.NonSerialized] public SkillData LastUsedSkill;
    // 性格効果の倍率。各 GetCurrent... が元ステータスへ適用する。
    [System.NonSerialized] public float PersonalityMultiplier = 1f;
    // 執念の攻撃力だけに掛ける倍率。HP・防御などの全能力倍率とは分けて扱う。
    [System.NonSerialized] public float PersonalityAttackMultiplier = 1f;
    // 毒・封印を受けた次のターン。0は発動待ちの状態異常がないことを表す。
    [System.NonSerialized] public int ObsessionStartTurn;
    // スタンで行動を休んだ次のターン。この1ターンだけ攻撃力を2倍にする。
    [System.NonSerialized] public int ObsessionStunRecoveryTurn;
    // 性格効果や耐性の計算に使う現在のターン番号。
    [System.NonSerialized] public int BattleTurn;
    // 克己の能力上昇に使う被ダメージ回数。適応は下の4種類の耐性を個別に使う。
    [System.NonSerialized] public int DamageTakenCount;
    // 適応の物理ダメージ軽減率（％）。通常攻撃の命中で+5、強撃・全身全霊の命中で+10ポイント。
    [System.NonSerialized] public int PhysicalAdaptation;
    // 適応の魔法ダメージ軽減率（％）。属性に関係なく、魔法の命中で+20ポイント。
    [System.NonSerialized] public int MagicAdaptation;
    // 適応の毒耐性。毒ダメージを受けるごとに軽減率+10ポイント。
    [System.NonSerialized] public int PoisonAdaptation;
    // 適応のスタン耐性。スタンの付与成功で+10ポイント。
    [System.NonSerialized] public int StunAdaptation;
    // 不屈が致死ダメージを耐えた回数。初回保証の判定にも使う。
    [System.NonSerialized] public int EndureCount;
    // 無能の覚醒が済んだかどうか。
    [System.NonSerialized] public bool Awakened;
    // 陰湿が相手に弱体無効を確認したかどうか。
    [System.NonSerialized] public bool DebuffRejected;
    // モンスター固有の弱体耐性。性格倍率のリセット対象にしない。
    [System.NonSerialized] public bool DebuffImmune;

    // 性格倍率を反映した能力値。HP・命中・回避はゲーム内の上限・下限も適用する。
    public int GetCurrentMaxHp() => Mathf.Max(1, Mathf.RoundToInt(Stats.MaxHp * PersonalityMultiplier));
    public int GetCurrentSpeed() => Mathf.RoundToInt(Stats.Speed * PersonalityMultiplier);
    public int GetCurrentGrowth() => Mathf.RoundToInt(Stats.Growth * PersonalityMultiplier);
    public int GetCurrentAccuracy() => Mathf.Min(100, Mathf.RoundToInt(Stats.Accuracy * PersonalityMultiplier));
    public int GetCurrentEvasion() => Mathf.Min(100, Mathf.RoundToInt(Stats.Evasion * PersonalityMultiplier));

    /// <summary>
    /// 実際に戦闘で使う攻撃力
    /// </summary>
    public int GetCurrentAttack()
    {
        int attack = Mathf.Max(1, Mathf.RoundToInt(Stats.Attack * PersonalityMultiplier) + AttackBuff);
        return Mathf.Max(1, Mathf.RoundToInt(attack * PersonalityAttackMultiplier));
    }

    /// <summary>
    /// 実際に戦闘で使う防御力
    /// </summary>
    public int GetCurrentDefense()
    {
        return Mathf.Max(0, Mathf.RoundToInt(Stats.Defense * PersonalityMultiplier) + DefenseBuff);
    }

    /// <summary>
    /// 実際に戦闘で使う会心率
    /// </summary>
    public int GetCurrentCriticalRate()
    {
        return Mathf.Clamp(Mathf.RoundToInt(Stats.CriticalRate * PersonalityMultiplier) + CriticalBuff, 0, 100);
    }
}
