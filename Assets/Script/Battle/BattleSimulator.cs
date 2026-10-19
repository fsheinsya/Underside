using UnityEngine;

public class BattleSimulator
{
    public const int MaxTurns = 40;
    private ActionAI ai = new(); // 両者のスキル選択を担当する性格AI。行動履歴は各 MonsterData に保存する。
    private SkillExecutor executor = new();
    private StatusSystem status = new();
    private BattleLogger logger = new();

    public MatchResultData Simulate(MonsterData left, MonsterData right, BetData bet, ModeType mode)
    {
        logger = new BattleLogger();
        double payoutMultiplier = bet != null ? bet.PayoutMultiplier : 0.0;
        // MonsterDataは再利用されるため、各試合の一時状態を初期化してから初期倍率を反映する。
        PersonalityRules.Reset(left);
        PersonalityRules.Reset(right);
        PersonalityRules.Refresh(left, right, logger, left, right);
        PersonalityRules.Refresh(right, left, logger, left, right);

        logger.Add($"{left.Name} vs {right.Name} 開始！", left, right);

        int turn = 1; // 性格AIへ渡す戦闘全体のターン番号。冷静の序盤判定と挑発の奇数・偶数判定に使う。

        while (left.CurrentHp > 0 && right.CurrentHp > 0 && turn <= MaxTurns)
        {
            // 1ターン内は、性格更新 → 左行動 → 右行動 → 毒などのターン終了効果 → 決着処理の順。
            logger.Add($"--- Turn {turn} ---", left, right);
            // 20ターンを超えて戦闘が続いたとき、21ターン目の開始時に一度だけ表示する。
            if (turn == 21)
                logger.Add("決着の時が訪れた！おかしな出来事が始まろうとしている、、、", left, right);
            // ターン番号に応じた覚醒・倍率更新を左右の行動より先に行う。
            PersonalityRules.BeginTurn(left, right, turn, logger, left, right);
            PersonalityRules.BeginTurn(right, left, turn, logger, left, right);

            ProcessTurn(left, right, turn, left, right); // 左側が先に行動する。信仰AIは、この時点で記録済みの右側の直近の技を参照する。
            if (left.CurrentHp <= 0 || right.CurrentHp <= 0) break;

            ProcessTurn(right, left, turn, left, right); // 右側も同じターン番号で行動する。左側が行動済みなら信仰AIはその新しい記録を参照する。
            if (left.CurrentHp <= 0 || right.CurrentHp <= 0) break;

            status.ApplyEndTurn(left, logger, left, right);
            if (left.CurrentHp <= 0) break;
            status.ApplyEndTurn(right, logger, left, right);
            if (right.CurrentHp <= 0) break;

            if (turn >= BattleTurnEvents.StartTurn)
            {
                payoutMultiplier = BattleTurnEvents.Apply(left, right, logger, payoutMultiplier, bet != null);
            }

            turn++; // 両者の行動とターン終了処理の後で、共通のターン番号を1つ進める。
        }

        bool leftWins = left.CurrentHp > 0;
        if (left.CurrentHp > 0 && right.CurrentHp > 0)
        {
            if (mode == ModeType.story)
            {
                logger.Add($"{MaxTurns}ターン経過――闘技場の扉が蹴破られた！", left, right);
                logger.Add("王国の兵士が来た！『違法賭博の現行犯だ！全員、動くな！』", left, right);
                logger.Add("兵士：そこを動くな！今から検挙する！", left, right);
                logger.Add("あなたは捕らえられ、コインはすべて没収された……。", left, right);
                return new MatchResultData
                {
                    IsKingdomRaidEnding = true,
                    FinalPayoutMultiplier = payoutMultiplier,
                    BattleLogs = logger.Logs
                };
            }

            float leftRatio = (float)left.CurrentHp / left.GetCurrentMaxHp();
            float rightRatio = (float)right.CurrentHp / right.GetCurrentMaxHp();
            leftWins = Mathf.Approximately(leftRatio, rightRatio) ? Random.value < 0.5f : leftRatio > rightRatio;
            logger.Add($"{MaxTurns}ターン経過：残りHP割合で判定（同率は抽選）。{(leftWins ? left.Name : right.Name)} の勝利！", left, right);
        }
        PredictionSide winnerSide = leftWins ? PredictionSide.Left : PredictionSide.Right;
        var winner = leftWins ? left : right;

        bool isSuccess = bet != null && bet.Side == winnerSide;

        return new MatchResultData
        {
            WinnerName = winner.Name,
            WinnerSide = winnerSide,
            IsPredictionSuccess = isSuccess,
            FinalPayoutMultiplier = payoutMultiplier,
            RewardCoin = isSuccess ? CalculateReward(bet.Amount, payoutMultiplier) : 0,
            BattleLogs = logger.Logs
        };
    }

    // 777倍などが重なっても整数の桁あふれで配当がマイナスにならないようにする。
    public static int CalculateReward(int amount, double payoutMultiplier)
    {
        double reward = System.Math.Round(amount * payoutMultiplier);
        return (int)System.Math.Max(0.0, System.Math.Min(int.MaxValue, reward));
    }

    private void ProcessTurn(MonsterData attacker, MonsterData defender, int turn, MonsterData left, MonsterData right)
    {
        // 1回の行動は、行動可能判定 → 技選択 → 行動ログ → 効果実行 → 技履歴更新で完了する。
        // 行動不能でも、前回の行動で得た無敵はここで終了する。
        attacker.IsInvincible = false;
        if (!status.CanAct(attacker))
        {
            logger.AddStatusApplied($"{attacker.Name} はスタンで行動できない！", attacker, left, right);
            PersonalityRules.Refresh(attacker, defender, logger, left, right);
            return;
        }
        var skill = ai.ChooseSkill(attacker, defender, turn); // 本人の性格・HP・所持スキルと、相手の行動履歴・ターン番号から今回の技を選ぶ。

        // 覚醒後の専用技は戦闘ログだけ改名し、ステータス画面用の所持技名は「???」のまま保つ。
        string skillDisplayName = attacker.Personality == PersonalityType.Inept
            && attacker.Awakened && skill.SkillType == SkillType.StrongAttack
            && skill.SkillName == "???"
            ? "英雄の一撃" : skill.SkillName;

        // まず「この技を使う」という行動ログを1回だけ入れる
        logger.AddAction(
            $"{attacker.Name} は {skillDisplayName} を使った！",
            left,
            right,
            skill.SkillType,
            attacker == left,
            ShouldPlayMotion(attacker, skill.SkillType)
        );

        // 選んだ技の効果や成功判定を処理する。性格AI自体は効果の適用を担当しない。
        executor.Execute(attacker, defender, skill, logger, left, right);

        // 相手の信仰AIが参照できるよう、実行結果に関係なく「今回選んだ技」の種別を残す。
        ai.RecordAction(attacker, skill); // 失敗・封印時も更新する。ランダムスキルは内部で発動した技ではなく RandomSkill を記録する。
    }

    private bool ShouldPlayMotion(MonsterData attacker, SkillType skillType)
    {
        // 中二・挑戦は通常攻撃や魔法を選んだ場合も、行動に伴うモーションを出さない。
        // 相手の行動でこのスライムが被弾する際のモーションは通常どおり再生する。
        if (attacker.Personality == PersonalityType.Delusional || attacker.Personality == PersonalityType.Aggressive)
            return false;

        switch (skillType)
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
                return true;

            default:
                return false;
        }
    }

}
