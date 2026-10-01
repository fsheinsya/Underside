using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Threading;

public class BetSceneController : MonoBehaviour
{
    [Header("上部表示")]
    [SerializeField] private TMP_Text coinText;
    [SerializeField] private CanvasGroup coinCanvas;

    //左側のキャラクターのUI表示
    [Header("左側UI")]
    [SerializeField] private TMP_Text leftCharaNameText;
    [SerializeField] private Image leftCharaImage;
    [SerializeField] private CanvasGroup leftCharaCanvas;
    [SerializeField] private Button leftStatusButton;
    [SerializeField] private Button leftSelectButton;

    //右側のキャラクターのUI表示
    [Header("右側UI")]
    [SerializeField] private TMP_Text rightCharaNameText;
    [SerializeField] private Image rightCharaImage;
    [SerializeField] private CanvasGroup rightCharaCanvas;
    [SerializeField] private Button rightStatusButton;
    [SerializeField] private Button rightSelectButton;

    //UIのルート表示
    [Header("UI Root")]
    [SerializeField] private GameObject selectedUIRoot;
    [SerializeField] private GameObject betAmountRoot;

    //
    [Header("賭け金")]
    [SerializeField] private TMP_Text betAmountText;

    [Header("詳細画面")]
    [SerializeField] private LeftStatusDetailView leftStatusView;
    [SerializeField] private RightStatusDetailView rightStatusView;

    [Header("SE")]
    [SerializeField] private AudioSource audio;
    [SerializeField] private AudioClip betSE;     // カチカチ音
    [SerializeField] private AudioClip selectSE;  // 決定音

    private bool isOpeningStatus = false;

    private PredictionSide selectedSide;
    private bool hasSelectedSide = false;

    // =========================
    // 🔥 賭け金
    // =========================

    //初期値に10を設定する
    private int currentBetAmount = 10;

    //1を最小値に設定をする
    private const int MIN_BET = 1;

    //1ずつカウントを進める
    private const int BET_STEP = 1;

    // =========================
    // 🔥 長押し
    // =========================

    //
    private bool isPressing = false;
    private bool isIncrease = true;
    private CancellationTokenSource cts;

    // =========================
    // 🔊 SE再生
    // =========================
    private void PlaySE(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
        {
            // 少しピッチランダムで気持ちよく
            AudioSource.PlayClipAtPoint(
                clip,
                Camera.main.transform.position,
                Random.Range(0.95f, 1.05f)
            );
        }
    }

    private void Start()
    {
        if (GameSession.Instance == null)
        {
            SceneManager.LoadScene(SceneNames.Title);
            return;
        }

        RefreshCoinText();
        SetupMonsterViews();
        RefreshBetText();

        leftStatusView?.Clear();
        leftStatusView?.Close();
        rightStatusView?.Clear();
        rightStatusView?.Close();

        OpenSelectedUI();
        OpenBetAmountUI();

        if (leftCharaCanvas != null) leftCharaCanvas.alpha = 0f;
        if (rightCharaCanvas != null) rightCharaCanvas.alpha = 0f;

        PlayIntro().Forget();
    }

    // =========================
    // 🎬 開幕演出
    // =========================
    private async UniTask PlayIntro()
    {
        //最初に透明に設定している
        coinCanvas.alpha = 0f;

        //コインキャンパスを0.5秒かけて表示させる
        await coinCanvas.DOFade(1f, 0.5f).AsyncWaitForCompletion();

        //300ミリ秒待機させる
        await UniTask.Delay(300);

        //キャンパスを不透明にする
        leftCharaCanvas.alpha = 1f;
        rightCharaCanvas.alpha = 1f;

        //キャンパスの位置情報を取得する
        RectTransform l = leftCharaImage.rectTransform;
        RectTransform r = rightCharaImage.rectTransform;

        //それぞれのX座標を取得する
        float lx = l.anchoredPosition.x;
        float rx = r.anchoredPosition.x;

        //プレイヤーの位置情報を800に固定する
        float offset = 800f;

        //Vector2(x,y)としてイラストのUIを固定をする
        l.anchoredPosition = new Vector2(-offset, l.anchoredPosition.y);
        r.anchoredPosition = new Vector2(offset, r.anchoredPosition.y);

        //
        await UniTask.WhenAll(
            l.DOAnchorPosX(lx + 80, 0.4f).SetEase(Ease.OutBack).AsyncWaitForCompletion().AsUniTask(),
            r.DOAnchorPosX(rx - 80, 0.4f).SetEase(Ease.OutBack).AsyncWaitForCompletion().AsUniTask()
        );

        //
        await UniTask.WhenAll(
            l.DOAnchorPosX(lx, 0.2f).AsyncWaitForCompletion().AsUniTask(),
            r.DOAnchorPosX(rx, 0.2f).AsyncWaitForCompletion().AsUniTask()
        );
    }

