using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using Cysharp.Threading.Tasks;

/// <summary>
/// Resultシーン全体を管理するクラス
/// 主な役割:
/// ・勝者名の表示
/// ・勝者画像の表示
/// ・現在コイン数の表示
/// ・現在ラウンド数の表示
/// ・勝敗演出
/// ・クリックで次ラウンド or 最終結果へ遷移
/// </summary>
public class ResultSceneController : MonoBehaviour
{
    [Header("結果表示UI")]
    [SerializeField] private TMP_Text winnerNameText;
    [SerializeField] private Image winnerImage;
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text boolingWinnerText;


    [Header("SE")]
    [SerializeField] private AudioSource audio;
    [SerializeField] private AudioClip WinSound;
    [SerializeField] private AudioClip LoseSound;
    [SerializeField] private AudioClip CoinSound;

    // 連打で複数回遷移しないようにするフラグ
    private bool isTransitioning = false;

    //モード別でラウンド数の終了の処理を帰る
    private ModeType mode;

    //勝敗予想結果のデータを取得する
    MatchResultData result = GameSession.Instance.CurrentMatchResult;

    /// <summary>
    /// シーン開始時に呼ばれる
    /// セッション情報と試合結果をチェックしてUIを反映する
    /// </summary>
    private void Start()
    {
        // GameSession または試合結果が無いなら Title に戻す
        if (GameSession.Instance == null ||
            GameSession.Instance.CurrentMatchResult == null)
        {
            SceneManager.LoadScene(SceneNames.Title);
            return;
        }

        // 結果画面の基本表示をセット
        SetupResultView();

        // 勝敗演出を開始
        PlayResultAnimation().Forget();
    }

