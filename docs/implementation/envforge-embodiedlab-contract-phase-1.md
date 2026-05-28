# EnvForge / EmbodiedLab 契約設計 Phase 1

## 目的

このフェーズの目的は、EnvForge と EmbodiedLab の接続を
コードの密結合ではなく、明示的なデータ契約として定義することである。

Phase 0 の基盤整備は完了扱いとし、ここからは以下の 3 つを
設計対象にする。

- Scenario Bundle
- Result Bundle
- Replay Log

## 前提

EnvForge は、ユーザが環境を作成し、学習結果を再生する
Unity アプリケーションである。

EmbodiedLab は、Scenario Bundle を受け取り、クラウド上で
学習を実行し、Result Bundle と Replay Log を返す。

EmbodiedLab は Unity や ML-Agents を必ずしも使わない。
クラウド向きの学習環境で、EnvForge のシナリオ条件を
意味的に再現できることを優先する。

## Scenario Bundle 初期案

Scenario Bundle は、ユーザが EnvForge 上で作成した
学習シナリオを表す。

初期版では以下を含める。

- schema version
- EnvForge binary compatibility
- world size または座標系情報
- static walls
- static obstacles
- goal
- robot start pose
- robot type
- sensor spec
- reward spec
- training spec
- seed

当面、障害物は静的オブジェクトに限定する。
人間や動的障害物は schema に拡張余地を残すが、
初期実装の対象にはしない。

## Reward Spec 初期案

報酬体系は任意コードではなく、宣言的な component の集合にする。

初期候補は以下である。

- goal reached reward
- goal progress reward
- collision penalty
- obstacle proximity penalty
- step penalty
- stuck penalty
- zone entry reward or penalty

各 component は、名前、重み、有効条件、対象オブジェクト、
必要なら閾値を持つ。

## Result Bundle 初期案

Result Bundle は、EmbodiedLab が返す学習結果を表す。

初期版では以下を含める。

- result schema version
- scenario id
- job id
- status
- model artifact location
- model format
- training summary
- evaluation summary
- replay log location
- error report
- compatibility metadata

compatibility metadata には、少なくとも以下を含める。

- scenario schema version
- robot version
- sensor version
- action layout
- observation layout
- EnvForge binary compatibility

## Replay Log 初期案

Replay Log は動画ではなく、EnvForge がローカル再生できる
構造化ログとする。

初期版では以下を含める。

- episode id
- step index
- simulation time
- robot position
- robot rotation
- action
- total reward
- reward component breakdown
- termination reason
- collision events
- compact sensor summaries

初期版では、全ステップの観測画像は必須にしない。
サイズが大きくなりやすいため、必要なら後からオプションとして
追加する。

## Scenario Bundle v0 サンプル

最初の Scenario Bundle は、EnvForge で作った静的なナビゲーション
シナリオを表す。座標系は、EnvForge と EmbodiedLab の間で共有する
右手系の 2D 平面として扱う。`x` と `z` を水平面、`y` を高さとする。
単位は meter とする。

```json
{
  "schema_version": "scenario-bundle.v0",
  "scenario_id": "scenario_demo_001",
  "created_by": {
    "tool": "EnvForge",
    "version": "0.1.0"
  },
  "compatibility": {
    "envforge_min_version": "0.1.0",
    "robot_version": "simple_robot.v0",
    "sensor_version": "basic_sensors.v0"
  },
  "world": {
    "coordinate_system": "envforge_xz_meters",
    "bounds": {
      "min": { "x": 0.0, "z": 0.0 },
      "max": { "x": 10.0, "z": 10.0 }
    },
    "static_walls": [
      {
        "id": "wall_north",
        "center": { "x": 5.0, "z": 10.0 },
        "size": { "x": 10.0, "z": 0.2 },
        "rotation_y_degrees": 0.0
      }
    ],
    "static_obstacles": [
      {
        "id": "box_001",
        "shape": "box",
        "center": { "x": 4.5, "z": 5.0 },
        "size": { "x": 1.0, "z": 1.0 },
        "rotation_y_degrees": 0.0
      }
    ],
    "goal": {
      "id": "goal_001",
      "position": { "x": 8.5, "z": 8.5 },
      "radius": 0.5
    }
  },
  "robot": {
    "type": "simple_robot",
    "start_pose": {
      "position": { "x": 1.0, "z": 1.0 },
      "rotation_y_degrees": 0.0
    },
    "action_space": {
      "type": "continuous",
      "layout": ["forward", "turn"]
    }
  },
  "sensors": [
    {
      "id": "front_camera",
      "type": "forward_camera",
      "width": 84,
      "height": 84,
      "semantic_mode": "traversable_vs_blocked"
    },
    {
      "id": "front_distance",
      "type": "distance_sensor",
      "range_meters": 5.0,
      "direction": "forward"
    }
  ],
  "reward": {
    "components": [
      {
        "name": "goal_reached",
        "type": "terminal_reward",
        "weight": 10.0
      },
      {
        "name": "goal_progress",
        "type": "distance_delta",
        "target": "goal_001",
        "weight": 0.5
      },
      {
        "name": "collision_penalty",
        "type": "collision",
        "weight": -5.0
      },
      {
        "name": "step_penalty",
        "type": "per_step",
        "weight": -0.01
      }
    ]
  },
  "training": {
    "algorithm": "ppo",
    "timesteps": 5000,
    "seed": 10,
    "max_episode_steps": 512
  }
}
```

