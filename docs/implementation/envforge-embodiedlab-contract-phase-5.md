# EnvForge / EmbodiedLab 契約実装 Phase 5

## 目的

Phase 5 では、EmbodiedLab が生成した学習結果を EnvForge 側で取得し、
人間が Unity 上で挙動を確認できる最小導線を実装する。

人間が最初に確認したいのは、学習済みモデルを完全に製品利用することではなく、
自分が設定した環境と報酬体系に対して、ロボットがどのように動き、どこで失敗し、
どの reward component が効いたかである。そのため、初期版では実推論よりも
Replay Bundle の取得とローカル再生を優先する。

## 今回の範囲

今回の最小範囲は以下である。

- Result Document の top-level `artifacts` と `result_bundle.artifacts` を読める
  DTO を用意する。
- `gcs` artifact location から public GCS URL を作る。
- Replay Bundle artifact を download して、manifest と gzip JSONL chunk を parse する。
- Replay Bundle を既存の Navigation scene 上で再生する。
- 通常 ONNX を local file として保存する。
- Replay Bundle がない状態では demo replay へ暗黙 fallback せず、取得が必要な状態を
  明示する。

Unity 側の最小 UI は IMGUI overlay とし、以下の操作を提供する。

- `Submit + Train`
- `Download Replay`
- `Download Model`
- `Replay`

`Job details` では、どの Scenario Bundle / training / reward 設定で
ジョブを送信したか、現在取得している学習 status がどの
`submission_id` / `scenario_id` に対応するか、ロード済み Replay Bundle が
どの `job_id` / `scenario_id` / episode / step 数に対応するかを分けて表示する。
Replay step 自体は詳細な学習設定を含まないため、同一セッションで送信した
job と一致する場合は送信時の設定要約を併記し、demo や外部由来の replay は
設定の由来を明示する。
現行の主経路では bundled demo replay は保持せず、Replay は job history に紐づく
Replay Bundle manifest と chunk を正本として扱う。

API settings asset が未割り当ての場合は、`http://localhost:8000` を fallback の
API base URL として使う。これにより、ローカルで EmbodiedLab API を起動した状態なら
追加 asset を作らなくても疎通確認できる。

## Result artifact の扱い

EmbodiedLab は trainer 完了時に以下を返す。

    results/<submission_id>/model/policy.zip
    results/<submission_id>/model/policy.onnx
    results/<submission_id>/replay/replay.jsonl

EnvForge は replay を直接再生に使う。ONNX artifact は
`Application.persistentDataPath/EnvForge/<submission_id>/` に保存する。
ONNX Runtime CPU native plugin による実推論を Unity runtime に組み込み、
download 済みの `policy.onnx` を直接ロードする。これにより、Sentis / Unity 専用 cache
を経由せず、ONNX を正本として扱える。

## Job history の最小仕様

Unity を終了しても成果物を後から回収できるように、EnvForge はローカルに
job history を保存する。保存先は Unity の `Application.persistentDataPath`
配下の `EnvForge/job-history.json` とし、リポジトリやドキュメントに個人環境の
絶対 path は固定しない。

初期版の job record は以下を持つ。

- `submission_id`
- `scenario_id`
- `submitted_at_utc`
- `status`
- `trainer_summary`
- `training_timesteps`
- `training_seed`
- `progress_phase`
- `progress_current_step`
- `progress_total_steps`
- replay / ONNX artifact metadata
- local replay path
- local ONNX path

EnvForge は以下のタイミングで job history を更新する。

- Submit 成功時に `submission_id`、scenario、training summary を保存する。
- WebSocket result stream 受信時に status、progress、artifact metadata を保存する。
- Replay artifact を download したら、ローカル保存 path を保存する。
- ONNX artifact を download したら、ローカル保存 path を保存する。

Replay artifact は、直接メモリに読み込むだけでなく、
`EnvForge/<submission_id>/replay/replay.jsonl` に保存してから再生する。
これにより、Unity を閉じた後でも replay file を確認できる。

## 学習済みモデル推論モードの初期仕様

学習済みモデルの正本は `policy.onnx` とする。Unity / EnvForge 専用形式は、
必要になった場合も正本ではなく派生 cache として扱う。これにより、学習済みモデルを
Unity 以外の実行環境、ロボット実機、サーバ推論、検証スクリプトでも同じ契約で扱う。

EnvForge 側の推論モードは、job history に保存された local ONNX path を
入力として扱う。右上 HUD の `AI` ボタンで推論モードを開始・停止し、
開始時には手動入力 controller を無効化する。`policy.onnx` は修士論文の
「図 2.3 行動モデルのネットワーク構造」に合わせ、`obs_0` として
`batch x 3 x 84 x 112` の segmentation image、`obs_1` として
`batch x 2` の `[angle, distance]` numeric observation を受け取る。
EnvForge は segmentation camera の readback と navigation metrics からこれらを作り、
モデル出力の 2 連続値を `AgentMotor` に渡す。推論エラーの全文は Unity Console に出し、
HUD には短い失敗表示と Job details への導線だけを出す。

