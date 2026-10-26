using System;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

/// <summary>
/// Betシーンの入力受付と画面遷移を調停する。
/// 金額はBetAmountController、表示はBetScenePresentation、保存はBetPlacementServiceに委譲する。
/// Inspector参照とUnityEventの入口は維持するため、既存シーンの再設定は不要。
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
    private BetAmountController amountController;
    private BetScenePresentation presentation;
    private bool introCompleted;
    private bool isTransitioning;

    private bool CanAcceptInput => isActiveAndEnabled && inputGate.IsOpen && !isTransitioning
        && presentation != null && !presentation.IsOpeningDetails;

    private void Awake()
    {
        amountController = new BetAmountController(betAmountText, betAmountInput, betAmountRoot,
            () => GameSession.Instance != null ? GameSession.Instance.CurrentCoin : 0,
            () => CanAcceptInput, RefreshInteractionState, () => PlaySE(betSE));
        presentation = new BetScenePresentation(coinText, coinCanvas,
            leftCharaNameText, leftCharaImage, leftCharaCanvas,
            rightCharaNameText, rightCharaImage, rightCharaCanvas,
            selectedUIRoot, betAmountRoot, leftStatusView, rightStatusView, RefreshInteractionState);
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
            isTransitioning = true;
            SceneManager.LoadScene(SceneNames.Title);
            return;
        }
        presentation.Initialize(GameSession.Instance);
        amountController.ClampAmount();
        PlayIntroAsync().Forget();
    }

    private async UniTask PlayIntroAsync()
    {
        var token = this.GetCancellationTokenOnDestroy();
        try
        {
            await presentation.PlayIntroAsync(token);
            introCompleted = true;
            if (isActiveAndEnabled && !isTransitioning)
                inputGate.Schedule(Time.unscaledTime, inputEnableDelay);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private void Update() => amountController?.Tick(Time.unscaledTime);

    private void LateUpdate()
    {
        if (!introCompleted || isTransitioning || inputGate.IsOpen) return;
        if (inputGate.TryOpen(Time.unscaledTime, Time.frameCount, IsInputHeld()))
        {
            EventSystem.current?.SetSelectedGameObject(null);
            RefreshInteractionState();
        }
    }

    private static bool IsInputHeld()
    {
        if (!Application.isFocused || Input.anyKey || Input.touchCount > 0) return true;
        var module = EventSystem.current?.currentInputModule as InputSystemUIInputModule;
        return module != null && (module.submit?.action?.IsPressed() == true
            || module.leftClick?.action?.IsPressed() == true
            || module.rightClick?.action?.IsPressed() == true
            || module.middleClick?.action?.IsPressed() == true);
    }

    private void RefreshInteractionState()
    {
        bool canUseMenu = CanAcceptInput;
        bool canBet = canUseMenu && amountController != null && amountController.Amount >= 1
            && GameSession.Instance != null && GameSession.Instance.CurrentCoin >= 1;
        if (leftStatusButton != null) leftStatusButton.interactable = canUseMenu;
        if (rightStatusButton != null) rightStatusButton.interactable = canUseMenu;
        if (leftSelectButton != null) leftSelectButton.interactable = canBet;
        if (rightSelectButton != null) rightSelectButton.interactable = canBet;
        amountController?.SetInteractable(canBet);
    }

    // 既存のButton / EventTriggerが呼ぶ入口。
    public void OnClickBetUp() => amountController?.ChangeOnce(1);
    public void OnClickBetDown() => amountController?.ChangeOnce(-1);
    public void StartIncrease() => amountController?.StartLongPress(true, Time.unscaledTime);
    public void StartDecrease() => amountController?.StartLongPress(false, Time.unscaledTime);
    public void StopLongPress() => amountController?.StopLongPress();
    public void OnClickLeftStatus() => OpenStatusAsync(PredictionSide.Left).Forget();
    public void OnClickRightStatus() => OpenStatusAsync(PredictionSide.Right).Forget();

    private async UniTask OpenStatusAsync(PredictionSide side)
    {
        if (!CanAcceptInput) return;
        StopLongPress();
        amountController.FinishEditing();
        var token = this.GetCancellationTokenOnDestroy();
        try
        {
            await presentation.OpenDetailsAsync(GameSession.Instance, side, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public void OnClickBackMenu()
    {
        if (CanAcceptInput) presentation.ShowMenu();
    }

    public void OnClickLeftSelect() => SelectAndStartBattle(PredictionSide.Left);
    public void OnClickRightSelect() => SelectAndStartBattle(PredictionSide.Right);

    private void SelectAndStartBattle(PredictionSide side)
    {
        if (!CanAcceptInput) return;
        StopLongPress();
        amountController.FinishEditing();
        amountController.ClampAmount();
        if (!BetPlacementService.TrySave(GameSession.Instance, side, amountController.Amount)) return;

        isTransitioning = true;
        inputGate.Lock();
        RefreshInteractionState();
        HighlightSelection(side);
        PlaySE(selectSE);
        SceneManager.LoadScene(SceneNames.Battle);
    }

    private void HighlightSelection(PredictionSide side)
    {
        var left = leftSelectButton != null ? leftSelectButton.GetComponent<Image>() : null;
        var right = rightSelectButton != null ? rightSelectButton.GetComponent<Image>() : null;
        Color selected = new Color(1f, 0.9f, 0.4f);
        if (left != null) left.color = side == PredictionSide.Left ? selected : Color.white;
        if (right != null) right.color = side == PredictionSide.Right ? selected : Color.white;
    }

    private void PlaySE(AudioClip clip)
    {
        if (clip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, Random.Range(0.95f, 1.05f));
    }

    private void OnDisable()
    {
        StopLongPress();
        inputGate.Lock();
        RefreshInteractionState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) StopLongPress();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) StopLongPress();
    }

    private void OnDestroy() => amountController?.Dispose();
}
