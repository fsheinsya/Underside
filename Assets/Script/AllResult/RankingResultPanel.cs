using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>最終結果のランキングパネルを生成・配置・表示する。保存の判断はControllerが担当する。</summary>
public sealed class RankingResultPanel
{
    private readonly GameObject root;
    private readonly TMP_FontAsset font;
    private readonly Button submitButton;
    private readonly Button declineButton;
    private readonly TMP_Text feedbackText;
    private readonly TMP_Text rankingText;

    public RankingResultPanel(Canvas canvas, TMP_FontAsset font, string playerName, int coin, int round,
        UnityAction submit, UnityAction decline, UnityAction returnToTitle)
    {
        this.font = font;
        root = new GameObject("RankingRegistrationPanel", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);
        root.SetActive(false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.12f, 0.94f);

        CreateText(root.transform, "Heading", "ランキング更新の確認", 32, 0, 205, 740, 46);
        TMP_Text name = CreateText(root.transform, "PlayerName",
            string.IsNullOrWhiteSpace(playerName) ? "プレイヤー名なし" : playerName,
            24, 0, 149, 740, 56);
        name.richText = false;
        name.enableAutoSizing = true;
        name.fontSizeMin = 12;
        name.fontSizeMax = 24;
        CreateText(root.transform, "Score", $"{coin} コイン  /  到達 {round} ラウンド", 25, 0, 100, 740, 40);
        CreateText(root.transform, "Question", "今回の結果で記録を更新しますか？", 24, 0, 57, 740, 40);
        submitButton = CreateButton("SubmitButton", "更新する", -155, 7, 270, 44, submit);
        declineButton = CreateButton("DeclineButton", "更新しない", 155, 7, 270, 44, decline);
        feedbackText = CreateText(root.transform, "Feedback", "自己ベストを上回る場合のみ更新します。", 19, 0, -43, 740, 48);
        CreateText(root.transform, "RankingHeading", "この端末のランキング  /  コイン優先・同点はラウンド順", 20, 0, -88, 740, 36);
        rankingText = CreateText(root.transform, "RankingEntries", "", 18, 0, -157, 740, 105);
        rankingText.alignment = TextAlignmentOptions.TopLeft;
        rankingText.richText = false;
        CreateButton("ReturnButton", "タイトルへ戻る", 0, -210, 250, 42, returnToTitle);

        if (string.IsNullOrWhiteSpace(playerName))
        {
            submitButton.interactable = false;
            SetFeedback("プレイヤー名がありません。名前入力画面から開始してください。");
        }
    }

    public void Show() => root.SetActive(true);
    public void SetFeedback(string message) => feedbackText.text = message;
    public void ShowLoadError() => rankingText.text = "ランキングデータを読み込めませんでした。";

    public void CompleteDecision()
    {
        submitButton.interactable = false;
        declineButton.interactable = false;
    }

    public void ShowEntries(IReadOnlyList<RankingData> entries)
    {
        if (entries.Count == 0)
        {
            rankingText.text = "まだ記録がありません";
            return;
        }
        var builder = new StringBuilder();
        int count = Mathf.Min(5, entries.Count);
        for (int index = 0; index < count; index++)
        {
            var entry = entries[index];
            builder.Append(index + 1).Append("位  ")
                .Append(entry.userName).Append("    ")
                .Append(entry.coin).Append(" コイン    ")
                .Append(entry.round).Append(" ラウンド");
            if (index < count - 1) builder.AppendLine();
        }
        rankingText.text = builder.ToString();
    }

    private TMP_Text CreateText(Transform parent, string objectName, string value, int fontSize,
        float x, float y, float width, float height)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        Place(textObject.GetComponent<RectTransform>(), x, y, width, height);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, string label, float x, float y, float width, float height,
        UnityAction action)
    {
        var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(root.transform, false);
        Place(buttonObject.GetComponent<RectTransform>(), x, y, width, height);
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.75f, 0.3f, 0.24f, 1f);
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateText(buttonObject.transform, "Label", label, 23, 0, 0, width, height);
        return button;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }
}
