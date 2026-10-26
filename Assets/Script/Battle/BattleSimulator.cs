using UnityEngine; // 終了時のHP割合比較と、同率時の勝者抽選に使用する。

/// <summary>
/// 1試合を最後まで計算し、結果と再生用ログを返す。画面上のアニメーションはここでは再生しない。
/// 各ターンの司令塔。判断はActionAI、効果はSkillExecutor、状態異常はStatusSystemへ委譲する。
/// </summary>
public class BattleSimulator
{
    public const int MaxTurns = 40; // 最終ターンも行動・毒・イベントまで実行し、生存者が2体ならモード別の決着処理へ進む。
    private ActionAI ai = new(); // 両者のスキル選択を担当する性格AI。行動履歴は各 MonsterData に保存する。
    private SkillExecutor executor = new(); // 選んだ技の成功判定と効果を担当する。
    private StatusSystem status = new(); // スタンの行動停止とターン終了時の毒を担当する。
    private BattleLogger logger = new(); // 各処理時点のHP・状態を再生用ログとして保存する。

    /// <summary>左右のMonsterDataを直接更新して戦う。再利用する場合も試合開始時に一時状態をリセットする。</summary>
    /// <param name="left">画面左側。左右の配置に関係なく、現在の素早さが高い側が先に行動する。</param>
    /// <param name="right">画面右側。素早さが同じ場合は毎ターン先手を等確率で抽選する。</param>
    /// <param name="bet">賭け情報。nullなら予想的中・報酬・配当イベントの加算を扱わない。</param>
    /// <param name="mode">制限ターンで未決着の場合、storyは隠し終了、その他はHP割合で勝敗を決める。</param>
    public MatchResultData Simulate(MonsterData left, MonsterData right, BetData bet, ModeType mode)
    {
        logger = new BattleLogger(); // 前の試合のログを引き継がないよう、新しい記録先を用意する。
        double payoutMultiplier = bet != null ? bet.PayoutMultiplier : 0.0; // 元のオッズを読み出し、イベント倍率はこのローカル変数へ積む。
        // MonsterDataは再利用されるため、各試合の一時状態を初期化してから初期倍率を反映する。
        PersonalityRules.Reset(left); // 左のHP・バフ・耐性・技履歴などを試合開始状態へ戻す。
        PersonalityRules.Reset(right); // 右も同様に戻す。左右の初期化後に相性による倍率を計算する。
        PersonalityRules.Refresh(left, right, logger, left, right); // 左の逆境など、開始時から有効な性格倍率を反映する。
        PersonalityRules.Refresh(right, left, logger, left, right); // 引数の対象と相手だけを入れ替え、ログの左右は固定する。

        logger.Add($"{left.Name} vs {right.Name} 開始！", left, right); // 開始時点の左右の状態をログに記録する。

        int turn = 1; // 性格AIへ渡す戦闘全体のターン番号。冷静の序盤判定と挑発の奇数・偶数判定に使う。

        while (left.CurrentHp > 0 && right.CurrentHp > 0 && turn <= MaxTurns) // 両者生存かつ40ターン目以内の間だけ次のターンを開始する。
        {
            // 性格更新 → 素早さで先手決定 → 両者の行動 → 毒・イベント → 成長の順。
            logger.Add($"--- Turn {turn} ---", left, right); // ターンの区切りを再生側へ伝える。
            // 20ターンを超えて戦闘が続いたとき、21ターン目の開始時に一度だけ表示する。
            if (turn == 21) // イベントの開始条件は下のStartTurn=20。これは説明文を出すタイミング。
                logger.Add("決着の時が訪れた！おかしな出来事が始まろうとしている、、、", left, right); // 21ターン目に到達した場合だけ記録する。
            // ターン番号に応じた覚醒・倍率更新を左右の行動より先に行う。
            PersonalityRules.BeginTurn(left, right, turn, logger, left, right); // 左の覚醒・経験済み耐性の成長・執念などを更新する。
            PersonalityRules.BeginTurn(right, left, turn, logger, left, right); // 右も行動前に更新する。スタンで休んでもターン開始効果は受ける。

            MonsterData first = BattleStatRules.SelectFirstActor(left, right);
            MonsterData second = first == left ? right : left; // このターン内の順序は固定。成長や被弾による速度変化は次ターンから。
            ProcessTurn(first, second, turn, left, right);
            if (left.CurrentHp <= 0 || right.CurrentHp <= 0) break; // 決着後は後手が行動しない。

            ProcessTurn(second, first, turn, left, right);
            if (left.CurrentHp <= 0 || right.CurrentHp <= 0) break; // 後手の行動で決着したら毒やイベントを追加しない。

            status.ApplyEndTurn(left, logger, left, right); // 左の毒ダメージを先に処理する。
            if (left.CurrentHp <= 0) break; // 左が毒で倒れた時点で終了し、右の毒処理は行わない。
            status.ApplyEndTurn(right, logger, left, right); // 左が生存していれば右の毒ダメージを処理する。
            if (right.CurrentHp <= 0) break; // 右が倒れた場合もイベントへ進まない。

            if (turn >= BattleTurnEvents.StartTurn) // 20ターン目以降、行動と毒の処理後にイベントを適用する。
            {
                payoutMultiplier = BattleTurnEvents.Apply(left, right, logger, payoutMultiplier, bet != null); // 更新後の配当倍率を受け取り、次のターンへ保持する。
            }

            if (left.CurrentHp <= 0 || right.CurrentHp <= 0) break;
            BattleStatRules.ApplyEndTurnGrowth(left, turn);
            BattleStatRules.ApplyEndTurnGrowth(right, turn);

            turn++; // 両者の行動とターン終了処理の後で、共通のターン番号を1つ進める。
        }

        bool leftWins = left.CurrentHp > 0; // 通常決着では左が生存なら左勝利。両者生存の場合だけ以下で上書きする。
        if (left.CurrentHp > 0 && right.CurrentHp > 0) // 上限ターンまで両者が生き残った場合。
        {
            if (mode == ModeType.story) // ストーリーでは勝者を決めず、王国兵による終了結果を返す。
            {
                logger.Add($"{MaxTurns}ターン経過――闘技場の扉が蹴破られた！", left, right);
                logger.Add("王国の兵士が来た！『違法賭博の現行犯だ！全員、動くな！』", left, right);
                logger.Add("兵士：そこを動くな！今から検挙する！", left, right);
                logger.Add("あなたは捕らえられ、コインはすべて没収された……。", left, right);
                return new MatchResultData // 通常の勝者・報酬計算へ進まず、この結果で試合を終了する。
                {
                    IsKingdomRaidEnding = true, // 後続の画面処理へ隠しエンディングへの遷移を指示する。
                    FinalPayoutMultiplier = payoutMultiplier, // 経過倍率は残すが、この結果には報酬を設定しない。
                    BattleLogs = logger.Logs // ここまでの試合と終了演出のメッセージを再生できるようにする。
                };
            }

            float leftRatio = (float)left.CurrentHp / left.GetCurrentMaxHp(); // 小数で左の残HP割合を求める。HPの絶対量は比較しない。
            float rightRatio = (float)right.CurrentHp / right.GetCurrentMaxHp(); // 右も現在の性格倍率反映後の最大HPを分母にする。
            leftWins = Mathf.Approximately(leftRatio, rightRatio) ? Random.value < 0.5f : leftRatio > rightRatio; // ほぼ同率なら50%抽選、差があれば割合が大きい側。
            logger.Add($"{MaxTurns}ターン経過：残りHP割合で判定（同率は抽選）。{(leftWins ? left.Name : right.Name)} の勝利！", left, right);
        }
        PredictionSide winnerSide = leftWins ? PredictionSide.Left : PredictionSide.Right; // 勝者の左右を賭け情報と比較できる形式にする。
        var winner = leftWins ? left : right; // 勝者の名前などを取り出すための参照。

        bool isSuccess = bet != null && bet.Side == winnerSide; // 賭け情報があり、予想した側が勝った場合のみ的中。

        return new MatchResultData // 計算結果をUIへ渡す。ここではコイン残高やシーンを直接変更しない。
        {
            WinnerName = winner.Name, // 結果表示用の勝者名。
            WinnerSide = winnerSide, // 左右どちらが勝ったか。
            IsPredictionSuccess = isSuccess, // 賭けが的中したか。
            FinalPayoutMultiplier = payoutMultiplier, // イベントの上昇分まで含む最終倍率。
            RewardCoin = isSuccess ? CalculateReward(bet.Amount, payoutMultiplier) : 0, // 的中時だけ報酬を計算。betがnullなら右の0だけを評価する。
            BattleLogs = logger.Logs // 再生用に時系列のログ一覧を返す。
        };
    }

