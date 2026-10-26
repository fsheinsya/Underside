using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>金額操作、被弾判定、演出キャンセル、既存シーン参照の移行を実オブジェクトで確認する。</summary>
public static class UIRefactorChecks
{
    private static int assertions;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/UnderSide/Check UI Refactor")]
    public static void Run()
    {
        assertions = 0;
        var randomState = Random.state;
        var previousSession = GameSession.Instance;
        bool logging = Debug.unityLogger.logEnabled;
        try
        {
            Random.InitState(20261023);
            Debug.unityLogger.logEnabled = false;
            CheckAmountInput();
            CheckDamageFlags();
            CheckScenesAndSharedViews();
            CheckCancellationAndPlacement();
        }
        finally
        {
            SetSession(previousSession);
            Random.state = randomState;
            Debug.unityLogger.logEnabled = logging;
        }
        Debug.Log($"UI_REFACTOR_CHECKS_PASSED: {assertions} assertions.");
    }

    public static void RunAll()
    {
        Run();
        PersonalityImplementationChecks.Run();
        SceneInputGateChecks.Run();
    }

    private static void Assert(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("UI refactor check failed: " + message);
    }

    private static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, PrivateInstance).SetValue(instance, value);

    private static object Invoke(object instance, string method, params object[] arguments)
        => instance.GetType().GetMethod(method, PrivateInstance).Invoke(instance, arguments);

    private static void SetSession(GameSession session)
        => typeof(GameSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, session);

    private static void CheckAmountInput()
    {
        var root = new GameObject("AmountTest", typeof(RectTransform));
        var inputObject = new GameObject("Input", typeof(RectTransform), typeof(TMP_InputField));
        inputObject.transform.SetParent(root.transform);
        var input = inputObject.GetComponent<TMP_InputField>();
        int coin = int.MaxValue;
        bool unlocked = true;
        int sounds = 0;
        using (var controller = new BetAmountController(null, input, root, () => coin, () => unlocked,
            () => { }, () => sounds++))
        {
            controller.ClampAmount();
            Assert(controller.Amount == 10 && input.text == "10", "initial amount appears in input");
            controller.StartLongPress(true, 0f);
            Assert(controller.Amount == 11 && sounds == 1, "pointer down changes once and plays sound");
            controller.Tick(0.29f);
            Assert(controller.Amount == 11, "short click has no repeat");
            controller.Tick(4.99f);
            Assert(controller.Amount == 12, "before five seconds repeats by one without catch-up bursts");
            controller.Tick(5.3f);
            Assert(controller.Amount == 22, "after five seconds repeats by ten");
            controller.Tick(10f);
            Assert(controller.Amount == 122, "ten seconds repeats by one hundred");
            controller.Tick(15f);
            Assert(controller.Amount == 1122 && sounds == 1, "fifteen seconds repeats by one thousand silently");
            controller.StopLongPress();
            controller.Tick(30f);
            Assert(controller.Amount == 1122, "release stops repeats");

            controller.StartLongPress(false, 40f);
            controller.Tick(50f);
            Assert(controller.Amount == 1021, "decrease uses the same ten-second step");
            controller.StopLongPress();
            controller.StartLongPress(true, 60f);
            controller.Tick(60.3f);
            Assert(controller.Amount == 1023, "repress resets step to one");
            root.SetActive(false);
            controller.Tick(80f);
            root.SetActive(true);
            controller.Tick(90f);
            Assert(controller.Amount == 1023, "closing UI stops hold permanently");

            input.onEndEdit.Invoke("9999999999");
            Assert(controller.Amount == int.MaxValue, "large direct input clamps to integer coin cap");
            controller.StartLongPress(true, 100f);
            controller.Tick(150f);
            Assert(controller.Amount == int.MaxValue, "large hold steps cannot overflow");
            controller.StopLongPress();
            input.onEndEdit.Invoke("0");
            Assert(controller.Amount == 1, "minimum bet is one");
            input.onEndEdit.Invoke("abc");
            Assert(controller.Amount == 1 && input.text == "1", "invalid input keeps committed amount");
            Assert(input.onValidateInput("", 0, 'a') == '\0' && input.onValidateInput("", 0, '7') == '7',
                "only ASCII digits accepted");
            input.SetTextWithoutNotify("73");
            controller.FinishEditing();
            Assert(controller.Amount == 73, "selection without enter commits edited value");
            unlocked = false;
            input.onEndEdit.Invoke("90");
            controller.StartLongPress(true, 200f);
            controller.Tick(210f);
            Assert(controller.Amount == 73 && input.text == "73", "locked UI ignores edits and holds");
            coin = 0;
            controller.ClampAmount();
            Assert(controller.Amount == 0, "no coins allows only zero");
        }
        Assert(input.onValidateInput == null, "disposing releases validator");
        Object.DestroyImmediate(root);
        Assert(BetAmountController.GetHoldStep(4.999f) == 1 && BetAmountController.GetHoldStep(5f) == 10,
            "five second boundary");
        Assert(BetAmountController.GetHoldStep(9.999f) == 10 && BetAmountController.GetHoldStep(10f) == 100,
            "ten second boundary");
    }

    private static MonsterData Monster(string name) => new MonsterData
    {
        Name = name,
        Element = ElementType.Fire,
        Personality = PersonalityType.Stoic,
        Stats = new MonsterStats { MaxHp = 100, Attack = 30, Defense = 5, Accuracy = 0, Evasion = 0 },
        CurrentHp = 100,
        Skills = new List<SkillData> { MonsterFactory.CreateNormalAttack() }
    };

    private static void CheckDamageFlags()
    {
        var simulator = new BattleSimulator();
        bool sawDamage = false;
        bool sawFailure = false;
        for (int scenario = 0; scenario < 6; scenario++)
        for (int trial = 0; trial < 50; trial++)
        {
            var attacker = Monster("attacker");
            var target = Monster("target");
            if (scenario == 1) target.IsInvincible = true;
            if (scenario == 2) { target.Personality = PersonalityType.Adaptive; target.PhysicalAdaptation = target.MagicAdaptation = 100; }
            if (scenario == 3) target.Stats.Evasion = 100;
            if (scenario == 4) { target.Personality = PersonalityType.Unyielding; target.CurrentHp = 1; }
            if (scenario == 5) attacker.Skills[0] = new SkillData { SkillType = SkillType.StrongAttack, SuccessRate = 0 };
            var logger = new BattleLogger();
            Set(simulator, "logger", logger);
            bool reversed = trial % 2 != 0;
            int before = target.CurrentHp;
            Invoke(simulator, "ProcessTurn", attacker, target, 1, reversed ? target : attacker, reversed ? attacker : target);
            var action = logger.Logs.Single(log => log.IsAction);
            Assert(action.TargetTookDamage == (target.CurrentHp < before), "action stores actual target HP loss");
            Assert(action.IsLeftAction != reversed, "damage result retains action side");
            if (scenario >= 1 && scenario <= 4) Assert(!action.TargetTookDamage, "blocked attack never requests hit motion");
            if (scenario == 0 && action.TargetTookDamage) sawDamage = true;
            if (scenario == 5 && action.UsedSkillType == SkillType.StrongAttack)
            {
                sawFailure = true;
                Assert(!action.TargetTookDamage && attacker.CurrentHp < 100, "recoil cannot look like target damage");
            }
        }
        Assert(sawDamage && sawFailure, "successful attacks and failed strong attacks both exercised");
    }

    private static void CheckScenesAndSharedViews()
    {
        // 編集中のシーンを閉じたり変更したりしないよう、検証専用のPreview Sceneを使う。
        var betScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Bet.unity");
        try
        {
        var objects = betScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject).ToArray();
        Assert(objects.Sum(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount) == 0, "Bet scene has no missing scripts");
        var views = objects.SelectMany(go => go.GetComponents<StatusDetailView>()).ToArray();
        Assert(views.Length == 2 && views.Any(view => view is LeftStatusDetailView)
            && views.Any(view => view is RightStatusDetailView), "both existing view script GUIDs still resolve");
        foreach (var view in views)
        {
            var serialized = new SerializedObject(view);
            foreach (string field in new[] { "rootObject", "charaImage", "hpText", "atkText", "defText",
                "elementText", "personalityText", "oddsText", "skillText1", "skillText2", "statusPanel", "elementPanel" })
            {
                var property = serialized.FindProperty(field);
                Assert(property != null && property.objectReferenceValue != null, "inherited field retains scene assignment: " + field);
            }
        }
        foreach (var button in objects.SelectMany(go => go.GetComponents<Button>())) CheckListeners(button.onClick);
        foreach (var trigger in objects.SelectMany(go => go.GetComponents<EventTrigger>()))
            foreach (var entry in trigger.triggers) CheckListeners(entry.callback);

        var data = Monster("display");
        foreach (var view in views)
        {
            // Awakeを手動実行して、非アクティブなシーン内Viewも初期化する。
            typeof(StatusDetailView).GetMethod("Awake", PrivateInstance).Invoke(view, null);
            view.Show(data, 2f);
            var serialized = new SerializedObject(view);
            var image = (Image)serialized.FindProperty("charaImage").objectReferenceValue;
            Assert(image.transform.localScale.x == (view is LeftStatusDetailView ? 2f : -2f), "fixed left/right orientation");
            Assert(((TMP_Text)serialized.FindProperty("hpText").objectReferenceValue).text == "HP:100",
                "shared implementation displays data");
        }

        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(betScene);
        }

        var allResultScene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/AllResult.unity");
        try
        {
        var canvas = allResultScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).First();
        var font = canvas.GetComponentsInChildren<TMP_Text>(true).First().font;
        int submits = 0;
        var panel = new RankingResultPanel(canvas, font, "test", 123, 4, () => submits++, () => { }, () => { });
        panel.Show();
        var rootPanel = canvas.transform.Find("RankingRegistrationPanel");
        Assert(rootPanel != null && rootPanel.gameObject.activeSelf, "ranking panel generated under owning Canvas");
        Assert(rootPanel.GetComponentsInChildren<Button>(true).Length == 3, "ranking actions retained");
        rootPanel.Find("SubmitButton").GetComponent<Button>().onClick.Invoke();
        Assert(submits == 1, "ranking button calls controller action");
        var entries = Enumerable.Range(0, 6).Select(i => new RankingData { userName = "p" + i, coin = i, round = (ulong)i }).ToList();
        panel.ShowEntries(entries);
        var ranking = rootPanel.Find("RankingEntries").GetComponent<TMP_Text>();
        Assert(ranking.text.Contains("p4") && !ranking.text.Contains("p5") && entries.Count == 6 && !ranking.richText,
            "ranking shows top five without changing saved list");
        Assert(rootPanel.Find("SubmitButton").GetComponent<RectTransform>().anchoredPosition == new Vector2(-155, 7),
            "ranking layout preserved");
        panel.CompleteDecision();
        Assert(!rootPanel.Find("SubmitButton").GetComponent<Button>().interactable
            && !rootPanel.Find("DeclineButton").GetComponent<Button>().interactable, "decision prevents repeat submission");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(allResultScene);
        }
    }

    private static void CheckListeners(UnityEngine.Events.UnityEventBase calls)
    {
        for (int index = 0; index < calls.GetPersistentEventCount(); index++)
        {
            var target = calls.GetPersistentTarget(index);
            if (!(target is BetSceneController)) continue;
            Assert(target.GetType().GetMethod(calls.GetPersistentMethodName(index)) != null, "existing Bet UnityEvent entry preserved");
        }
    }

    private static void CheckCancellationAndPlacement()
    {
        var sessionObject = new GameObject("SessionTest");
        var session = sessionObject.AddComponent<GameSession>();
        SetSession(session);
        Set(session, "monsterFactory", new MonsterFactory());
        session.StartNewGame();
        Assert(!BetPlacementService.TrySave(session, PredictionSide.Left, 0)
            && !BetPlacementService.TrySave(session, PredictionSide.Left, 101), "invalid bet is rejected");
        Assert(BetPlacementService.TrySave(session, PredictionSide.Right, 73)
            && session.CurrentBet.Amount == 73 && session.CurrentBet.Side == PredictionSide.Right
            && session.CurrentCoin == 100, "saving bet keeps amount/side and does not consume coins early");

        var controllerObject = new GameObject("ResultCancellationTest");
        controllerObject.SetActive(false);
        var controller = controllerObject.AddComponent<ResultSceneController>();
        var imageObject = new GameObject("Winner", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(controllerObject.transform);
        var image = imageObject.GetComponent<Image>();
        Set(controller, "winnerImage", image);
        Set(controller, "result", new MatchResultData());
        using (var cancellation = new CancellationTokenSource())
        {
            Set(controller, "animationCancellation", cancellation);
            var task = (UniTask)Invoke(controller, "PlayResultAnimation", cancellation.Token);
            Assert(DOTween.IsTweening(image), "result animation starts a real tween");
            Tween tween = DOTween.TweensByTarget(image).Single();
            Invoke(controller, "OnDisable");
            Assert(cancellation.IsCancellationRequested && !tween.IsActive(),
                $"leaving result cancels and kills its tween (cancelled={cancellation.IsCancellationRequested}, active={tween.IsActive()}, task={task.Status})");
            Assert(task.GetAwaiter().IsCompleted, "cancelled animation completes without touching later UI");
            task.GetAwaiter().GetResult(); // 正常キャンセルなら例外を公開しない。
            Set(controller, "animationCancellation", null);
        }
        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(sessionObject);
        SetSession(null);
    }
}
