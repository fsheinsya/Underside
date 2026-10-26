using System;

/// <summary>
/// モードごとの出現候補。enum に追加するだけでは抽選対象にならない。
/// Select で選んだ GameSession.mode を、MonsterFactory 経由で受け取る。
/// モードをここに保存せず、抽選のたびに引数を使うため、前回のモードを持ち越さない。
/// </summary>
public static class MonsterSpawnRules
{
    // 両モードで出現する基本属性。ストーリーの抽選対象はこの配列だけ。
    private static readonly ElementType[] CommonElements =
    {
        ElementType.Fire, // 共通候補：炎。
        ElementType.Water, // 共通候補：水。
        ElementType.Grass // 共通候補：草。
    };

    // ランキングのときだけ、基本属性にこの候補を加えて抽選する。
    private static readonly ElementType[] RankingOnlyElements =
    {
        ElementType.Light, // ランキング追加候補：光。
        ElementType.Dark // ランキング追加候補：闇。
    };

    // ストーリーではこの6種だけを出す。ランキングでも共通で出現する。
    private static readonly PersonalityType[] CommonPersonalities =
    {
        PersonalityType.Bold, // 大胆。
        PersonalityType.Calm, // 冷静。
        PersonalityType.faith, // 信仰。enum名の小文字は既存定義に合わせる。
        PersonalityType.Taunt, // 挑発。
        PersonalityType.Aggressive, // 挑戦。
        PersonalityType.Timid // 臆病。
    };

    // TODOの追加性格はランキング限定。既存enumの番号は変更しない。
    private static readonly PersonalityType[] RankingOnlyPersonalities =
    {
        PersonalityType.Delusional, PersonalityType.Inept, PersonalityType.Stoic, // 中二・無能・克己。
        PersonalityType.Explorer, PersonalityType.Devoted, PersonalityType.Adversity, // 探求・献身・逆境。
        PersonalityType.Unyielding, PersonalityType.Impatient, PersonalityType.Headlong, // 不屈・短気・猛進。
        PersonalityType.Obsessive, PersonalityType.Sincere, PersonalityType.Insidious, // 執念・誠実・陰湿。
        PersonalityType.Adaptive, PersonalityType.Flexible // 適応・柔軟。追加時はFactory・ActionAI・PersonalityRulesも確認する。
    };

    /// <summary>
    /// ストーリーは火・水・草、ランキングはそれに光・闇を加えた候補から1つ返す。
    /// </summary>
    public static ElementType GetRandomElement(ModeType mode)
    {
        return Pick(mode, CommonElements, RankingOnlyElements); // 現在はstoryで各1/3、rankingで各1/5の確率。
    }

    /// <summary>
    /// 共通の6性格を基本とし、ランキングのときだけ登録済みの限定性格を候補に加える。
    /// </summary>
    public static PersonalityType GetRandomPersonality(ModeType mode)
    {
        return Pick(mode, CommonPersonalities, RankingOnlyPersonalities); // 現在はstoryで各1/6、rankingで全20性格が各1/20。
    }

    // ランキングでは共通候補と限定候補を合わせ、各候補を同じ確率で選ぶ。
    // T は属性・性格などの型。共通候補は1件以上必要で、候補の重複登録は避ける。
    private static T Pick<T>(ModeType mode, T[] common, T[] rankingOnly)
    {
        int count; // 抽選対象の総件数。候補配列を結合せず、件数だけを合計する。
        switch (mode) // モードごとに使う候補範囲を決める。
        {
            case ModeType.story:
                // 限定候補の数を含めないため、ストーリーに限定内容が混ざらない。
                count = common.Length; // 共通候補の添字だけが抽選される。
                break;
            case ModeType.ranking:
                // 2つの配列を1つの候補一覧とみなす。結合用の配列は作らない。
                count = common.Length + rankingOnly.Length; // 共通と限定を同じ重みで扱う。先にグループを50%ずつ選ぶ方式ではない。
                break;
            default:
                // 新しいモードを追加したとき、出現ルールの設定漏れを検出する。
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "未対応のゲームモードです。"); // 未設定のモードを黙ってstory扱いにしない。
        }

        // 整数版のRangeは上限を含まないので、0～候補数-1の範囲を抽選する。
        int index = UnityEngine.Random.Range(0, count); // 結合したと仮定した一覧上の位置を1回だけ抽選する。
        // 共通候補の範囲を超えたら、その件数を引いて限定配列の添字へ変換する。
        return index < common.Length ? common[index] : rankingOnly[index - common.Length]; // 共通6件ならindex=6が限定配列の0番目に対応する。
    }
}
