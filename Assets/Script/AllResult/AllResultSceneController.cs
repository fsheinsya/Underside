using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using DG.Tweening;

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

    //モード設定
    private ModeType mode;

    private async void Start()
    {
        // ゲームセッションが空だったら
        if (GameSession.Instance == null)
        {
            //タイトル画面に戻す機能
            SceneManager.LoadScene(SceneNames.Title);
            return;
        }

        // 演出開始
        await PlayResultAnimation();
    }

    private void Update()
    {
        // アニメ中はクリック無効
        if (isAnimating) return;

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

        int coin = GameSession.Instance.CurrentCoin;

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
            ending = $"コイン取得数:{coin}";
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
}