    // =========================
    // 🔥 賭け金操作（SEあり）
    // =========================
    public void OnClickBetUp()
    {
        audio.PlayOneShot(betSE);

        int max = GameSession.Instance.CurrentCoin;

        currentBetAmount += BET_STEP;
        if (currentBetAmount > max)
            currentBetAmount = max;

        RefreshBetText();
    }

    public void OnClickBetDown()
    {
        audio.PlayOneShot(betSE);

        currentBetAmount -= BET_STEP;
        if (currentBetAmount < MIN_BET)
            currentBetAmount = MIN_BET;

        RefreshBetText();
    }

    private void RefreshBetText()
    {
        betAmountText.text = currentBetAmount.ToString();
    }

    // =========================
    // 🔥 長押し（SEなし）
    // =========================
    private void Execute()
    {
        if (isIncrease)
            Increase_NoSE();
        else
            Decrease_NoSE();
    }

    private void Increase_NoSE()
    {
        int max = GameSession.Instance.CurrentCoin;

        currentBetAmount += BET_STEP;
        if (currentBetAmount > max)
            currentBetAmount = max;

        RefreshBetText();
    }

    private void Decrease_NoSE()
    {
        currentBetAmount -= BET_STEP;
        if (currentBetAmount < MIN_BET)
            currentBetAmount = MIN_BET;

        RefreshBetText();
    }

    public void StartIncrease()
    {
        isIncrease = true;
        StartLongPress();
    }

    public void StartDecrease()
    {
        isIncrease = false;
        StartLongPress();
    }

    public void StopLongPress()
    {
        isPressing = false;
        cts?.Cancel();
    }

    private void StartLongPress()
    {
        isPressing = true;

        cts?.Cancel();
        cts = new CancellationTokenSource();

        HandleLongPress(cts.Token).Forget();
    }

    private async UniTaskVoid HandleLongPress(CancellationToken token)
    {
        Execute();

        await UniTask.Delay(300, cancellationToken: token);

        float interval = 0.2f;

        while (isPressing && !token.IsCancellationRequested)
        {
            Execute();

            interval = Mathf.Max(0.05f, interval - 0.02f);

            await UniTask.Delay((int)(interval * 1000), cancellationToken: token);
        }
    }

    // =========================
    // ステータス
    // =========================
    public void OnClickLeftStatus() => OpenLeftStatusAsync().Forget();
    public void OnClickRightStatus() => OpenRightStatusAsync().Forget();

    private async UniTask OpenLeftStatusAsync()
    {
        if (isOpeningStatus) return;
        isOpeningStatus = true;

        coinText.gameObject.SetActive(false);
        CloseSelectedUI();
        CloseBetAmountUI();

        rightStatusView?.Close();

        float odds = BetOddsCalculator.CalculatePayoutMultiplier(
            GameSession.Instance.LeftMonster,
            GameSession.Instance.RightMonster,
            PredictionSide.Left
        );

        await leftStatusView.PlayOpenAnimation(GameSession.Instance.LeftMonster, odds);

        isOpeningStatus = false;
    }

