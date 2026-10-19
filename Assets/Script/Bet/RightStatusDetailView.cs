using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using System.Collections.Generic;

/// <summary>
/// 右ステータス画面（完全対称版🔥）
/// </summary>
public class RightStatusDetailView : MonoBehaviour
{
    [SerializeField] private GameObject rootObject;

    [Header("キャラ画像")]
    [SerializeField] private Image charaImage;

    [Header("ステータス")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text growthText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text accuracyText;
    [SerializeField] private TMP_Text evasionText;

    [Header("追加情報")]
    [SerializeField] private TMP_Text elementText;
    [SerializeField] private TMP_Text personalityText;
    [SerializeField] private TMP_Text oddsText;

    [Header("スキル")]
    [SerializeField] private TMP_Text skillText1;
    [SerializeField] private TMP_Text skillText2;

    [Header("スライド対象")]
    [SerializeField] private RectTransform statusPanel;
    [SerializeField] private RectTransform elementPanel;
    [SerializeField] private RectTransform oddsPanel;
    [SerializeField] private RectTransform personalityPanel;
    [SerializeField] private RectTransform skillPanel1;
    [SerializeField] private RectTransform skillPanel2;

    private readonly Dictionary<RectTransform, Vector2> originalPos = new();
    private bool hasPlayedAnimation = false;

    private void Awake()
    {
        Cache();
        Clear();
        HideAllPanels();
    }

    // =========================
    // 位置保存
    // =========================
    private void Cache()
    {
        Save(statusPanel);
        Save(elementPanel);
        Save(oddsPanel);
        Save(personalityPanel);
        Save(skillPanel1);
        Save(skillPanel2);
    }

    private void Save(RectTransform rect)
    {
        if (rect != null && !originalPos.ContainsKey(rect))
        {
            originalPos[rect] = rect.anchoredPosition;
        }
    }

    // =========================
    // 表示制御
    // =========================
    private void HideAllPanels()
    {
        SetPanel(statusPanel, false);
        SetPanel(elementPanel, false);
        SetPanel(oddsPanel, false);
        SetPanel(personalityPanel, false);
        SetPanel(skillPanel1, false);
        SetPanel(skillPanel2, false);
    }

    private void SetPanel(RectTransform rect, bool visible)
    {
        if (rect != null)
        {
            rect.gameObject.SetActive(visible);
        }
    }

    public void Open()
    {
        rootObject?.SetActive(true);
    }

    public void Close()
    {
        rootObject?.SetActive(false);

        if (charaImage != null)
        {
            charaImage.transform.localScale = Vector3.one;
        }
    }

    public void Clear()
    {
        SetText(hpText, "HP:----");
        SetText(atkText, "攻撃力:----");
        SetText(defText, "防御力:----");
        SetText(growthText, "成長率:----");
        SetText(speedText, "素早さ:----");
        SetText(accuracyText, "命中率:----");
        SetText(evasionText, "回避率:----");

        SetText(elementText, "　");
        SetText(personalityText, "　");
        SetText(oddsText, "　");

        SetText(skillText1, "スキルなし");
        SetText(skillText2, "スキルなし");

        if (charaImage != null)
        {
            charaImage.sprite = null;
            charaImage.enabled = false;
        }
    }

    // =========================
    // データ表示
    // =========================
    public void Show(MonsterData data, float odds)
    {
        if (data == null || data.Stats == null) return;

        charaImage.sprite = data.Icon;
        charaImage.enabled = true;
        charaImage.color = Color.white;
        charaImage.preserveAspect = true;

        // 🔥 右は反転
        charaImage.rectTransform.localScale = new Vector3(-2f, 2f, 1f);

        hpText.text = $"HP:{data.Stats.MaxHp}";
        atkText.text = $"攻撃力:{data.Stats.Attack}";
        defText.text = $"防御力:{data.Stats.Defense}";
        growthText.text = $"成長率:{data.Stats.Growth}";
        speedText.text = $"素早さ:{data.Stats.Speed}";
        accuracyText.text = $"命中率:{data.Stats.Accuracy}";
        evasionText.text = $"回避率:{data.Stats.Evasion}";

        elementText.text = $"{GetElementName(data.Element)}";
        personalityText.text = $"{GetPersonalityName(data.Personality)}";
        oddsText.text = $"{odds:F1}";

        skillText1.text = GetSkillName(data, 0);
        skillText2.text = GetSkillName(data, 1);
    }

    // =========================
    // アニメーション
    // =========================
    public async UniTask PlayOpenAnimation(MonsterData data, float odds)
    {
        Open();
        Show(data, odds);

        if (hasPlayedAnimation)
        {
            ShowAllPanels();
            return;
        }

        hasPlayedAnimation = true;
        HideAllPanels();

        charaImage.transform.localScale = Vector3.zero;

        await charaImage.transform
            .DOScale(new Vector3(-2f, 2f, 1f), 0.3f)
            .SetEase(Ease.OutBack)
            .AsyncWaitForCompletion();

        await Slide(statusPanel);
        await Slide(elementPanel);
        await Slide(oddsPanel);
        await Slide(personalityPanel);
        await Slide(skillPanel1);
        await Slide(skillPanel2);
    }

    private void ShowAllPanels()
    {
        SetPanel(statusPanel, true);
        SetPanel(elementPanel, true);
        SetPanel(oddsPanel, true);
        SetPanel(personalityPanel, true);
        SetPanel(skillPanel1, true);
        SetPanel(skillPanel2, true);
    }

    private async UniTask Slide(RectTransform rect)
    {
        if (rect == null) return;

        Vector2 target = originalPos[rect];

        rect.gameObject.SetActive(true);
        rect.anchoredPosition = new Vector2(target.x + 800f, target.y);

        await rect.DOAnchorPos(target, 0.4f)
            .SetEase(Ease.OutCubic)
            .AsyncWaitForCompletion();

        await UniTask.Delay(30);
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private string GetSkillName(MonsterData data, int index)
    {
        if (data.Skills == null || index >= data.Skills.Count)
            return "スキルなし";

        return data.Skills[index].SkillName;
    }

    private string GetElementName(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => "炎",
            ElementType.Water => "水",
            ElementType.Grass => "草",
            ElementType.Light => "光",
            ElementType.Dark => "闇",
            _ => "不明"
        };
    }

    private string GetPersonalityName(PersonalityType type)
    {
        return PersonalityNames.Get(type);
    }
}
