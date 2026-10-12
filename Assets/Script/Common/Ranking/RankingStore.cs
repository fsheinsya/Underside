using UnityEngine;

public static class RankingStore
{
    //ランキングデータの識別キー
    private const string SaveKey = "v1";

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