    // 777倍などが重なっても整数の桁あふれで配当がマイナスにならないようにする。
    public static int CalculateReward(int amount, double payoutMultiplier)
    {
        double reward = System.Math.Round(amount * payoutMultiplier); // 賭け金×最終倍率を整数相当へ丸める。既定の中間値丸めは偶数側。
        return (int)System.Math.Max(0.0, System.Math.Min(int.MaxValue, reward)); // 0～int上限へ収めてから整数に変換する。
    }

    private void ProcessTurn(MonsterData attacker, MonsterData defender, int turn, MonsterData left, MonsterData right)
    {
        // 1回の行動は、行動可能判定 → 技選択 → 行動ログ → 効果実行 → 技履歴更新で完了する。
        // 行動不能でも、前回の行動で得た無敵はここで終了する。
        attacker.IsInvincible = false; // 前回の無敵を解除する。次のスタン判定で休む場合も解除される。
        if (!status.CanAct(attacker)) // スタンの消費と解除を行い、今回行動できるかを取得する。
        {
            logger.AddStatusApplied($"{attacker.Name} はスタンで行動できない！", attacker, left, right);
            PersonalityRules.Refresh(attacker, defender, logger, left, right); // スタン解除後の性格倍率を更新する。
            return; // AIの技選択・乱数抽選・履歴更新を行わず、今回の行動機会を終了する。
        }
        var skill = ai.ChooseSkill(attacker, defender, turn); // 本人の性格・HP・所持スキルと、相手の行動履歴・ターン番号から今回の技を選ぶ。

        // 覚醒後の専用技は戦闘ログだけ改名し、ステータス画面用の所持技名は「???」のまま保つ。
        string skillDisplayName = attacker.Personality == PersonalityType.Inept // 無能の場合だけ別名の候補になる。
            && attacker.Awakened && skill.SkillType == SkillType.StrongAttack // 覚醒済みで、今回使う技が強攻撃であることを確認する。
            && skill.SkillName == "???" // 共通の強撃ではなく、専用の隠された技名だけを置き換える。
            ? "英雄の一撃" : skill.SkillName; // 元のSkillDataは変更せず、このログに出す文字列だけを変える。

        // まず「この技を使う」という行動ログを1回だけ入れる
        BattleLogEntry actionLog = logger.AddAction(
            $"{attacker.Name} は {skillDisplayName} を使った！",
            left, // 行動者がどちらでも、ログの左には画面左側のモンスターを渡す。
            right, // ログの右も画面右側で固定する。
            skill.SkillType, // 再生時の技・演出の種類を伝える。
            attacker == left, // 今回行動した側が左かどうかを記録する。
            ShouldPlayMotion(attacker, skill.SkillType) // 性格と技種別に応じて行動モーションの有無を決める。
        );

        // 選んだ技の効果や成功判定を処理する。性格AI自体は効果の適用を担当しない。
        int targetHpBefore = defender.CurrentHp;
        executor.Execute(attacker, defender, skill, logger, left, right);
        // 失敗・回避・無敵・完全耐性・HP1での踏みとどまりでは被弾させない。
        actionLog.TargetTookDamage = defender.CurrentHp < targetHpBefore;

        // 相手の信仰・柔軟AIが参照できるよう、実行結果に関係なく「今回選んだ技」を残す。
        ai.RecordAction(attacker, skill); // 失敗・封印時も更新する。ランダムスキルは内部で発動した技ではなく RandomSkill を記録する。
    }

    private bool ShouldPlayMotion(MonsterData attacker, SkillType skillType)
    {
        // 中二・挑戦は通常攻撃や魔法を選んだ場合も、行動に伴うモーションを出さない。
        // 相手の行動でこのスライムが被弾する際のモーションは通常どおり再生する。
        if (attacker.Personality == PersonalityType.Delusional || attacker.Personality == PersonalityType.Aggressive) // 技の種別より性格による抑制を優先する。
            return false; // 効果の成功・失敗には関係なく、この行動のモーションを省略する。

        switch (skillType) // 列挙した技だけモーションを許可する。新しい技は必要に応じてここへ追加する。
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
            case SkillType.FireMagic:
            case SkillType.WaterMagic:
            case SkillType.GrassMagic:
            case SkillType.LightMagic:
            case SkillType.DarkMagic:
            case SkillType.AllOutAttack:
            case SkillType.Heal:
            case SkillType.AttackBuff:
            case SkillType.DefenseBuff:
            case SkillType.PoisonDebuff:
                return true; // 再生側にこの行動のモーションを許可する。

            default:
                return false; // 待機・祈りなど、一覧にない技の行動モーションは省略する。
        }
    }

}