## Result Bundle v0 サンプル

Result Bundle は、EmbodiedLab が学習完了または失敗時に返す結果である。
成功時は model artifact と Replay Log の場所を含む。失敗時は
`status` を `failed` にし、`error` に診断情報を入れる。

```json
{
  "schema_version": "result-bundle.v0",
  "scenario_id": "scenario_demo_001",
  "job_id": "job_20260528_001",
  "status": "completed",
  "compatibility": {
    "scenario_schema_version": "scenario-bundle.v0",
    "envforge_min_version": "0.1.0",
    "robot_version": "simple_robot.v0",
    "sensor_version": "basic_sensors.v0",
    "action_layout": ["forward", "turn"],
    "observation_layout": [
      "front_camera_semantic",
      "front_distance"
    ]
  },
  "summary": {
    "training_timesteps": 5000,
    "training_seed": 10,
    "success_rate": 0.82,
    "average_episode_reward": 6.4,
    "average_episode_steps": 118.5
  },
  "artifacts": {
    "model": {
      "storage": "gcs",
      "bucket": "embodiedlab-models",
      "path": "results/job_20260528_001/model/policy.onnx",
      "format": "onnx"
    },
    "replay_log": {
      "storage": "gcs",
      "bucket": "embodiedlab-models",
      "path": "results/job_20260528_001/replay/replay.jsonl",
      "format": "jsonl"
    }
  },
  "error": null
}
```

## Replay Log v0 サンプル

Replay Log は、episode と step の列として扱う。初期版では JSON Lines を
候補にする。ここでは読みやすさのため、2 step 分を配列で示す。

```json
[
  {
    "schema_version": "replay-log.v0",
    "scenario_id": "scenario_demo_001",
    "job_id": "job_20260528_001",
    "episode_id": "episode_0001",
    "step_index": 0,
    "time_seconds": 0.0,
    "robot": {
      "position": { "x": 1.0, "z": 1.0 },
      "rotation_y_degrees": 0.0
    },
    "action": {
      "forward": 0.2,
      "turn": 0.0
    },
    "reward": {
      "total": -0.01,
      "components": {
        "step_penalty": -0.01
      }
    },
    "events": [],
    "sensors": {
      "front_distance": 5.0
    },
    "terminated": false,
    "termination_reason": null
  },
  {
    "schema_version": "replay-log.v0",
    "scenario_id": "scenario_demo_001",
    "job_id": "job_20260528_001",
    "episode_id": "episode_0001",
    "step_index": 1,
    "time_seconds": 0.1,
    "robot": {
      "position": { "x": 1.02, "z": 1.0 },
      "rotation_y_degrees": 0.0
    },
    "action": {
      "forward": 0.2,
      "turn": 0.0
    },
    "reward": {
      "total": 0.04,
      "components": {
        "goal_progress": 0.05,
        "step_penalty": -0.01
      }
    },
    "events": [],
    "sensors": {
      "front_distance": 5.0
    },
    "terminated": false,
    "termination_reason": null
  }
]
```

## リポジトリごとの責務

EnvForge が主に責任を持つものは以下である。

- ユーザが理解するシナリオ意味論
- Unity 上の配置、報酬設定、再生体験
- Scenario Bundle の生成
- Replay Log の再生
- モデルとバイナリ互換性の検証

EmbodiedLab が主に責任を持つものは以下である。

- Scenario Bundle の検証
- 学習環境への変換
- 学習ジョブの実行
- 成果物保存
- Result Bundle の生成
- Replay Log の出力

## 次の具体タスク

1. EnvForge 側で Scenario Bundle を生成する場所を決める。
2. EmbodiedLab 側で Scenario Bundle を受け取る API 境界を決める。
3. 契約の versioning 方針を決める。
4. 両リポジトリで契約テストを持つ方法を決める。
5. EmbodiedLab 側で Pydantic model を追加する。
6. Result Bundle と Replay Log の artifact 保存方針を決める。

## 保留事項

- JSON Schema を採用するか、Pydantic model を source of truth にするか。
- 契約定義を片方のリポジトリに置くか、小さな共有パッケージにするか。
- EnvForge 側の Unity データ構造と契約形式の対応表。
- Replay Log の圧縮形式。
- Replay Log の分割単位。
- 大規模シナリオでの座標系と単位。
- モデル形式を ONNX に固定するか。
