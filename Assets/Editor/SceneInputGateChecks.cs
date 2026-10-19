using System;
using UnityEditor;
using UnityEngine;

public static class SceneInputGateChecks
{
    [MenuItem("Tools/UnderSide/Check Scene Input Gate")]
    public static void Run()
    {
        var gate = new SceneInputGate();
        Assert(!gate.IsOpen && !gate.TryOpen(100f, 1, false), "演出が完了する前は開かない");

        gate.Schedule(10f, 0.5f);
        Assert(!gate.TryOpen(10.49f, 2, false), "設定した待機時間中は開かない");
        Assert(!gate.TryOpen(10.5f, 3, true), "時間を過ぎても押しっぱなしなら開かない");
        Assert(!gate.TryOpen(15f, 4, true), "長押ししたままでも開かない");
        Assert(!gate.TryOpen(15.01f, 5, false), "離したフレームでは開かない");
        Assert(!gate.TryOpen(15.01f, 5, false), "同じフレームの再呼び出しでも開かない");
        Assert(gate.TryOpen(15.02f, 6, false) && gate.IsOpen, "解放後のフレームで開く");
        Assert(!gate.TryOpen(16f, 7, false) && gate.IsOpen, "解放通知は一度だけ");

        gate.Schedule(20f, 0.5f);
        Assert(!gate.IsOpen, "次の画面では再び閉じる");
        Assert(!gate.TryOpen(20.5f, 10, false), "解放を確認する");
        Assert(!gate.TryOpen(20.51f, 11, true), "解放途中の再押下は受け付けない");
        Assert(!gate.TryOpen(20.52f, 12, false), "再押下の後も解放フレームを待つ");
        Assert(gate.TryOpen(20.53f, 13, false), "再解放後に開く");

        gate.Lock();
        Assert(!gate.IsOpen && !gate.TryOpen(100f, 30, false), "遷移開始・無効化後は勝手に開かない");
        gate.Schedule(100f, 1f);
        gate.Lock();
        Assert(!gate.TryOpen(200f, 40, false), "無効化で予約済みの解放も取り消す");

        gate.Schedule(200f, 0f);
        Assert(!gate.TryOpen(200f, 41, false), "待機0秒でも持ち越しクリックを防ぐ");
        Assert(gate.TryOpen(200f, 42, false), "同じ時刻でもフレームが進めば解放できる");
        Debug.Log("SCENE_INPUT_GATE_CHECKS_PASSED: 17 assertions");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("Scene input gate: " + message);
    }
}
