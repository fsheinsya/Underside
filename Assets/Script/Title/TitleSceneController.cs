using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Threading;


public class TitleSceneController : MonoBehaviour
{
    //titleのテキストを表示させる
    [Header("テキスト")]
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI click;

    //あらすじ説明のボタンを設定をする
    [Header("あらすじ")]
    [SerializeField] private CanvasGroup opButton;

    //SEを設定する
    [Header("SE")]
    [SerializeField] private AudioClip s;

    //1秒の待機時間を用意する
    float waitTime = 1f;

    // 連打防止をするためにフラグを用意をする
    private bool isTransitioning = false;

    // キャンセル管理
    private CancellationTokenSource cts;

    //初期値の設定
    void Awake()
    {
        cts = new CancellationTokenSource();

        title.alpha = 0f;
        click.alpha = 0f;
        opButton.alpha = 0f;

        TitleUIAnim(cts.Token).Forget();
    }

    //オブジェクトの破棄
    void OnDestroy()
    {
        // シーン破棄時に全部止める
        cts.Cancel();
        cts.Dispose();

        DOTween.KillAll(); // Tween強制停止（重要）
    }

    void Update()
    {
        if (isTransitioning) return;

        //画面をクリックをしたら賭けるシーンに移動をする
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            //Openingシーンに移行するためにUI上のクリックを反映させない
            if (EventSystem.current.IsPointerOverGameObject()) return;

            OnClickStartGame().Forget();
        }
    }

    //あらすじをクリックした時の処理
    public void OnClick()
    {
        //Openingシーンに移動
        SceneManager.LoadScene(SceneNames.Opening);
    }

    //画面をクリックをした時の処理
    public async UniTask OnClickStartGame()
    {
        //画面遷移のフラグをtrueにする
        isTransitioning = true;

        //  クリックをしたらunity上で設定をしたSEを再生をする
        if (s != null)
        { 
            AudioSource.PlayClipAtPoint(s, Camera.main.transform.position);
        }

        // 点滅停止
        click.DOKill();

        // タイトルとテキストをそれぞれフェードアウト
        await title.DOFade(0f, 0.5f).AsyncWaitForCompletion();
        await click.DOFade(0f, 0.5f).AsyncWaitForCompletion();
        await opButton.DOFade(0f,0.5f).AsyncWaitForCompletion();

        // GameSessionがあるとき
        if (GameSession.Instance != null)
        {
            //ゲームセッションを始める
            GameSession.Instance.StartNewGame();
        }
        else
        {
            Debug.LogWarning("GameSessionが存在しません");
        }

        //Betシーンに移動
        SceneManager.LoadScene(SceneNames.Bet);
    }


    //タイトルシーンのアニメーションの管理
    public async UniTask TitleUIAnim(CancellationToken token)
    {
        try
        {
            // タイトルフェードイン(1秒で完全に表示させる)
            await title.DOFade(1f, 1f).AsyncWaitForCompletion();
            //あらすじボタンをフェードイン(1秒で完全に表示させる)
            await opButton.DOFade(1f,1f).AsyncWaitForCompletion();

            //1秒間待機をさせる処理
            await UniTask.Delay(TimeSpan.FromSeconds(waitTime), cancellationToken: token);

            // クリック点滅開始
            click.alpha = 1f;

            //0.3fをループさせている(Yoyoはヨーヨーのようにアニメーションを繰り返す)
            click.DOFade(0.3f, 0.8f).SetLoops(-1, LoopType.Yoyo);
        }
        catch (OperationCanceledException)
        {
            // シーン遷移時にここに来る（正常）
        }
    }
}