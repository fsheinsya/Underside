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
        ElementType.Fire,
        ElementType.Water,
        ElementType.Grass
    };

    // ランキングのときだけ、基本属性にこの候補を加えて抽選する。
    private static readonly ElementType[] RankingOnlyElements =
    {
        ElementType.Light,
        ElementType.Dark
    };

    // ストーリーではこの6種だけを出す。ランキングでも共通で出現する。
    private static readonly PersonalityType[] CommonPersonalities =
    {
        PersonalityType.Bold,
        PersonalityType.Calm,
        PersonalityType.faith,
        PersonalityType.Taunt,
        PersonalityType.Aggressive,
        PersonalityType.Timid
    };

    // TODOの追加性格はランキング限定。既存enumの番号は変更しない。
    private static readonly PersonalityType[] RankingOnlyPersonalities =
    {
        PersonalityType.Delusional, PersonalityType.Inept, PersonalityType.Stoic,
        PersonalityType.Explorer, PersonalityType.Devoted, PersonalityType.Adversity,
        PersonalityType.Unyielding, PersonalityType.Impatient, PersonalityType.Headlong,
        PersonalityType.Obsessive, PersonalityType.Sincere, PersonalityType.Insidious,
        PersonalityType.Adaptive, PersonalityType.Flexible
    };

    /// <summary>
    /// ストーリーは火・水・草、ランキングはそれに光・闇を加えた候補から1つ返す。
    /// </summary>
    public static ElementType GetRandomElement(ModeType mode)
    {
        return Pick(mode, CommonElements, RankingOnlyElements);
    }

    /// <summary>
    /// 共通の6性格を基本とし、ランキングのときだけ登録済みの限定性格を候補に加える。
    /// </summary>
    public static PersonalityType GetRandomPersonality(ModeType mode)
    {
        return Pick(mode, CommonPersonalities, RankingOnlyPersonalities);
    }

    // ランキングでは共通候補と限定候補を合わせ、各候補を同じ確率で選ぶ。
    // T は属性・性格などの型。共通候補は1件以上必要で、候補の重複登録は避ける。
    private static T Pick<T>(ModeType mode, T[] common, T[] rankingOnly)
    {
        int count;
        switch (mode)
        {
            case ModeType.story:
                // 限定候補の数を含めないため、ストーリーに限定内容が混ざらない。
                count = common.Length;
                break;
            case ModeType.ranking:
                // 2つの配列を1つの候補一覧とみなす。結合用の配列は作らない。
                count = common.Length + rankingOnly.Length;
                break;
            default:
                // 新しいモードを追加したとき、出現ルールの設定漏れを検出する。
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "未対応のゲームモードです。");
        }

        // 整数版のRangeは上限を含まないので、0～候補数-1の範囲を抽選する。
        int index = UnityEngine.Random.Range(0, count);
        // 共通候補の範囲を超えたら、その件数を引いて限定配列の添字へ変換する。
        return index < common.Length ? common[index] : rankingOnly[index - common.Length];
    }
}
