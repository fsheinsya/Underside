using UnityEngine;

/// <summary>
/// ランキング全体をJSON文字列にして、PlayerPrefsでこの端末に保存・読み込みする。
/// 順位や自己ベストの条件はRankingServiceが担当し、このクラスは保存方法だけを扱う。
/// ゲーム中だけ情報を持つGameSessionと異なり、保存した記録はアプリ終了後も残る。
/// </summary>
public static class RankingStore
{
    // 全員分の名前・成績をまとめたJSONの保存キー。ユーザーごとのキーではない。
    private const string SaveKey = "v1";

    /// <summary>
    /// 保存済みJSONをデータへ戻す。初回など、キー自体がなければ空の一覧を返す。
    /// 壊れたJSONや不正な一覧は例外で知らせ、正常な空データとは区別する。
    /// </summary>
    public static RankingDataList Load()
    {
        //ユーザーデータが空だったら、空のデータを返す
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            Debug.Log($"[Ranking/Store] 保存キー「{SaveKey}」は未作成です。空の一覧を返します。");
            return new RankingDataList();
        }

        Debug.Log($"[Ranking/Store] 保存キー「{SaveKey}」を読み込みます。");
        string json = PlayerPrefs.GetString(SaveKey);

        //json文字列をRankingDataList型のデータへ変換をする
        var data = JsonUtility.FromJson<RankingDataList>(json);

        // 変換結果、または記録一覧がnullになっていないか確認する。
        if (data == null || data.user == null)
        {
            // 正常に読み込めなかったことを例外として呼び出し元へ伝える。
            throw new System.InvalidOperationException( "ランキングデータを読み込めませんでした。"); 
        }

        Debug.Log($"[Ranking/Store] 読み込み完了: 登録者数={data.user.Count}");
        return data;
    }

    /// <summary>
    /// 渡された一覧全体で保存内容を置き換える。1人分の追記処理ではない。
    /// ServiceがLoadした一覧を更新してから渡すことで、ほかの人の登録も残す。
    /// </summary>
    public static void Save(RankingDataList data)
    {
        //jsonデータに変換
        string json = JsonUtility.ToJson(data);

        //指定したキーに文字列を指定をする
        Debug.Log($"[Ranking/Store] 保存開始: キー={SaveKey}, 登録者数={data.user.Count}, JSON文字数={json.Length}");
        PlayerPrefs.SetString(SaveKey, json);

        //PlayerPrefsに設定をした変更内容を保存先に書き込む
        PlayerPrefs.Save();
        Debug.Log("[Ranking/Store] PlayerPrefs.Save()が完了しました。");
    }

    // ランキングの保存キーだけを削除する。設定や他のセーブデータは残す。
    public static void DeleteAllRankingData()
    {
        PlayerPrefs.DeleteKey(SaveKey); // この一覧に含まれる全プレイヤー名・記録を削除する。
        PlayerPrefs.Save(); // キーの削除を保存先へ反映する。
        Debug.Log("[Ranking/Store] ランキングデータを全件削除しました。");
    }
}
