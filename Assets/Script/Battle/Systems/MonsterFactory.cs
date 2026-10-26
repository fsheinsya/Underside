using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// モンスター生成クラス
/// モードに対応した候補から生成し、渡された画像をモンスター画像として使う。
/// ここは「持っている技の定義」、ActionAIは「今回使う技の選択」、SkillExecutorは「効果の実行」。
/// 所持技にあるだけで必ず使うとは限らない。共通攻撃枠や猛進・柔軟は一時的な技も作る。
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
        int hp = Random.Range(45, 91); // 基本最大HPは45～90。整数版Rangeの上限91は含まない。
        int atk = Random.Range(8, 26); // 基本攻撃力は8～25。
        int def = Random.Range(8, 26); // 基本防御力は8～25。
        int growth = Random.Range(5, 101); // 成長率は5～100%。ターン終了時に成長する確率。
        int speed = Random.Range(5, 31); // 速さは5～30。補正・成長後の値で毎ターンの先手を決める。
        int accuracy = Random.Range(5, 31); // 初期命中は5～30。技成功率とは別に、攻撃命中率へ加算する。
        int evasion = Random.Range(5, 31); // 初期回避は5～30。相手の攻撃命中率から差し引く。
        int critical = Random.Range(5, 21); // 基本会心率は5～20%。実際の会心判定では性格倍率・バフを加味する。

        // 属性、性格、特殊能力をランダム決定する
        // モードによる出現制限はMonsterSpawnRulesにまとめる。能力値と特殊能力は両モード共通。
        ElementType element = MonsterSpawnRules.GetRandomElement(mode); // ストーリーは3属性、ランキングは光・闇を含む5属性から抽選する。
        PersonalityType personality = MonsterSpawnRules.GetRandomPersonality(mode); // ストーリーは6性格、ランキングは追加14性格を含む候補から抽選する。
        SpecialAbilityType specialAbility = GetRandomSpecialAbility(); // 特殊能力の表示用データを作る。現在の戦闘処理には専用の効果分岐はない。

        // 性格に対応した所持スキルを2つ作る。AIは原則としてこのリストから行動を選ぶ。
        List<SkillData> skills = new List<SkillData> // 格納順はPowerなどが同値だった場合のAIの優先順にもなる。
        {
            CreateSkillByPersonality(personality, element, 0), // index=0の分岐で、1つ目の所持スキルを作る。
            CreateSkillByPersonality(personality, element, 1) // indexが0以外の分岐で、2つ目の所持スキルを作る。
        };

        // モンスター情報を作って返す
        return new MonsterData // 基本能力と技をまとめた新しい1体を返す。ここで戦闘は開始しない。
        {
            Name = monsterName, // 呼び出し元が指定した表示名を保持する。
            Icon = icon, // ← ここにスライム画像が入る
            Stats = new MonsterStats // 戦闘中の一時補正とは別に、生成時の基本能力を保存する。
            {
                MaxHp = hp, // 基本最大HP。
                Attack = atk, // 基本攻撃力。
                Defense = def, // 基本防御力。
                Growth = growth, // 成長の基本値。
                Speed = speed, // 速さの基本値。
                Accuracy = accuracy, // 命中率の基本値。
                Evasion = evasion, // 回避率の基本値。
                CriticalRate = critical // 会心率の基本値。
            },
            CurrentHp = hp, // 生成時は基本最大HPで満タン。試合開始時の倍率反映はPersonalityRules。
            SpecialAbility = specialAbility, // 抽選した特殊能力を表示などのために保持する。
            Element = element, // 属性相性と通常・強攻撃の属性として使う。
            Personality = personality, // ActionAIが毎回のスキル選択で参照する行動方針を保存する。
            StatusAilment = StatusAilmentType.None, // 生成時点では状態異常なし。
            Skills = skills, // 性格に応じて生成した2つのスキルをAIの選択候補として保存する。
            AttackBuff = 0, // 攻撃の加算補正はなし。
            DefenseBuff = 0, // 防御の加算補正はなし。
            CriticalBuff = 0, // 会心の加算補正はなし。
            IsInvincible = false, // 生成時点では無敵にしない。
            // IsStunnedThisTurn = false // 行動停止はStatusAilmentを使用する。
        };
    }

    /// <summary>
    /// 特殊能力をランダムに決める
    /// </summary>
    private SpecialAbilityType GetRandomSpecialAbility()
    {
        int value = Random.Range(0, 6); // 現実装は0～5を抽選する。enumのNone=0を含み、Lucky=6は含まない。
        return (SpecialAbilityType)value; // 抽選した整数をenumへ変換する。列挙順と抽選範囲が対応している点に注意。
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
                if (index == 0) return normalAttack; // 所持はするが、現在のChooseSkillは中二の通常攻撃を選ばない。
                return new SkillData
                {
                    SkillName = "超絶滅亡王漆黒波", // 行動ログで表示する自称必殺技の名前。
                    SkillType = SkillType.Delusion, // 実行側では効果なしのメッセージだけを出す。
                    Power = 0, // ダメージを与える技ではない。
                    SuccessRate = 100 // 技の処理には必ず入るが、成功しても効果は発生しない。
                };
            case PersonalityType.Inept: // 無能：覚醒後に選べる強攻撃を2つ目に持たせる。
                if (index == 0) return normalAttack; // 無能の1つ目は覚醒前にも使う通常攻撃。
                return new SkillData
                {
                    SkillName = "???", // 所持技一覧では隠された名前。覚醒後の行動ログだけBattleSimulatorが別名にする。
                    SkillType = SkillType.StrongAttack, // 強攻撃の実行処理と物理耐性の対象になる。
                    Power = 20, // 覚醒後の固有枠でPower最大として選ばれ、ダメージに20加算される。
                    SuccessRate = 100 // この専用強攻撃は共通の強撃65%とは違い、成功率100%。
                };
            case PersonalityType.Stoic: // 克己：固有の強化はPersonalityRulesが担当。
            case PersonalityType.Adversity: // 逆境：相性での倍率変化はPersonalityRulesが担当。
            case PersonalityType.Unyielding: // 不屈：致死耐久はPersonalityRulesが担当。
            case PersonalityType.Obsessive: // 執念：攻撃専用倍率はPersonalityRulesが担当。
                return index == 0 ? normalAttack : elementSkill; // 4性格とも通常攻撃と属性魔法を所持する。
            case PersonalityType.Explorer: // 探求：2つ目は発掘の抽選処理へ進む技。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "発掘", // 行動ログに表示する技名。
                    SkillType = SkillType.Dig, // アイテムの抽選はSkillExecutor.ExecuteDigへ渡す。
                    Power = 0, // 発掘アイテムの効果量は実行側が決めるので、この値は使わない。
                    SuccessRate = 100 // 発掘処理自体は必ず開始する。石ころなどの内訳は別抽選。
                };
            case PersonalityType.Devoted: // 献身：2つ目は現在HPを使う全力攻撃。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "全身全霊", // 献身の専用技名。
                    SkillType = SkillType.AllOutAttack, // 実行時に現在HPを追加威力へ変換する。
                    Power = 0, // 追加威力は実行時のHPから取るため、このPowerは使わない。
                    SuccessRate = 100 // 技成功後に自分のHPを1にする。相手への実ダメージ量とは独立。
                };
            case PersonalityType.Impatient: // 短気：我慢と強攻撃をそれぞれ作る。
                if (index == 0) // 短気の1つ目は待機。固有枠の65%でこの技を選ぶ。
                {
                    return new SkillData
                    {
                        SkillName = "我慢", // 待機時の表示名。
                        SkillType = SkillType.Wait, // 短気の我慢回数を1つためて待つ。
                        Power = 0, // Power比較では次の「我慢の限界」より小さい。
                        SuccessRate = 100 // 待機の実行を失敗させない。
                    };
                }
                return new SkillData
                {
                    SkillName = "我慢の限界", // 短気の専用攻撃名。
                    SkillType = SkillType.StrongAttack, // 強攻撃の効果経路を使う。
                    UsesPatienceCharge = true, // ダメージ全体に1.1^我慢回数を掛け、使用時に回数を消費する。
                    Power = 18, // 所持技のPower最大なので、固有枠の35%で選ばれる。
                    SuccessRate = 100 // 35%は選択確率。この技が選ばれた後の成功率は100%。
                };
            case PersonalityType.Headlong: // 猛進：戦法転換はAIがターンに応じた実際の技へ展開する。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "戦法転換", // 所持技一覧向けの表示名。
                    SkillType = SkillType.BattleCycle, // 通常のAI経路では強撃・絡め手・回復・防御強化に展開される。
                    Power = 0, // 実際に使う技の値は展開先の共通生成メソッドで決まる。
                    SuccessRate = 100 // この表示用定義の成功率。展開した強撃などはそれぞれの成功率を使う。
                };
            case PersonalityType.Sincere: // 誠実：1つ目に攻撃強化、2つ目に属性魔法を持たせる。
                if (index != 0) return elementSkill; // 誠実の2つ目は属性魔法。HPが半分以下の固有枠で選ばれる。
                return new SkillData
                {
                    SkillName = "実直な鍛錬", // 誠実の攻撃強化の表示名。
                    SkillType = SkillType.AttackBuff, // 実行時にAttackBuffを5加算する。
                    Power = 0, // 強化量はこの値ではなくSkillExecutorで決まる。
                    SuccessRate = 100 // 強化技の成功率。
                };
            case PersonalityType.Insidious: // 陰湿：2つ目の絡め手で弱体を段階的に付与する。
                if (index == 0) return normalAttack;
                return CreateDebuffCombo(); // 陰湿の2つ目は猛進と共通の絡め手。
            case PersonalityType.Adaptive: // 適応：2つ目にHP回復の再生を持たせる。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "再生", // 適応の回復技名。
                    SkillType = SkillType.Heal, // 回復処理は共通のHeal分岐。
                    Power = 15, // この技ではPowerが回復量になり、最大HPを上限に15回復する。
                    SuccessRate = 100 // HP1/3以下という条件はActionAIが判定し、実行は100%成功する。
                };
            case PersonalityType.Flexible: // 柔軟：模倣はAIが相手の直近の技へ展開する。
                if (index == 0) return normalAttack;
                return new SkillData
                {
                    SkillName = "模倣", // 所持技一覧向けの表示名。
                    SkillType = SkillType.Mimic, // ActionAIが相手の直近の許可された技を複製して返す。
                    Power = 0, // 実際のコピー技のPowerはコピー元から取得する。
                    SuccessRate = 100 // 実際のコピー技の成功率は元の技を使う。この100%で上書きしない。
                };
            case PersonalityType.Bold: // 大胆：通常攻撃と強撃。通常のChooseSkillでは強撃70%・通常攻撃30%。
                return index == 0 ? normalAttack : CreateStrongAttack(); // 所持技と一時的な強撃で同じ定義を共有する。

            case PersonalityType.Calm: // 冷静：固有枠では全体の1・2ターン目は防御集中、以降は魔法。共通攻撃枠は別。
                return index == 0 ? CreateDefenseBuff() : elementSkill; // 防御集中Power=0、属性魔法Power=10なので最大Powerは魔法。

            case PersonalityType.faith: // 信仰：通常攻撃と祈り。固有枠では相手の直前の技と同じ種別を探し、なければ所持技を等確率抽選。
                return index == 0 ? normalAttack : new SkillData // 1つ目は通常攻撃、2つ目は以下の祈りを返す。
                {
                    SkillName = "祈り", // ランダム効果を起こすスキルの表示名。
                    SkillType = SkillType.RandomSkill, // 奇跡では相手に固定ダメージ・ランダムデバフ・即死のいずれか1つを起こす。
                    Power = 0, // 祈り自体の効果量には使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 100 // 祈りの処理開始は100%成功するが、その中で別途、成功・何も起きない・天罰を抽選する。
                };

            case PersonalityType.Taunt: // 挑発：固有枠では全体の奇数ターンに挑発の構え、偶数ターンに反撃強打を選ぶ。
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
                    SuccessRate = 2.5 // 即死効果の成功率2.5%。SkillExecutorで小数の確率を判定し、不屈の耐久は別途適用する。
                } : new SkillData // indexが0以外なら、以下のランダムスキルを作る。
                {
                    SkillName = "乱数魔技", // 祈りと同じRandomSkill処理を使う別名のスキル。
                    SkillType = SkillType.RandomSkill, // 祈りと共通の奇跡・不発・天罰処理。効果は所持技に依存しない。
                    Power = 0, // ランダム効果そのものには使わず、AIのPower比較用の値は0にする。
                    SuccessRate = 100 // 処理開始は100%成功するが、内部の効果抽選や再実行する技の成功判定は別に行う。
                };

            case PersonalityType.Timid: // 臆病：固有枠でHPが最大HPの整数除算による半分以下なら回復、それ以外はこの2つから抽選。無敵技は所持しない。
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
            SkillName = "強撃", // 大胆・猛進・代替攻撃で共通の表示名。
            SkillType = SkillType.StrongAttack, // 強攻撃の成功時ダメージと失敗時反動の分岐を使う。
            Power = 12, // ダメージ計算へ加える威力。
            SuccessRate = 65 // 成功率65%。失敗時は相手へのダメージはなく、自分に現在攻撃力の1/4の反動がある。
        };
    }

    /// <summary>陰湿と猛進が使う「絡め手」。毒・防御低下・封印の順で相手を弱体化する。</summary>
    public static SkillData CreateDebuffCombo()
    {
        return new SkillData
        {
            SkillName = "絡め手", // 陰湿・猛進で共有する表示名。
            SkillType = SkillType.DebuffCombo, // 毒→防御低下→封印の段階判定へ進む。
            Power = 0, // 効果の段階はSkillExecutor.ApplyDebuffで判定する。
            SuccessRate = 100 // 技の成功率。相手の弱体無効・毒耐性100%による拒否は実行側で別途判定する。
        };
    }

    /// <summary>臆病と猛進が使う「回復」。最大HPを上限に20回復する。</summary>
    public static SkillData CreateHeal()
    {
        return new SkillData
        {
            SkillName = "回復", // 共通の回復技名。
            SkillType = SkillType.Heal, // 使用者自身のHPを回復する。
            Power = 20, // この技では回復量として使う。
            SuccessRate = 100 // 回復技の成功率。封印中は実行側で使用を止める。
        };
    }

    /// <summary>冷静と猛進が使う「防御集中」。実行時に防御補正を5加算する。</summary>
    public static SkillData CreateDefenseBuff()
    {
        return new SkillData
        {
            SkillName = "防御集中", // 共通の防御強化名。
            SkillType = SkillType.DefenseBuff, // 防御力そのものではなく加算補正を更新する。
            Power = 0, // 強化量5はSkillExecutorで適用する。
            SuccessRate = 100 // 防御強化の成功率。
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
            case ElementType.Light: // 光属性に対応する魔法。
                return new SkillData { SkillName = "光魔法", SkillType = SkillType.LightMagic, Power = 10, SuccessRate = 90 };
            case ElementType.Dark: // 闇属性に対応する魔法。
                return new SkillData { SkillName = "闇魔法", SkillType = SkillType.DarkMagic, Power = 10, SuccessRate = 90 };
            default:
                // 新属性の魔法を作り忘れた場合、nullのスキルを持たせず生成時点で知らせる。
                throw new System.ArgumentOutOfRangeException(nameof(element), element, "属性魔法が未定義です。");
        }
    }
}
