using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;

public abstract class BattleCharacterView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] protected RectTransform rect;
    [SerializeField] protected Image image;

    [Header("SE（任意）")]
    [SerializeField] protected AudioSource audioSource;
    [SerializeField] protected AudioClip attackSE;
    [SerializeField] protected AudioClip hitSE;
    [SerializeField] protected AudioClip healSE;
    [SerializeField] protected AudioClip buffSE;

    protected Vector2 originalPos;
    protected Color originalColor;
    private StatusAilmentType currentStatus;
    private bool isSkillLocked;
    private Transform motionSpace; // 左右共通のCanvas座標で、画面上の移動量を指定する。

    protected virtual void Awake()
    {
        originalPos = rect.anchoredPosition;
        originalColor = image.color;
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        motionSpace = canvas != null ? canvas.rootCanvas.transform : rect.root;
    }

    protected void PlaySE(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    // =========================
    // 抽象メソッド
    // =========================
    public abstract UniTask PlayAttack();

    // 左右とも同じ距離・時間・加減速を使う。左は+1、右は-1を渡し、相手側へ移動する。
    protected async UniTask PlayAttackTowards(float direction)
    {
        PlaySE(attackSE);
        Vector2 offset = GetLocalMotionOffset(new Vector2(120f * direction, 0f));
        await rect.DOAnchorPos(originalPos + offset, 0.15f)
            .SetEase(Ease.OutQuad).AsyncWaitForCompletion();
        await rect.DOAnchorPos(originalPos, 0.2f)
            .SetEase(Ease.OutBack).AsyncWaitForCompletion();
        rect.anchoredPosition = originalPos; // 演出を繰り返しても開始位置がずれないように戻す。
    }

    // 親の拡大率が左右で違っても、Canvas上で同じ距離になるよう親座標へ変換する。
    private Vector2 GetLocalMotionOffset(Vector2 canvasOffset)
    {
        Vector3 worldOffset = motionSpace.TransformVector(new Vector3(canvasOffset.x, canvasOffset.y, 0f));
        return rect.parent != null ? (Vector2)rect.parent.InverseTransformVector(worldOffset) : (Vector2)worldOffset;
    }

    // =========================
    // 共通処理
    // =========================
    public virtual async UniTask PlayHit()
    {
        PlaySE(hitSE);
        // ランダムな揺れをやめ、左右とも同じ幅・順番・時間で揺らす。
        Vector2 offset = GetLocalMotionOffset(new Vector2(20f, 0f));
        Sequence hit = DOTween.Sequence();
        hit.Append(rect.DOAnchorPos(originalPos + offset, 0.05f).SetEase(Ease.Linear));
        hit.Append(rect.DOAnchorPos(originalPos - offset, 0.05f).SetEase(Ease.Linear));
        hit.Append(rect.DOAnchorPos(originalPos + offset * 0.5f, 0.05f).SetEase(Ease.Linear));
        hit.Append(rect.DOAnchorPos(originalPos, 0.05f).SetEase(Ease.Linear));
        await hit.AsyncWaitForCompletion();
        rect.anchoredPosition = originalPos;
    }

    public virtual async UniTask PlayHeal()
    {
        PlaySE(healSE);
        await PlayColorFlash(Color.lightSkyBlue);
    }

    public virtual async UniTask PlayBuff()
    {
        PlaySE(buffSE);
        await PlayColorFlash(Color.orange);
    }

    // 状態異常中は回復・強化でも色を変えず、演出の待ち時間だけ合わせる。
    private async UniTask PlayColorFlash(Color flashColor)
    {
        if (currentStatus != StatusAilmentType.None || isSkillLocked)
        {
            await UniTask.Delay(500);
            return;
        }
        await image.DOColor(flashColor, 0.2f).AsyncWaitForCompletion();
        await image.DOColor(originalColor, 0.3f).AsyncWaitForCompletion();
    }

    // ログの状態を受け取り、解除まで維持する色を決める。色変化のTweenが残っていれば止める。
    public void SetStatus(StatusAilmentType status, bool skillLocked)
    {
        currentStatus = status;
        isSkillLocked = skillLocked;
        image.DOKill();

        // 同時に複数の状態がある場合はスタン→毒→封印の順で色を優先する。
        Color statusColor = originalColor;
        if (status == StatusAilmentType.Stun) statusColor = Color.yellow;
        else if (status == StatusAilmentType.Poison) statusColor = Color.purple;
        else if (status == StatusAilmentType.SkillSeal || skillLocked) statusColor = Color.gray;
        statusColor.a = originalColor.a; // 状態色を付けても元の透明度は保つ。
        image.color = statusColor; // 状態なし・封印なしならAwakeで保存した元の色に戻る。
    }
}
