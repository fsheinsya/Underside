using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine.UI;
using System.Text;

/// <summary>
/// 
/// </summary>

public class AllResultSceneController : MonoBehaviour
{
    [Header("最終結果表示UI")]
    [SerializeField] private TMP_Text finalCoinText;
    [SerializeField] private TMP_Text endingText;

    //アニメーションの設定
    private bool isAnimating = false;
    private ModeType currentMode;
    private int finalCoin;
    private int finalRound;
    private GameObject rankingPanel;
    private string rankingPlayerName;
    private Button submitButton;
    private Button declineButton;
    private TMP_Text feedbackText;
    private TMP_Text rankingText;
    private bool hasRankingDecision;

    private async void Start()
    {
        // ゲームセッションが空だったら
        if (GameSession.Instance == null)
        {
            //タイトル画面に戻す機能
            SceneManager.LoadScene(SceneNames.Title);
            return;
        }

        // 結果画面に入った時点の名前と成績を固定し、確認中に変わらないようにする。
        currentMode = GameSession.Instance.mode;
        finalCoin = GameSession.Instance.CurrentCoin;
        finalRound = GameSession.Instance.CurrentRound;
        rankingPlayerName = GameSession.Instance.RankingPlayerName;

        if (currentMode == ModeType.ranking)
        {
            Debug.Log($"[Ranking/Result] 名前「{rankingPlayerName}」: 所持コイン={finalCoin}, 到達ラウンド={finalRound}。更新の選択を待ちます。", this);
            CreateRankingPanel();
        }

        // 演出開始
        await PlayResultAnimation();

        if (rankingPanel != null)
        {
            rankingPanel.SetActive(true);
            RefreshRanking();
        }
    }

