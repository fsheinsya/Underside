using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// プレイ全体の最終成績を表示し、ランキングモードでは記録更新の確認を行う。
/// 名前はReserveからGameSession経由で受け取り、この画面に入った時点の成績と一緒に保持する。
/// 「更新する」でRankingService.Submitを呼び、「更新しない」やタイトルへ戻る操作では保存しない。
/// 自己ベストの比較・保存はServiceに任せ、この画面は演出・選択受付・上位5人の表示を担当する。
/// </summary>

public class AllResultSceneController : MonoBehaviour
{
    [Header("最終結果表示UI")]
    //最終表示コイン
    [SerializeField] private TMP_Text finalCoinText;
    //エンディング文
    [SerializeField] private TMP_Text endingText;
    //タップして案内に戻るの案内文を表示する
    [SerializeField] private TMP_Text announceText;
　　

    //アニメーションの設定
    private bool isAnimating = false;
    private ModeType currentMode;
    private int finalCoin;
    private int finalRound;
    private RankingResultPanel rankingPanel;
    private string rankingPlayerName;
    private bool hasRankingDecision;
    private bool isKingdomRaidEnding;

    private async void Awake()
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
        isKingdomRaidEnding = currentMode == ModeType.story
            && GameSession.Instance.CurrentMatchResult != null
            && GameSession.Instance.CurrentMatchResult.IsKingdomRaidEnding;
        finalCoin = isKingdomRaidEnding ? 0 : GameSession.Instance.CurrentCoin;
        finalRound = GameSession.Instance.CurrentRound;
        rankingPlayerName = GameSession.Instance.RankingPlayerName;

        if (isKingdomRaidEnding)
        {
            // 長い隠しエンドの文章を、既存の説明枠の中に折り返して表示する。
            Canvas canvas = endingText.GetComponentInParent<Canvas>();
            endingText.transform.SetParent(canvas.transform, false);
            Place(endingText.rectTransform, 0, -76, 660, 180);
            endingText.enableAutoSizing = true;
            endingText.fontSizeMin = 18;
            endingText.fontSizeMax = 24;
            endingText.alignment = TextAlignmentOptions.Center;
        }

        //テキストは最初は0に設定する
        endingText.alpha = 0;
        announceText.alpha = 0;

        //ランキングモードの場合UIを表示させる
        if (currentMode == ModeType.ranking)
        {
            Debug.Log($"[Ranking/Result] 名前「{rankingPlayerName}」: 所持コイン={finalCoin}, 到達ラウンド={finalRound}。更新の選択を待ちます。", this);
            CreateRankingPanel();
        }

        // 演出開始
        await PlayResultAnimation();

        if (rankingPanel != null)
        {
            rankingPanel.Show();
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
    /// アニメーションの演出
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

        float coinProgress = 0f;

        await DOTween.To(() => coinProgress, x =>
        {
            coinProgress = x;
            // int上限付近もfloatへの丸めで負数にならないよう、doubleで補間する。
            int displayCoin = (int)System.Math.Min(coin, System.Math.Round(coin * (double)Mathf.Clamp01(x)));
            finalCoinText.text = displayCoin.ToString();
        }, 1f, 1.5f) // ←時間調整OK
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
            // 摘発はコイン0の通常エンドより優先する。
            if (isKingdomRaidEnding)
                ending = "摘発エンド(SECRET END)\n長引く勝負の最中、王国の兵士が闘技場に踏み込んだ。\n違法賭博に参加していたあなたは捕らえられ、コインもすべて没収。\n一攫千金の夢は、牢屋の中で幕を閉じた……。\n「てかよくそんなとこまでいったね　by製作者」";
            //コインが0以下のとき債務者
            else if (coin <= 0)
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
        // タイピング中の文字が見えるよう、空にしてから透明度を戻す。
        endingText.alpha = 1f;

        //一文字ずつタイピングアニメーション形式で出力する
        foreach (char c in ending)
        {
            endingText.text += c;
            await UniTask.Delay(30); // ←30ミリ秒毎に一文字ずつアニメーション
        }

        //-------------------------
        // ④ 操作案内のフェードイン
        //-------------------------
       
        //0.5秒かけてクリックして画面に戻るように指示をする
        await announceText.DOFade(1f, 0.5f)
            .AsyncWaitForCompletion();

        //表示させたらフラグをfalseにする
        isAnimating = false;
    }

