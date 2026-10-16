using UnityEngine;
using System; 
using System.Collections.Generic;

/// <summary>
/// ランキングのルールを担当する。画面の表示やPlayerPrefsへの直接アクセスは行わない。
/// 名前の入力画面: Existsで確認し、新しい名前ならTryRegisterで登録する。
/// 最終結果画面: 「更新する」が選ばれたときだけSubmitで成績を受け付ける。
/// 順位の表示画面: GetRankingで成績登録済みの人を順位順に受け取る。
/// 保存・読み込みの実作業はRankingStoreへ任せる。
/// 各メソッドはstaticなので、呼び出し側はRankingService.Submit(...)のように使う。
/// </summary>
public class RankingService : MonoBehaviour
{
    // nullを空文字にし、前後の空白を除く。名前の途中にある空白は残す。
   private static string NormalizeName(string name)
    {
        return (name ??  string.Empty).Trim();
    }

    //ユーザー名が登録ルールに合っているか確認する(合っていなければ禁止をする)
    private static bool IsValidName(string name)
    {
        // C#のstring.Lengthで1～8の範囲だけ許可する。空文字と9以上は拒否する。
        if(name.Length == 0 || name.Length > 8) { 
            return false;
        }

        // 改行などの制御文字と、表示タグに使われる山括弧を拒否する。
        foreach(char c in name)
        {
            if(char.IsControl(c) || c == '<' || c == '>')
            {
                return false;
            }
        }

        return true;
    }

    // 一覧から同名の登録を1件探す。英字の大小を区別せず、未登録ならnullを返す。
    private static RankingData Find(
            RankingDataList data,
            string name
        )
    {
        return data.user.Find(entry =>
        entry != null &&
        string.Equals(
            entry.userName,//保存済みのユーザー名
            name,//検索したいユーザー名
            StringComparison.OrdinalIgnoreCase//英文字の大小は区別しない
            ));
    }

    /// <summary>
    /// 名前が保存済みか調べる。Reserved=false（成績未登録）の名前も見つかる。
    /// 読み込みの失敗は例外になるため、画面側で受け取り、利用者に知らせる。
    /// </summary>
    public static bool Exists(string name)
    {
        name = NormalizeName(name);

        return Find(RankingStore.Load(), name) != null;
    }

    /// <summary>
    /// 新しい名前だけを登録する。この時点では順位表に載るプレイ成績は保存しない。
    /// 既存名は追加せずfalseを返す。入力画面は先にExistsで確認し、既存名を再利用する。
    /// </summary>
    /// <param name="message">out引数。成功・拒否の理由を画面へ渡す。</param>
    /// <returns>新しい名前を保存できた場合にtrue。不正な名前・既存名はfalse。</returns>
    public static bool TryRegister(string name,out string message)
    {
        name = NormalizeName (name);

        // 入力名が長さ・禁止文字の条件を満たさなければ、保存前に終了する。
        if(!IsValidName(name))
        {
            message = "名前は1～8文字の範囲内で特殊文字を除いて入力してネ！";
            Debug.LogWarning($"[Ranking/Register] 名前の入力を拒否しました。文字数={name.Length}");

            return false;
        }

        var data = RankingStore.Load();

        if(Find(data,name) != null)
        {
            message = $"{name}さん！また来ましたね！";
            Debug.Log($"[Ranking/Register] 登録済みの名前「{name}」です。新規追加はしません。");

            return false;
        }

        // coinとroundは初期値0。Reserved=falseのため、GetRankingではまだ表示されない。
        data.user.Add(new RankingData
        {
            userName = name,
            Reserved = false
        });

        //新しいプレイヤーを追加した一覧を保存する
        RankingStore.Save(data);

        //登録の完了メッセージを返す
        message = $"{name}さん！ようこそうらの世界へ、、";
        Debug.Log($"[Ranking/Register] 名前「{name}」を保存しました。プレイ記録は未登録です。");

        //登録が成功したことを返す
        return true;
    }

