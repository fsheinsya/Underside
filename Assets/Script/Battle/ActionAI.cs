using UnityEngine; // 所持スキルからランダムに選ぶため、Unity の Random を利用する。

/// <summary>
/// 共通の70/15/15抽選と、性格・現在HP・ターン数・相手の直近の技から行動を選ぶAI。
/// スキルの効果や成功判定は行わず、選んだスキルの実行は SkillExecutor に任せる。
/// </summary>

public class ActionAI // 性格ごとの選択ルールと、行動履歴の記録をまとめる。
{
    /// <summary>
    /// 行動する側の所持スキルから、性格のルールに従って1つ返す。
    /// 所持スキルがある場合、各要素には有効な SkillData が入っている前提で選ぶ。
    /// </summary>

    /// <param name="attacker">今回行動するモンスター。性格・HP・所持スキルを参照する。</param>
    /// <param name="defender">対戦相手。性格が信仰の場合に直近のスキル種別を参照する。</param>
    /// <param name="turn">戦闘全体のターン番号。BattleSimulator では1から始まり、左右が同じ番号を使う。</param>

    //とりあえず設計をStateを使用して書いてみる

    public interface IState
    {
        void OnEnter();
        void ChooseSkill();
        void OnExit();
    }

    public SkillData ChooseSkill(MonsterData attacker, MonsterData defender, int turn)
    {
        // 挑戦・中二は共通の通常攻撃・魔法抽選を使わず、固有技だけを試す。
        if (attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional)
            return ChooseSpecialOnlySkill(attacker);

        // 技を選ぶ確率であり、技自体の成功率は実行時に別途判定する。
        if (attacker.IsSkillLocked) return MonsterFactory.CreateNormalAttack();
        // 無能は覚醒まで共通抽選を行わず、通常攻撃だけを使う。
        if (attacker.Personality == PersonalityType.Inept && !attacker.Awakened)
            return MonsterFactory.CreateNormalAttack();

        int actionRoll = Random.Range(0, 100);
        // 大胆だけは魔法枠を設けず、強撃70%・通常攻撃30%。
        if (attacker.Personality == PersonalityType.Bold)
            return actionRoll < 70 ? MonsterFactory.CreateStrongAttack() : MonsterFactory.CreateNormalAttack();

        if (actionRoll < 70) return ChoosePersonalitySkill(attacker, defender, turn);
        if (actionRoll < 85) return MonsterFactory.CreateNormalAttack();
        return MonsterFactory.CreateElementSkill(attacker.Element);
    }

