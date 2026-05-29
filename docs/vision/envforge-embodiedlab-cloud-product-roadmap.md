# EnvForge / EmbodiedLab クラウド連携ロードマップ

## 位置づけ

EnvForge は、ユーザがロボット学習用の環境を作成し、学習結果を
手元で確認するための Unity アプリケーションである。

EmbodiedLab は、EnvForge から渡されたシナリオを受け取り、
クラウド上で学習を実行し、モデル、ログ、リプレイ用データを
返すための学習実行基盤である。

両者は別リポジトリのまま維持する。密結合を避けるため、 Unity の内部実装や Python
の内部実装を直接共有するのではなく、 明示的なデータ契約で接続する。

## 現在の判断

Phase 0 の基盤整備は完了したものとして扱う。

過去の MVP 実装や旧 grid-world 経路は、現行仕様を動かすためのコードとしては
保持しない。必要になった場合は Git 履歴を参照する。

完了済みの内容は以下である。

- EnvForge と EmbodiedLab を別リポジトリのまま扱う方針を確認した。 - EmbodiedLab
  のドキュメント構成を EnvForge に近づけた。 - `AGENTS.md` の役割を整理した。 -
  EmbodiedLab の Python 実行環境を Python 3.13 と `uv` に寄せた。 - EmbodiedLab
  で Python lint、Markdown lint、pytest を `make check` で実行できるようにした。
  - ローカル確認系は `.env` なしで動き、クラウド操作系は
  不足している環境変数を明示して止まるようにした。

## Product Flow

想定するユーザ体験は以下である。

1. ユーザは EnvForge の Unity バイナリを起動する。 2.
   壁、障害物、ゴール、ロボット、報酬体系を設定する。 3. EnvForge は設定を
   Scenario Bundle として保存する。 4. ユーザがクラウド学習ボタンを押す。 5.
   Scenario Bundle が EmbodiedLab に送信される。 6. EmbodiedLab
   はクラウド向きの学習環境に変換して学習する。 7.
   学習済みモデル、結果サマリ、Replay Log を保存する。 8. EnvForge は Result
   Bundle と Replay Log を取得する。 9. ユーザは EnvForge
   上でロボットの挙動を再生し、報酬設計を確認する。

## Phase 1: データ契約の設計

次に取り組む中心課題は、EnvForge と EmbodiedLab の間の
データ契約を決めることである。

最初に定義する契約は以下である。

- Scenario Bundle - Result Bundle - Replay Log

Scenario Bundle は、EnvForge で作った学習シナリオを表す。
初期版では、壁、静的障害物、ゴール、ロボット初期位置、
センサ構成、報酬定義、学習パラメータを含める。

Result Bundle は、EmbodiedLab が返す学習結果を表す。
モデル、学習サマリ、評価結果、エラー情報、Replay Log の場所、
互換性メタデータを含める。

Replay Log は、動画ではなく構造化ログとして保存する。 EnvForge
バイナリがこれを読み、視点変更や一時停止を含む ローカル再生を行う。

## Phase 2: Scenario Bundle 主経路の整合

EmbodiedLab は Scenario Bundle を API 境界として受け取る方針に移行した。
次の段階では、EnvForge 側の DTO、API ドキュメント、Replay Log 受信境界を
EmbodiedLab 側の Pydantic model に追従させる。

ただし、EmbodiedLab が Unity や ML-Agents を実行する必要はない。
重要なのは、EnvForge で定義された学習条件を、
クラウド実行に適した環境で意味的に再現することである。

当面の対象は以下に限定する。

- 静的な壁 - 静的な障害物 - 1 種類のロボット - 正面カメラ 1 台の抽象表現 -
  距離センサ 1 つの抽象表現 - ゴール到達と衝突による終了条件 - 宣言的な reward
  component

Phase 2 の作業順序は以下とする。

1. EmbodiedLab の Pydantic model を source of truth として EnvForge DTO を同期する。
2. 古い grid-world / `models/<submission_id>/` 前提の文書を更新する。
3. Result Bundle と Replay Log の受信・再生境界を EnvForge 側に追加する。

## Phase 3: EnvForge 側のクラウド送信導線

EnvForge 側では、ユーザが作った環境を Scenario Bundle に変換し、 EmbodiedLab API
に送信する導線を作る。

この段階で重要なのは、Unity の内部表現をそのまま送らないことである。 EnvForge
が持つオブジェクト配置や報酬設定を、バージョン付きの
契約形式に変換してから送信する。

Phase 3 の最初の実装は、クラウド送信 UI ではなく Scenario Bundle fixture
の検証導線とする。EnvForge 側に canonical fixture を置き、EmbodiedLab 側の
Pydantic model で同じ JSON を検証することで、HTTP、認証、ジョブ起動の前に
データ契約の一致を確認する。

## Phase 4: リプレイ体験

学習中のロボット挙動は、動画ではなく Replay Log として扱う。

EnvForge は Replay Log を読み込み、以下を表示できるようにする。

- ロボットの位置と向き - 行動 - 報酬合計 - 報酬成分ごとの内訳 - 終了理由 -
  衝突や接触イベント - 必要最小限のセンサ要約

Replay Log v0 は、Unity `JsonUtility` で読めるように、任意 key の object ではなく
`name` / `value` の配列を基本形にする。

ユーザの主目的は、学習済みモデルそのものだけでなく、
報酬体系が意図通りに働いているかを確認することである。

## Phase 5: 人間が試せるクラウド結果導線

Phase 5 の最小到達点は、EmbodiedLab が生成した結果を EnvForge 側で取得し、
ユーザがローカルで Replay Log を再生できることである。

この段階では、学習済みモデルを Sentis で安定推論することまでは必須にしない。
ただし、Result Bundle に含まれる model artifact metadata を読み取り、
通常 ONNX と Sentis ONNX をローカルに保存できる導線は用意する。

今回の最小範囲は以下である。

- EnvForge が Result Document / Result Bundle の artifact metadata を読める。
- EnvForge が GCS 上の Replay Log を download できる。
- EnvForge が Replay Log を Unity シーン上で再生できる。
- EnvForge が通常 ONNX と Sentis ONNX を local artifact として保存できる。
- backend がない状態でも demo Replay Log を読み込んで再生確認できる。

これにより、人間はまず replay で報酬設計とロボット挙動を確認できる。

## Phase 6: 実行基盤の堅牢化

初期連携が動いた後に、本番運用に必要な項目を整える。

- GCS 上の成果物アクセス制御 - ユーザ認証 - ジョブキャンセル - 再実行 -
  失敗時の診断 - quota とコスト制御 - 長時間学習のタイムアウト - Replay Log
  の圧縮と分割 - モデル互換性の検証

## Phase 7: 将来拡張

初期版の後に検討する拡張は以下である。

- 動的障害物 - 人間のような移動体 - 複数ロボット - センサ構成の拡張 -
  カリキュラム学習 - 複数アルゴリズム - より高忠実度なシミュレーション -
  EnvForge 側での評価・比較 UI

## 保留事項

- Scenario Bundle の正式な schema 記述形式 - 契約の source of truth
  をどちらのリポジトリに置くか - EmbodiedLab 側で `/v1` と `/v2` を分けるか -
  Replay Log のサイズ管理 - モデル形式と Unity 側ロード方式 -
  報酬成分の最小セット - EnvForge と EmbodiedLab のバージョン互換性の表現
