using UnityEngine;

/// <summary>
/// シーンをまたいでゲーム全体の状態を保持するクラス
/// 主な役割:
/// ・所持コインの管理
/// ・現在ラウンド数の管理
/// ・左右モンスター情報の保持
/// ・賭け情報の保持
/// ・試合結果の保持
/// ・スライム画像のランダム選択
/// 
/// ・ランキングモードとストーリーモードによって最大ラウンド数を変更
/// </summary>
public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    [Header("プレイヤー側・敵側で使うスライム画像一覧")]
    [SerializeField] private Sprite[] slimeSprites;

    // 名前入力画面で選んだ名前を、結果画面まで引き継ぐ。
    public string RankingPlayerName { get; private set; } = string.Empty;

    // 登録済みの名前を今回のプレイヤー名として保持する。記録の保存は行わない。
    public void SetRankingPlayerName(string userName)
    {
        RankingPlayerName = (userName ?? string.Empty).Trim();
        Debug.Log($"[Ranking/Session] 今回のプレイヤー名を「{RankingPlayerName}」に設定しました。");
    }

    // 現在の所持コイン
    public int CurrentCoin { get; private set; } = 100;

    // 現在何試合目か
    public int CurrentRound { get; private set; }

    // 最大試合数を設定をする
    public int MaxRound { get; private set; }

    // 現在の左モンスター
    public MonsterData LeftMonster { get; private set; }

    // 現在の右モンスター
    public MonsterData RightMonster { get; private set; }

    // 現在の賭け情報
    public BetData CurrentBet { get; private set; }

    // 直前の試合結果を参照をする
    public MatchResultData CurrentMatchResult { get; private set; }

    // モンスター生成用のマスターデータを習得
    private MonsterFactory monsterFactory;

    //現在のモードを取得
    public ModeType mode;

    /// <summary>
    /// 最初に1回だけ生成される
    /// 重複生成を防ぐ
    /// </summary>
    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        monsterFactory = new MonsterFactory();
    }

    /// <summary>
    /// 新しいゲームを開始する
    /// </summary>
    public void StartNewGame()
    {
        // 新しいプレイに前回の名前を持ち越さない。名前入力画面で改めて設定する。
        RankingPlayerName = string.Empty;

        //初期所持コインを100に設定
        CurrentCoin = 100;
        //現在のラウンド数を1に設定
        CurrentRound = 1;
        //
        if (mode == ModeType.story)
        {
            MaxRound = 3;
        }
        else if (mode == ModeType.ranking)
        {
            MaxRound = 9999;
        }

        CurrentBet = null;
        CurrentMatchResult = null;

        GenerateNewMatch();
    }

    /// <summary>
    /// 新しい対戦カードを生成する
    /// 毎回スライム画像をランダムで選ぶ
    /// </summary>
    public void GenerateNewMatch()
    {
        // スライム画像が未設定なら、nullで生成する
        if (slimeSprites == null || slimeSprites.Length == 0)
        {
            LeftMonster = monsterFactory.CreateRandom("Left Slime", null);
            RightMonster = monsterFactory.CreateRandom("Right Slime", null);

            CurrentBet = null;
            CurrentMatchResult = null;
            return;
        }

        // 左右で別の画像を選ぶ
        Sprite leftSprite = GetRandomSlimeSprite();
        Sprite rightSprite = GetRandomDifferentSlimeSprite(leftSprite);

        // 名前も色っぽく変えたいなら sprite.name を使う
        LeftMonster = monsterFactory.CreateRandom(GetMonsterNameFromSprite(leftSprite), leftSprite);
        RightMonster = monsterFactory.CreateRandom(GetMonsterNameFromSprite(rightSprite), rightSprite);

        CurrentBet = null;
        CurrentMatchResult = null;
    }

    /// <summary>
    /// 配列からランダムに1枚スライム画像を選ぶ
    /// </summary>
    private Sprite GetRandomSlimeSprite()
    {
        int index = Random.Range(0, slimeSprites.Length);
        return slimeSprites[index];
    }

    /// <summary>
    /// 指定画像と異なる画像をランダムに選ぶ
    /// 画像が1枚しかない場合は同じ画像を返す
    /// </summary>
    private Sprite GetRandomDifferentSlimeSprite(Sprite excludeSprite)
    {
        // 1枚しかないなら同じものを返す
        if (slimeSprites.Length <= 1)
        {
            return slimeSprites[0];
        }

        Sprite selected = excludeSprite;

        // 同じ画像が出なくなるまで引き直す
        while (selected == excludeSprite)
        {
            selected = GetRandomSlimeSprite();
        }

        return selected;
    }

    /// <summary>
    /// Sprite名から表示用のモンスター名を作る
    /// </summary>
    private string GetMonsterNameFromSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return "Slime";
        }

        // 例: slimeBlue → slimeBlue
        // 必要ならここで日本語名に変えてもOK
        return sprite.name;
    }

    /// <summary>
    /// 賭け情報を保存する
    /// </summary>
    public void SetBet(BetData betData)
    {
        CurrentBet = betData;
    }

    /// <summary>
    /// コインを消費する
    /// </summary>
    public bool TryConsumeCoin(int amount)
    {
        if (amount < 0) return false;
        if (CurrentCoin < amount) return false;

        CurrentCoin -= amount;
        return true;
    }

    /// <summary>
    /// コインを加算する
    /// </summary>
    public void AddCoin(int amount)
    {
        if (amount < 0) return;
        CurrentCoin += amount;
    }

    /// <summary>
    /// 試合結果を保存する
    /// </summary>
    public void SetMatchResult(MatchResultData result)
    {
        CurrentMatchResult = result;
    }

    /// <summary>
    /// 最終ラウンドかどうかを返す
    /// </summary>
    public bool IsLastRound()
    {
        return CurrentRound >= MaxRound;
    }

    /// <summary>
    /// 次の試合へ進む
    /// </summary>
    public void NextRound()
    {
        CurrentRound++;
        GenerateNewMatch();
    }
}