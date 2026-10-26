using UnityEngine; // 所持スキルからランダムに選ぶため、Unity の Random を利用する。

/// <summary>
/// 共通の70/15/15抽選と、性格・現在HP・ターン数・相手の直近の技から行動を選ぶAI。
/// スキルの効果や成功判定は行わず、選んだスキルの実行は SkillExecutor に任せる。
/// 学習型AIではなく、条件分岐と乱数によって行動を決めるルールベースのAI。
/// </summary>
// 読む順番：BattleSimulator.ProcessTurn → ChooseSkill → ChoosePersonalitySkill → SkillExecutor.Execute → RecordAction。
// 調整箇所：行動の割合はChooseSkill、性格ごとの判断はChoosePersonalitySkill、技の値はMonsterFactory。
// 覚醒・耐性などの受動効果はPersonalityRules、攻防・属性の計算はDamageCalculatorを確認する。
// returnで技を返した時点で今回の選択は終了する。後続の条件や乱数抽選は実行されない。
// 「??」は左の技がnull（見つからない）ときだけ右の代替処理を実行する。
// 「条件 ? A : B」は条件成立時にA、不成立時にBだけを実行する。
public class ActionAI // 性格ごとの選択ルールと、行動履歴の記録をまとめる。
{
    // 状態パターン用の宣言。現在のChooseSkillからは使用しておらず、以下のswitchが実際の判断処理。
    // このインターフェースに処理を追加するだけではAIの動きは変わらない。
    public interface IState // 状態を表す型が実装するメソッドの形を定義する。
    {
        void OnEnter(); // 状態へ入る際の処理を想定した宣言。ここには実装も呼び出しもない。
        void ChooseSkill(); // 状態内の選択処理を想定した宣言。下のSkillDataを返すメソッドとは別。
        void OnExit(); // 状態を抜ける際の処理を想定した宣言。
    }

    /// <summary>
    /// 通常の行動選択の入口。例外を先に判定し、残りを固有行動70%・通常攻撃15%・属性魔法15%に分ける。
    /// attackerとdefenderは有効なモンスターを渡し、所持技の各要素にはnullを含めない。
    /// 所持技を選ぶ場合と、一時的な共通技を新規生成する場合がある。所持技一覧への追加はしない。
    /// </summary>
    /// <param name="attacker">今回行動する側。左側・右側のどちらにもなり得る。</param>
    /// <param name="defender">相手。信仰の種別参照・柔軟のコピー・陰湿の弱体判定などに使う。</param>
    /// <param name="turn">戦闘全体のターン番号。1から始まり、左右が同じ番号を使う。</param>
    public SkillData ChooseSkill(MonsterData attacker, MonsterData defender, int turn)
    {
        // 挑戦・中二は共通の通常攻撃・魔法抽選を使わず、固有技だけを試す。
        if (attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional) // 他の例外より先に判定する。
            return ChooseSpecialOnlySkill(attacker); // 封印中や固有技がない場合も通常攻撃にはせず、待機する。

        if (attacker.IsSkillLocked) return MonsterFactory.CreateNormalAttack(); // 上記2性格以外は、封印中でも通常攻撃できる。
        // 無能は覚醒まで共通抽選を行わず、通常攻撃だけを使う。
        if (attacker.Personality == PersonalityType.Inept && !attacker.Awakened) // 覚醒フラグはPersonalityRules.BeginTurnが更新する。
            return MonsterFactory.CreateNormalAttack(); // 通常の進行では1～9ターン目がこの分岐。

        int actionRoll = Random.Range(0, 100); // 整数の0～99を一度だけ抽選する。100は出ない。
        // 大胆だけは魔法枠を設けず、強撃70%・通常攻撃30%。
        if (attacker.Personality == PersonalityType.Bold) // 大胆は下の共通70/15/15抽選へ進まない。
            return actionRoll < 70 ? MonsterFactory.CreateStrongAttack() : MonsterFactory.CreateNormalAttack(); // 0～69は強撃、70～99は通常攻撃。

        // 技を選ぶ確率と、選んだ技が成功する確率は別。後者はSkillExecutorがSuccessRateで判定する。
        if (actionRoll < 70) return ChoosePersonalitySkill(attacker, defender, turn); // 0～69の70通り。固有判断が通常攻撃を返すこともある。
        if (actionRoll < 85) return MonsterFactory.CreateNormalAttack(); // 上で70未満は終了済みなので、70～84の15通り。
        return MonsterFactory.CreateElementSkill(attacker.Element); // 残る85～99の15通り。使用者の属性の魔法を新規生成する。
    }