    /// <summary>
    /// 毎フレーム呼ばれる
    /// クリックで次へ進む
    /// </summary>
    private void Update()
    {
        // すでに遷移中なら無視
        if (isTransitioning) return;

        // 左クリックまたは右クリックで次へ
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            GoNext();
        }
    }

    /// <summary>
    /// 勝者名・勝者画像・コイン・ラウンド数を表示する
    /// </summary>
    private void SetupResultView()
    {
        MatchResultData result = GameSession.Instance.CurrentMatchResult;

        // ラウンド表示
        roundText.text = $"round {GameSession.Instance.CurrentRound} / {GameSession.Instance.MaxRound}";

        // コイン表示
        // ここでは現在のセッション値をそのまま表示する
        coinText.text = $"Coin\n{GameSession.Instance.CurrentCoin}";

        // 勝者モンスター取得
        MonsterData winnerMonster = GetWinnerMonster(result.WinnerSide);

        // 勝者名表示
        winnerNameText.text = winnerMonster.Name;

        // 勝者画像表示
        if (winnerImage != null)
        {
            winnerImage.sprite = winnerMonster.Icon;
            winnerImage.color = Color.white;
            winnerImage.preserveAspect = true;
        }
    }

    /// <summary>
    /// 勝者が左右どちらかに応じて該当モンスターを返す
    /// </summary>
    private MonsterData GetWinnerMonster(PredictionSide winnerSide)
    {
        if (winnerSide == PredictionSide.Left)
        {
            return GameSession.Instance.LeftMonster;
        }

        return GameSession.Instance.RightMonster;
    }

    /// <summary>
    /// 勝敗に応じた結果演出を流す
    /// </summary>
    private async UniTask PlayResultAnimation()
    {

        // プレイヤーの予想が当たったかどうか
        bool isWin = IsPlayerWin(result);

        // ---------------------------
        // 初期状態リセット
        // ---------------------------
        if (winnerImage != null)
        {
            winnerImage.color = new Color(1f, 1f, 1f, 0f);
        }

        if (winnerNameText != null)
        {
            winnerNameText.rectTransform.anchoredPosition = new Vector2(-700f, 0f);
        }

        if (boolingWinnerText != null)
        {
            boolingWinnerText.transform.localScale = Vector3.zero;
        }

        // ---------------------------
        // ① キャラ画像フェードイン
        // ---------------------------
        if (winnerImage != null)
        {
            await winnerImage.DOFade(1f, 0.5f).AsyncWaitForCompletion();
        }

        // ---------------------------
        // ② 名前スライドイン
        // ---------------------------
        if (winnerNameText != null)
        {
            await winnerNameText.rectTransform
                .DOAnchorPosX(-250f, 0.5f)
                .SetEase(Ease.OutBack)
                .AsyncWaitForCompletion();
        }

        // ---------------------------
        // ③ 勝敗テキスト演出
        // ---------------------------
        if (boolingWinnerText != null)
        {
            if (isWin)
            {
                boolingWinnerText.text = "予想成功！";

                // 一度大きくしてから戻す
                boolingWinnerText.transform.localScale = Vector3.zero;

                await boolingWinnerText.transform
                    .DOScale(1.5f, 0.3f)
                    .SetEase(Ease.OutBack)
                    .AsyncWaitForCompletion();

                await boolingWinnerText.transform
                    .DOScale(1f, 0.2f)
                    .SetEase(Ease.InOutQuad)
                    .AsyncWaitForCompletion();

                audio.PlayOneShot(WinSound);
                
            }
            else
            {
                boolingWinnerText.text = "予想失敗…";
                boolingWinnerText.transform.localScale = Vector3.one * 0.5f;

                await boolingWinnerText.transform
                    .DOScale(1f, 0.8f)
                    .SetEase(Ease.OutQuad)
                    .AsyncWaitForCompletion();

                audio.PlayOneShot(LoseSound);
            }
        }

        // ---------------------------
        // ④ コイン表示演出
        // ---------------------------
        await AnimateCoin(result, isWin);
        audio.PlayOneShot(CoinSound);
    }

    /// <summary>
    /// コイン表示だけを演出する
    /// 実際のセッション値はここでは変更しない
    /// </summary>
    private async UniTask AnimateCoin(MatchResultData result, bool isWin)
    {
        // BattleScene 側で CurrentCoin が最終値になっている前提
        int targetCoin = GameSession.Instance.CurrentCoin;

        // 演出開始用の見た目上の初期値を計算する
        int startCoin;

        if (isWin)
        {
            // 的中していた場合は、現在値から報酬分を引いた値を開始値とする
            startCoin = targetCoin - result.RewardCoin;
        }
        else
        {
            // 外していた場合は、賭け金を失った後の値が currentCoin のはずなので
            // 見た目上は「賭ける前」から減ったように見せる
            int lostAmount = 0;

            if (GameSession.Instance.CurrentBet != null)
            {
                lostAmount = GameSession.Instance.CurrentBet.Amount;
            }

            startCoin = targetCoin + lostAmount;
        }

        // 不自然なマイナス防止
        startCoin = Mathf.Max(0, startCoin);

        // 一旦開始値を表示
        coinText.text = $"Coin\n{startCoin}";

        // 少しガチャガチャした演出
        float shuffleDuration = 0.6f;
        float timer = 0f;

        while (timer < shuffleDuration)
        {
            timer += Time.deltaTime;

            int min = Mathf.Min(startCoin, targetCoin);
            int max = Mathf.Max(startCoin, targetCoin);

            int fake = Random.Range(min, max + 1);
            coinText.text = $"Coin\n{fake}";

            await UniTask.Yield();
        }

        // 最終値へ滑らかに寄せる
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2f;

            int value = Mathf.RoundToInt(Mathf.Lerp(startCoin, targetCoin, t));
            coinText.text = $"Coin\n{value}";

            await UniTask.Yield();
        }

        // 最終値で確定
        coinText.text = $"Coin\n{targetCoin}";
    }

    /// <summary>
    /// プレイヤーの予想が当たったかどうかを判定する
    /// 現在の賭け情報と勝者サイドを比較する
    /// </summary>
    private bool IsPlayerWin(MatchResultData result)
    {
        // 賭け情報が無ければ失敗扱い
        if (GameSession.Instance == null || GameSession.Instance.CurrentBet == null)
        {
            return false;
        }

        // 予想した側と勝者側が一致していれば成功
        return GameSession.Instance.CurrentBet.Side == result.WinnerSide;
    }

    /// <summary>
    /// 次の画面へ進む
    /// 最終ラウンドなら AllResult、それ以外なら次の Bet に進む
    /// </summary>
    private void GoNext()
    {
        // 多重遷移防止
        isTransitioning = true;

        // 最終ラウンドなら最終結果へ
        if(mode == ModeType.story)
        {
            //ストーリーモード
            if (GameSession.Instance.IsLastRound() || GameSession.Instance.CurrentCoin <= 0)
            {
                SceneManager.LoadScene(SceneNames.AllResult);
                return;
            }
        }
        else if(mode == ModeType.ranking)
        {
             //ランキングモード
             //自分のスライムが負けると結果画面に戻る
             if(!IsPlayerWin(result))
            {
                SceneManager.LoadScene(SceneNames.AllResult);
                return;
            }
        }
       

        // 次ラウンドへ進めて次のBetへ
        GameSession.Instance.NextRound();
        SceneManager.LoadScene(SceneNames.Bet);
    }
}