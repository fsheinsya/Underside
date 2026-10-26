using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 1体の基本情報と、戦闘中に変化する状態をまとめる。
/// Statsは生成時の基礎値、GetCurrent...は性格倍率・一時補正を反映した現在値。
/// ActionAIはここを読んで選択し、SkillExecutor・PersonalityRules・StatusSystemが状態を更新する。
/// </summary>
[System.Serializable]
public class MonsterData
{
    public string Name; // UIと戦闘ログに表示する名前。
    public Sprite Icon; // 表示用のスライム画像。AIの判断には使わない。
    public MonsterStats Stats; // 生成時の基本能力。戦闘中のバフはこの値を書き換えず別フィールドで保持する。
    public int CurrentHp; // 現在HP。0で戦闘不能。AIの回復判定や献身の追加威力にも使用する。

    // 状態管理
    public bool IsInvincible; // ApplyDamage経由のダメージを防ぐ。次の自分の行動機会の冒頭で解除する。
    public bool IsSkillLocked; // 封印フラグ。毒・スタンと共存できるようStatusAilmentとは別に持つ。
    // 旧実装の未使用欄。攻防・会心は下のBuff欄、毒はStatusAilmentで管理する。
    // public int BuffAttack;
    // public int BuffDefense;
    // public int CritRateBonus;
    // public bool IsPoisoned;

    // 既存
    public SpecialAbilityType SpecialAbility; // 生成・表示用の特殊能力。現在の戦闘AI・効果処理にはこの値による分岐はない。

    // 追加パラメータ
    public ElementType Element; // 属性相性・属性魔法生成・逆境の倍率判定に使う。
    public PersonalityType Personality; // ActionAI が行動を選ぶときの性格。MonsterFactory はこの性格に応じた所持スキルも生成する。
    public StatusAilmentType StatusAilment; // 毒・スタンなどを1種類保持する。毒とスタンは互いに上書きされ、封印だけは別フラグと共存する。

    // 固有枠で探す所持技一覧。Powerが同値の場合や種別検索では一覧順を優先する。成功率での比較はしない。
    public List<SkillData> Skills = new List<SkillData>(); // 共通攻撃やコピーの一時的な技はここへ追加しない。要素にはnullを入れない。

    // 戦闘中一時補正
    public int AttackBuff; // 攻撃の加算補正。全能力倍率と克己の攻撃倍率を掛けた後、執念の攻撃倍率を掛ける前に加える。
    public int DefenseBuff; // 防御の加算補正。弱体化すると負数になり、陰湿が弱体完了を判断する材料にもなる。
    public int CriticalBuff; // 会心率への加算ポイント。最終的な会心率は0～100に収める。

    // ターン経過の成長分。性格倍率の後に加算し、基本能力や次の試合へ持ち越さない。
    [System.NonSerialized] public int GrowthAttackBonus;
    [System.NonSerialized] public int GrowthDefenseBonus;
    [System.NonSerialized] public int GrowthSpeedBonus;
    [System.NonSerialized] public int GrowthAccuracyBonus;
    [System.NonSerialized] public int GrowthEvasionBonus;

    // 現在の行動不能判定はStatusSystem.CanActがStatusAilmentを確認する。
    // public bool IsStunnedThisTurn; // 現在の行動判定では未使用。

    // public ActionType? LastAction; // 履歴はLastUsedSkillTypeを使用する。
    // public int BetCoin; // 賭け情報はGameSession.CurrentBetを使用する。

    // 本人が直近に選んだスキル種別。相手の信仰AIがまねする種別として参照し、失敗・封印時も記録される。
    public SkillType LastUsedSkillType = SkillType.None; // 生成直後の None は行動履歴なしを表し、ActionAI.RecordAction が更新する。

