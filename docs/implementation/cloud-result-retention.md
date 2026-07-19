# クラウド結果保持台帳

## 目的

クラウド側の不要リソースを整理するときに、残すべき学習結果を誤って削除しないための
保持台帳である。Cloud Run、Firestore、GCS のいずれか一箇所だけを見ると
結果の対応関係を取り違えるため、削除前にはこの文書を確認する。

`cloud-result-retention.json` の `active_retention` を、削除対象から除外する対象の
機械可読な正本とする。この文書は判断理由と確認履歴を人間向けに記録する。
`released_retention` と「保持解除履歴」に移した submission は保持対象ではなく、
過去の存在や現在の復元可能性を保証しない。Cloud Run execution metadata や logs は
クラウド側の保持期間に左右される可能性があるため、active な長期保持対象では
Firestore と GCS artifact を正本として扱う。

## 運用ルール

- 削除作業の前に、Firestore `submissions`、Firestore `results`、Firestore
  `job_debug`、GCS `results/<submission_id>/`、Cloud Run executions を横断して確認する。
- `cloud-result-retention.json` の `active_retention` に載っている `submission_id` と
  一致する Firestore document、GCS prefix、Cloud Run execution は削除しない。
- `job_debug` document が存在しない場合は、存在しないことも確認結果として記録する。
- 新しく保持対象を追加するときは、JSON の `active_retention` とこの文書の
  「現在の保持対象」の両方へ、確認日、保持理由、結果 summary、artifact path を追記する。
- 保持対象を解除する場合は、削除する前にこの文書から消すのではなく、解除理由と解除日を
  追記し、JSON とこの文書の両方で active から released へ移してから削除作業へ進む。

## 現在の保持対象

現在の保持対象はない。`cloud-result-retention.json` の `active_retention` も空である。

## 保持解除履歴

### RETAIN-2026-06-29-latest-successful-navigation

| 項目 | 値 |
| --- | --- |
| 保持理由 | Replay / WebSocket / D3D11 調査時点で参照する最新の成功済みクラウド学習結果 |
| 確認日 | 2026-07-01 |
| GCP project | `rosy-hangout-261702` |
| Firestore database | `eldb` |
| GCS bucket | `gs://embodiedlab-models` |
| Cloud Run job | `embodiedlab-trainer` |
| Region | `asia-northeast1` |
| Cloud Run execution | `embodiedlab-trainer-fch9d` |
| Submission ID | `274301e2-76f9-4482-9bc7-933147b82320` |
| Scenario ID | `navigation_default` |
| Status | `completed` / 2026-07-01 保持解除済み・削除可 |

### 解除理由

2026-07-01 に `simple_robot.v1` / `robot.radius` 契約へ移行する方針に変更した。
この結果は `simple_robot.v0` 相当の点ロボット collision 判定で生成されたため、
EnvForge / EmbodiedLab の最新仕様確認には使わない。コード修正とデプロイ後、
Firestore document、GCS prefix、Cloud Run execution は削除対象として扱う。

### 2026-07-19 現在状態確認

- Firestore `submissions` / `results` / `job_debug` document はすべて `404 Not Found`。
- GCS prefix は live object、7 日 soft-delete 対象 object のどちらも存在しない。
- Cloud Run execution `embodiedlab-trainer-fch9d` は存在しない。
- submission ID を含む現在参照可能な Cloud Logging entry は存在しない。

これは 2026-07-01 の保持解除と整合する。復元対象の active retention ではないため、
artifact の再生成、クラウド再デプロイ、残存リソースの変更は行わない。

### 実行情報

| 項目 | 値 |
| --- | --- |
| Execution created | `2026-06-29T10:19:35.120532Z` |
| Execution started | `2026-06-29T10:19:39.295339Z` |
| Execution completed | `2026-06-29T13:37:06.192355Z` |
| Completion message | `Execution completed successfully in 3h17m26.89s.` |
| Succeeded count | `1` |
| Image digest | `asia-northeast1-docker.pkg.dev/rosy-hangout-261702/embodiedlab/trainer@sha256:e35e24216365dfa24ff51314253b83d7d9695cbeb12c1c622ad93417880e7925` |
| Resources | `cpu=4`, `memory=4Gi` |
| Service account | `embodiedlab-runtime@rosy-hangout-261702.iam.gserviceaccount.com` |

### Firestore

| Collection | Document ID | 保持状態 |
| --- | --- | --- |
| `submissions` | `274301e2-76f9-4482-9bc7-933147b82320` | Not found on 2026-07-19 / released |
| `results` | `274301e2-76f9-4482-9bc7-933147b82320` | Not found on 2026-07-19 / released |
| `job_debug` | `274301e2-76f9-4482-9bc7-933147b82320` | Not found on 2026-07-19 / released |