    /// <summary>
    /// 登録済みの名前に、今回の最終所持コインと到達ラウンドを提出する。
    /// 初回、コイン増加、または同コインでラウンド増加の場合だけ自己ベストを更新する。
    /// 例: 100コイン・10ラウンドより、120コイン・3ラウンドを上位として保存する。
    /// コインとラウンドは必ず同じプレイの組で保持し、それぞれの最大値は合成しない。
    /// 保存に失敗した場合の例外は、呼び出した画面側が処理する。
    /// </summary>
    /// <returns>正常に受け付ければtrue。自己ベスト未更新でもtrue。不正値・未登録名はfalse。</returns>
    public static bool Submit(string name, long coin, ulong round, out string message)
    {
        //コイン数が負、またはラウンドが0の場合は記録しない
        if (coin < 0 || round == 0)
        {
            message = "コイン数またはラウンド数が不正です。";
            Debug.LogWarning($"[Ranking/Submit] 不正な記録を拒否しました。coin={coin}, round={round}");
            return false;
        }

        //保存済みのランキングを読み込む
        var data = RankingStore.Load();

        // 名前を整えて保存済みの登録を探す。ここで新しい名前を作ることはない。
        var entry = Find(data, NormalizeName(name));

        //指定した名前が登録されていないか確認する
        if (entry == null)
        {
            message = "未登録";
            Debug.LogWarning("[Ranking/Submit] 名前が未登録のため、記録を保存しませんでした。");

            return false;
        }

        //プレイ記録がなかったら、今回が最初の記録になる
        bool isFirstRecord = !entry.Reserved;
        Debug.Log($"[Ranking/Submit] 名前「{entry.userName}」: 今回={coin}コイン/{round}ラウンド, 保存済み={entry.coin}コイン/{entry.round}ラウンド, 初回={isFirstRecord}");

        // 比較の優先順は「初回登録」「コイン数」「同額ならラウンド数」。
        // 以前よりコインが少ない場合、ラウンドが多くても更新しない。

        bool isBetter =
            isFirstRecord || // 初めてのプレイ記録なら無条件で登録する。
             coin > entry.coin || // 以前よりコイン数が多ければ更新する。
            (coin == entry.coin && round > entry.round); // 同額ならラウンド数で比較する。

        if (!isBetter)
        {
            message = "以前の自己ベストを維持しました。";
            Debug.Log("[Ranking/Submit] 更新条件を満たさないため、以前の記録を維持しました。");
            // 更新不要という正常な結果なので、trueを返す。
            return true;
        }

        // 自己ベストのコイン数を、今回のプレイ終了時の所持コイン数に置き換える。
        entry.coin = coin;

        // 到達ラウンドも、今回と同じプレイの値に置き換える。
        entry.round = round;

        // プレイ結果が登録済みであることを記録する。
        entry.Reserved = true;

        // 更新された記録を含む、ランキング全体を保存する。
        RankingStore.Save(data);

        // 最初の記録かどうかで、表示するメッセージを切り替える。
        message = isFirstRecord
            ? "最初の記録を登録しました！" // 最初の記録だった場合のメッセージ。
            : "自己ベストを更新しました！"; // 以前の記録を上回った場合のメッセージ。

        Debug.Log($"[Ranking/Submit] 保存完了: {message}");

        // 記録の登録・更新に成功したことを返す。
        return true;
    }

    /// <summary>
    /// 成績登録済みの人を「コインの多い順、同額ならラウンドの多い順」で返す。
    /// 両方同じ場合は名前順で表示順を固定する。ここでは保存データを書き換えない。
    /// 全件を返し、表示件数の制限や1位・2位などの番号は画面側が決める。
    /// </summary>
    public static List<RankingData> GetRanking()
    {
        var data = RankingStore.Load();

        // 名前だけ登録した人を除き、表示用の新しいリストを作る。
        var ranking = data.user.FindAll(
            entry => entry != null && entry.Reserved);

        // 2件ずつ比較するルールを指定して、一覧を並べ替える。
        ranking.Sort((a, b) =>
        {
            // bとaの順に比較し、コイン数が多い人を先にする。
            int coinOrder = b.coin.CompareTo(a.coin);

            // コイン数に差があれば、その比較結果で順番を決める。
            if (coinOrder != 0) return coinOrder;

            // コイン数が同じなら、到達ラウンドが高い人を先にする。
            int roundOrder = b.round.CompareTo(a.round);

            // ラウンド数に差があれば、その比較結果で順番を決める。
            if (roundOrder != 0) return roundOrder;

            // 両方同じ場合は、一覧の表示順を名前順に固定する。
            return string.Compare(
                a.userName, // 比較する1人目の名前。
                b.userName, // 比較する2人目の名前。
                StringComparison.OrdinalIgnoreCase); // 英字の大文字・小文字は区別しない。
        });
        
        Debug.Log($"[Ranking/List] 記録がある{ranking.Count}人を順位順で取得しました。");
        return ranking;
    }
}