    /// <summary>70%の固有行動枠で使う、性格ごとの技選択。共通の抽選はここでは重ねない。</summary>
    public SkillData ChoosePersonalitySkill(MonsterData attacker, MonsterData defender, int turn)
    {
        // 検証コードなどから直接呼ばれても、挑戦・中二の専用ルールを守る。
        if (attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional)
            return ChooseSpecialOnlySkill(attacker); // 固有技か待機だけを返す。

        // 封印中も通常攻撃は可能。補助技だけを選び続けて停止しないようにする。
        if (attacker.IsSkillLocked) return ChooseFallbackAttack(attacker); // 代替処理内でも封印を確認し、通常攻撃を返す。
        // 所持スキル一覧が未設定、または空なら専用の代替処理に進む。
        if (attacker.Skills == null || attacker.Skills.Count == 0) // nullを先に確認するので、未設定の一覧のCountを読まない。
            return ChooseFallbackAttack(attacker); // 所持技がなくても、共通の攻撃3種類から選べる。

        // ここからは固有行動枠の中の判断。例えば短気の攻撃35%は、全行動の35%ではなくこの枠の35%。
        // 性格固有のルールが技を返せばここで選択完了。breakしたケースだけ末尾のランダム選択へ進む。
        switch (attacker.Personality) // 行動する側の性格に対応する分岐へ進む。
        {
            case PersonalityType.Inept: // 無能：覚醒フラグによって専用強攻撃を解禁する。
                return attacker.Awakened ? GetStrongest(attacker) : FindSkill(attacker, SkillType.NormalAttack) ?? ChooseFallbackAttack(attacker); // 未覚醒側は直接呼び出された場合の備え。
            case PersonalityType.Stoic: // 克己：被弾による強化はPersonalityRulesが担当。
            case PersonalityType.Adversity: // 逆境：属性不利による強化はPersonalityRulesが担当。
            case PersonalityType.Unyielding: // 不屈：致死ダメージへの耐久はPersonalityRulesが担当。
            case PersonalityType.Obsessive: // 執念：状態異常による攻撃強化はPersonalityRulesが担当。
                return GetStrongest(attacker); // 4性格とも行動選択は同じ。実ダメージ予測ではなくPower最大の技を選ぶ。
            case PersonalityType.Explorer: // 探求：発掘の中身の抽選はSkillExecutor.ExecuteDigで行う。
                return FindSkill(attacker, SkillType.Dig) ?? ChooseFallbackAttack(attacker); // 発掘を持っていなければ代替攻撃。
            case PersonalityType.Devoted: // 献身：現在HPを使う攻撃の実行はSkillExecutorに任せる。
                return FindSkill(attacker, SkillType.AllOutAttack) ?? ChooseFallbackAttack(attacker); // HP1でも再使用を禁止しない。
            case PersonalityType.Impatient: // 短気：この固有行動枠でだけ、攻撃か我慢かを追加抽選する。
                return Random.value < 0.35f ? GetStrongest(attacker) : FindSkill(attacker, SkillType.Wait) ?? ChooseFallbackAttack(attacker); // 35%で最大Power、残り65%で待機を探す。
            case PersonalityType.Headlong: // 猛進：個人の行動回数ではなく戦闘全体のターンで戦法を決める。
                // 2ターンずつ攻撃→妨害→防御。表示用の戦法転換から実際の技へ展開する。
                switch (((turn - 1) / 2) % 3) // 1～2ターンは0、3～4は1、5～6は2。7ターンで0へ戻る。
                {
                    case 0:
                        // 攻撃重視の2ターン。大胆が使う「強撃」と同じ定義を使う。
                        return MonsterFactory.CreateStrongAttack(); // 所持技の「戦法転換」自体は実行せず、一時的な強撃を返す。
                    case 1:
                        // 妨害重視の2ターン。陰湿が使う「絡め手」と同じ定義を使う。
                        return MonsterFactory.CreateDebuffCombo(); // 弱体の進行段階は実行側で相手の状態から判断する。
                    default:
                        // 防御重視の2ターン。HPが減っていれば回復、満タンなら防御強化を選ぶ。
                        if (attacker.CurrentHp < attacker.GetCurrentMaxHp()) // 性格倍率反映後の最大HPと比較する。
                        {
                            return MonsterFactory.CreateHeal(); // 少しでもHPが欠けていれば回復を優先する。
                        }
                        return MonsterFactory.CreateDefenseBuff(); // HP満タンなら防御補正を積み上げる。
                }
            case PersonalityType.Sincere: // 誠実：HPが多いうちは攻撃強化、減ったら最大Powerの技。
                return attacker.CurrentHp > attacker.GetCurrentMaxHp() / 2 // 整数除算で切り捨て。最大HP60なら31以上で強化、30以下で攻撃。
                    ? FindSkill(attacker, SkillType.AttackBuff) ?? ChooseFallbackAttack(attacker) : GetStrongest(attacker); // 生成時の所持技では最大Powerは属性魔法。
            case PersonalityType.Insidious: // 陰湿：妨害を続ける価値がないと判断したら攻撃へ切り替える。
                if (attacker.DebuffRejected || defender.CurrentHp <= defender.GetCurrentMaxHp() / 2 // 弱体無効を確認済み、または相手が半分以下なら攻撃。
                    || (defender.StatusAilment == StatusAilmentType.Poison && defender.DefenseBuff <= -10 && defender.IsSkillLocked)) // 毒・防御-10以下・封印がすべて揃っても攻撃。
                    return FindSkill(attacker, SkillType.NormalAttack) ?? ChooseFallbackAttack(attacker); // 攻撃への切り替え先は通常攻撃。
                return FindSkill(attacker, SkillType.DebuffCombo) ?? ChooseFallbackAttack(attacker); // それ以外は絡め手を選び、実行側で次の弱体を決める。
            case PersonalityType.Adaptive: // 適応：耐性の蓄積は別処理。ここではHPによる再生の選択だけを扱う。
                return attacker.CurrentHp <= attacker.GetCurrentMaxHp() / 3 // 最大HPの整数除算による1/3以下で回復する。
                    ? FindSkill(attacker, SkillType.Heal) ?? ChooseFallbackAttack(attacker) : FindSkill(attacker, SkillType.NormalAttack) ?? ChooseFallbackAttack(attacker); // 回復しない場合は通常攻撃。
            case PersonalityType.Flexible: // 柔軟：相手の実際の技データを複製し、ダメージ倍率を弱める。
                // ① 無能・挑戦・中二はコピー元にしない。通常攻撃を使った場合も対象外。
                if (defender.Personality == PersonalityType.Inept // 無能は覚醒の前後を問わず対象外。
                    || defender.Personality == PersonalityType.Aggressive // 挑戦は対象外。
                    || defender.Personality == PersonalityType.Delusional) // 中二は対象外。
                    return ChooseFallbackAttack(attacker); // コピー抽選も行わず、自分の基本攻撃へ進む。

                // ② 相手が直近に選んだ技を確認する。行動履歴がなければ共通の攻撃から選ぶ。
                var previous = defender.LastUsedSkill; // RecordActionが保存した直近1回分。成功した技に限定しない。
                if (previous == null) return ChooseFallbackAttack(attacker); // 初回行動前など、コピー元がない場合。

                // ③ コピーできるのは通常攻撃・強撃・実装済みの5属性魔法だけ。
                // 回復・強化・妨害・即死・発掘・全身全霊などの特殊技はコピーしない。
                switch (previous.SkillType) // 許可リスト方式。新しい技種別を増やしても自動ではコピー可能にならない。
                {
                    case SkillType.NormalAttack: // 通常攻撃を許可する。
                    case SkillType.StrongAttack: // 専用名の強攻撃も、種別がStrongAttackなら許可する。
                    case SkillType.FireMagic: // 炎魔法を許可する。
                    case SkillType.WaterMagic: // 水魔法を許可する。
                    case SkillType.GrassMagic: // 草魔法を許可する。
                    case SkillType.LightMagic: // 光魔法を許可する。
                    case SkillType.DarkMagic: // 闇魔法を許可する。
                        break; // 対象の攻撃技なら、次のコピー成功判定へ進む。
                    default:
                        return ChooseFallbackAttack(attacker); // 許可していない技はコピーせず、代替攻撃にする。
                }

                // ④ 適応の技は50％でコピーできる。それ以外の対象性格からは必ずコピーする。
                // 失敗時は共通の攻撃から選ぶ。適応の種類別耐性など、性格そのものの効果はコピーしない。
                if (defender.Personality == PersonalityType.Adaptive && Random.value >= 0.5f) // 相手が適応のときだけ追加の乱数を消費する。
                    return ChooseFallbackAttack(attacker); // コピー失敗後も何もしないわけではなく、代替攻撃を実行する。

                // ⑤ 元のデータを変更せず、今回使うコピー技を作る。技自体の成功率は実行時に別途判定する。
                return new SkillData // コピー元のSkillDataは共有参照なので、直接書き換えず別のオブジェクトを作る。
                {
                    SkillName = "模倣：" + previous.SkillName.Replace("模倣：", ""), // 多重コピーでも表示名の「模倣：」を重ねない。
                    SkillType = previous.SkillType, // 効果の種類を引き継ぐ。魔法の攻撃属性も実行側がこの種別から決める。
                    Element = previous.Element, // 元の属性フィールドを保持する。通常・強攻撃は実行時に使用者の属性で攻撃する。
                    Power = previous.Power, // 選択用・追加威力用の値はそのまま引き継ぐ。
                    SuccessRate = previous.SuccessRate, // コピー成功後も、技自体の成功判定は元の率で必要になる。
                    // Powerだけ減らすと威力0の通常攻撃が弱くならないため、ダメージ全体を80％にする。
                    // コピー技をさらにコピーした場合も、コピー元から80％に下げる。
                    DamageMultiplier = previous.DamageMultiplier * 0.8f // 1回なら0.8倍、コピーのコピーなら0.64倍。
                };
            case PersonalityType.Bold: // 大胆：このメソッドを直接呼んだ場合の処理。通常の入口では70/30抽選で先に終了する。
                return GetStrongest(attacker); // 成功率やスキル種別は考慮せず、Power の大きさで選ぶ。

            case PersonalityType.Calm: // 冷静：最初の2ターンは防御・回復を探し、その後は Power を優先する。
                // 個人の行動回数ではなく、渡された戦闘全体のターン番号で判定する。
                if (turn <= 2) // 通常の呼び出しでは1・2ターン目が対象になる。

                    return GetDefensive(attacker); // 一覧で先に見つかった防御強化・回復を選び、両方なければ代替攻撃を選ぶ。
                return GetStrongest(attacker); // 3ターン目以降は Power が最大のスキルを選ぶ。

            case PersonalityType.faith: // 信仰：相手が直近に選んだスキルと同じ種別を、自分の所持スキルから探す。
                var copied = FindSameType(attacker, defender); // 相手の技そのものを取得したり、新しく複製したりはしない。
                if (copied != null) return copied; // 同じ種別の所持スキルが見つかれば、それを使う。
                break; // 見つからなければ switch を抜け、最後のランダム選択に進む。

            case PersonalityType.Taunt: // 挑発：奇数ターンと偶数ターンで、Power の選び方を切り替える。
                return turn % 2 == 1 ? GetWeak(attacker) : GetStrongest(attacker); // 奇数なら最小、偶数なら最大の Power を選ぶ。

            case PersonalityType.Timid: // 臆病：HPが少なければ回復を探し、それ以外は無敵化を探す。
                if (attacker.CurrentHp <= attacker.GetCurrentMaxHp() / 2) // 整数除算による半分以下。最大HP60なら30以下で回復を優先する。
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

    // 最大Powerの「技」を選ぶ。モンスターの選択ではなく、成功率・属性相性・実ダメージも比較しない。
    // 呼び出し元が所持技の存在を確認済みなので、Skills[0]から比較を開始できる。

    private SkillData GetStrongest(MonsterData monster)
    {
        SkillData best = monster.Skills[0]; // 先頭のスキルを、比較を始める時点の候補にする。
        foreach (var s in monster.Skills) // 所持スキルを先頭から順番に調べる。
            if (s.Power > best.Power) // 現在の候補より Power が大きいときだけ更新し、同値なら先にある候補を残す。
                best = s; // より大きい Power のスキルを新しい候補にする。
        return best; // 全件の比較後、Power が最大のスキルを返す。
    }

    // Power が最小のスキルを返す。Power が0の補助技も選ばれるため、必ずしも弱い攻撃とは限らない。
    // 挑発の奇数ターンで使用する。生成時の所持技ならPower=0の「挑発の構え」が選ばれる。

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
        if (!attacker.IsSkillLocked && attacker.Skills != null) // 封印されておらず、一覧があるときだけ技を探す。
        {
            SkillType type = attacker.Personality == PersonalityType.Aggressive ? SkillType.InstantDeath : SkillType.Delusion; // 挑戦は即死、中二は効果なしの自称必殺技。
            var skill = FindSkill(attacker, type); // SuccessRateの大小で選ぶのではなく、必要な種別を指定して探す。
            if (skill != null) return skill; // 見つかれば成功率を変えずにその技を返す。
        }
        return CreateWaitSkill(); // 封印・一覧未設定・該当技なしはすべて待機。
    }

    private static SkillData CreateWaitSkill()
    {
        return new SkillData { SkillName = "様子見", SkillType = SkillType.Wait, SuccessRate = 100 }; // ダメージを与えず待つ一時的な技。封印中も実行できる。
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

        return null; // 同種のスキルを持っていなければ、ChoosePersonalitySkill末尾の所持技抽選に任せる。
    }

    /// <summary>技が見つからない・コピーできないなど、行動手段がないときの共通の攻撃選択。</summary>
    public static SkillData ChooseFallbackAttack(MonsterData attacker)
    {
        if (attacker.Personality == PersonalityType.Aggressive || attacker.Personality == PersonalityType.Delusional) // 他の処理から直接呼ばれても専用ルールを守る。
            return CreateWaitSkill(); // 挑戦・中二には代替の通常攻撃や魔法を与えない。

        // ① 封印中は強撃・魔法が使えないため、必ず通常攻撃にする。
        if (attacker.IsSkillLocked) return MonsterFactory.CreateNormalAttack();

        // ② 上の例外以外では、所持スキルとは別に基本の攻撃を等確率で1つ選ぶ。
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
    /// 今回選んだ技と種別を記録し、対戦相手の柔軟・信仰AIが次に参照できるようにする。
    /// BattleSimulator は実行処理後に呼ぶため、失敗・封印時も記録される。
    /// ランダムスキルの場合、内部で発動した技ではなく、最初に選んだ RandomSkill が記録される。
    /// </summary>
    public void RecordAction(MonsterData attacker, SkillData skill)
    {
        // 実行の成否ではなく、AIが選んだ技を記録する。次に行動する信仰・柔軟がこの履歴を読む。
        attacker.LastUsedSkill = skill; // 柔軟用に技そのものの参照を保存する。コピー時には新しいSkillDataを作って保護する。
        attacker.LastUsedSkillType = skill.SkillType; // 行動した本人の履歴を今回の種別で上書きし、過去の履歴は残さない。
    }
}
