
/// <summary>
/// 行動AIの方針を決める性格。ActionAI がこの値に応じて所持スキルの選び方を切り替える。
/// MonsterSpawnRules のモード別候補から抽選し、MonsterFactory が所持スキルを作る。
/// ランキング限定の性格は末尾に追加し、RankingOnlyPersonalities へ登録する。
/// </summary>

public enum PersonalityType
{
    Bold,       // 0・大胆：毎回 Power が最大のスキルを選ぶ。同値なら一覧で先にあるものを優先する。
    Calm,       // 1・冷静：最初の2ターンは防御強化か回復（なければ先頭）、3ターン目以降は Power 最大を選ぶ。
    faith,   // 2・信仰：相手の直近のスキルと同じ種別を自分の所持スキルから選び、なければランダムに選ぶ。
    Taunt,      // 3・挑発：奇数ターンは Power が最小、偶数ターンは Power が最大のスキルを選ぶ。
    Aggressive, // 4・挑戦：SuccessRate（スキルの成功率）が最小のスキルを選ぶ。同率なら一覧で先にあるものを優先する。
    Timid,      // 5・臆病
    Delusional, // 厨二
    Inept,      // 無能
    Stoic,      // 克己
    Explorer,   // 探求
    Devoted,    // 献身
    Adversity,  // 逆境
    Unyielding, // 不屈
    Impatient,  // 短気
    Headlong,   // 猛進
    Obsessive,  // 執念
    Sincere,    // 誠実
    Insidious,  // 陰湿
    Adaptive,   // 適応
    Flexible    // 柔軟
}


