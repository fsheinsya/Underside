using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Battleシーン全体を管理するクラス
/// ・左右キャラ表示
/// ・左右簡易ステータス表示
/// ・ログ表示
/// ・戦闘実行
/// ・結果保存
/// ・Resultシーン遷移
/// を担当する
/// </summary>
public class BattleSceneController : MonoBehaviour
{
    [Header("上部表示")]
    [SerializeField] private TMP_Text coinText;

    [Header("左キャラ表示")]
    [SerializeField] private Image leftCharaImage;
    [SerializeField] private BattleStatusView leftStatusView;

    [Header("右キャラ表示")]
    [SerializeField] private Image rightCharaImage;
    [SerializeField] private BattleStatusView rightStatusView;

    [Header("ログ表示")]
    [SerializeField] private ActionLogView actionLogView;

    [Header("演出")]
    [SerializeField] private LeftCharacterView leftView;
    [SerializeField] private RightCharacterView rightView;

    [Header("SE,effect")]
    [SerializeField] private AudioSource audio;
    [SerializeField] private BattleEffectPlayer effectPlayer;

    // バトル計算本体
    private BattleSimulator battleSimulator;

    //モード毎に処理を変える
    private ModeType mode;

    /// <summary>
    /// シーン開始時に呼ばれる
    /// </summary>
    private async void Start()
    {
        // セッションがなければタイトルへ戻す
        if (GameSession.Instance == null)
        {
            SceneManager.LoadScene(SceneNames.Title);
            return;
        }

        // 賭け情報が無ければBetへ戻す
        if (GameSession.Instance.CurrentBet == null)
        {
            SceneManager.LoadScene(SceneNames.Bet);
            return;
        }


        // シミュレータ生成
        battleSimulator = new BattleSimulator();

        // UI初期表示
        SetupBattleView();

        // 少し待ってから開始
        await UniTask.Delay(500);

        // 実際の戦闘を開始
        await PlayBattleAsync();
    }

    /// <summary>
    /// 戦闘開始前のUI初期化
    /// </summary>
    private void SetupBattleView()
    {
        MonsterData leftMonster = GameSession.Instance.LeftMonster;
        MonsterData rightMonster = GameSession.Instance.RightMonster;

        // コイン表示
        if (coinText != null)
        {
            coinText.text = $"coin:{GameSession.Instance.CurrentCoin}";
        }

        // 左右画像表示
        if (leftCharaImage != null)
        {
            leftCharaImage.sprite = leftMonster.Icon;
        }

        if (rightCharaImage != null)
        {
            rightCharaImage.sprite = rightMonster.Icon;
        }

        // 左右簡易ステータス表示
        if (leftStatusView != null)
        {
            leftStatusView.Show(leftMonster);
        }

        if (rightStatusView != null)
        {
            rightStatusView.Show(rightMonster);
        }

        // ログ欄初期化
        if (actionLogView != null)
        {
            actionLogView.Clear();
        }
    }

    /// <summary>
    /// 戦闘本体を進行する
    /// </summary>
    private async UniTask PlayBattleAsync()
    {
        GameSession session = GameSession.Instance;

        // 先に賭け金を消費する
        bool consumeSuccess = session.TryConsumeCoin(session.CurrentBet.Amount);

        // コイン不足ならBetへ戻す
        if (!consumeSuccess)
        {
            SceneManager.LoadScene(SceneNames.Bet);
            return;
        }

        // 消費後のコイン表示更新
        if (coinText != null)
        {
            coinText.text = $"coin:{session.CurrentCoin}";
        }

        // Battleシーンの開始メッセージ
        await actionLogView.ShowMessageAnimated("闇の闘技場、開幕...");
        await UniTask.Delay(700);

        // 戦闘結果計算
        MatchResultData result = battleSimulator.Simulate(
            session.LeftMonster,
            session.RightMonster,
            session.CurrentBet
        );

        // ログ1件ずつ表示して、その時点のHPも反映
        foreach (BattleLogEntry log in result.BattleLogs)
        {
            await actionLogView.ShowMessageAnimated(log.Message);

            if (effectPlayer != null)
            {
                await effectPlayer.Play(log);
            }

            if (leftStatusView != null)
                leftStatusView.SetHp(log.LeftHp);

            if (rightStatusView != null)
                rightStatusView.SetHp(log.RightHp);

            await UniTask.Delay(500);
        }

        // 的中時はコイン加算
        if (result.IsPredictionSuccess)
        {
            session.AddCoin(result.RewardCoin);

            if (coinText != null)
            {
                coinText.text = $"coin:{session.CurrentCoin}";
            }

            await actionLogView.ShowMessageAnimated($"予想的中！ {result.RewardCoin} コイン獲得！");
        }
        else
        {
            await actionLogView.ShowMessageAnimated("予想失敗……掛け金は没収された。");
        }

        // 結果保存
        session.SetMatchResult(result);

        // 少し待ってからResultへ
        await UniTask.Delay(1000);
        SceneManager.LoadScene(SceneNames.Result);
    }
}