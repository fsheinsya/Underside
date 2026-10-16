using UnityEngine;

public class BattleSimulator
{
    private ActionAI ai = new(); // 両者のスキル選択を担当する性格AI。行動履歴は各 MonsterData に保存する。
    private SkillExecutor executor = new();
    private StatusSystem status = new();
    private BattleLogger logger = new();

    private bool suddenDeathTriggered = false;

    public MatchResultData Simulate(MonsterData left, MonsterData right, BetData bet)
    {
        left.CurrentHp = left.Stats.MaxHp;
        right.CurrentHp = right.Stats.MaxHp;

        logger.Add($"{left.Name} vs {right.Name} 開始！", left, right);

        int turn = 1; // 性格AIへ渡す戦闘全体のターン番号。冷静の序盤判定と挑発の奇数・偶数判定に使う。

        while (left.CurrentHp > 0 && right.CurrentHp > 0)
        {
            logger.Add($"--- Turn {turn} ---", left, right);

            ProcessTurn(left, right, turn, left, right); // 左側が先に行動する。信仰AIは、この時点で記録済みの右側の直近の技を参照する。
            if (right.CurrentHp <= 0) break;

            ProcessTurn(right, left, turn, left, right); // 右側も同じターン番号で行動する。左側が行動済みなら信仰AIはその新しい記録を参照する。

            status.ApplyEndTurn(left, logger, left, right);
            status.ApplyEndTurn(right, logger, left, right);

            if (turn > 20 && !suddenDeathTriggered)
            {
                suddenDeathTriggered = true;
                ApplySuddenDeath(left, right, bet);
            }

            turn++; // 両者の行動とターン終了処理の後で、共通のターン番号を1つ進める。
        }

        PredictionSide winnerSide = left.CurrentHp > 0 ? PredictionSide.Left : PredictionSide.Right;
        var winner = left.CurrentHp > 0 ? left : right;

        bool isSuccess = bet != null && bet.Side == winnerSide;

        return new MatchResultData
        {
            WinnerName = winner.Name,
            WinnerSide = winnerSide,
            IsPredictionSuccess = isSuccess,
            RewardCoin = isSuccess ? Mathf.RoundToInt(bet.Amount * bet.PayoutMultiplier) : 0,
            BattleLogs = logger.Logs
        };
    }

    private void ProcessTurn(MonsterData attacker, MonsterData defender, int turn, MonsterData left, MonsterData right)
    {
        if (!status.CanAct(attacker)) return; // 行動不能ならスキル選択と履歴更新を行わず、直近の記録はそのまま残る。

        var skill = ai.ChooseSkill(attacker, defender, turn); // 本人の性格・HP・所持スキルと、相手の行動履歴・ターン番号から今回の技を選ぶ。

        // まず「この技を使う」という行動ログを1回だけ入れる
        logger.AddAction(
            $"{attacker.Name} は {skill.SkillName} を使った！",
            left,
            right,
            skill.SkillType,
            attacker == left,
            ShouldPlayMotion(skill.SkillType)
        );

        // 選んだ技の効果や成功判定を処理する。性格AI自体は効果の適用を担当しない。
        executor.Execute(attacker, defender, skill, logger, left, right);

        // 相手の信仰AIが参照できるよう、実行結果に関係なく「今回選んだ技」の種別を残す。
        ai.RecordAction(attacker, skill); // 失敗・封印時も更新する。ランダムスキルは内部で発動した技ではなく RandomSkill を記録する。
    }

    private bool ShouldPlayMotion(SkillType skillType)
    {
        switch (skillType)
        {
            case SkillType.NormalAttack:
            case SkillType.StrongAttack:
            case SkillType.Heal:
            case SkillType.AttackBuff:
            case SkillType.DefenseBuff:
            case SkillType.PoisonDebuff:
                return true;

            default:
                return false;
        }
    }

    private void ApplySuddenDeath(MonsterData left, MonsterData right, BetData bet)
    {
        logger.Add("決着の刻が訪れた…！", left, right);

        int[] damages = { 10, 20, 30, 50 };
        int dmg = damages[Random.Range(0, damages.Length)];

        int pattern = Random.Range(0, 3);

        switch (pattern)
        {
            case 0:
                left.CurrentHp = Mathf.Max(0, left.CurrentHp - dmg);
                logger.Add($"左に {dmg} ダメージ！", left, right);
                break;

            case 1:
                right.CurrentHp = Mathf.Max(0, right.CurrentHp - dmg);
                logger.Add($"右に {dmg} ダメージ！", left, right);
                break;

            case 2:
                left.CurrentHp = Mathf.Max(0, left.CurrentHp - dmg);
                right.CurrentHp = Mathf.Max(0, right.CurrentHp - dmg);
                logger.Add($"両者に {dmg} ダメージ！", left, right);
                break;
        }

        if (bet != null)
        {
            int bonus = bet.Amount;
            bet.Amount += bonus;
            logger.Add($"🔥 かけ金が倍になった！（+{bonus}）", left, right);
        }
    }
}
