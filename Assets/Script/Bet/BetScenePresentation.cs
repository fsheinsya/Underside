using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Bet画面のモンスター表示、登場演出、詳細画面の開閉を担当する。</summary>
public sealed class BetScenePresentation
{
    private readonly TMP_Text coinText;
    private readonly CanvasGroup coinCanvas;
    private readonly TMP_Text leftName;
    private readonly Image leftImage;
    private readonly CanvasGroup leftCanvas;
    private readonly TMP_Text rightName;
    private readonly Image rightImage;
    private readonly CanvasGroup rightCanvas;
    private readonly GameObject menuRoot;
    private readonly GameObject amountRoot;
    private readonly StatusDetailView leftDetails;
    private readonly StatusDetailView rightDetails;
    private readonly Action interactionChanged;

    public bool IsOpeningDetails { get; private set; }

    public BetScenePresentation(TMP_Text coinText, CanvasGroup coinCanvas,
        TMP_Text leftName, Image leftImage, CanvasGroup leftCanvas,
        TMP_Text rightName, Image rightImage, CanvasGroup rightCanvas,
        GameObject menuRoot, GameObject amountRoot, StatusDetailView leftDetails,
        StatusDetailView rightDetails, Action interactionChanged)
    {
        this.coinText = coinText;
        this.coinCanvas = coinCanvas;
        this.leftName = leftName;
        this.leftImage = leftImage;
        this.leftCanvas = leftCanvas;
        this.rightName = rightName;
        this.rightImage = rightImage;
        this.rightCanvas = rightCanvas;
        this.menuRoot = menuRoot;
        this.amountRoot = amountRoot;
        this.leftDetails = leftDetails;
        this.rightDetails = rightDetails;
        this.interactionChanged = interactionChanged;
    }

    public void Initialize(GameSession session)
    {
        coinText.text = $"Coin: {session.CurrentCoin}";
        leftName.text = session.LeftMonster.Name;
        leftImage.sprite = session.LeftMonster.Icon;
        rightName.text = session.RightMonster.Name;
        rightImage.sprite = session.RightMonster.Icon;
        leftDetails?.Clear();
        rightDetails?.Clear();
        ShowMenu();
        leftCanvas.alpha = rightCanvas.alpha = 0f;
    }

    public async UniTask PlayIntroAsync(CancellationToken token)
    {
        coinCanvas.alpha = 0f;
        await coinCanvas.DOFade(1f, 0.5f).SetUpdate(true)
            .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token);
        await UniTask.Delay(300, ignoreTimeScale: true, cancellationToken: token);
        leftCanvas.alpha = rightCanvas.alpha = 1f;

        RectTransform left = leftImage.rectTransform;
        RectTransform right = rightImage.rectTransform;
        float lx = left.anchoredPosition.x;
        float rx = right.anchoredPosition.x;
        left.anchoredPosition = new Vector2(-800f, left.anchoredPosition.y);
        right.anchoredPosition = new Vector2(800f, right.anchoredPosition.y);
        await UniTask.WhenAll(
            left.DOAnchorPosX(lx + 80, 0.4f).SetEase(Ease.OutBack).SetUpdate(true)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token),
            right.DOAnchorPosX(rx - 80, 0.4f).SetEase(Ease.OutBack).SetUpdate(true)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token));
        await UniTask.WhenAll(
            left.DOAnchorPosX(lx, 0.2f).SetUpdate(true)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token),
            right.DOAnchorPosX(rx, 0.2f).SetUpdate(true)
                .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, cancellationToken: token));
    }

    public async UniTask OpenDetailsAsync(GameSession session, PredictionSide side, CancellationToken token)
    {
        StatusDetailView selected = side == PredictionSide.Left ? leftDetails : rightDetails;
        if (selected == null || IsOpeningDetails) return;
        IsOpeningDetails = true;
        interactionChanged();
        try
        {
            coinText.gameObject.SetActive(false);
            menuRoot?.SetActive(false);
            amountRoot?.SetActive(false);
            (side == PredictionSide.Left ? rightDetails : leftDetails)?.Close();
            float odds = BetOddsCalculator.CalculatePayoutMultiplier(session.LeftMonster, session.RightMonster, side);
            await selected.PlayOpenAnimation(side == PredictionSide.Left ? session.LeftMonster : session.RightMonster,
                odds, token);
        }
        finally
        {
            IsOpeningDetails = false;
            if (!token.IsCancellationRequested) interactionChanged();
        }
    }

    public void ShowMenu()
    {
        leftDetails?.Close();
        rightDetails?.Close();
        coinText.gameObject.SetActive(true);
        menuRoot?.SetActive(true);
        amountRoot?.SetActive(true);
    }
}
