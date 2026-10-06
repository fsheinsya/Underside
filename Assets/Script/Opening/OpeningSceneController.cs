using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using TMPro;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using System.Threading;

//ActionLogViewの再開発

public class OpeningSceneController : MonoBehaviour
{
    //テキストの設定
    [Header("テキスト")]
    //あらすじ
    [SerializeField] private TextMeshProUGUI outline;
    //あらすじの概要
    [SerializeField] private TextMeshProUGUI explan;

    //キャンバスグルーブを格納する
    [Header("キャンバスグループ")]
    //タイトルに戻るボタン
    [SerializeField] private CanvasGroup BackTitleButton;
    //ウィンドウログ
    [SerializeField] private CanvasGroup proceedLog;

    [Header("1文字の表示間隔")]
    [SerializeField] private int perChar = 20;

    //表示テキストを設定する
    [SerializeField, TextArea(1, 3)]
    private string[] messages =
    {
        "ここはとある城下町の裏社会。",
        "とある村人Aが平和に過ごしていたが、",
        "ふと、あるチラシを見つける。",
        "そこでは裏でこっそり、",
        "スライム賭けがおこなわれているそうな……。(クリックで画面に戻る)"
    };

    // 連打防止をするためにフラグを用意をする
    private bool isTransitioning = false;

    // キャンセル管理のトークンを設定
    private CancellationTokenSource cts;

    //タイピングアニメーションの変数を用意する
    private Tween openingTween;

    //　初期値の呼び出し
    void Awake()
    {
        cts = new CancellationTokenSource();

        outline.alpha = 0f;
        explan.alpha = 1f;
        explan.text = "";
        proceedLog.alpha = 1f;

        OpeningUIAnim(cts.Token).Forget();
    }

    public void OnClick()
    {
        ReturnTitle();
    }

    //タイトルのシーンに戻る
    void ReturnTitle()
    {
        //連打の防止
        if (isTransitioning) return;

        //遷移中にする
        isTransitioning = true;

        //シーン移動
        SceneManager.LoadScene(SceneNames.Title);
    }

    //ログを空にする
    public void Clear()
    {
        explan.text = "";
    }

    //設定した文章を1行ずつ出力する
    public void AppendLine(string message)
    {
        //文章が空じゃなかったら改行する
        if(!string.IsNullOrEmpty(explan.text))
        {
            //改行
            explan.text += "\n";
        }
        explan.text += message;
    }

    //1文字ずつ出力する
    public async UniTask TypeAnim(string message,CancellationToken token)
    {
        // 前述の文章をなくす
        explan.text = message;
        explan.maxVisibleCharacters = 0;
        explan.ForceMeshUpdate();
        int count = explan.textInfo.characterCount;

        float elapsed = 0f;
        float interval = Mathf.Max(1, perChar) / 1000f;
        explan.maxVisibleCharacters = Mathf.Min(1, count);


        //1文字ずつコマ送りで出力する
        while (explan.maxVisibleCharacters < count)
        {
            // 開始に使ったクリックを拾わず、次のフレームから入力を見る
            await UniTask.NextFrame(cancellationToken: token);

            //クリックをしたら検知をする
            if (IsClicked())
            {
                explan.maxVisibleCharacters = count;
                return;
            }

            // 経過時間に合わせて文字を増やしつつ、クリックは毎フレーム確認する
            elapsed += Time.deltaTime;
            explan.maxVisibleCharacters = Mathf.Min(count,
                1 + Mathf.FloorToInt(elapsed / interval));
        }
    }

    //オープニングシーンのアニメーションの管理
    public async UniTask OpeningUIAnim(CancellationToken token)
    {
        try
        {
            if (EventSystem.current.IsPointerOverGameObject()) return;

            openingTween = outline.DOFade(1f, 1f).OnKill(() => openingTween = null);
            //最初にシーン遷移時フェードインで"あらすじ"を表示をさせる
            await openingTween.AsyncWaitForCompletion();

            //☆クリックをすると１行ずつ説明が進む
            //ここはとある城下町の裏社会
            //とある村人Aが平和に過ごしていたが
            //ふとあるチラシを見つける
            //そこでは裏でこっそり
            //スライム賭けがおこなわれているそうな、、、(クリックで画面に戻る)


            foreach(string message in messages)
            {
                await WaitForClick(token);
                await TypeAnim(message, token);
            }

            //一秒待機をした後画面をクリックをしたらタイトルに戻る
            await UniTask.Delay(TimeSpan.FromSeconds(1f), cancellationToken: token);
            //クリックでタイトルに戻る
            await WaitForClick(token);
            ReturnTitle();
        }
        catch (OperationCanceledException)
        {
            //シーンが終了したら処理を終わらせる
        }
    }

    //別フレームで誤ったクリックをしない
    private async UniTask WaitForClick(CancellationToken token)
    {
        await UniTask.NextFrame(cancellationToken: token);
        await UniTask.WaitUntil(IsClicked, cancellationToken: token);
    }

    //クリックを検知したときの処理
    private static bool IsClicked()
    {
        return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
    }

    //シーンの破棄
    void OnDestroy()
    {
        // シーン破棄時に全部止める
        cts.Cancel();

        openingTween?.Kill(); // openingTweenのアニメーションの動きを止める

        cts.Dispose();
    }
}