    private async UniTask OpenRightStatusAsync()
    {
        if (isOpeningStatus) return;
        isOpeningStatus = true;

        coinText.gameObject.SetActive(false);
        CloseSelectedUI();
        CloseBetAmountUI();

        leftStatusView?.Close();

        float odds = BetOddsCalculator.CalculatePayoutMultiplier(
            GameSession.Instance.LeftMonster,
            GameSession.Instance.RightMonster,
            PredictionSide.Right
        );

        await rightStatusView.PlayOpenAnimation(GameSession.Instance.RightMonster, odds);

        isOpeningStatus = false;
    }

    public void OnClickBackMenu()
    {
        leftStatusView?.Close();
        rightStatusView?.Close();

        coinText.gameObject.SetActive(true);

        OpenSelectedUI();
        OpenBetAmountUI();
    }

    // =========================
    // UI
    // =========================
    private void OpenSelectedUI() => selectedUIRoot?.SetActive(true);
    private void CloseSelectedUI() => selectedUIRoot?.SetActive(false);

    private void OpenBetAmountUI() => betAmountRoot?.SetActive(true);
    private void CloseBetAmountUI() => betAmountRoot?.SetActive(false);

    //コインの情報を反映させる
    private void RefreshCoinText()
    {
        coinText.text = $"Coin: {GameSession.Instance.CurrentCoin}";
    }

    private void SetupMonsterViews()
    {
        //左モンスターの情報を保持している
        var left = GameSession.Instance.LeftMonster;

        //右モンスターの情報を保持している
        var right = GameSession.Instance.RightMonster;

        //左モンスターの名前の情報
        leftCharaNameText.text = left.Name;
        //左モンスターのアイコン
        leftCharaImage.sprite = left.Icon;

        //右モンスターの名前の情報
        rightCharaNameText.text = right.Name;
        //右モンスターのアイコンの情報
        rightCharaImage.sprite = right.Icon;
    }


    // =========================
    // 🎯 選択（SEあり）
    // =========================

    //左を選択をした場合
    public void OnClickLeftSelect()
    {
        //効果音を再生をする
        PlaySE(selectSE);

        //選択したデータを反映をする
        selectedSide = PredictionSide.Left;
        hasSelectedSide = true;

        //
        UpdateSelectButtonColors();
        SaveBetData();

        SceneManager.LoadScene(SceneNames.Battle);
    }

    //右を選択をした場合
    public void OnClickRightSelect()
    {
        PlaySE(selectSE);

        selectedSide = PredictionSide.Right;
        hasSelectedSide = true;

        UpdateSelectButtonColors();
        SaveBetData();

        SceneManager.LoadScene(SceneNames.Battle);
    }

    //アニメーションの更新
    private void UpdateSelectButtonColors()
    {
        if (leftSelectButton == null || rightSelectButton == null) return;

        Color normalColor = Color.white;
        Color selectedColor = new Color(1f, 0.9f, 0.4f);

        Image leftButtonImage = leftSelectButton.GetComponent<Image>();
        Image rightButtonImage = rightSelectButton.GetComponent<Image>();

        if (leftButtonImage == null || rightButtonImage == null) return;

        if (!hasSelectedSide)
        {
            leftButtonImage.color = normalColor;
            rightButtonImage.color = normalColor;
            return;
        }

        if (selectedSide == PredictionSide.Left)
        {
            leftButtonImage.color = selectedColor;
            rightButtonImage.color = normalColor;
        }
        else
        {
            leftButtonImage.color = normalColor;
            rightButtonImage.color = selectedColor;
        }
    }

    private void SaveBetData()
    {
        if (!hasSelectedSide) return;

        float payoutMultiplier = BetOddsCalculator.CalculatePayoutMultiplier(
            GameSession.Instance.LeftMonster,
            GameSession.Instance.RightMonster,
            selectedSide
        );

        BetData betData = new BetData
        {
            Side = selectedSide,
            Amount = currentBetAmount,
            InspectLevel = InspectLevel.Full,
            PayoutMultiplier = payoutMultiplier
        };

        GameSession.Instance.SetBet(betData);
    }
}