この導線では ML-Agents trainer へ接続しない。EnvForge の現行 Unity runtime からは
ML-Agents 依存を削除し、学習は EmbodiedLab 側、ローカル推論は ONNX Runtime 側の
責務として分離する。

Unity Player / バイナリ配布版でもユーザーが追加 install なしで ONNX 推論できるよう、
初期方針では Windows x86_64 向け CPU 版 ONNX Runtime の native plugin を
EnvForge バイナリへ同梱する。
現時点ではナビゲーション policy の推論負荷は CPU 実行で実用上問題ないと判断するが、
これは暫定判断であり、後日再度検討する。再検討条件は、CPU 推論の frame time が
UI / 操作感へ影響する場合、より大きな visual policy を扱う場合、複数 agent を
同時推論する場合、または GPU 版 ONNX Runtime / Unity Inference Engine cache の
配布コストに見合う性能差が確認された場合である。
Windows arm64 版 ONNX Runtime は、この環境の PowerShell ホスト上で native load が
クラッシュしたため、今回の同梱対象から外す。arm64 player を対象にする場合は、
Unity Player 上で別途検証してから追加する。

## Replay 再生の扱い

Replay Bundle を読み込んだ時点で、対象 agent の live control を止める。
具体的には `AgentMotor` を disable し、`Rigidbody` を kinematic にして、
Replay step の位置と Y 回転を authoritative state として扱う。現在の主経路では
ML-Agents の `Agent` / `DecisionRequester` は使わない。

Replay UI は再生、一時停止、停止、1 step 前後移動を提供する。
画面上には現在 step、行動、報酬合計、reward component、終了理由を表示する。

## ユーザー要望ベースの次 TODO

Phase 5 の完了後、または Phase 5 を仕上げながら進める実装 TODO は以下である。

- 学習済みモデルをダウンロード後、そのモデルを用いて EnvForge 上で推論するモードを
- 学習済みモデルをダウンロード後、そのモデルを用いて EnvForge 上で推論するモードの
  動作確認を進める。実装は `policy.onnx` 正本、CPU 版 ONNX Runtime native plugin
  同梱、2 連続値 action の直接実行に寄せた。
- EnvForge の現行 runtime から ML-Agents 依存を削除した。現行の EnvForge /
  EmbodiedLab 連携主経路では、学習は EmbodiedLab、推論は ONNX Runtime に寄せる。
- HTTP 定期取得をやめ、Pub/Sub 型の通知で学習 status と成果物準備完了を受け取る。
  EmbodiedLab trainer は status transition ごとに Pub/Sub event を publish し、
  notification service が `/ws/results/{submission_id}` で EnvForge に fan-out する。
  EnvForge は `ApiSettings` または scene 設定の WebSocket URL template に接続し、
  受信した Result Document を job history に保存する。API service と notification
  service は別 URL になり得るため、HTTP base URL から WebSocket URL は推測しない。
- Unity を終了しても、学習済みモデル、Replay Bundle、学習ログを後から回収できるように
  job history と artifact 回収導線を用意する。
- Replay 表示欄に reward、終了理由、学習概要を読みやすく表示する。表示欄が
  切れないよう、Replay overlay と Job details の縦幅も調整する。
- 細かい HUD / UI の見た目は後日まとめて再設計する。今回の暫定対応では、右上の
  Cloud panel、左下の Replay panel、`Job details` の情報は読める状態を優先し、
  余白、行揃え、ボタン名、色、折り返し、下端はみ出しを継続改善 TODO とする。
- 右上のクラウド操作パネルのデザインを再考する。現状は情報が見えることを優先した
  暫定 HUD であり、見た目、余白、ボタン配置、状態表示の視線誘導を改めて設計する。
- 右上のクラウド操作パネルと左下の Replay パネルのフォントサイズを再設計する。
  少なくとも左上の既存情報パネルより小さくならないようにし、画面全体の情報階層を
  揃える。
- 右上のクラウド操作パネルは、通常時は環境観察を邪魔しにくい compact HUD とし、
  `Settings` / `Job` を開いた時だけ詳細操作パネルを表示する方針へ寄せた。
  詳細表示は `Esc` で畳めるようにし、クラウド UI 全体は `F10` で表示切り替えできる。
- 左下の Replay overlay も同じ方針で、通常時は再生操作だけを残した compact bar とし、
  reward component や action などの詳細は `Details` を開いた時だけ表示する。
  Replay 詳細は `Esc` で畳めるようにし、Replay UI 全体は `F9` で表示切り替えできる。
