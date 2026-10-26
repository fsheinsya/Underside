using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;

public class SelectSceneController: MonoBehaviour
{

    [Header("テキスト")]
    //テキスト
    [SerializeField] private TextMeshProUGUI explan;

    //ボタン
    [Header("ボタン")]
    //ストーリーのボタン
    [SerializeField] private CanvasGroup StoryButton;
    //ランキングのボタンの設定
    [SerializeField] private CanvasGroup RankButton;
    //タイトルに戻るボタン
    [SerializeField] private CanvasGroup ReturnButton;
    //ランキングへ遷移をするボタン
    [SerializeField] private CanvasGroup RankViewButton;

    [Header("ポジション")]
    //ストーリーのボタンの座標を保存
    [SerializeField] private RectTransform StoryBtnPos;
    //ランキングのボタンの座標を保存
    [SerializeField] private RectTransform RankBtnPos;
    //テキストの座標を保存
    [SerializeField] private RectTransform explanTxtPos;
    //ランキング表示画面への遷移
    [SerializeField] private RectTransform ViewBtnPos;

    //モードの初期値の設定
    private ModeType mode;

    //キャンセル管理のトークンを設定
    private CancellationTokenSource cts;

    //初期値の呼び出し
    void Awake()
    {
        cts = new CancellationTokenSource();

        AwakeExplantion();

        StoryBtnPos.anchoredPosition = new Vector2(-570f,50f);
        RankBtnPos.anchoredPosition = new Vector2(-570f,-80f);
        explanTxtPos.anchoredPosition = new Vector2(145f,60f);
        ViewBtnPos.anchoredPosition = new Vector2(-190f, -190f);

        ReturnButton.alpha = 0f;
        RankViewButton.alpha = 0f;

        SelectUIAnim(cts.Token).Forget();
    }

    /// <summary>
    /// ボタン関係
    /// </summary>
    
    //初期
    public void AwakeExplantion()
    {
        explan.text = $"モードを選択しよう！";
    }

    //ストーリーボタンに触れる
    public void StoryExplantion()
    {
        explan.text = $"3回マッチして\n裏社会の王を目指そう！";
    }

    //ランキングボタンに触れる
    public void RankingExplantion()
    {
        explan.text = $"負けるまで終わらない激熱マッチ！！！\n他の賭博者と競い合おう！";
    }


    //titleに戻る
    public void ReturnTitleButton()
    {
        SceneManager.LoadScene(SceneNames.Title);
        return;
    }

    //Betに移行する
    void MoveBetScene()
    {
        SceneManager.LoadScene(SceneNames.Bet);
        return;
    }

    //Reserveに移行する
    void MoveReserveScene()
    {
        SceneManager.LoadScene(SceneNames.Reserve);
        return;
    }

    //ストーリーモードを選択を場合
    public void SelectStory()
    {
        //セッションからストーリーモードを選択する
        GameSession.Instance.mode = ModeType.story;
        //ゲームをスタートをする
        GameSession.Instance.StartNewGame();
        //Betシーンに移行
        MoveBetScene();
    }

    //ランキングモードの選択をした場合
    public void SelectRanking()
    {
        //セッションからランキングモードを選択する
        GameSession.Instance.mode = ModeType.ranking;
        //ゲームをスタートをする
        GameSession.Instance.StartNewGame();
        //Reserveシーンに移行
        MoveReserveScene();
    }

    public void SelectRankingView()
    {
        SceneManager.LoadScene(SceneNames.Ranking);
    }

    //セレクトシーンのアニメーションの管理
    public async UniTask SelectUIAnim(CancellationToken token)
    {
        try
        {

            //ストーリーモードのボタンをスライドで出現
            await StoryBtnPos.DOAnchorPos(new Vector2(-190f,50f),1.0f).AsyncWaitForCompletion();
            //ランキングモードのボタンをスライドで出現
            await RankBtnPos.DOAnchorPos(new Vector2(-190f,-100f), 1.0f).AsyncWaitForCompletion();
            //タイトルに戻るとランキング表示画面はフェードイン
            await UniTask.WhenAll(
            //タイトルに戻るボタン
             ReturnButton.DOFade(1f, 1f).ToUniTask(
                    tweenCancelBehaviour: TweenCancelBehaviour.KillAndCancelAwait,
                    cancellationToken: token),
             //ランキングを見るボタン
             RankViewButton.DOFade(1f, 1f).ToUniTask(
                    tweenCancelBehaviour: TweenCancelBehaviour.KillAndCancelAwait,
                    cancellationToken: token)
             );
            //画面をクリックでアニメーションスキップ

        }
        catch (OperationCanceledException)
        {
            //シーンが終了したら処理を終わらせる
        }
    }

    //シーンを破棄をするときオブジェクトも破棄する
    void OnDestroy()
    {
        cts.Cancel();
        cts.Dispose();

    }
}
