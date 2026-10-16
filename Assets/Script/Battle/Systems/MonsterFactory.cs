using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// モンスター生成クラス
/// 第2引数で渡された画像を、そのままモンスター画像として使う
/// </summary>
public class MonsterFactory
{
    /// <summary>
    /// ランダムなモンスターを1体作る
    /// </summary>
    public MonsterData CreateRandom(string monsterName, Sprite icon = null)
    {
        // 基本能力値をランダム生成する
        int hp = Random.Range(45, 91);
        int atk = Random.Range(8, 26);
        int def = Random.Range(8, 26);
        int growth = Random.Range(5, 31);
        int speed = Random.Range(5, 31);
        int accuracy = Random.Range(65, 96);
        int evasion = Random.Range(5, 26);
        int critical = Random.Range(5, 21);

        // 属性、性格、特殊能力をランダム決定する
        ElementType element = GetRandomElement(); // 属性スキルと通常・強攻撃の属性に使う属性を抽選する。
        PersonalityType personality = GetRandomPersonality(); // 所持スキルの組み合わせと戦闘中の選び方を決める性格を抽選する。
        SpecialAbilityType specialAbility = GetRandomSpecialAbility();

        // 性格に対応した所持スキルを2つ作る。AIは原則としてこのリストから行動を選ぶ。
        List<SkillData> skills = new List<SkillData> // 格納順はPowerなどが同値だった場合のAIの優先順にもなる。
        {
            CreateSkillByPersonality(personality, element, 0), // index=0の分岐で、1つ目の所持スキルを作る。
            CreateSkillByPersonality(personality, element, 1) // indexが0以外の分岐で、2つ目の所持スキルを作る。
        };

        // モンスター情報を作って返す
        return new MonsterData
        {
            Name = monsterName,
            Icon = icon, // ← ここにスライム画像が入る
            Stats = new MonsterStats
            {
                MaxHp = hp,
                Attack = atk,
                Defense = def,
                Growth = growth,
                Speed = speed,
                Accuracy = accuracy,
                Evasion = evasion,
                CriticalRate = critical
            },
            CurrentHp = hp,
            SpecialAbility = specialAbility,
            Element = element, // 属性相性と通常・強攻撃の属性として使う。
            Personality = personality, // ActionAIが毎回のスキル選択で参照する行動方針を保存する。
            StatusAilment = StatusAilmentType.None,
            Skills = skills, // 性格に応じて生成した2つのスキルをAIの選択候補として保存する。
            AttackBuff = 0,
            DefenseBuff = 0,
            CriticalBuff = 0,
            IsInvincible = false,
            IsStunnedThisTurn = false
        };
    }

    /// <summary>
    /// 属性をランダムに決める
    /// </summary>
    private ElementType GetRandomElement()
    {
        int value = Random.Range(0, 2); // 整数の上限2は含まないため、現状はFire(0)かWater(1)だけを抽選し、Grass(2)は出ない。
        return (ElementType)value; // 抽選した整数を属性の列挙値へ変換する。
    }

    /// <summary>
    /// 6種類の性格を同じ確率で抽選する。性格未設定に相当する列挙値はない。
    /// </summary>
    private PersonalityType GetRandomPersonality() // 新しいモンスターの性格を1つ決める。
    {
        int value = Random.Range(0, 6); // 整数の上限6は含まないため、0～5の6通りを等確率で選ぶ。
        return (PersonalityType)value; // 列挙順に、大胆・冷静・信仰・挑発・挑戦・臆病のいずれかに変換する。
    }

    /// <summary>
    /// 特殊能力をランダムに決める
    /// </summary>
    private SpecialAbilityType GetRandomSpecialAbility()
    {
        int value = Random.Range(0, 6);//なしに基本的にならないように設定
        return (SpecialAbilityType)value;
    }

