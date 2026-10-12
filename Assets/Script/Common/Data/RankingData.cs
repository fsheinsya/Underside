using System; 
using System.Collections.Generic;

[Serializable]
public class RankingData
{
    //ユーザー名
    public string userName;
    //コイン
    public long coin;
    //ラウンド
    public ulong round;
    //プレイ結果が登録済みかどうか
    public bool Reserved;
}

[Serializable]
public class RankingDataList
{
    //ユーザーデータをリスト化をする
    public List<RankingData> user =  new List<RankingData>();
}

