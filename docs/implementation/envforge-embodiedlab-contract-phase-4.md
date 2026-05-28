# EnvForge / EmbodiedLab 契約実装 Phase 4

## 目的

Phase 4 では、学習中のロボット挙動を動画ではなく Replay Log として扱うための
最小契約を実装する。

ユーザが確認したいのは、報酬体系が意図通りに働いているか、失敗時に何が
起きたか、ロボットがどの行動を選んだかである。そのため初期版では、視点固定の
動画ではなく、EnvForge がローカルで再生できる構造化ログを優先する。

## 今回の範囲

今回の最小範囲は以下である。

- Replay Log の canonical JSONL fixture を置く。
- EmbodiedLab 側の `ReplayLogStep` を `JsonUtility` 互換の配列中心 shape にする。
- EnvForge 側に Replay Log DTO と JSONL parser 境界を置く。
- Replay Log artifact は Result Bundle の `artifacts.replay_log` から参照する。

fixture は以下に置く。

    fixtures/replay-logs/navigation_default_replay.jsonl

## JsonUtility 互換の方針

Unity `JsonUtility` は arbitrary dictionary を直接扱いにくい。そのため Replay Log
v0 では、action、reward components、sensor summaries を key/value map ではなく
配列で表す。

代表的な形は以下である。

    "action": {
      "values": [
        { "name": "forward", "value": 0.2 },
        { "name": "turn", "value": 0.0 }
      ]
    }

reward components も同様に、`components` を `{ "name": ..., "value": ... }` の
配列にする。これにより、ユーザ定義 reward component 名が増えても、Unity 側は
固定 DTO で読み込める。

## 保留事項

- Replay Log を実際の trainer rollout から生成する処理。
- EnvForge 上で Replay Log を再生する UI。
- 長い Replay Log の圧縮、分割、部分読み込み。
- collision/contact event の詳細 schema。
- 数値以外の sensor summary を扱う場合の型別 schema。