    private void Update()
    {
        // アニメ中はクリック無効
        if (isAnimating) return;

        // ランキング中は更新確認のボタンを操作するため、画面クリックでは遷移しない。
        if (currentMode == ModeType.ranking && rankingPanel != null) return;

        //画面をクリックをしてタイトル画面に遷移をする
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            SceneManager.LoadScene(SceneNames.Title);
        }
    }

    /// <summary>
    /// 結果演出メイン
    /// </summary>
    private async UniTask PlayResultAnimation()
    {
        isAnimating = true;

        int coin = finalCoin;

        //モードタイプを実装をする
        var mode = currentMode;

        //-------------------------
        // ① コインカウントアップ
        //-------------------------
        finalCoinText.text = "0";

        int displayCoin = 0;

        await DOTween.To(() => displayCoin, x =>
        {
            displayCoin = x;
            finalCoinText.text = displayCoin.ToString();
        }, coin, 1.5f) // ←時間調整OK
        .SetEase(Ease.OutCubic)//イージングを設定
        .AsyncWaitForCompletion();

        await UniTask.Delay(300);

        //-------------------------
        // ② ストーリーモードの場合:コイン数に応じてテキスト内容を変える
        //    ランキングモードの場合:(一旦)コイン数を表示させる
        //-------------------------

        string ending = "";

        if (mode == ModeType.story)
        { 
            //コインが0以下のとき債務者
            if (coin <= 0)
                ending = "債務者エンド(WORST END)\nうらに全てを奪われ、スライムとして戦うことになってしまった、、、";
            //コインが100以下の時は敗北エンドに移行
            else if (coin <= 100)
                ending = "敗北エンド(NORMAL END)\nうらに負けてしまったので、二度と関わらないと決意した。";
            //コインが300以下の時は凡人エンドに移行
            else if (coin <= 300)
                ending = "凡人エンド(BAD END)\nうらに少し勝った。その後も賭けに挑むが彼が幸せになることはなかった。";
            //コインが700以下の時は勝利エンドに移行
            else if (coin <= 700)
                ending = "勝利エンド(GOOD END)\nうらにかった！彼はひと時の金持ちとして有名となった!";
            //コインが700以降の時は裏社会の王エンドに移行
            else
                ending = "裏社会の王エンド(HAPPY END)\nうらを全て掌握し裏闘技場の王となり、街を支配し隣町の魔王も恐れる伝説となった！";

        }

        else if(mode == ModeType.ranking)
        {
            ending = $"コイン: {coin}  到達ラウンド: {finalRound}";
        }

            //-------------------------
            // ③ タイピング演出
            //-------------------------

        endingText.text = "";

        //一文字ずつタイピングアニメーション形式で出力する
        foreach (char c in ending)
        {
            endingText.text += c;
            await UniTask.Delay(30); // ←30ミリ秒毎に一文字ずつアニメーション
        }

        //-------------------------
        // ④ フェードイン（追加演出）
        //-------------------------
        endingText.alpha = 0;
        //0.5秒かけてテキストを表示させる
        await endingText.DOFade(1f, 0.5f)
            .AsyncWaitForCompletion(); 

        //表示させたらフラグをfalseにする
        isAnimating = false;
    }

    //
    private void SubmitRanking()
    {
        if (hasRankingDecision || isAnimating) return;

        // 名前が引き継がれていない場合は、別の名前を推測して保存しない。
        if (string.IsNullOrWhiteSpace(rankingPlayerName))
        {
            feedbackText.text = "プレイヤー名がありません。名前入力画面から開始してください。";
            Debug.LogWarning("[Ranking/Result] プレイヤー名がないため更新できません。", this);
            return;
        }

        Debug.Log($"[Ranking/Result] 名前「{rankingPlayerName}」の更新が許可されました。coin={finalCoin}, round={finalRound}", this);

        // intからulongへ変換する前に、不正な負数や0を除外する。
        if (finalCoin < 0 || finalRound < 1)
        {
            feedbackText.text = "コイン数またはラウンド数が不正です。";
            return;
        }

        try
        {
            // 「更新する」を押したときだけ、入力画面で選んだ名前の記録を更新する。
            if (!RankingService.Submit(rankingPlayerName, finalCoin, (ulong)finalRound, out string message))
            {
                feedbackText.text = message;
                return;
            }

            CompleteRankingDecision();
            feedbackText.text = message;
        }
        catch (System.Exception exception)
        {
            // 保存に失敗した場合は決定済みにせず、再試行または更新の辞退を可能にする。
            Debug.LogWarning($"[Ranking/Result] ランキングの保存に失敗しました: {exception}", this);
            feedbackText.text = "記録を保存できませんでした。もう一度お試しください。";
            return;
        }

        RefreshRanking();
    }

    private void DeclineRanking()
    {
        if (hasRankingDecision || isAnimating) return;

        // 辞退した場合は保存処理を呼ばず、以前の記録を残す。
        CompleteRankingDecision();
        feedbackText.text = "今回の記録は保存しませんでした。";
        Debug.Log($"[Ranking/Result] 名前「{rankingPlayerName}」の更新が辞退されました。記録は変更しません。", this);
    }

    private void CompleteRankingDecision()
    {
        // 一度決定した結果を、連打や別のボタンから再度処理しない。
        hasRankingDecision = true;
        submitButton.interactable = false;
        declineButton.interactable = false;
    }

    private void ReturnToTitle()
    {
        if (isAnimating) return;

        // 確認前に戻る場合も、スコアを自動保存しない。
        if (!hasRankingDecision) DeclineRanking();
        SceneManager.LoadScene(SceneNames.Title);
    }
    private void RefreshRanking()
    {
        System.Collections.Generic.List<RankingData> entries;
        try
        {
            // サービスから順位順の一覧を取得し、上位5人に絞る。
            entries = RankingService.GetRanking();
            if (entries.Count > 5)
            {
                entries.RemoveRange(5, entries.Count - 5);
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"ランキングの読み込みに失敗しました: {exception}");
            rankingText.text = "ランキングデータを読み込めませんでした。";
            return;
        }

        if (entries.Count == 0)
        {
            rankingText.text = "まだ記録がありません";
            return;
        }

        var builder = new StringBuilder();
        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            builder.Append(index + 1).Append("位  ")
                .Append(entry.userName).Append("    ")
                .Append(entry.coin).Append(" コイン    ")
                .Append(entry.round).Append(" ラウンド");
            if (index < entries.Count - 1) builder.AppendLine();
        }

        rankingText.text = builder.ToString();
        Debug.Log($"[Ranking/Result] 上位{entries.Count}人のランキングを表示しました。", this);
    }

    //ランキングのパネルに関する仕組み
    private void CreateRankingPanel()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("AllResult シーンに Canvas がありません。ランキング更新の確認を表示できません。");
            return;
        }

        rankingPanel = new GameObject("RankingRegistrationPanel", typeof(RectTransform), typeof(Image));
        rankingPanel.transform.SetParent(canvas.transform, false);
        rankingPanel.SetActive(false);
        var panelRect = rankingPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        rankingPanel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.12f, 0.94f);

        CreateText(rankingPanel.transform, "Heading", "ランキング更新の確認", 32, 0, 205, 740, 46);

        // 名前は入力欄ではなく、今回使用したプレイヤー名として表示する。
        TMP_Text playerNameText = CreateText(rankingPanel.transform, "PlayerName",
            string.IsNullOrWhiteSpace(rankingPlayerName) ? "プレイヤー名なし" : rankingPlayerName,
            24, 0, 149, 740, 56);
        playerNameText.richText = false;
        playerNameText.enableAutoSizing = true;
        playerNameText.fontSizeMin = 12;
        playerNameText.fontSizeMax = 24;

        CreateText(rankingPanel.transform, "Score", $"{finalCoin} コイン  /  到達 {finalRound} ラウンド", 25, 0, 100, 740, 40);
        CreateText(rankingPanel.transform, "Question", "今回の結果で記録を更新しますか？", 24, 0, 57, 740, 40);

        submitButton = CreateButton(rankingPanel.transform, "SubmitButton", "更新する", -155, 7, 270, 44, SubmitRanking);
        declineButton = CreateButton(rankingPanel.transform, "DeclineButton", "更新しない", 155, 7, 270, 44, DeclineRanking);
        feedbackText = CreateText(rankingPanel.transform, "Feedback", "自己ベストを上回る場合のみ更新します。", 19, 0, -43, 740, 48);

        CreateText(rankingPanel.transform, "RankingHeading", "この端末のランキング  /  コイン優先・同点はラウンド順", 20, 0, -88, 740, 36);
        rankingText = CreateText(rankingPanel.transform, "RankingEntries", "", 18, 0, -157, 740, 105);
        rankingText.alignment = TextAlignmentOptions.TopLeft;
        rankingText.richText = false;
        CreateButton(rankingPanel.transform, "ReturnButton", "タイトルへ戻る", 0, -235, 250, 42, ReturnToTitle);

        if (string.IsNullOrWhiteSpace(rankingPlayerName))
        {
            submitButton.interactable = false;
            feedbackText.text = "プレイヤー名がありません。名前入力画面から開始してください。";
        }
    }

    private TMP_Text CreateText(Transform parent, string objectName, string value, int fontSize,
        float x, float y, float width, float height)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Place(textObject.GetComponent<RectTransform>(), x, y, width, height);

        var textComponent = textObject.GetComponent<TextMeshProUGUI>();
        textComponent.font = endingText.font;
        textComponent.text = value;
        textComponent.fontSize = fontSize;
        textComponent.color = Color.white;
        textComponent.alignment = TextAlignmentOptions.Center;
        textComponent.raycastTarget = false;
        return textComponent;
    }

    private Button CreateButton(Transform parent, string objectName, string label,
        float x, float y, float width, float height, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Place(buttonObject.GetComponent<RectTransform>(), x, y, width, height);
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.75f, 0.3f, 0.24f, 1f);
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateText(buttonObject.transform, "Label", label, 23, 0, 0, width, height);
        return button;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}