    // 性格の一時状態。試合開始時にリセットし、元のStatsは変更しない。
    // NonSerialized により保存データへ混ぜず、対戦ごとの計算値として扱う。
    [System.NonSerialized] public SkillData LastUsedSkill; // 柔軟がコピー元にする直近の技。試合開始時はnull、行動後はRecordActionが更新。
    // 性格効果の倍率。各 GetCurrent... が元ステータスへ適用する。
    [System.NonSerialized] public float PersonalityMultiplier = 1f;
    // 逆境はHPや命中・回避・会心へ倍率を掛けない。
    [System.NonSerialized] public bool AdversityActive;
    private float AdversityMultiplier => AdversityActive ? PersonalityRules.AdversityStatMultiplier : 1f;
    // 克己の攻撃・防御・素早さだけに掛ける倍率。HPや他の能力には適用しない。
    [System.NonSerialized] public float StoicStatMultiplier = 1f;
    // 執念の攻撃力だけに掛ける倍率。
    [System.NonSerialized] public float PersonalityAttackMultiplier = 1f;
    // 毒・封印を受けた次のターン。0は発動待ちの状態異常がないことを表す。
    [System.NonSerialized] public int ObsessionStartTurn;
    // スタンで行動を休んだ次のターン。この1ターンだけ攻撃力を2倍にする。
    [System.NonSerialized] public int ObsessionStunRecoveryTurn;
    // 性格効果や耐性の計算に使う現在のターン番号。
    [System.NonSerialized] public int BattleTurn;
    // 克己の攻撃・防御・素早さの上昇に使う被ダメージ回数。適応は下の6種類の耐性を個別に使う。
    [System.NonSerialized] public int DamageTakenCount;
    // 適応の物理ダメージ軽減率（％）。通常攻撃で+5、強撃・絡め手の追加攻撃で+10。全身全霊を受けて生存すると物理・魔法が100。
    [System.NonSerialized] public int PhysicalAdaptation;
    // 適応の魔法ダメージ軽減率（％）。魔法の命中で通常+10、受ける側に有利なら+20、不利なら+5ポイント。
    [System.NonSerialized] public int MagicAdaptation;
    // 適応の毒耐性。毒ダメージを受けるごとに軽減率+10ポイント。
    [System.NonSerialized] public int PoisonAdaptation;
    // 適応のスタン耐性。スタンの付与成功で+10ポイント。
    [System.NonSerialized] public int StunAdaptation;
    // 爆破耐性。相手の爆弾・自分への巻き込みでHPが減るたび+10ポイント。
    [System.NonSerialized] public int ExplosionAdaptation;
    // 強攻撃失敗時の反動耐性。HPが減るたび+10ポイント。
    [System.NonSerialized] public int RecoilAdaptation;
    // 不屈が致死ダメージを耐えた回数。初回保証の判定にも使う。
    [System.NonSerialized] public int EndureCount;
    // 短気が「我慢」した回数。「我慢の限界」の使用時と試合開始時に0へ戻す。
    [System.NonSerialized] public int PatienceWaitCount;
    // 無能の覚醒が済んだかどうか。
    [System.NonSerialized] public bool Awakened;
    // 陰湿が相手に弱体無効を確認したかどうか。
    [System.NonSerialized] public bool DebuffRejected;
    // モンスター固有の弱体耐性。性格倍率のリセット対象にしない。
    [System.NonSerialized] public bool DebuffImmune;

    // 性格倍率を反映した能力値。HP・命中・回避はゲーム内の上限・下限も適用する。
    public int GetCurrentMaxHp() => Mathf.Max(1, Mathf.RoundToInt(Stats.MaxHp * PersonalityMultiplier)); // 最大HPは最低1。回復上限やAIのHP割合の基準になる。
    public int GetCurrentSpeed() => Mathf.Max(0, Mathf.RoundToInt(Stats.Speed * PersonalityMultiplier * StoicStatMultiplier * AdversityMultiplier) + GrowthSpeedBonus); // 毎ターン開始時に比較し、大きい側から行動する。
    public int GetCurrentGrowth() => AdversityActive ? PersonalityRules.AdversityGrowthRate : Mathf.Clamp(Mathf.RoundToInt(Stats.Growth * PersonalityMultiplier), 0, 100); // 逆境中は成長率100%。
    public int GetCurrentAccuracy() => Mathf.Clamp(Mathf.RoundToInt(Stats.Accuracy * PersonalityMultiplier) + GrowthAccuracyBonus, 0, 100); // 攻撃命中率への加算ポイント。初期値の最大30は生成側で設定する。
    public int GetCurrentEvasion() => Mathf.Clamp(Mathf.RoundToInt(Stats.Evasion * PersonalityMultiplier) + GrowthEvasionBonus, 0, 100); // 相手の攻撃命中率から引くポイント。成長・性格倍率で初期上限30を超えられる。

    /// <summary>
    /// 実際に戦闘で使う攻撃力
    /// </summary>
    public int GetCurrentAttack()
    {
        int attack = Mathf.Max(1, Mathf.RoundToInt(Stats.Attack * PersonalityMultiplier * StoicStatMultiplier * AdversityMultiplier) + AttackBuff); // 克己・逆境の倍率は加算バフより先に適用。
        return Mathf.Max(1, Mathf.RoundToInt(attack * PersonalityAttackMultiplier) + GrowthAttackBonus); // 執念は加算バフ込みで2倍。ターン成長は倍率の後に加算。
    }

    /// <summary>
    /// 実際に戦闘で使う防御力
    /// </summary>
    public int GetCurrentDefense()
    {
        return Mathf.Max(0, Mathf.RoundToInt(Stats.Defense * PersonalityMultiplier * StoicStatMultiplier * AdversityMultiplier) + DefenseBuff + GrowthDefenseBonus); // 基本防御×倍率＋補正＋ターン成長。最低0。
    }

    /// <summary>
    /// 実際に戦闘で使う会心率
    /// </summary>
    public int GetCurrentCriticalRate()
    {
        return Mathf.Clamp(Mathf.RoundToInt(Stats.CriticalRate * PersonalityMultiplier) + CriticalBuff, 0, 100); // 基本会心率×倍率＋補正を0～100%に制限する。
    }
}
