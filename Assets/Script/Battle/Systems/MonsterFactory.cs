using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// モンスター生成クラス
/// モードに対応した候補から生成し、渡された画像をモンスター画像として使う。
/// </summary>
public class MonsterFactory
{
    /// <summary>
    /// ランダムなモンスターを1体作る
    /// </summary>
    /// <param name="monsterName">画面に表示するモンスター名。</param>
    /// <param name="mode">Selectで選択したモード。出現候補の制限に使うため省略できない。</param>
    /// <param name="icon">表示する画像。未設定の場合も属性・性格の抽選ルールは同じ。</param>
    public MonsterData CreateRandom(string monsterName, ModeType mode, Sprite icon = null)
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
        // モードによる出現制限はMonsterSpawnRulesにまとめる。能力値と特殊能力は両モード共通。
        ElementType element = MonsterSpawnRules.GetRandomElement(mode);
        PersonalityType personality = MonsterSpawnRules.GetRandomPersonality(mode);
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
    /// 特殊能力をランダムに決める
    /// </summary>
    private SpecialAbilityType GetRandomSpecialAbility()
    {
        int value = Random.Range(0, 6);//なしに基本的にならないように設定
        return (SpecialAbilityType)value;
    }

    /// <summary>
    /// 性格に応じて所持スキルを作る。indexが0なら1つ目、0以外なら2つ目のスキルを返す。
    /// PowerはAIの大小比較にも使う共通値で、物理攻撃ならダメージ計算への加算値、回復なら回復量になる。属性魔法ではAI比較だけに使う。
    /// SuccessRateはSkillExecutorが効果の実行前に行う成功判定の百分率で、能力値のAccuracy（命中率）とは別。
    /// </summary>
    private SkillData CreateSkillByPersonality(PersonalityType personality, ElementType element, int index) // 性格・属性・スキルの位置から設定を作る。
    {
        SkillData normalAttack = CreateNormalAttack(); // 生成時も、AIが代わりに攻撃するときも同じ通常攻撃の定義を使う。

        SkillData elementSkill = CreateElementSkill(element); // 冷静・執念など、性格ごとの分岐で所持技として返す属性魔法を用意する。

        switch (personality) // 抽選した性格に対応する2つの所持スキルを振り分ける。
        {
            case PersonalityType.Delusional: // 中二：2つ目は実行時に効果が発生しない技。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "超絶滅亡王漆黒波",
                    SkillType = SkillType.Delusion,
                    Power = 0,
                    SuccessRate = 100
                };
            case PersonalityType.Inept: // 無能：覚醒後に選べる強攻撃を2つ目に持たせる。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "???",
                    SkillType = SkillType.StrongAttack,
                    Power = 20,
                    SuccessRate = 100
                };
            case PersonalityType.Stoic:
            case PersonalityType.Adversity:
            case PersonalityType.Unyielding:
            case PersonalityType.Obsessive:
                return index == 0 ? normalAttack : elementSkill;
            case PersonalityType.Explorer: // 探求：2つ目は発掘の抽選処理へ進む技。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "発掘",
                    SkillType = SkillType.Dig,
                    Power = 0,
                    SuccessRate = 100
                };
            case PersonalityType.Devoted: // 献身：2つ目は現在HPを使う全力攻撃。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "全身全霊",
                    SkillType = SkillType.AllOutAttack,
                    Power = 0,
                    SuccessRate = 100
                };
            case PersonalityType.Impatient: // 短気：我慢と強攻撃をそれぞれ作る。
                if (index == 0)
                {
                    return new SkillData
                    {
                        SkillName = "我慢",
                        SkillType = SkillType.Wait,
                        Power = 0,
                        SuccessRate = 100
                    };
                }
                return new SkillData
                {
                    SkillName = "我慢の限界",
                    SkillType = SkillType.StrongAttack,
                    Power = 18,
                    SuccessRate = 100
                };
            case PersonalityType.Headlong: // 猛進：戦法転換はAIがターンに応じた実際の技へ展開する。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "戦法転換",
                    SkillType = SkillType.BattleCycle,
                    Power = 0,
                    SuccessRate = 100
                };
            case PersonalityType.Sincere: // 誠実：1つ目に攻撃強化、2つ目に属性魔法を持たせる。
                if (index != 0) return elementSkill;
                return new SkillData
                {
                    SkillName = "実直な鍛錬",
                    SkillType = SkillType.AttackBuff,
                    Power = 0,
                    SuccessRate = 100
                };
            case PersonalityType.Insidious: // 陰湿：2つ目の絡め手で弱体を段階的に付与する。
                if (index == 0) return normalAttack;
                return CreateDebuffCombo();
            case PersonalityType.Adaptive: // 適応：2つ目にHP回復の再生を持たせる。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "再生",
                    SkillType = SkillType.Heal,
                    Power = 15,
                    SuccessRate = 100
                };
            case PersonalityType.Flexible: // 柔軟：模倣はAIが相手の直近の技へ展開する。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "模倣",
                    SkillType = SkillType.Mimic,
                    Power = 0,
                    SuccessRate = 100
                };
            case PersonalityType.Bold: // 大胆：通常攻撃と強撃。AIはPower最大の強撃を選ぶ。
                return index == 0 ? normalAttack : CreateStrongAttack();

            case PersonalityType.Calm: // 冷静：防御集中と属性魔法。AIは全体の1・2ターン目は防御集中、以降は魔法を選ぶ。
                return index == 0 ? CreateDefenseBuff() : elementSkill;

            case PersonalityType.faith: // 信仰：通常攻撃と祈り。相手の直前の技と同じ種別を所持していれば選び、なければランダムに選ぶ。
                return index == 0 ? normalAttack : new SkillData // 1つ目は通常攻撃、2つ目は以下の祈りを返す。
                {
                    SkillName = "祈り", // ランダム効果を起こすスキルの表示名。
                    SkillType = SkillType.RandomSkill, // 奇跡では相手に固定ダメージ・ランダムデバフ・即死のいずれか1つを起こす。
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

            case PersonalityType.Aggressive: // 挑戦：所持技は即死狙いと乱数魔技。AIは即死狙いのみ選び、封印中は待機する。
                return index == 0 ? new SkillData // 1つ目には以下の即死スキルを作る。
                {
                    SkillName = "即キル狙い", // 即死効果の表示名。
                    SkillType = SkillType.InstantDeath, // 成功判定を通ると相手のHPを0にする（不屈は踏みとどまれる）。
                    Power = 0, // 即死の確率や効果には使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 2.5 // 2.5%の確率で相手を即シさせる
                } : new SkillData // indexが0以外なら、以下のランダムスキルを作る。
                {
                    SkillName = "乱数魔技", // 祈りと同じRandomSkill処理を使う別名のスキル。
                    SkillType = SkillType.RandomSkill, // 祈りと共通の奇跡・不発・天罰処理。効果は所持技に依存しない。
                    Power = 0, // ランダム効果そのものには使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 100 // 処理開始は100%成功するが、内部の効果抽選や再実行する技の成功判定は別に行う。
                };

            case PersonalityType.Timid: // 臆病：回復と毒霧。AIはHPが最大HPの整数除算による半分以下なら回復、それ以外ではこの2つからランダムに選ぶ。
                return index == 0 ? CreateHeal() : new SkillData // indexが0以外なら、以下の毒付与スキルを作る。
                {
                    SkillName = "毒霧", // 毒状態を与えるスキルの表示名。
                    SkillType = SkillType.PoisonDebuff, // 相手の状態異常を毒に設定する。継続ダメージはターン終了時の別処理で発生する。
                    Power = 0, // 毒のダメージ量には使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 70 // 70%で成功した場合に毒を付与する。
                };

            default: // 定義済みの性格に一致しない値が渡された場合の予備処理。
                return index == 0 ? normalAttack : elementSkill; // 1つ目は通常攻撃、0以外は属性魔法を返す。
        }
    }

    // 共通技の定義はここで確認する。技ごとに全設定値を明記し、呼ぶたびに新しいデータを作る。
    /// <summary>全性格で共有する「通常攻撃」。所持技の生成時と、代わりの攻撃を選ぶときに使う。</summary>
    public static SkillData CreateNormalAttack()
    {
        return new SkillData // 毎回新しく作り、コピーなどで他のモンスターの技が書き換わることを防ぐ。
        {
            SkillName = "通常攻撃", // 行動ログの表示名。
            SkillType = SkillType.NormalAttack, // 使用者の属性で攻撃。スキル封印中も使用できる。
            Power = 0, // 技の追加威力は0だが、使用者の攻撃力によるダメージは発生する。
            SuccessRate = 100 // 通常攻撃の成功判定は必ず通る。
        };
    }

    /// <summary>大胆・猛進や、代わりの攻撃として使う共通の「強撃」。調整はこの1か所で行う。</summary>
    public static SkillData CreateStrongAttack()
    {
        return new SkillData
        {
            SkillName = "強撃",
            SkillType = SkillType.StrongAttack,
            Power = 12, // ダメージ計算へ加える威力。
            SuccessRate = 65 // 大胆と同じ成功率。失敗した場合はダメージが発生しない。
        };
    }

    /// <summary>陰湿と猛進が使う「絡め手」。毒・防御低下・封印の順で相手を弱体化する。</summary>
    public static SkillData CreateDebuffCombo()
    {
        return new SkillData
        {
            SkillName = "絡め手",
            SkillType = SkillType.DebuffCombo,
            Power = 0, // 効果の段階はSkillExecutor.ApplyDebuffで判定する。
            SuccessRate = 100
        };
    }

    /// <summary>臆病と猛進が使う「回復」。最大HPを上限に20回復する。</summary>
    public static SkillData CreateHeal()
    {
        return new SkillData
        {
            SkillName = "回復",
            SkillType = SkillType.Heal,
            Power = 20, // この技では回復量として使う。
            SuccessRate = 100
        };
    }

    /// <summary>冷静と猛進が使う「防御集中」。実行時に防御補正を5加算する。</summary>
    public static SkillData CreateDefenseBuff()
    {
        return new SkillData
        {
            SkillName = "防御集中",
            SkillType = SkillType.DefenseBuff,
            Power = 0, // 強化量5はSkillExecutorで適用する。
            SuccessRate = 100
        };
    }

    /// <summary>
    /// 属性に対応した魔法スキルを返す。攻撃属性はSkillTypeに応じてSkillExecutorが決める。
    /// 生成時と、AIが代わりの攻撃を選ぶときに共用する。使用者の属性を渡す。
    /// 光・闇の出現可否は生成時のMonsterSpawnRulesで判定済み。ここでは対応する技を作る。
    /// 全5属性とも成功率90％。有利時は攻撃力2倍、それ以外は通常攻撃相当の計算を使う。
    /// Power=10は既存AIが魔法を選ぶための比較値で、魔法ダメージへの加算には使わない。
    /// </summary>
    public static SkillData CreateElementSkill(ElementType element) // 指定属性の魔法を作る共通の入口。
    {
        switch (element) // モンスターの属性に対応する魔法へ分岐する。
        {
            case ElementType.Fire: // 火属性なら炎魔法を選ぶ。
                return new SkillData { SkillName = "炎魔法", SkillType = SkillType.FireMagic, Power = 10, SuccessRate = 90 };
            case ElementType.Water: // 水属性なら水魔法を選ぶ。
                return new SkillData { SkillName = "水魔法", SkillType = SkillType.WaterMagic, Power = 10, SuccessRate = 90 };
            case ElementType.Grass: // 草属性なら草魔法を選ぶ。
                return new SkillData { SkillName = "草魔法", SkillType = SkillType.GrassMagic, Power = 10, SuccessRate = 90 };
            case ElementType.Light:
                return new SkillData { SkillName = "光魔法", SkillType = SkillType.LightMagic, Power = 10, SuccessRate = 90 };
            case ElementType.Dark:
                return new SkillData { SkillName = "闇魔法", SkillType = SkillType.DarkMagic, Power = 10, SuccessRate = 90 };
            default:
                // 新属性の魔法を作り忘れた場合、nullのスキルを持たせず生成時点で知らせる。
                throw new System.ArgumentOutOfRangeException(nameof(element), element, "属性魔法が未定義です。");
        }
    }
}