    /// <summary>70%の固有行動枠で使う、性格ごとの技選択。共通の抽選はここでは重ねない。</summary>
    public SkillData ChoosePersonalitySkill(MonsterData attacker, MonsterData defender, int turn)
    {
        if (attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional)
            return ChooseSpecialOnlySkill(attacker);

        // 封印中も通常攻撃は可能。補助技だけを選び続けて停止しないようにする。
        if (attacker.IsSkillLocked) return ChooseFallbackAttack(attacker);
        // 所持スキル一覧が未設定、または空なら専用の代替処理に進む。
        if (attacker.Skills == null || attacker.Skills.Count == 0) 
            return ChooseFallbackAttack(attacker); // 所持技がなくても、共通の攻撃3種類から選べる。

        ///<summary>
        /// ↓このswicth文で性格の分岐を実装する
        ///</summary>

        // 性格固有のルールが技を返せばここで選択完了。breakしたケースだけ末尾のランダム選択へ進む。
        switch (attacker.Personality) // 行動する側の性格に対応する分岐へ進む。
        {
            case PersonalityType.Inept:
                return attacker.Awakened ? GetStrongest(attacker) : FindSkill(attacker, SkillType.NormalAttack) ?? ChooseFallbackAttack(attacker);
            case PersonalityType.Stoic:
            case PersonalityType.Adversity:
            case PersonalityType.Unyielding:
            case PersonalityType.Obsessive:
                return GetStrongest(attacker);
            case PersonalityType.Explorer:
                return FindSkill(attacker, SkillType.Dig) ?? ChooseFallbackAttack(attacker);
            case PersonalityType.Devoted:
                return FindSkill(attacker, SkillType.AllOutAttack) ?? ChooseFallbackAttack(attacker);
            case PersonalityType.Impatient:
                return Random.value < 0.35f ? GetStrongest(attacker) : FindSkill(attacker, SkillType.Wait) ?? ChooseFallbackAttack(attacker);
            case PersonalityType.Headlong:
                // 2ターンずつ攻撃→妨害→防御。表示用の戦法転換から実際の技へ展開する。
                switch (((turn - 1) / 2) % 3)
                {
                    case 0:
                        // 攻撃重視の2ターン。大胆が使う「強撃」と同じ定義を使う。
                        return MonsterFactory.CreateStrongAttack();
                    case 1:
                        // 妨害重視の2ターン。陰湿が使う「絡め手」と同じ定義を使う。
                        return MonsterFactory.CreateDebuffCombo();
                    default:
                        // 防御重視の2ターン。HPが減っていれば回復、満タンなら防御強化を選ぶ。
                        if (attacker.CurrentHp < attacker.GetCurrentMaxHp())
                        {
                            return MonsterFactory.CreateHeal();
                        }
                        return MonsterFactory.CreateDefenseBuff();
                }
            case PersonalityType.Sincere:
                return attacker.CurrentHp > attacker.GetCurrentMaxHp() / 2
                    ? FindSkill(attacker, SkillType.AttackBuff) ?? ChooseFallbackAttack(attacker) : GetStrongest(attacker);
            case PersonalityType.Insidious:
                if (attacker.DebuffRejected || defender.CurrentHp <= defender.GetCurrentMaxHp() / 2
                    || (defender.StatusAilment == StatusAilmentType.Poison && defender.DefenseBuff <= -10 && defender.IsSkillLocked))
                    return FindSkill(attacker, SkillType.NormalAttack) ?? ChooseFallbackAttack(attacker);
                return FindSkill(attacker, SkillType.DebuffCombo) ?? ChooseFallbackAttack(attacker);
            case PersonalityType.Adaptive:
                return attacker.CurrentHp <= attacker.GetCurrentMaxHp() / 3
                    ? FindSkill(attacker, SkillType.Heal) ?? ChooseFallbackAttack(attacker) : FindSkill(attacker, SkillType.NormalAttack) ?? ChooseFallbackAttack(attacker);
            case PersonalityType.Flexible:
                // ① 無能・挑戦・中二はコピー元にしない。通常攻撃を使った場合も対象外。
                if (defender.Personality == PersonalityType.Inept
                    || defender.Personality == PersonalityType.Aggressive
                    || defender.Personality == PersonalityType.Delusional)
                    return ChooseFallbackAttack(attacker);

                // ② 相手が直近に選んだ技を確認する。行動履歴がなければ共通の攻撃から選ぶ。
                var previous = defender.LastUsedSkill;
                if (previous == null) return ChooseFallbackAttack(attacker);

                // ③ コピーできるのは通常攻撃・強撃・実装済みの5属性魔法だけ。
                // 回復・強化・妨害・即死・発掘・全身全霊などの特殊技はコピーしない。
                switch (previous.SkillType)
                {
                    case SkillType.NormalAttack:
                    case SkillType.StrongAttack:
                    case SkillType.FireMagic:
                    case SkillType.WaterMagic:
                    case SkillType.GrassMagic:
                    case SkillType.LightMagic:
                    case SkillType.DarkMagic:
                        break; // 対象の攻撃技なら、次のコピー成功判定へ進む。
                    default:
                        return ChooseFallbackAttack(attacker);
                }

                // ④ 適応の技は50％でコピーできる。それ以外の対象性格からは必ずコピーする。
                // 失敗時は共通の攻撃から選ぶ。適応の種類別耐性など、性格そのものの効果はコピーしない。
                if (defender.Personality == PersonalityType.Adaptive && Random.value >= 0.5f)
                    return ChooseFallbackAttack(attacker);

                // ⑤ 元のデータを変更せず、今回使うコピー技を作る。技自体の成功率は実行時に別途判定する。
                return new SkillData
                {
                    SkillName = "模倣：" + previous.SkillName.Replace("模倣：", ""),
                    SkillType = previous.SkillType,
                    Element = previous.Element,
                    Power = previous.Power,
                    SuccessRate = previous.SuccessRate,
                    // Powerだけ減らすと威力0の通常攻撃が弱くならないため、ダメージ全体を80％にする。
                    // コピー技をさらにコピーした場合も、コピー元から80％に下げる。
                    DamageMultiplier = previous.DamageMultiplier * 0.8f
                };
            case PersonalityType.Bold: // 大胆：毎回、Power が最大の所持スキルを使う。
                return GetStrongest(attacker); // 成功率やスキル種別は考慮せず、Power の大きさで選ぶ。

            case PersonalityType.Calm: // 冷静：最初の2ターンは防御・回復を探し、その後は Power を優先する。
                // 個人の行動回数ではなく、渡された戦闘全体のターン番号で判定する。
                if (turn <= 2) // 通常の呼び出しでは1・2ターン目が対象になる。

                    return GetDefensive(attacker); // 一覧で先に見つかった防御強化・回復を選び、両方なければ先頭を選ぶ。
                return GetStrongest(attacker); // 3ターン目以降は Power が最大のスキルを選ぶ。

            case PersonalityType.faith: // 信仰：相手が直近に選んだスキルと同じ種別を、自分の所持スキルから探す。
                var copied = FindSameType(attacker, defender); // 相手の技そのものを取得したり、新しく複製したりはしない。
                if (copied != null) return copied; // 同じ種別の所持スキルが見つかれば、それを使う。
                break; // 見つからなければ switch を抜け、最後のランダム選択に進む。

            case PersonalityType.Taunt: // 挑発：奇数ターンと偶数ターンで、Power の選び方を切り替える。
                return turn % 2 == 1 ? GetWeak(attacker) : GetStrongest(attacker); // 奇数なら最小、偶数なら最大の Power を選ぶ。

            case PersonalityType.Timid: // 臆病：HPが少なければ回復を探し、それ以外は無敵化を探す。
                if (attacker.CurrentHp <= attacker.GetCurrentMaxHp() / 2)
                {
                    var heal = FindSkill(attacker, SkillType.Heal); // 一覧の先頭から回復スキルを探す。
                    if (heal != null) return heal; // 回復があれば、無敵化よりも優先して選ぶ。
                }
                return GetEvasion(attacker); // HPが半分より多い場合や回復がない場合は、無敵化を優先し、なければランダムに選ぶ。
            default:
                return ChooseFallbackAttack(attacker); // 性格の行動ルールが未定義でも共通の攻撃で行動する。
        }

        return attacker.Skills[Random.Range(0, attacker.Skills.Count)]; // 信仰で一致する技がない場合は、通常攻撃・祈りなどの所持技から選ぶ。
    }