    /// <summary>
    /// 性格に応じて所持スキルを作る。indexが0なら1つ目、0以外なら2つ目のスキルを返す。
    /// PowerはAIの大小比較にも使う共通値で、攻撃ならダメージ計算への加算値、回復なら回復量になる。
    /// SuccessRateはSkillExecutorが効果の実行前に行う成功判定の百分率で、能力値のAccuracy（命中率）とは別。
    /// </summary>
    private SkillData CreateSkillByPersonality(PersonalityType personality, ElementType element, int index) // 性格・属性・スキルの位置から設定を作る。
    {
        SkillData normalAttack = new SkillData // 大胆・信仰・既定分岐で使う通常攻撃を用意する。
        {
            SkillName = "通常攻撃", // 行動ログなどに表示する名前。
            SkillType = SkillType.NormalAttack, // 使用者の属性で相手にダメージを与える処理を指定する。
            Power = 0, // ダメージ計算への追加値は0。基本攻撃力などによるダメージは発生する。
            SuccessRate = 100 // 実行前の成功判定は必ず通るが、スキル封印などの制限は別に受ける。
        };

        SkillData elementSkill = CreateElementSkill(element); // 属性魔法を用意する。実際に返すのは冷静の2つ目か既定分岐の2つ目だけ。

        switch (personality) // 抽選した性格に対応する2つの所持スキルを振り分ける。
        {
            case PersonalityType.Bold: // 大胆：通常攻撃と強撃。AIはPower最大の強撃を選ぶ。
                return index == 0 ? normalAttack : new SkillData // 1つ目は通常攻撃、2つ目は以下の強撃を返す。
                {
                    SkillName = "強撃", // 強攻撃の表示名。
                    SkillType = SkillType.StrongAttack, // 使用者の属性でダメージを与える強攻撃として実行する。
                    Power = 12, // ダメージ計算への加算値を12にし、Power=0の通常攻撃よりAIに優先させる。
                    SuccessRate = 65 // 効果の実行前に65%で成功し、失敗するとダメージ処理に進まない。
                };

            case PersonalityType.Calm: // 冷静：防御集中と属性魔法。AIは全体の1・2ターン目は防御集中、以降は魔法を選ぶ。
                return index == 0 ? new SkillData // 1つ目には以下の防御力強化スキルを作る。
                {
                    SkillName = "防御集中", // 防御力強化の表示名。
                    SkillType = SkillType.DefenseBuff, // 実行時に自分のDefenseBuffを5加算する。
                    Power = 0, // 強化量には使われず、AIのPower比較では0として扱う。
                    SuccessRate = 100 // 実行前の成功判定は100%で通る。
                } : elementSkill; // indexが0以外なら、使用者の属性に対応する魔法を返す。

            case PersonalityType.faith: // 信仰：通常攻撃と祈り。相手の直前の技と同じ種別を所持していれば選び、なければランダムに選ぶ。
                return index == 0 ? normalAttack : new SkillData // 1つ目は通常攻撃、2つ目は以下の祈りを返す。
                {
                    SkillName = "祈り", // ランダム効果を起こすスキルの表示名。
                    SkillType = SkillType.RandomSkill, // 成功分岐では、自分が持つRandomSkill以外の技を再実行する。この組み合わせでは通常攻撃が候補。
                    Power = 0, // 祈り自体の効果量には使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 100 // 祈りの処理開始は100%成功するが、その中で別途、成功・何も起きない・天罰を抽選する。
                };

            case PersonalityType.Taunt: // 挑発：挑発の構えと反撃強打。AIは全体の奇数ターンに前者、偶数ターンに後者を選ぶ。
                return index == 0 ? new SkillData // 1つ目には以下の攻撃力強化スキルを作る。
                {
                    SkillName = "挑発の構え", // 表示名は挑発だが、実際の効果は自分の攻撃力強化。
                    SkillType = SkillType.AttackBuff, // 実行時に自分のAttackBuffを5加算する。
                    Power = 0, // 強化量には使われず、奇数ターンのPower最小スキルとして選ばれる。
                    SuccessRate = 100 // 実行前の成功判定は100%で通る。
                } : new SkillData // indexが0以外なら、以下の強攻撃を作る。
                {
                    SkillName = "反撃強打", // 表示名は反撃だが、被弾を条件に発動する仕組みはない。
                    SkillType = SkillType.StrongAttack, // 自分の行動時に使用者の属性でダメージを与える。
                    Power = 15, // ダメージ計算への加算値を15にし、偶数ターンのPower最大スキルとして選ばれる。
                    SuccessRate = 75 // 効果の実行前に75%で成功する。
                };

            case PersonalityType.Aggressive: // 挑戦：即死狙いと乱数魔技。AIはSuccessRateが低い即死狙いを選ぶ。
                return index == 0 ? new SkillData // 1つ目には以下の即死スキルを作る。
                {
                    SkillName = "即キル狙い", // 即死効果の表示名。
                    SkillType = SkillType.InstantDeath, // 実行前の成功判定を通った後、別の15%判定に成功すると相手のHPを0にする。
                    Power = 0, // 即死の確率や効果には使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 2.5 // 2.5%の確率で相手を即シさせる
                } : new SkillData // indexが0以外なら、以下のランダムスキルを作る。
                {
                    SkillName = "乱数魔技", // 祈りと同じRandomSkill処理を使う別名のスキル。
                    SkillType = SkillType.RandomSkill, // 成功分岐で再実行する候補は所持スキルから選ぶ。この組み合わせでは即死狙いが候補。
                    Power = 0, // ランダム効果そのものには使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 100 // 処理開始は100%成功するが、内部の効果抽選や再実行する技の成功判定は別に行う。
                };

            case PersonalityType.Timid: // 臆病：回復と毒霧。AIはHPが最大HPの整数除算による半分以下なら回復、それ以外ではこの2つからランダムに選ぶ。
                return index == 0 ? new SkillData // 1つ目には以下の回復スキルを作る。この所持スキルにはAIが探す無敵スキルはない。
                {
                    SkillName = "回復", // HP回復の表示名。
                    SkillType = SkillType.Heal, // 自分のHPを回復し、最大HPを超えないようにする。
                    Power = 20, // 攻撃ダメージではなく回復量20として使う。AIのPower比較でも同じ値を参照する。
                    SuccessRate = 100 // 実行前の成功判定は100%で通る。
                } : new SkillData // indexが0以外なら、以下の毒付与スキルを作る。
                {
                    SkillName = "毒霧", // 毒状態を与えるスキルの表示名。
                    SkillType = SkillType.PoisonDebuff, // 相手の状態異常を毒に設定する。継続ダメージはターン終了時の別処理で発生する。
                    Power = 0, // 毒のダメージ量には使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 70 // 70%で成功した場合に毒を付与する。
                };

            default: // 定義済みの6性格に一致しない値が渡された場合の予備処理。
                return index == 0 ? normalAttack : elementSkill; // 1つ目は通常攻撃、0以外は属性魔法を返す。
        }
    }

