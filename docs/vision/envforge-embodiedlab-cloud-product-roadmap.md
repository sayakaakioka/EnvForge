# EnvForge / EmbodiedLab クラウド連携ロードマップ

## 位置づけ

EnvForge は、ユーザがロボット学習用の環境を作成し、学習結果を
手元で確認するための Unity アプリケーションである。

EmbodiedLab は、EnvForge から渡された Scenario Bundle を受け取り、
クラウド上で学習を実行し、モデル、結果サマリ、Replay Bundle を返す
学習実行基盤である。

両者は別リポジトリのまま維持する。Unity の内部実装や Python の内部実装を
直接共有するのではなく、明示的なデータ契約で接続する。

## 現在の判断

EnvForge の現行 runtime は ML-Agents を使わない。学習は EmbodiedLab 側に集約し、
EnvForge は `policy.onnx` を正本としてローカル推論する。

過去の MVP 実装、ローカル ML-Agents trainer、Python 環境、古い学習成果物は
現行仕様を動かすためのコードとして保持しない。必要になった場合は Git 履歴を参照する。

## Product Flow

想定するユーザ体験は以下である。

1. ユーザは EnvForge の Unity バイナリを起動する。
2. 平面サイズ、境界壁、追加壁、スタート地点、ゴール、報酬体系を設定する。
3. EnvForge は設定を Scenario Bundle として保存する。
4. ユーザがクラウド学習ジョブを投入する。
5. Scenario Bundle が EmbodiedLab に送信される。
6. EmbodiedLab はクラウド向きの学習環境に変換して学習する。
7. EmbodiedLab は `policy.onnx`、結果サマリ、Replay Bundle を保存する。
8. EnvForge は Result Bundle と Replay Bundle を取得する。
9. ユーザは EnvForge 上でロボットの挙動を再生し、必要に応じて
   `policy.onnx` を使ったローカル推論を確認する。

## データ契約

主な契約は以下である。

- Scenario Bundle: EnvForge で作った学習シナリオ。
- Result Bundle: EmbodiedLab が返す学習結果、互換性情報、artifact metadata。
- Replay Bundle: 学習中 trajectory と評価 trajectory を含む構造化 replay。

Replay Bundle は動画ではなく構造化ログとして保存する。`manifest.json` が
`train` / `eval` の gzip JSONL chunk を列挙し、EnvForge はそれを読み込んで
視点変更、一時停止、episode 移動を含むローカル再生を行う。

## 現在の優先事項

1. EnvForge の世界編集、Scenario Bundle 生成、クラウド送信導線を安定させる。
2. Result Bundle / Replay Bundle / `policy.onnx` の取得と履歴復元を安定させる。
3. EnvForge 上の Replay UI と Job details を、人間が長時間見ても読みやすい形へ整える。
4. ONNX Runtime によるローカル推論を、EmbodiedLab の artifact contract と揃えて検証する。
5. Replay Bundle の chunk 選択 UI と streaming load を追加する。

## 保留事項

- 認証つき GCS artifact access。
- ジョブキャンセル、再実行、失敗時診断、quota とコスト制御。
- Replay Bundle の巨大 chunk を全結合せずに読む streaming load。
- EnvForge / EmbodiedLab のバージョン互換性の正式表現。
- 複数ロボット、動的障害物、より高忠実度なシミュレーションへの拡張。
