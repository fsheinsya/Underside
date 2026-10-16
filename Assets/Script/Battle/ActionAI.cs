using UnityEngine; // 所持スキルからランダムに選ぶため、Unity の Random を利用する。

/// <summary>
/// 性格・現在HP・ターン数・相手の直近のスキル種別から、今回使うスキルを1つ選ぶAI。
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
        // 所持スキル一覧が未設定、または空なら専用の代替処理に進む。
        if (attacker.Skills == null || attacker.Skills.Count == 0) 
            return CreateNormalAttack(); // どの性格でも、代わりの通常攻撃を新しく作って返す。

        ///<summary>
        /// ↓このswicth文で性格の分岐を実装する
        ///</summary>

        switch (attacker.Personality) // 行動する側の性格に対応する分岐へ進む。
        {
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

            case PersonalityType.Aggressive: // 挑戦寄り：失敗しやすいスキルを優先する。
                return GetLowSuccess(attacker); // 威力や IsHighRisk フラグではなく、SuccessRate が最小のものを選ぶ。

            case PersonalityType.Timid: // 臆病：HPが少なければ回復を探し、それ以外は無敵化を探す。
                if (attacker.CurrentHp <= attacker.Stats.MaxHp / 2) // 最大HPの半分以下か判定する。整数除算なので奇数の最大HPは切り捨てる。
                {
                    var heal = FindSkill(attacker, SkillType.Heal); // 一覧の先頭から回復スキルを探す。
                    if (heal != null) return heal; // 回復があれば、無敵化よりも優先して選ぶ。
                }
                return GetEvasion(attacker); // HPが半分より多い場合や回復がない場合は、無敵化を優先し、なければランダムに選ぶ。
        }

        return attacker.Skills[Random.Range(0, attacker.Skills.Count)]; // 信仰で一致する技がない場合や未定義の性格では、全所持スキルから等確率で1つ選ぶ。
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

    // スキル実行前の成功判定に使われる SuccessRate が最小のスキルを返す。
    //　成功率の低いスキルを優先的に行う(即シ狙いなのでかならず負けるとは限らない)
    //　オッズを倍にする仕組みを作りたい

    private SkillData GetLowSuccess(MonsterData monster)
    {
        SkillData risky = monster.Skills[0]; // 先頭のスキルを、最も成功率が低い候補にする。
        foreach (var s in monster.Skills) // 所持スキルを先頭から順番に調べる。
            if (s.SuccessRate < risky.SuccessRate) // SuccessRate がさらに低いときだけ更新し、同率なら先にある候補を残す。
                risky = s; // 成功率がより低いスキルを新しい候補にする。
        return risky; // 即死などの効果内で行われる追加抽選は評価せず、設定された SuccessRate だけで選ぶ。
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
        return monster.Skills[0]; // 防御強化も回復もなければ、先頭のスキルを使う。
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

    // 所持スキルが未設定または空のときだけ使う、代わりの通常攻撃を作る。
    private SkillData CreateNormalAttack()
    {
        return new SkillData // 所持スキル一覧には追加せず、今回の行動用のデータを新しく作って返す。
        {
            SkillName = "通常攻撃", // 行動ログなどに表示する名前。
            SkillType = SkillType.NormalAttack, // SkillExecutor に通常攻撃として処理させる。
            Power = 0, // 攻撃力に加えるスキル分の威力は0。実際のダメージは攻撃力・防御力などから別途計算する。
            SuccessRate = 100 // スキル実行前の成功率を100%にする。スキル封印などの状態判定は実行側で行う。
        };
    }

    /// <summary>
    /// 今回選んだスキルの種別を記録し、対戦相手の信仰AIが次に参照できるようにする。
    /// BattleSimulator は実行処理後に呼ぶため、失敗・封印時も記録される。
    /// ランダムスキルの場合、内部で発動した技ではなく、最初に選んだ RandomSkill が記録される。
    /// </summary>
    public void RecordAction(MonsterData attacker, SkillData skill)
    {
        attacker.LastUsedSkillType = skill.SkillType; // 行動した本人の履歴を今回の種別で上書きし、過去の履歴は残さない。
    }
}