    /// <summary>
    /// 属性に対応した魔法スキルを返す。攻撃属性はSkillTypeに応じてSkillExecutorが決める。
    /// </summary>
    private SkillData CreateElementSkill(ElementType element) // 性格別スキルの候補として、指定属性の魔法を作る。
    {
        switch (element) // モンスターの属性に対応する魔法へ分岐する。
        {
            case ElementType.Fire: // 火属性なら炎魔法を選ぶ。
                return new SkillData { SkillName = "炎魔法", SkillType = SkillType.FireMagic, Power = 10, SuccessRate = 90 }; // 表示名・火魔法種別・ダメージ加算10・実行前成功率90%を設定する。
            case ElementType.Water: // 水属性なら水魔法を選ぶ。
                return new SkillData { SkillName = "水魔法", SkillType = SkillType.WaterMagic, Power = 10, SuccessRate = 90 }; // 表示名・水魔法種別・ダメージ加算10・実行前成功率90%を設定する。
            case ElementType.Grass: // 草属性なら草魔法を選ぶ。ただし現在のGetRandomElementの抽選からは草属性は出ない。
                return new SkillData { SkillName = "草魔法", SkillType = SkillType.GrassMagic, Power = 10, SuccessRate = 90 }; // 表示名・草魔法種別・ダメージ加算10・実行前成功率90%を設定する。

            default: // 上記の属性以外に対応する魔法は定義していない。
                return null; // 代替スキルは生成せずnullを返すため、この値を使う呼び出し側には注意が必要。
        }
    }
}