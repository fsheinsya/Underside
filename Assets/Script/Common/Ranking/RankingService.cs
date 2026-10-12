using UnityEngine;
using System; 
using System.Collections.Generic;

public class RankingService : MonoBehaviour
{
    //ユーザー名の空白をなくす
   private static string NormalizeName(string name)
    {
        return (name ??  string.Empty).Trim();
    }

    //ユーザー名が登録ルールに合っているか確認する(合っていなければ禁止をする)
    private static bool IsValidName(string name)
    {
        //名前が空の場合、または100文字を超えたら拒否をする
        if(name.Length == 0 || name.Length > 8) { 
            return false;
        }

        foreach(char c in name)
        {
            if(char.IsControl(c) || c == '<' || c == '>')
            {
                return false;
            }
        }

        return true;
    }

    //名前を検索をする
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

    //指定した名前が登録をしているかを調べる
    public static bool Exists(string name)
    {
        name = NormalizeName(name);

        return Find(RankingStore.Load(), name) != null;
    }

    //新しいユーザー名を登録をする
    public static bool TryRegister(string name,out string message)
    {
        name = NormalizeName (name);

        //もし禁止されているメッセージを検知をしたら
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

    //プレイ結果を受け取る
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

        //名前の前後の空白を除き、名前の記録を残す
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

        ///
        /// 記録更新のフラグの設定
        ///

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

        // 自己ベストのコイン数を、今回の獲得コイン数に置き換える。
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

    //コイン数の降順、同額なら到達ラウンドの降順に並べる
    public static List<RankingData> GetRanking()
    {
        var data = RankingStore.Load();

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
