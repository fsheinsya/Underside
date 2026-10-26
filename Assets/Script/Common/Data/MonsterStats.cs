// MonsterFactoryが作る基本能力。戦闘中の現在値はMonsterData.GetCurrent...から取得する。
// 生成範囲はMonsterFactory.CreateRandom、オッズ用の重みはBetOddsCalculatorで調整する。
[System.Serializable] // Unityが入れ子のデータとして保存・表示できるようにする。
public class MonsterStats
{
    public int MaxHp; // 基本最大HP。試合開始時のHPと、性格倍率反映前の回復上限。
    public int Attack; // 基本攻撃力。DamageCalculatorが補正後の値からダメージを算出する。
    public int Defense; // 基本防御力。ダメージ式では補正後の防御力を2で整数除算して差し引く。
    [UnityEngine.Range(0, 100)] public int Growth; // ターン終了時の成長確率（%）。30なら30%、最大100%。
    public int Speed; // 補正後の値が大きい側が先手。同値なら毎ターン等確率で抽選する。
    [UnityEngine.Range(0, 30)] public int Accuracy; // 初期最大30。1につき攻撃命中率を1ポイント上げる。
    [UnityEngine.Range(0, 30)] public int Evasion; // 初期最大30。1につき相手の攻撃命中率を1ポイント下げる。
    public int CriticalRate; // 基本会心率（%）。性格倍率・CriticalBuff反映後の値で会心を抽選する。
}
