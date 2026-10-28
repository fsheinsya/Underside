using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>ターン境界、独立抽選、単一効果、配当分布、隠しエンドを実際の戦闘処理で検証する。</summary>
public static class BattleTurnEventChecks
{
    private static int assertions;

    [MenuItem("Tools/UnderSide/Check Turn Events and Endings")]
    public static void Run()
    {
        var previous = Random.state;
        assertions = 0;
        try
        {
            CheckEventBoundaryAndTurnCap();
            CheckPayoutDistribution();
            CheckIndependentEvents();
            CheckTurnLimits();
            CheckCoinSettlement();
            Debug.Log($"TURN_EVENT_CHECKS_PASSED: {assertions} assertions.");
        }
        finally { Random.state = previous; }
    }

    // 初めてCIを試すための入口。配当分布などの網羅検証とは別にターン境界だけを実行する。
    [MenuItem("Tools/UnderSide/Check Turn 20 and 40 Boundaries")]
    public static void RunBoundaries()
    {
        var previous = Random.state;
        assertions = 0;
        try
        {
            CheckEventBoundaryAndTurnCap();
            Debug.Log($"TURN_BOUNDARY_CHECKS_PASSED: {assertions} assertions; 64 battles.");
        }
        finally { Random.state = previous; }
    }

