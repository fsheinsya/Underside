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

    [Header("ポジション")]
    //ストーリーのボタンの座標を保存
    [SerializeField] private RectTransform StoryBtnPos;
    //ランキングのボタンの座標を保存
    [SerializeField] private RectTransform RankBtnPos;
    //テキストの座標を保存
    [SerializeField] private RectTransform explanTxtPos;

    //モードの初期値の設定
    private ModeType mode;

    //キャンセル管理のトークンを設定
    private CancellationTokenSource cts;

    //初期値の呼び出し
    void Awake()
    {
        cts = new CancellationTokenSource();

        StoryBtnPos.anchoredPosition = new Vector2(-570f,50f);
        RankBtnPos.anchoredPosition = new Vector2(-570f,-100f);
        explanTxtPos.anchoredPosition = new Vector2(145f,60f);
        ReturnButton.alpha = 0f;

        SelectUIAnim(cts.Token).Forget();
    }

    /// <summary>
    /// ボタン関係
    /// </summary>
    
    //初期
    public void AwakeExplantion()
    {
        explan.text = $"";
    }

    //ストーリーボタンに触れる
    public void StoryExplantion()
    {
        explan.text = $"あなたは通りすがりの村人だ！\n果たして裏世界の王になれるのか,,,";
    }

    //ランキングボタンに触れる
    public void RankingExplantion()
    {
        explan.text = $"世界中の賭博者と競い合おう！\n負けるまで終わらない激熱マッチ！！！";
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

    //ストーリーモード
    public void SelectStory()
    {
        //ストーリーモードの設定に変更する
        mode = ModeType.story;
        Debug.Log(mode);
        //賭けるシーンに移行
        MoveBetScene();
    }

    //ランキングモード
    public void SelectRanking()
    {
        //ランキングモードの設定に変更する
        mode = ModeType.ranking;
        Debug.Log(mode);
        //賭けるシーンに移行
        MoveBetScene();
    }


    //セレクトシーンのアニメーションの管理
    public async UniTask SelectUIAnim(CancellationToken token)
    {
        try
        {

            //ストーリーモード出現
            await StoryBtnPos.DOAnchorPos(new Vector2(-190f,50f),1.0f).AsyncWaitForCompletion();
            //ランキングモード出現
            await RankBtnPos.DOAnchorPos(new Vector2(-190f,-100f), 1.0f).AsyncWaitForCompletion();
            //タイトルに戻るはフェードイン
            await ReturnButton.DOFade(1.0f, 1.0f).AsyncWaitForCompletion();
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