    // =========================
    // ここから FindSameType までは、既存の所持スキルを参照して選ぶ処理。
    // =========================

    // 一番攻撃力の高いスライムを選択をする

    private SkillData GetStrongest(MonsterData monster)
    {
        SkillData best = monster.Skills[0]; // 先頭のスキルを、比較を始める時点の候補にする。
        foreach (var s in monster.Skills) // 所持スキルを先頭から順番に調べる。
            if (s.Power > best.Power) // 現在の候補より Power が大きいときだけ更新し、同値なら先にある候補を残す。
                best = s; // より大きい Power のスキルを新しい候補にする。
        return best; // 全件の比較後、Power が最大のスキルを返す。
    }

    // Power が最小のスキルを返す。Power が0の補助技も選ばれるため、必ずしも弱い攻撃とは限らない。
    // 弱い攻撃または、毒の攻撃等の動きを実装する

    private SkillData GetWeak(MonsterData monster)
    {
        SkillData weak = monster.Skills[0]; // 先頭のスキルを最小値の候補にする。
        foreach (var s in monster.Skills) // 所持スキルを先頭から順番に調べる。
            if (s.Power < weak.Power) // 現在の候補より Power が小さいときだけ更新し、同値なら先にある候補を残す。
                weak = s; // より小さい Power のスキルを新しい候補にする。
        return weak; // 全件の比較後、Power が最小のスキルを返す。
    }

    private SkillData ChooseSpecialOnlySkill(MonsterData attacker)
    {
        // 固有技がない・封印中でも通常攻撃へ流さず、必ず待機する。
        if (!attacker.IsSkillLocked && attacker.Skills != null)
        {
            SkillType type = attacker.Personality == PersonalityType.Aggressive ? SkillType.InstantDeath : SkillType.Delusion;
            var skill = FindSkill(attacker, type);
            if (skill != null) return skill;
        }
        return CreateWaitSkill();
    }

    private static SkillData CreateWaitSkill()
    {
        return new SkillData { SkillName = "様子見", SkillType = SkillType.Wait, SuccessRate = 100 };
    }

    // 防御強化か回復のうち、一覧で先に見つかったものを返す。現在HPや効果量は比較しない。

