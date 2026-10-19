using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Globalization;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 賭け金操作の追い方：
/// ・ボタン長押し：StartIncrease / StartDecrease → StartLongPress → Update → ChangeBetAmount。
/// ・直接入力：Startでイベント登録 → CommitBetInput → SetBetAmount。
/// ・共通の確定処理：SetBetAmountで範囲を補正 → RefreshBetTextでUIに反映。
/// ・対戦開始：PrepareBetで入力を確定 → SaveBetDataでGameSessionへ保存。
/// </summary>
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
    // 以前からある数値テキスト。Betシーンでは入力欄の子に移し、表示用として再利用する。
    [SerializeField] private TMP_Text betAmountText;
    // Betシーンに追加したBetAmountInputを割り当てる。Text AreaのRectMask2Dで文字のはみ出しを防ぐ。
    // シーン側で1行入力・最大10桁・数字キーボード・選択時の全選択を設定している。
    [SerializeField] private TMP_InputField betAmountInput;

    [Header("詳細画面")]
    [SerializeField] private LeftStatusDetailView leftStatusView;
    [SerializeField] private RightStatusDetailView rightStatusView;

    [Header("SE")]
    [SerializeField] private AudioSource audio;
    [SerializeField] private AudioClip betSE;     // カチカチ音
    [SerializeField] private AudioClip selectSE;  // 決定音

    [Header("操作受付")]
    [Tooltip("登場演出が終わってから操作を受け付けるまでの秒数。押している入力の解放も待ちます。")]
    [SerializeField, Min(0f)] private float inputEnableDelay = 0.5f;

    private readonly SceneInputGate inputGate = new SceneInputGate();
    private Selectable[] betAmountControls = new Selectable[0];
    private bool introCompleted;
    private bool isTransitioning;
    private bool CanAcceptInput => isActiveAndEnabled && inputGate.IsOpen && !isTransitioning && !isOpeningStatus;

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

    // 長押しの繰り返しをUpdateで管理する。待機中の非同期処理を残さず、その場で停止できる。
    private bool isPressing = false;
    private bool isIncrease = true; // trueなら増額、falseなら減額。
    private float nextRepeatTime; // 次に1コイン変更する時刻。ゲーム速度の影響を受けない時間を使う。
    private float repeatInterval; // 繰り返す間隔。長押し中に短くして操作を加速する。

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

    private void Awake()
    {
        if (betAmountRoot != null)
            betAmountControls = betAmountRoot.GetComponentsInChildren<Selectable>(true);
        // StartやEventSystemの処理より前に無効化する。
        RefreshInteractionState();
    }

    private void OnEnable()
    {
        inputGate.Lock();
        if (introCompleted && !isTransitioning)
            inputGate.Schedule(Time.unscaledTime, inputEnableDelay);
        RefreshInteractionState();
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
        if (betAmountInput != null)
        {
            // 入力途中は数字だけを受け付け、Enterやフォーカスを外した時に金額を確定する。
            betAmountInput.onValidateInput = ValidateBetCharacter;
            betAmountInput.onEndEdit.AddListener(CommitBetInput);
            // 金額を直接編集し始めたら、ボタンの長押しによる変更を止める。
            betAmountInput.onSelect.AddListener(OnBetInputSelected);
        }
        // 初期値10より所持コインが少ない場合も、最初から有効な金額に補正する。
        SetBetAmount(currentBetAmount);

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
        var cancellationToken = this.GetCancellationTokenOnDestroy();
        //最初に透明に設定している
        coinCanvas.alpha = 0f;

        //コインキャンパスを0.5秒かけて表示させる
        await coinCanvas.DOFade(1f, 0.5f).SetUpdate(true).SetLink(gameObject)
            .AsyncWaitForCompletion().AsUniTask().AttachExternalCancellation(cancellationToken);

        //300ミリ秒待機させる
        await UniTask.Delay(300, ignoreTimeScale: true, cancellationToken: cancellationToken);

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
            l.DOAnchorPosX(lx + 80, 0.4f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject).AsyncWaitForCompletion().AsUniTask(),
            r.DOAnchorPosX(rx - 80, 0.4f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject).AsyncWaitForCompletion().AsUniTask()
        ).AttachExternalCancellation(cancellationToken);

        //
        await UniTask.WhenAll(
            l.DOAnchorPosX(lx, 0.2f).SetUpdate(true).SetLink(gameObject).AsyncWaitForCompletion().AsUniTask(),
            r.DOAnchorPosX(rx, 0.2f).SetUpdate(true).SetLink(gameObject).AsyncWaitForCompletion().AsUniTask()
        ).AttachExternalCancellation(cancellationToken);

        introCompleted = true;
        if (isActiveAndEnabled && !isTransitioning)
            inputGate.Schedule(Time.unscaledTime, inputEnableDelay);
    }

    private void LateUpdate()
    {
        if (!introCompleted || isTransitioning || inputGate.IsOpen) return;
        // EventSystemのUpdateで離した瞬間のクリックを処理し終えてから解放する。
        if (inputGate.TryOpen(Time.unscaledTime, Time.frameCount, IsInputHeld()))
        {
            EventSystem.current?.SetSelectedGameObject(null);
            RefreshInteractionState();
        }
    }

    private static bool IsInputHeld()
    {
        if (!Application.isFocused || Input.anyKey || Input.touchCount > 0) return true;
        // BetシーンのInput System側の決定・クリックも確認する。
        var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
        return module != null && (module.submit?.action?.IsPressed() == true
            || module.leftClick?.action?.IsPressed() == true
            || module.rightClick?.action?.IsPressed() == true
            || module.middleClick?.action?.IsPressed() == true);
    }

    private void RefreshInteractionState()
    {
        bool canUseMenu = CanAcceptInput;
        bool canBet = canUseMenu && currentBetAmount >= MIN_BET
            && GameSession.Instance != null && GameSession.Instance.CurrentCoin >= MIN_BET;
        if (leftStatusButton != null) leftStatusButton.interactable = canUseMenu;
        if (rightStatusButton != null) rightStatusButton.interactable = canUseMenu;
        if (leftSelectButton != null) leftSelectButton.interactable = canBet;
        if (rightSelectButton != null) rightSelectButton.interactable = canBet;
        foreach (var control in betAmountControls)
            if (control != null) control.interactable = canBet;
        if (betAmountInput != null) betAmountInput.interactable = canBet;
    }

    // =========================
    // 🔥 賭け金操作（SEあり）
    // =========================
    // 単発クリック用の入口。現在のBetシーンの増減ボタンは、下のPointerDown用メソッドを使う。
    // 同じボタンに両方を登録すると、押した時と離した時に二重加算するため注意する。
    public void OnClickBetUp()
    {
        if (!CanAcceptInput) return;
        FinishBetInput();
        ChangeBetAmount(BET_STEP, true);
    }

    public void OnClickBetDown()
    {
        if (!CanAcceptInput) return;
        FinishBetInput();
        ChangeBetAmount(-BET_STEP, true);
    }

    private void RefreshBetText()
    {
        // OSの地域設定に左右されない、桁区切りなしの数字で表示する。
        string value = currentBetAmount.ToString(CultureInfo.InvariantCulture);
        if (betAmountInput != null)
        {
            // コードから表示を更新するときは、onValueChangedを通知しない。
            betAmountInput.SetTextWithoutNotify(value);
        }
        else if (betAmountText != null)
        {
            // 入力欄が未設定の構成でも、以前のテキスト表示を使えるようにする。
            betAmountText.text = value;
        }

        // 表示更新によって、演出中・待機中の入力ロックを解除しない。
        RefreshInteractionState();
    }

    // ボタン操作と直接入力を、同じ範囲（1〜所持コイン）に補正する。
    private void SetBetAmount(long amount)
    {
        int max = GameSession.Instance == null ? 0 : Mathf.Max(0, GameSession.Instance.CurrentCoin);
        // 所持金がある場合は最低1・最高所持金。所持金がない場合だけ0を許可する。
        // 大きな入力も先に範囲内へ収めるので、最後にintへ変換しても桁あふれしない。
        currentBetAmount = max < MIN_BET ? 0 : (int)System.Math.Max(MIN_BET, System.Math.Min(amount, max));
        RefreshBetText();
    }

    // deltaは増減量。押した瞬間だけ音を鳴らし、長押し中の連続音や上限での空打ち音を防ぐ。
    private void ChangeBetAmount(int delta, bool playSound)
    {
        int previous = currentBetAmount;
        // intの上限で加算してもオーバーフローしないようlongで計算する。
        SetBetAmount((long)currentBetAmount + delta);
        if (playSound && previous != currentBetAmount) PlaySE(betSE);
    }

    private char ValidateBetCharacter(string text, int position, char character)
    {
        // 半角数字以外は空文字ではなく「無効な1文字」を表す'\0'を返して入力を拒否する。
        return character >= '0' && character <= '9' ? character : '\0';
    }

    private void OnBetInputSelected(string value) => StopLongPress();

    private void CommitBetInput(string value)
    {
        if (!CanAcceptInput)
        {
            RefreshBetText();
            return;
        }
        // 空欄や不正な入力では、最後に確定した金額に戻す。
        // TryParseなら変換できなくても例外にならず、longなら10桁の貼り付けも扱える。
        if (long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long amount))
            SetBetAmount(amount);
        else
            SetBetAmount(currentBetAmount);
    }

    // 入力欄からボタン操作へ移るとき、編集中の値を確定してから次の処理へ渡す。
    private void FinishBetInput()
    {
        if (betAmountInput == null) return;
        if (betAmountInput.isFocused) betAmountInput.DeactivateInputField();
        CommitBetInput(betAmountInput.text);
    }

    // 押した瞬間に1回、0.3秒後から繰り返す。長押し中は徐々に速くする。
    // BetシーンのEventTrigger設定：増額のPointerDown → StartIncrease、減額 → StartDecrease。
    // 両ボタンのPointerUpとPointerExit → StopLongPress。減額側のPointerClick設定をPointerExitに直した。
    public void StartIncrease()
    {
        StartLongPress(true);
    }

    public void StartDecrease()
    {
        StartLongPress(false);
    }

    public void StopLongPress()
    {
        isPressing = false;
    }

    private void StartLongPress(bool increase)
    {
        // 詳細画面を開いている間や賭け金UIが非表示の間は、長押しを開始しない。
        if (!CanAcceptInput || betAmountRoot == null || !betAmountRoot.activeInHierarchy) return;
        FinishBetInput();
        isIncrease = increase;
        isPressing = true;
        repeatInterval = 0.2f;
        // 最初の0.3秒は待機するため、短いクリックなら1コインだけ変更される。
        nextRepeatTime = Time.unscaledTime + 0.3f;
        ChangeBetAmount(isIncrease ? BET_STEP : -BET_STEP, true);
    }

    private void Update()
    {
        if (!isPressing) return;
        // PointerUp/Exitを受け取る前にUIが閉じられても、増減が続かないようにする。
        if (!CanAcceptInput || betAmountRoot == null || !betAmountRoot.activeInHierarchy)
        {
            StopLongPress();
            return;
        }

        // 予定時刻になるまで何もしない。低フレームレートでもまとめて大量加算はしない。
        if (Time.unscaledTime < nextRepeatTime) return;
        ChangeBetAmount(isIncrease ? BET_STEP : -BET_STEP, false);
        // 1回ごとに0.02秒短縮し、最短0.05秒間隔（最大で毎秒約20コイン）にする。
        repeatInterval = Mathf.Max(0.05f, repeatInterval - 0.02f);
        nextRepeatTime = Time.unscaledTime + repeatInterval;
    }

    // シーン遷移やコンポーネント無効化でも、押しっぱなしの状態を解除する。
    private void OnDisable()
    {
        StopLongPress();
        inputGate.Lock();
        RefreshInteractionState();
    }

    // 別ウィンドウへの切り替え中にボタンを離すと、PointerUpを受け取れない場合がある。
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) StopLongPress();
    }

    // アプリがバックグラウンドに回った場合も、復帰後に勝手に増減しないよう停止する。
    private void OnApplicationPause(bool paused)
    {
        if (paused) StopLongPress();
    }

    // =========================
    // ステータス
    // =========================
    public void OnClickLeftStatus() => OpenLeftStatusAsync().Forget();
    public void OnClickRightStatus() => OpenRightStatusAsync().Forget();

    private async UniTask OpenLeftStatusAsync()
    {
        if (!CanAcceptInput) return;
        FinishBetInput();
        isOpeningStatus = true;
        RefreshInteractionState();

        coinText.gameObject.SetActive(false);
        CloseSelectedUI();
        CloseBetAmountUI();

        rightStatusView?.Close();

        float odds = BetOddsCalculator.CalculatePayoutMultiplier(
            GameSession.Instance.LeftMonster,
            GameSession.Instance.RightMonster,
            PredictionSide.Left
        );

        await leftStatusView.PlayOpenAnimation(GameSession.Instance.LeftMonster, odds)
            .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

        isOpeningStatus = false;
        RefreshInteractionState();
    }

    private async UniTask OpenRightStatusAsync()
    {
        if (!CanAcceptInput) return;
        FinishBetInput();
        isOpeningStatus = true;
        RefreshInteractionState();

        coinText.gameObject.SetActive(false);
        CloseSelectedUI();
        CloseBetAmountUI();

        leftStatusView?.Close();

        float odds = BetOddsCalculator.CalculatePayoutMultiplier(
            GameSession.Instance.LeftMonster,
            GameSession.Instance.RightMonster,
            PredictionSide.Right
        );

        await rightStatusView.PlayOpenAnimation(GameSession.Instance.RightMonster, odds)
            .AttachExternalCancellation(this.GetCancellationTokenOnDestroy());

        isOpeningStatus = false;
        RefreshInteractionState();
    }

    public void OnClickBackMenu()
    {
        if (!CanAcceptInput) return;
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
    private void CloseBetAmountUI()
    {
        // UIを隠す前に長押しを止め、直接入力中の金額を確定する。
        StopLongPress();
        FinishBetInput();
        betAmountRoot?.SetActive(false);
    }

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
    public void OnClickLeftSelect() => SelectAndStartBattle(PredictionSide.Left);

    //右を選択をした場合
    public void OnClickRightSelect() => SelectAndStartBattle(PredictionSide.Right);

    private void SelectAndStartBattle(PredictionSide side)
    {
        if (!PrepareBet()) return;
        PlaySE(selectSE);

        selectedSide = side;
        hasSelectedSide = true;

        UpdateSelectButtonColors();
        SaveBetData();

        SceneManager.LoadScene(SceneNames.Battle);
    }

    //アニメーションの更新
    private void UpdateSelectButtonColors()
    {
        if (leftSelectButton == null || rightSelectButton == null) return;

        //選択をするとフレームが黄色になるように設定をする
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

    // Enterを押さずに対戦相手を選んでも、最新の入力額を保存できるようにする。
    private bool PrepareBet()
    {
        if (!CanAcceptInput || GameSession.Instance == null) return false;
        StopLongPress();
        FinishBetInput();
        // 対戦開始直前の所持コインで再確認し、保存する金額を範囲内に収める。
        SetBetAmount(currentBetAmount);
        if (currentBetAmount < MIN_BET) return false;
        // 左右の同時クリックや連続コールでも、保存とシーン遷移は1回だけ。
        isTransitioning = true;
        inputGate.Lock();
        RefreshInteractionState();
        return true;
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
