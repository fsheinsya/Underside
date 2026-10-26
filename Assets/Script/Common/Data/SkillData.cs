/// <summary>
/// 技1つ分の設定。MonsterFactoryが生成し、ActionAIが選び、SkillExecutorが効果を実行する。
/// ここに値を追加しても、それを読む選択処理・実行処理がなければ挙動は変わらない。
/// </summary>
[System.Serializable]
public class SkillData
{
    public string SkillName; // 行動ログなどに表示する技名。ActionAI は名前では選ばない。
    public SkillType SkillType; // 効果の種類。ActionAI が防御・回復・無敵化を探すときや、信仰AIの種別一致判定に使う。
    public ElementType Element; // 属性を保持する欄。現在の実行側は魔法ならSkillType、通常・強攻撃なら使用者のElementから攻撃属性を決める。

    public int Power; // AIの選択基準。物理攻撃では追加威力、回復では回復量（最低10）。属性魔法では選択優先度だけに使う。
    public float DamageMultiplier = 1f; // 攻撃ダメージ全体の倍率。通常は1倍、柔軟のコピーはコピー元の80％。
    public bool UsesPatienceCharge; // 短気の「我慢の限界」専用。表示名に依存せず、我慢の蓄積を消費する。
    public double SuccessRate; // 技の実行成功率（%）。例：2.5は2.5%。行動選択の70/15/15とは別で、現在のAIはこの値の大小で技を選ばない。

    // 以下は現在のActionAI・SkillExecutorでは参照していない設定欄。調整時は実際に読まれる値を変更する。
    public int HealAmount; // 現在の回復量はこの欄ではなくPower（最低10）で決まる。
    public int BuffAmount; // 現在の強化量はSkillExecutorのAttackBuff・DefenseBuff・CriticalBuff分岐に直接書かれている。
    public int DebuffAmount; // 現在の防御低下量はSkillExecutor側の固定値5を使う。

    public StatusAilmentType InflictAilment; // 現在の状態異常の種類はSkillTypeの分岐で決まる。
    public int InflictRate; // 現在の状態異常の付与判定はSuccessRateと相手の耐性を使う。

    public bool IsHighRisk; // 現在のAIはこのフラグで危険度を評価しない。
    public bool IsRandomSkill; // 現在の祈り処理はこのフラグではなくSkillType.RandomSkillで判定する。
}
