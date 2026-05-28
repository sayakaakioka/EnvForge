# EnvForge / EmbodiedLab 契約実装 Phase 2

## 目的

Phase 2 では、Phase 1 で決めた Scenario Bundle、Result Bundle、Replay Log
を、実装上の主経路として扱える状態に近づける。

この段階の source of truth は EmbodiedLab 側の Pydantic model とする。
EnvForge 側の C# DTO は、その JSON 契約に追従する送受信境界である。

## Phase 2A: 契約同期

EnvForge 側の Scenario Bundle DTO は、EmbodiedLab の `ScenarioBundle` と
同じフィールド名を使う。

今回同期した主な項目は以下である。

- `training.n_steps`
- `training.batch_size`
- `training.gamma`
- `training.learning_rate`
- `training.ent_coef`
- `training.eval_episodes`

座標系は `envforge_xz_meters` とし、Unity の `x` / `z` 平面を meter 単位の
連続座標として扱う。EnvForge の現在のシーンは中心原点の床を使うため、
`world.bounds` は `-half` から `+half` の範囲になる。

## Phase 2B: 文書更新

古い grid-world 前提の API 説明は主経路から外す。

現在の主経路は以下である。

1. EnvForge が Scenario Bundle を生成する。
2. EmbodiedLab の `POST /submissions` が Scenario Bundle を受け取る。
3. EmbodiedLab が学習結果を `result_bundle` として返す。
4. EnvForge が model artifact と Replay Log artifact を取得する。
5. EnvForge が Replay Log をローカルで再生する。

`models/<submission_id>/...` 形式の古い artifact path は、現在の文書では
`results/<submission_id>/model/...` と `results/<submission_id>/replay/...`
に寄せる。

## Phase 2C: Runtime 境界

EnvForge 側には Result Bundle と Result Document の DTO 境界を置く。

Replay Log の `reward.components` や `sensors` は EmbodiedLab 側では辞書として
表現される。Unity の標準 `JsonUtility` は辞書型の JSON を直接扱いにくいため、
今回の Phase 2C では Replay Log DTO を実装しない。Replay Log の読み込みは
次タスクで、小さな専用 parser を `JsonUtility` 前提のまま設計する。

## 次の実装候補

- EnvForge 側で Scenario Bundle の JSON fixture を出力し、EmbodiedLab 側の
  Pydantic model で検証する。
- Result Bundle の互換性メタデータを読んで、EnvForge binary、robot、sensor、
  action layout の不一致を検出する。
- Replay Log JSON Lines を読み、ローカル再生用の内部イベント列へ変換する。
- EmbodiedLab 側で空の Replay Log ではなく、実際のステップログを出力する。