    /// <summary>
    /// 「更新する」ボタンの処理。最終成績を提出し、正常に受け付けられたら選択を確定する。
    /// Submitがtrueでも自己ベスト更新とは限らないため、Serviceが返した説明を表示する。
    /// </summary>
    private void SubmitRanking()
    {
        if (hasRankingDecision || isAnimating) return;

        // 名前が引き継がれていない場合は、別の名前を推測して保存しない。
        if (string.IsNullOrWhiteSpace(rankingPlayerName))
        {
            rankingPanel.SetFeedback("プレイヤー名がありません。名前入力画面から開始してください。");
            Debug.LogWarning("[Ranking/Result] プレイヤー名がないため更新できません。", this);
            return;
        }

        Debug.Log($"[Ranking/Result] 名前「{rankingPlayerName}」の更新が許可されました。coin={finalCoin}, round={finalRound}", this);

        // intからulongへ変換する前に、不正な負数や0を除外する。
        if (finalCoin < 0 || finalRound < 1)
        {
            rankingPanel.SetFeedback("コイン数またはラウンド数が不正です。");
            return;
        }

        try
        {
            // 「更新する」を押したときだけ、入力画面で選んだ名前の記録を更新する。
            if (!RankingService.Submit(rankingPlayerName, finalCoin, (ulong)finalRound, out string message))
            {
                rankingPanel.SetFeedback(message);
                return;
            }

            CompleteRankingDecision();
            rankingPanel.SetFeedback(message);
        }
        catch (System.Exception exception)
        {
            // 保存に失敗した場合は決定済みにせず、再試行または更新の辞退を可能にする。
            Debug.LogWarning($"[Ranking/Result] ランキングの保存に失敗しました: {exception}", this);
            rankingPanel.SetFeedback("記録を保存できませんでした。もう一度お試しください。");
            return;
        }

        RefreshRanking();
    }

    private void DeclineRanking()
    {
        if (hasRankingDecision || isAnimating) return;

        // 辞退した場合は保存処理を呼ばず、以前の記録を残す。
        CompleteRankingDecision();
        rankingPanel.SetFeedback("今回の記録は保存しませんでした。");
        Debug.Log($"[Ranking/Result] 名前「{rankingPlayerName}」の更新が辞退されました。記録は変更しません。", this);
    }

    private void CompleteRankingDecision()
    {
        // 一度決定した結果を、連打や別のボタンから再度処理しない。
        hasRankingDecision = true;
        rankingPanel.CompleteDecision();
    }

    private void ReturnToTitle()
    {
        if (isAnimating) return;

        // 確認前に戻る場合も、スコアを自動保存しない。
        if (!hasRankingDecision) DeclineRanking();
        SceneManager.LoadScene(SceneNames.Title);
    }
    // 読み込みはここで行い、上位5人の文字列生成はViewに任せる。
    private void RefreshRanking()
    {
        try
        {
            rankingPanel.ShowEntries(RankingService.GetRanking());
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning($"ランキングの読み込みに失敗しました: {exception}");
            rankingPanel.ShowLoadError();
        }
    }

    private void CreateRankingPanel()
    {
        Canvas canvas = endingText.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("AllResult シーンに Canvas がありません。ランキング更新の確認を表示できません。");
            return;
        }
        rankingPanel = new RankingResultPanel(canvas, endingText.font, rankingPlayerName, finalCoin, finalRound,
            SubmitRanking, DeclineRanking, ReturnToTitle);
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