    private SkillData GetDefensive(MonsterData monster)
    {
        foreach (var s in monster.Skills) // 所持スキルを一覧の順番で調べる。
        {
            if (s.SkillType == SkillType.DefenseBuff || // 防御力を上げるスキルなら採用する。
                s.SkillType == SkillType.Heal) // または回復スキルなら採用する。防御強化との優先順位は一覧順で決まる。
                return s; // 最初に条件を満たしたスキルを返し、検索を終える。
        }
        return ChooseFallbackAttack(monster); // 防御強化も回復もなければ、共通の攻撃で行動する。
    }

    // 名前は回避用だが、実際には Invincible（無敵化）を探す。回避率の大小は参照しない。
    private SkillData GetEvasion(MonsterData monster)
    {
        foreach (var s in monster.Skills) // 所持スキルを一覧の順番で調べる。
        {
            if (s.SkillType == SkillType.Invincible) // 無敵化スキルを持っているか確認する。
                return s; // 最初に見つかった無敵化スキルを返す。
        }
        return monster.Skills[Random.Range(0, monster.Skills.Count)]; // 無敵化がなければ全所持スキルから等確率で選ぶため、HPが多いときも回復が選ばれ得る。
    }

    // 指定した種別のスキルを1つ探す。該当するものがないことは null で呼び出し元に伝える。
    private SkillData FindSkill(MonsterData monster, SkillType type)
    {
        foreach (var s in monster.Skills) // 所持スキルを先頭から順番に調べる。
            if (s.SkillType == type) // 探している種別と一致するか確認する。
                return s; // 同種のスキルが複数あっても、先に見つかった1つを返す。
        return null; // 一致するスキルを持っていない場合は、見つからなかったことを返す。
    }

    // 信仰AI用。相手が直近に選んだスキルの「種別」だけをまねし、使う技は自分の所持スキルから選ぶ。
    private SkillData FindSameType(MonsterData attacker, MonsterData defender)
    {
        // 履歴は相手が行動するたびに更新されるため、同じターン内の直前の行動も対象になる。

        if (defender.LastUsedSkillType == SkillType.None) return null; // 相手の行動履歴がまだなければ、比較対象がないので検索を終える。

        foreach (var s in attacker.Skills) // 自分が持っているスキルだけを先頭から調べる。
        {
            if (s.SkillType == defender.LastUsedSkillType) // 相手の直近のスキル種別と一致するか確認する。
                return s; // 最初に一致した自分のスキルを返す。名前・威力・成功率は自分のスキルの値を使う。
        }

        return null; // 同種のスキルを持っていなければ、ChooseSkill 側のランダム選択に任せる。
    }

    /// <summary>技が見つからない・コピーできないなど、行動手段がないときの共通の攻撃選択。</summary>
    public static SkillData ChooseFallbackAttack(MonsterData attacker)
    {
        if (attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional)
            return CreateWaitSkill();

        // ① 封印中は強撃・魔法が使えないため、必ず通常攻撃にする。
        if (attacker.IsSkillLocked) return MonsterFactory.CreateNormalAttack();

        // ② 所持スキルとは別に、全性格で使える基本の攻撃を等確率で1つ選ぶ。
        // コピーではないためダメージ倍率は1倍。成功判定はSkillExecutorで通常どおり行う。
        switch (Random.Range(0, 3)) // 整数の上限3は含まず、0・1・2がそれぞれ約33％で出る。
        {
            case 0:
                return MonsterFactory.CreateNormalAttack(); // 追加威力0、成功率100％。
            case 1:
                return MonsterFactory.CreateStrongAttack(); // 共通の強撃。追加威力12、成功率65％。
            default:
                return MonsterFactory.CreateElementSkill(attacker.Element); // 自分の属性の魔法。有利時は攻撃力2倍、成功率90％。
        }
    }

    /// <summary>
    /// 今回選んだスキルの種別を記録し、対戦相手の信仰AIが次に参照できるようにする。
    /// BattleSimulator は実行処理後に呼ぶため、失敗・封印時も記録される。
    /// ランダムスキルの場合、内部で発動した技ではなく、最初に選んだ RandomSkill が記録される。
    /// </summary>
    public void RecordAction(MonsterData attacker, SkillData skill)
    {
        // 実行の成否ではなく、AIが選んだ技を記録する。次に行動する信仰・柔軟がこの履歴を読む。
        attacker.LastUsedSkill = skill;
        attacker.LastUsedSkillType = skill.SkillType; // 行動した本人の履歴を今回の種別で上書きし、過去の履歴は残さない。
    }
}
