using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>賭け金の直接入力、範囲補正、増減ボタンと長押しだけを担当する。</summary>
public sealed class BetAmountController : IDisposable
{
    private readonly TMP_Text amountText;
    private readonly TMP_InputField input;
    private readonly GameObject root;
    private readonly Selectable[] controls;
    private readonly Func<int> availableCoin;
    private readonly Func<bool> canAcceptInput;
    private readonly Action amountChanged;
    private readonly Action playChangeSound;
    private bool isPressing;
    private int direction;
    private float pressStartedAt;
    private float nextRepeatTime;
    private float repeatInterval;

    public int Amount { get; private set; } = 10;

    public BetAmountController(TMP_Text amountText, TMP_InputField input, GameObject root,
        Func<int> availableCoin, Func<bool> canAcceptInput, Action amountChanged, Action playChangeSound)
    {
        this.amountText = amountText;
        this.input = input;
        this.root = root;
        this.availableCoin = availableCoin;
        this.canAcceptInput = canAcceptInput;
        this.amountChanged = amountChanged;
        this.playChangeSound = playChangeSound;
        controls = root != null ? root.GetComponentsInChildren<Selectable>(true) : Array.Empty<Selectable>();
        if (input != null)
        {
            input.onValidateInput = ValidateCharacter;
            input.onEndEdit.AddListener(CommitInput);
            input.onSelect.AddListener(OnInputSelected);
        }
    }

    public void SetInteractable(bool interactable)
    {
        foreach (var control in controls)
            if (control != null) control.interactable = interactable;
        if (input != null) input.interactable = interactable;
    }

    public void ClampAmount() => SetAmount(Amount);

    private void SetAmount(long value)
    {
        int maximum = Math.Max(0, availableCoin());
        Amount = maximum == 0 ? 0 : (int)Math.Max(1, Math.Min(value, maximum));
        RefreshText();
        amountChanged();
    }

    private void RefreshText()
    {
        string value = Amount.ToString(CultureInfo.InvariantCulture);
        if (input != null) input.SetTextWithoutNotify(value);
        else if (amountText != null) amountText.text = value;
    }

    private static char ValidateCharacter(string text, int position, char character)
        => character >= '0' && character <= '9' ? character : '\0';

    private void OnInputSelected(string value) => StopLongPress();

    private void CommitInput(string value)
    {
        if (!canAcceptInput())
        {
            RefreshText();
            return;
        }
        SetAmount(long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
            ? parsed : Amount);
    }

    public void FinishEditing()
    {
        if (input == null) return;
        if (input.isFocused) input.DeactivateInputField();
        CommitInput(input.text);
    }

    public void ChangeOnce(int direction)
    {
        if (!canAcceptInput()) return;
        FinishEditing();
        ChangeAmount(direction, true);
    }

    private void ChangeAmount(int delta, bool playSound)
    {
        int previous = Amount;
        SetAmount((long)Amount + delta); // 100刻み以上でもint上限を越えてから変換しない。
        if (playSound && previous != Amount) playChangeSound();
    }

    public void StartLongPress(bool increase, float now)
    {
        if (!canAcceptInput() || root == null || !root.activeInHierarchy) return;
        FinishEditing();
        direction = increase ? 1 : -1;
        isPressing = true;
        pressStartedAt = now;
        repeatInterval = 0.2f;
        nextRepeatTime = now + 0.3f;
        ChangeAmount(direction, true);
    }

    // 実時間を受け取るので、ゲームのTimeScaleに影響されず境界時刻も検証できる。
    public void Tick(float now)
    {
        if (!isPressing) return;
        if (!canAcceptInput() || root == null || !root.activeInHierarchy)
        {
            StopLongPress();
            return;
        }
        if (now < nextRepeatTime) return;
        ChangeAmount(direction * GetHoldStep(now - pressStartedAt), false);
        repeatInterval = Mathf.Max(0.05f, repeatInterval - 0.02f);
        nextRepeatTime = now + repeatInterval;
    }

    // 0～5秒は1、5～10秒は10、10～15秒は100。5秒ごとに桁を増やす。
    public static int GetHoldStep(float heldSeconds)
    {
        int levels = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, heldSeconds) / 5f), 0, 9);
        int step = 1;
        for (int i = 0; i < levels; i++) step *= 10;
        return step;
    }

    public void StopLongPress() => isPressing = false;

    public void Dispose()
    {
        StopLongPress();
        if (input == null) return;
        input.onEndEdit.RemoveListener(CommitInput);
        input.onSelect.RemoveListener(OnInputSelected);
        if (input.onValidateInput == ValidateCharacter) input.onValidateInput = null;
    }
}
