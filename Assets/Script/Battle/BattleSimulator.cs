using UnityEngine;

public class BattleSimulator
{
    private ActionAI ai = new();
    private SkillExecutor executor = new();
    private StatusSystem status = new();
    private BattleLogger logger = new();

    private bool suddenDeathTriggered = false;

    public MatchResultData Simulate(MonsterData left, MonsterData right, BetData bet)
    {
        left.CurrentHp = left.Stats.MaxHp;
        right.CurrentHp = right.Stats.MaxHp;

        logger.Add($"{left.Name} vs {right.Name} 開始！", left, right);

        int turn = 1;

        while (left.CurrentHp > 0 && right.CurrentHp > 0)
        {
            logger.Add($"--- Turn {turn} ---", left, right);

            ProcessTurn(left, right, turn, left, right);
            if (right.CurrentHp <= 0) break;

            ProcessTurn(right, left, turn, left, right);

            status.ApplyEndTurn(left, logger, left, right);
            status.ApplyEndTurn(right, logger, left, right);

            if (turn > 20 && !suddenDeathTriggered)
            {
                suddenDeathTriggered = true;
                ApplySuddenDeath(left, right, bet);
            }

            turn++;
        }

        PredictionSide winnerSide = left.CurrentHp > 0 ? PredictionSide.Left : PredictionSide.Right;
        var winner = left.CurrentHp > 0 ? left : right;

        bool isSuccess = bet != null && bet.Side == winnerSide;

        return new MatchResultData
        {
            WinnerName = winner.Name,
            WinnerSide = winnerSide,
            IsPredictionSuccess = isSuccess,
            RewardCoin = isSuccess ? bet.Amount * 2 : 0,
            BattleLogs = logger.Logs
        };
    }

    private void ProcessTurn(MonsterData attacker, MonsterData defender, int turn, MonsterData left, MonsterData right)
    {
        if (!status.CanAct(attacker)) return;

        var skill = ai.ChooseSkill(attacker, defender, turn);

        // まず「この技を使う」という行動ログを1回だけ入れる
        logger.AddAction(
            $"{attacker.Name} は {skill.SkillName} を使った！",
            left,
            right,
            skill.SkillType,
            attacker == left,
            ShouldPlayMotion(skill.SkillType)
        );

        // 実際の処理
        executor.Execute(attacker, defender, skill, logger, left, right);

        // 柔軟AI用
        ai.RecordAction(attacker, skill);
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