- job history の表示 UI を再設計する。現状は `Job details` に保存件数と latest job
  の artifact readiness を最小表示するだけなので、後続で job 一覧、選択、再取得、
  local artifact の開き直しを扱える形にする。
- 推論 action の契約を前進専用に保つ。EmbodiedLab 側の action space / replay /
  exported ONNX は `forward: 0..1`、`turn: -1..1` に揃える。EnvForge 側は
  推論モデルの出力を原則そのまま実行し、契約外の負の `forward` は UI に警告として
  表示する。負の `forward` が出る古いモデルは再学習対象とする。
- MVP preset は修士論文「表 3.1 報酬設計」に合わせる。目的地到達 +100、
  壁衝突 -50、action ごと -0.01、目的地に近づいた場合 +0.1、目的地方向角が
  ±90 度を超える場合 -0.1、±150 度を超える場合 -5.0、`v` が 0 かつ
  `omega` が -0.3..0.3 の場合 -0.1 とする。
- 壁パーツをユーザーが自由に立てられるようにし、その配置を Scenario Bundle に
  反映する。
- 初期地形は緑の平面と青い境界壁だけにし、固定の内壁は主経路から外す。
  平面サイズは World panel で変更でき、境界壁は平面サイズに追従する。
  ユーザーが追加した青い壁は `static_walls` として Scenario Bundle に反映する。
  ロボットの表示色は床、壁、ゴールと判別しやすいマゼンタ系に変更する。
- `simple_robot.v1` では Scenario Bundle の `robot.radius` をロボットの x/z 平面上の
  円形 footprint 半径として扱う。EnvForge は Unity の `CapsuleCollider.radius` と
  Scenario Bundle の `robot.radius` を同じ値から生成し、EmbodiedLab は移動 collision
  判定で壁・障害物をこの半径ぶん膨張させる。これにより、EnvForge 上で見える接触と
  EmbodiedLab の collision / failure 判定を同じ契約に揃える。
- 無人の実モデル確認用に、Unity Player 起動引数から複数エピソード推論評価を
  実行できる入口を追加する。`-envforgeEvaluateModel` で ONNX を指定し、
  `-envforgeEvaluationOutput` に JSON 結果を書き出す。評価中は通常の
  live controller ではなく評価 runner が Goal / collision event を受け取り、
  成功率、衝突数、timeout 数、平均 step を記録する。
- EnvForge 生成の Scenario Bundle を使って無人で EmbodiedLab に投入するため、
  Unity Player 起動引数 `-envforgeSubmitJob` を追加する。`-envforgeTrainingPreset mvp`
  と `-envforgeWorldVariant default/random-small/random-medium/random-large` を使い、
  本番テスト設定とランダム環境の投入を人間操作なしで再現できるようにする。
- コードを現行の主経路に合わせてきれいにシンプルにする。過去の MVP や旧実験の
  遺産は、必要なら履歴として参照し、現行 runtime の複雑さとして残さない。
- テストも同じ方針で整理し、Scenario Bundle、Result Bundle、Replay、artifact 回収、
  推論モードに沿った構成へ寄せる。
- Replay は単一 `replay_log` artifact ではなく Replay Bundle を主契約にする。
  `replay/manifest.json` が `train` / `eval` の gzip JSONL chunk を列挙し、
  EnvForge は manifest と chunk を取得してローカルに結合したうえで ReplayPlayer
  に渡す。学習中の stochastic trajectory と、評価 checkpoint の deterministic
  trajectory を同じ bundle 内で扱う。

優先順は以下を基本とする。

1. Replay / Job details 表示の縦幅と情報表示を調整する。
2. Unity 終了後も成果物を回収できる job history / artifact 回収導線を用意する。
3. 学習済みモデル推論モードを ONNX Runtime 前提で実機確認し、必要なら入力/出力
   metadata の扱いを EmbodiedLab の export 仕様と揃える。
4. HTTP 定期取得を Pub/Sub 型通知へ置き換える。EnvForge 側は WebSocket result
   stream を主経路とし、手動更新 UI は持たない。
5. 壁パーツ編集を実装する。
6. コードとテストを、現行主経路に合わせて段階的に整理する。

## 保留事項

- CPU 版 ONNX Runtime native plugin を使った `policy.onnx` 推論の実モデルでの
  end-to-end 検証。
- GPU 版 ONNX Runtime、Unity Inference Engine cache、またはその他 runtime への
  切り替えが必要かの再評価。
- 認証つき GCS artifact access。
- WebSocket による自動状態更新。
- `submission_id`、送信時刻、preset、主要な学習設定、status、artifact readiness を
  EnvForge 側に保存するローカル job history。
- Replay Bundle の chunk 選択 UI と streaming load。現時点では取得した chunk を
  ローカル JSONL に結合して再生する。
- Replay step と Scene object id の厳密な対応。
- Cloud run configuration asset の editor tooling。