保持解除時に記録した `results` document の `status` は `completed`、
`progress.phase` は `completed`、
`progress.current_step` / `progress.total_steps` は `3000000` / `3000000` である。

### 結果 summary

| 項目 | 値 |
| --- | --- |
| Runtime | `continuous_navigation` |
| Policy | `ppo` |
| Training timesteps | `3000000` |
| Training seed | `10` |
| Success rate | `0.9` |
| Evaluation episodes | `20` |
| Average episode steps | `187.55` |
| Average episode reward | `-160.16549981711432` |
| Obstacle count | `12` |
| Robot start | `x=-15`, `z=-11`, `rotation_y_degrees=45.000003814697266` |
| Goal | `x=14.399999618530273`, `z=10.399999618530273`, `radius=1.2000000476837158` |

### Training / scenario

| 項目 | 値 |
| --- | --- |
| Algorithm | `ppo` |
| CPU count | `4` |
| Number of envs | `4` |
| Torch threads | `2` |
| Max episode steps | `1000` |
| Eval episodes | `20` |
| PPO n_steps | `512` |
| PPO batch_size | `64` |
| PPO n_epochs | `3` |
| Gamma | `0.9900000095367432` |
| Learning rate | `0.0003000000142492354` |
| Entropy coefficient | `0.0005000000237487257` |
| World bounds | `min=(-16, -12)`, `max=(16, 12)` |
| Front camera | `112x84`, `vertical_fov_degrees=70`, `mount_height_meters=0.6000000238418579` |
| Front distance range | `5` |

### Artifact

保持解除前に使っていた履歴上の GCS prefix は以下である。現在の保持対象ではなく、
2026-07-19 時点で live / soft-deleted object は存在しない。

    gs://embodiedlab-models/results/274301e2-76f9-4482-9bc7-933147b82320/

主要 artifact は以下である。

| 種別 | GCS path | 備考 |
| --- | --- | --- |
| ONNX model | `results/274301e2-76f9-4482-9bc7-933147b82320/model/policy.onnx` | Canonical model artifact |
| Sentis ONNX model | `results/274301e2-76f9-4482-9bc7-933147b82320/model/policy.sentis.onnx` | Unity Sentis target artifact |
| Model archive | `results/274301e2-76f9-4482-9bc7-933147b82320/model/policy.zip` | Trainer model archive |
| Replay manifest | `results/274301e2-76f9-4482-9bc7-933147b82320/replay/manifest.json` | Replay Bundle entry point |
| Eval replay chunks | `results/274301e2-76f9-4482-9bc7-933147b82320/replay/eval/checkpoint_01000000.jsonl.gz` ほか | `01000000`, `02000000`, `03000000` |
| Train replay chunks | `results/274301e2-76f9-4482-9bc7-933147b82320/replay/train/chunk_000000.jsonl.gz` ほか | `chunk_000000` から `chunk_000300` までを確認 |

### 互換性情報

| 項目 | 値 |
| --- | --- |
| Scenario schema version | `scenario-bundle.v0` |
| Robot version | `simple_robot.v0` |
| Sensor version | `basic_sensors.v0` |
| EnvForge minimum version | `0.1.0` |
| Observation layout | `front_camera`, `front_distance` |
| Action layout | `forward`, `turn` |
| Sentis input | `observation`, shape `[1, 28226]`, dtype `float32` |
| Sentis output | `action`, layout `forward`, `turn` |
| Action mapping | `forward=sigmoid(policy_forward)`, `turn=clip(policy_turn, -3, 3) / 3` |

## 削除前チェックリスト

削除作業を行う前に、少なくとも以下を確認する。

- `cloud-result-retention.json` を読み、削除候補の submission が `active_retention` にない。
- active retention がある場合、対応する Firestore `submissions` / `results` / `job_debug`、
  GCS prefix、Cloud Run execution を削除対象から除外した。
- released retention を削除するときも、Unity の job history や Replay 画面など、現在の
  利用箇所が残っていないことを別途確認した。

## 管理方法

`cloud-result-retention.json` は、`retention_id`、`submission_id`、`gcs_prefix`、
`firestore_documents`、`cloud_run_execution` と active / released の区分を持つ。
この文書は同じ `retention_id` を対応する見出しに置き、人間向けの説明と判断理由を残す。
自動 test は JSON 内の ID 重複、active / released の混在、この文書の見出しとの不一致を拒否する。
