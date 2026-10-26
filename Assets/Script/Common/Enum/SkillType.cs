/// <summary>
/// 技の効果を識別する種別。表示名ではなく、この値でActionAIの検索とSkillExecutorの実行を分岐する。
/// 新種別の追加時は生成・選択・実行・コピー許可・耐性・演出の対応箇所を確認する。
/// 保存された数値との対応を保つため、既存項目の並び順は変更しない。
/// </summary>
public enum SkillType
{
    NormalAttack, // 通常攻撃。封印中も使用可能。使用者の属性を使う。
    StrongAttack, // 強攻撃。Powerを追加威力に使い、失敗時は自傷する。
    Heal, // 使用者を回復。回復量はPower、最低10。
    FireMagic, // 炎魔法。魔法PowerはAI比較用でダメージには加算しない。
    WaterMagic, // 水魔法。
    GrassMagic, // 草魔法。
    ThunderMagic, // 予約された種別。現在のFactory・Executorには雷魔法の生成・効果分岐がない。
    RockMagic, // 予約された種別。現在のFactory・Executorには岩魔法の生成・効果分岐がない。
    DarkMagic, // 闇魔法。
    LightMagic, // 光魔法。
    AttackBuff, // 使用者の攻撃補正+5。
    DefenseBuff, // 使用者の防御補正+5。
    CriticalBuff, // 使用者の会心率補正+20ポイント。
    Invincible, // 次の自分の行動機会まで、ApplyDamage経由の攻撃を無効化。
    StunDebuff, // 相手の次の行動機会を1回休ませる。
    PoisonDebuff, // 相手を毒にする。継続ダメージはターン終了時に処理。
    SkillSealDebuff, // 相手の技を封印。毒・スタンと共存する別フラグを使う。
    DefenseDown, // 相手の防御補正-5。
    InstantDeath, // 相手の残HP全量を減らす。不屈で耐える余地はある。
    RandomSkill, // 祈りの奇跡40%・不発20%・天罰40%を実行。
    None, // 行動履歴なし、または攻撃種別未指定。enumの既定値0はこれではなくNormalAttack。
    Delusion, // 中二の自称必殺技。効果なし。
    Dig, // 探求の発掘。4種類のアイテムから抽選。
    AllOutAttack, // 献身の全身全霊。現在HPを追加威力にし、自分のHPは1になる。
    Wait, // 待機。封印中も使用可能で、HPや能力は変化しない。
    BattleCycle, // 猛進の表示用定義。通常はActionAIが実際に使う技へ展開する。
    DebuffCombo, // 絡め手。毒→防御低下→封印の順に1段階ずつ進める。
    Mimic // 柔軟の表示用定義。通常はActionAIが相手の技を別のSkillDataへ複製する。
}
