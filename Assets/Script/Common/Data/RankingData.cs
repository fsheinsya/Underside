using System; 
using System.Collections.Generic;

/// <summary>
/// 登録した名前と、その名前の自己ベスト1回分を保存するデータ。
/// 名前登録時は Reserved=false、成績を初めて保存すると true になる。
/// RankingStore がこのデータをJSONに変換し、端末へ保存する。
/// </summary>
[Serializable]
public class RankingData
{
    // プレイヤーを探すための名前。RankingServiceでは英字の大小を区別せず照合する。
    public string userName;
    // 自己ベストとなったプレイ終了時の所持コイン。累計獲得額ではない。
    public long coin;
    // 上のコインを記録した同じプレイの到達ラウンド。単独の最高ラウンドではない。
    public ulong round;
    // trueなら成績登録済みで順位表示の対象。falseなら名前だけ登録済み。
    public bool Reserved;
}

/// <summary>
/// 全プレイヤーの登録情報をまとめ、JSONの保存単位にする入れ物。
/// userには名前だけ登録済みの人も含まれる。順位表示用の抽出はServiceが行う。
/// </summary>
[Serializable]
public class RankingDataList
{
    //ユーザーデータをリスト化をする
    public List<RankingData> user =  new List<RankingData>();
}