    private static void CheckEventBoundaryAndTurnCap()
    {
        // 期待値を本体のStartTurn/MaxTurnsから取ると、誤った変更まで正解にしてしまう。
        const int expectedStart = 20;
        const int expectedLast = 40;
        const string announcement = "決着の時が訪れた！おかしな出来事が始まろうとしている、、、";

        foreach (var mode in new[] { ModeType.story, ModeType.ranking })
        {
            bool sawTurn20Event = false;
            bool sawTurn21Event = false;
            bool sawTurn40Event = false;
            bool sawTurn20WithoutEvent = false;

            // 同じseedで毎回再現できる実戦を実行する。イベントの必発化はしない。
            for (int seed = 0; seed < 32; seed++)
            {
                Random.InitState(seed);
                var left = Passive("境界テスト左");
                var right = Passive("境界テスト右");
                left.DebuffImmune = right.DebuffImmune = true;
                var bet = new BetData { Amount = 10, PayoutMultiplier = 2, Side = PredictionSide.Left };
                var result = new BattleSimulator().Simulate(left, right, bet, mode);
                string context = $"mode={mode}, seed={seed}";

                Assert(left.CurrentHp > 0 && right.CurrentHp > 0,
                    $"boundary fixtures must survive all 40 turns ({context})");
                Assert(result.BattleLogs.Where(log => log.Message.StartsWith("--- Turn "))
                    .Select(log => log.Message)
                    .SequenceEqual(Enumerable.Range(1, expectedLast).Select(turn => $"--- Turn {turn} ---")),
                    $"battle must process turns 1..40 exactly once, with no turn 41 ({context})");

                int currentTurn = 0;
                int announcementCount = 0;
                int turn20Events = 0;
                int actionsThisTurn = 0;
                bool eventAlreadyStarted = false;
                foreach (var log in result.BattleLogs)
                {
                    if (log.Message.StartsWith("--- Turn "))
                    {
                        currentTurn++;
                        actionsThisTurn = 0;
                        eventAlreadyStarted = false;
                    }
                    if (log.IsAction)
                    {
                        Assert(!eventAlreadyStarted, $"actions must precede end-of-turn events ({context}, turn={currentTurn})");
                        actionsThisTurn++;
                    }
                    if (log.Message == announcement)
                    {
                        announcementCount++;
                        Assert(currentTurn == 21 && actionsThisTurn == 0,
                            $"announcement must appear at the start of turn 21 ({context})");
                    }
                    if (!log.Message.Contains("イベント！")) continue;

                    Assert(currentTurn >= expectedStart && currentTurn <= expectedLast,
                        $"events must occur only on turns 20..40 ({context}, actual turn={currentTurn})");
                    Assert(actionsThisTurn == 2,
                        $"events must follow both surviving actors' actions ({context}, turn={currentTurn})");
                    eventAlreadyStarted = true;
                    if (currentTurn == 20) { sawTurn20Event = true; turn20Events++; }
                    if (currentTurn == 21) sawTurn21Event = true;
                    if (currentTurn == 40) sawTurn40Event = true;
                }
                Assert(announcementCount == 1, $"announcement must appear exactly once ({context})");
                sawTurn20WithoutEvent |= turn20Events == 0;

                if (mode == ModeType.story)
                    Assert(result.IsKingdomRaidEnding && !result.IsPredictionSuccess
                        && result.RewardCoin == 0 && result.WinnerName == null,
                        $"story must end in a raid without a winner or payout after turn 40 ({context})");
                else
                    Assert(!result.IsKingdomRaidEnding
                        && result.BattleLogs.Any(log => log.Message.StartsWith("40ターン経過：")),
                        $"ranking must adjudicate the battle after turn 40 ({context})");
            }

            Assert(sawTurn20Event, $"events must be reachable on turn 20 ({mode})");
            Assert(sawTurn21Event, $"events must remain reachable on turn 21 ({mode})");
            Assert(sawTurn40Event, $"events must still be processed on the final turn 40 ({mode})");
            Assert(sawTurn20WithoutEvent, $"events are random, not guaranteed on turn 20 ({mode})");
            Debug.Log($"TURN_BOUNDARY_CASE_PASSED: {mode}; no events before 20; events on 20/21/40; announcement on 21; stops at 40; no turn 41.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Turn event check failed: " + message);
    }

    private static MonsterData Passive(string name, int hp = 10000)
    {
        return new MonsterData
        {
            Name = name,
            Personality = PersonalityType.Delusional,
            Stats = new MonsterStats { MaxHp = hp, Attack = 10, Defense = 10 },
            CurrentHp = hp,
            Skills = new List<SkillData>
            {
                new SkillData { SkillName = "自称", SkillType = SkillType.Delusion, SuccessRate = 100 }
            }
        };
    }

    private static void CheckPayoutDistribution()
    {
        var counts = new Dictionary<double, int>();
        for (int draw = 0; draw < BattleTurnEvents.PayoutDrawCount; draw++)
        {
            double factor = BattleTurnEvents.GetPayoutFactor(draw);
            if (!counts.ContainsKey(factor)) counts[factor] = 0;
            counts[factor]++;
        }
        Assert(counts.Count == 5 && counts[1.1] == 50000 && counts[1.5] == 30000
            && counts[2.0] == 15000 && counts[3.0] == 4999 && counts[777.0] == 1,
            "exact payout distribution: 50%, 30%, 15%, 4.999%, 0.001%");
        Assert(BattleSimulator.CalculateReward(10, 2 * 1.1 * 1.5) == 33, "payout factors multiply cumulatively");
        Assert(BattleSimulator.CalculateReward(100, 2 * 777) == 155400, "777 factor pays out");
        Assert(BattleSimulator.CalculateReward(int.MaxValue, 777) == int.MaxValue, "large payout cannot overflow");
    }

    private static void CheckIndependentEvents()
    {
        var masks = new HashSet<int>();
        var damageAmounts = new HashSet<int>();
        var targets = new HashSet<string>();
        bool lethalWithPayout = false;
        for (int seed = 0; seed < 4096; seed++)
        {
            Random.InitState(seed);
            bool damage = Random.value < 0.25f;
            bool debuff = Random.value < 0.25f;
            bool buff = Random.value < 0.25f;
            bool payout = Random.value < 0.25f;
            masks.Add((damage ? 1 : 0) | (debuff ? 2 : 0) | (buff ? 4 : 0) | (payout ? 8 : 0));

            Random.InitState(seed);
            var left = Passive("左");
            var right = Passive("右");
            var logger = new BattleLogger();
            double multiplier = BattleTurnEvents.Apply(left, right, logger, 2, true);
            Assert(logger.Logs.Count(x => x.Message.StartsWith("ダメージイベント！")) == (damage ? 1 : 0), "one damage event per hit");
            Assert(logger.Logs.Count(x => x.Message.StartsWith("デバフイベント！")) == (debuff ? 1 : 0), "one debuff per hit");
            Assert(logger.Logs.Count(x => x.Message.StartsWith("バフイベント！")) == (buff ? 1 : 0), "one buff per hit");
            Assert(logger.Logs.Count(x => x.Message.StartsWith("配当イベント！")) == (payout ? 1 : 0), "independent payout hit");
            Assert(payout ? multiplier > 2 : multiplier == 2, "payout changes only on its own hit");

            int leftLoss = 10000 - left.CurrentHp;
            int rightLoss = 10000 - right.CurrentHp;
            if (damage)
            {
                Assert((leftLoss > 0) != (rightLoss > 0), "damage targets exactly one monster");
                damageAmounts.Add(leftLoss + rightLoss);
                targets.Add(leftLoss > 0 ? "左" : "右");
            }
            else Assert(leftLoss == 0 && rightLoss == 0, "other events do not deal immediate damage");

            if (damage && payout && !lethalWithPayout)
            {
                Random.InitState(seed);
                double lethalMultiplier = BattleTurnEvents.Apply(Passive("左", 1), Passive("右", 1), new BattleLogger(), 2, true);
                Assert(lethalMultiplier > 2, "lethal event does not cancel independently selected payout");
                lethalWithPayout = true;
            }
        }
        Assert(masks.Count == 16, "all combinations of four independent events can occur");
        Assert(damageAmounts.SetEquals(new[] { 5, 10, 30, 50 }) && targets.Count == 2, "damage candidates and both targets");
        Assert(lethalWithPayout, "lethal plus payout case exercised");
    }

    private static void CheckTurnLimits()
    {
        var simulator = new BattleSimulator();
        var bet = new BetData { Amount = 10, PayoutMultiplier = 2, Side = PredictionSide.Left };
        var left = Passive("左");
        var right = Passive("右", 20000);
        left.DebuffImmune = right.DebuffImmune = true;
        Random.InitState(172026);
        var ranking = simulator.Simulate(left, right, bet, ModeType.ranking);
        Assert(!ranking.IsKingdomRaidEnding && ranking.BattleLogs.Count(x => x.Message.StartsWith("--- Turn")) == 40,
            "ranking completes exactly 40 turns");
        Assert(left.CurrentHp > 0 && right.CurrentHp > 0, "timeout fixture survives events");
        Assert(ranking.BattleLogs.Any(x => x.Message.StartsWith("40ターン経過：")), "ranking timeout adjudication logged");
        float leftRatio = (float)left.CurrentHp / left.GetCurrentMaxHp();
        float rightRatio = (float)right.CurrentHp / right.GetCurrentMaxHp();
        Assert(Mathf.Approximately(leftRatio, rightRatio)
            || ranking.WinnerSide == (leftRatio > rightRatio ? PredictionSide.Left : PredictionSide.Right), "ranking winner uses HP ratio");
        int turn = 0;
        var eventTurns = new HashSet<int>();
        foreach (var log in ranking.BattleLogs)
        {
            if (log.Message.StartsWith("--- Turn")) turn++;
            if (log.Message.Contains("イベント！"))
            {
                Assert(turn >= 20 && turn <= 40, "events only occur from turn 20 through 40");
                eventTurns.Add(turn);
            }
        }
        Assert(eventTurns.Count > 1, "events recur on multiple turns");
        bool sawTurn20Event = eventTurns.Contains(20);
        for (int seed = 0; seed < 64 && !sawTurn20Event; seed++)
        {
            Random.InitState(seed);
            var boundary = simulator.Simulate(left, right, bet, ModeType.ranking);
            int start = boundary.BattleLogs.FindIndex(x => x.Message == "--- Turn 20 ---");
            int end = boundary.BattleLogs.FindIndex(x => x.Message == "--- Turn 21 ---");
            sawTurn20Event = boundary.BattleLogs.Skip(start).Take(end - start).Any(x => x.Message.Contains("イベント！"));
        }
        Assert(sawTurn20Event, "events can occur on turn 20 itself");
        Assert(bet.Amount == 10 && bet.PayoutMultiplier == 2, "original stake and odds are preserved");

        Random.InitState(172026);
        var story = simulator.Simulate(left, right, bet, ModeType.story);
        Assert(story.IsKingdomRaidEnding && !story.IsPredictionSuccess && story.RewardCoin == 0 && story.WinnerName == null,
            "story timeout has no winner or payout");
        Assert(story.BattleLogs.Count(x => x.Message.StartsWith("--- Turn")) == 40
            && story.BattleLogs.Any(x => x.Message.Contains("王国の兵士")), "raid happens after turn 40");
        Assert(story.FinalPayoutMultiplier == ranking.FinalPayoutMultiplier, "simulator reuse resets event payout");

        var attacker = Passive("勝者");
        attacker.Personality = PersonalityType.Inept;
        attacker.Stats.Attack = 1000;
        attacker.Skills[0] = MonsterFactory.CreateNormalAttack();
        // 覚醒前の無能は通常攻撃100%なので、乱数によらず初回で決着する。
        var early = simulator.Simulate(attacker, Passive("敗者", 1), bet, ModeType.story);
        Assert(!early.IsKingdomRaidEnding && early.WinnerSide == PredictionSide.Left && early.RewardCoin == 20,
            "ordinary story win before timeout still pays normally");
    }

    private static void CheckCoinSettlement()
    {
        // メニュー実行時にプレイ中のセッションを変更しない。空のエディターまたはbatchmodeで検証する。
        if (GameSession.Instance != null) return;
        var gameObject = new GameObject("TurnEventCheckSession");
        try
        {
            var session = gameObject.AddComponent<GameSession>();
            // EditModeではMonoBehaviour.Awakeが呼ばれないため、生成器だけを準備する。
            typeof(GameSession).GetField("monsterFactory", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(session, new MonsterFactory());
            session.mode = ModeType.story;
            session.AddCoin(int.MaxValue);
            Assert(session.CurrentCoin == int.MaxValue, "coin balance saturates without overflow");
            session.SetMatchResult(new MatchResultData { IsKingdomRaidEnding = true });
            Assert(session.CurrentCoin == 0, "raid confiscates all coins");
            session.StartNewGame();
            Assert(session.CurrentCoin == 100 && session.CurrentMatchResult == null, "new game clears raid ending");
        }
        finally { UnityEngine.Object.DestroyImmediate(gameObject); }
    }
}
