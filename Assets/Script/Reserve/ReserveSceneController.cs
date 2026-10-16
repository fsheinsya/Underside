using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// ランキング挑戦前に、今回使うプレイヤー名を確定する画面。
/// 未登録名はRankingService.TryRegisterで保存し、登録済みの名前はそのまま使用する。
/// 名前はGameSessionへ引き継ぎ、Betへ進む。成績の保存はプレイ後のAllResultが担当する。
/// </summary>
public class ReserveSceneController : MonoBehaviour
{
    [Header("ユーザー")]
    [SerializeField] private TMP_InputField nameInput;

    [Header("結果メッセージ")]
    [SerializeField] private TMP_Text messageText;

    [Header("結果")]
    [SerializeField] private CanvasGroup BackSelectButton;
    [SerializeField] private CanvasGroup RegistButton;

    private bool isStartingGame;

    private void Start()
    {
        // 登録画面を開いたことと、Inspectorの参照設定を確認する。
        Debug.Log("[Ranking/UI] 名前登録画面を開きました。", this);
        ValidateReferences();
        CheckButtonBinding(RegistButton, nameof(OnClickRegister));
        CheckButtonBinding(BackSelectButton, nameof(BackSelectScene));

        // Reserveシーンを直接再生した場合は、ゲームが未初期化のことがある。
        if (GameSession.Instance == null ||
            GameSession.Instance.CurrentRound < 1 ||
            GameSession.Instance.mode != ModeType.ranking)
        {
            Debug.LogWarning("[Ranking/UI] ランキングのゲーム状態が未初期化です。Selectからランキングモードを開始してください。", this);
        }
    }

    private bool ValidateReferences()
    {
        if (nameInput != null && messageText != null) return true;

        Debug.LogError("[Ranking/UI] InspectorのName InputまたはMessage Textが未設定です。", this);
        return false;
    }

    private void CheckButtonBinding(CanvasGroup group, string methodName)
    {
        // Inspectorに設定したクリックイベントを調べる。実行時追加のイベントは検査対象外。
        Button button = group != null ? group.GetComponent<Button>() : null;
        if (button == null)
        {
            Debug.LogWarning($"[Ranking/UI] {methodName}用のButtonを確認できません。Inspectorの設定を確認してください。", this);
            return;
        }

        for (int index = 0; index < button.onClick.GetPersistentEventCount(); index++)
        {
            if (button.onClick.GetPersistentTarget(index) == this &&
                button.onClick.GetPersistentMethodName(index) == methodName &&
                button.onClick.GetPersistentListenerState(index) != UnityEngine.Events.UnityEventCallState.Off)
            {
                return;
            }
        }

        Debug.LogWarning($"[Ranking/UI] ボタン「{button.name}」のInspectorのOn Clickに{methodName}が設定されていません。", this);
    }
    public void BackSelectScene()
    {
        Debug.Log("[Ranking/UI] 戻るボタン: Selectへ移動します。", this);
        SceneManager.LoadScene(SceneNames.Select);
    }

    /// <summary>
    /// 登録ボタンの入口。準備確認、名前の登録・再利用、名前の引き継ぎ、画面移動の順に処理する。
    /// 読み書きに失敗した場合は画面に理由を表示し、もう一度操作できるようにする。
    /// </summary>
    public void OnClickRegister()
    {
        if (isStartingGame) return;

        Debug.Log("[Ranking/UI] 登録ボタンが押されました。", this);
        if (!ValidateReferences()) return;

        // ゲーム状態がないまま名前だけ登録して遷移しないようにする。
        var session = GameSession.Instance;
        if (session == null || session.CurrentRound < 1 || session.mode != ModeType.ranking)
        {
            messageText.text = "Selectからランキングモードを開始してください。";
            Debug.LogWarning("[Ranking/UI] ゲーム状態が未初期化のため開始できません。", this);
            return;
        }

        // 前後の空白を除き、同じ名前は既存プレイヤーとして使用する。
        string userName = nameInput.text.Trim();

        try
        {
            if (RankingService.Exists(userName))
            {
                messageText.text = $"{userName}さん！また来ましたね！";
                Debug.Log($"[Ranking/UI] 登録済みの名前「{userName}」で開始します。", this);
            }
            else
            {
                if (!RankingService.TryRegister(userName, out string message))
                {
                    messageText.text = message;
                    Debug.Log($"[Ranking/UI] 登録を終了しました: {message}", this);
                    return;
                }

                messageText.text = message;
            }

            // この名前を結果画面でも使用する。ここではプレイ結果は保存しない。
            nameInput.text = userName;
            session.SetRankingPlayerName(userName);
            isStartingGame = true;

            Debug.Log($"[Ranking/UI] 名前「{userName}」を引き継いでBetへ移動します。", this);
            SceneManager.LoadScene(SceneNames.Bet);
        }
        catch (System.Exception ex)
        {
            isStartingGame = false;
            Debug.LogError($"[Ranking/UI] 登録またはシーン移動に失敗しました: {ex}", this);
            messageText.text = "開始できませんでした。もう一度お試しください。";
        }
    }
}
