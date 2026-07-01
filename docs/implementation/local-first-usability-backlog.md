# Local-first 操作性バックログ

## 目的

EnvForge の local-first navigation editor を実際に使いながら見つかった、
細かい違和感、操作しづらさ、調査したい点を一箇所に集約する。

この文書は、すぐに実装へ入るための仕様書ではなく、ユーザー観察ログと
実装 TODO の間に置くバックログである。各項目は、症状、期待、次の確認を
分けて記録し、後で優先順位を決めて実装タスクへ切り出す。

## 運用方針

- 実行中に見つけた違和感は、まずこの文書に追加する。
- 実装に入る前に、再現条件、期待する挙動、影響範囲を確認する。
- クラウドリソース削除や接続設定変更のような破壊的・環境依存の作業は、
  別途チェックリストを作ってから実行する。
- Unity 起動中やユーザーが目視確認中の場合、コード変更や Unity 起動を伴う検証は
  明示的な許可があるまで行わない。

## バックログ

| ID | 分類 | 症状・観察 | 期待 | 優先度 | 状態 | 次の確認 |
| --- | --- | --- | --- | --- | --- | --- |
| UX-001 | Map camera | マップを真上から見たい。 | 編集時に、現在の斜め view と top-down view を切り替えて位置関係を把握できる。 | High | Done | ユーザー目視で `Top` / `Angle` 切り替えと `Place Wall` ボタン表示を確認済み。 |
| UX-002 | Wall editing | 壁をコピペできない。 | 既存の壁を複製し、位置や長さだけ微調整できる。 | Medium | New | 複製ショートカット、ボタン、コンテキスト操作のどれが自然か確認する。 |
| UX-003 | Wall handles | 壁の拡大マーカーと縮小マーカーらしき表示の違いが分かりづらい。 | マーカーごとの操作結果が見た目と操作感で分かる。 | Medium | Needs investigation | 実際に各マーカーを操作し、何が変わるのかを記録する。 |
| UX-004 | Wall placement | `Place Wall` で壁を設置すると既存の壁に重なり、操作しづらい。 | 新規壁が既存壁を邪魔しない位置に置かれる、または配置直後に動かしやすい。 | High | New | 新規壁の初期位置、選択状態、衝突回避の必要性を確認する。 |
| UX-005 | Wall selection | 壁が近い間隔で設置されていると、意図した壁を選択状態にしづらい。 | 密集した壁でも狙った壁を安定して選択できる。 | High | New | クリック判定、選択候補の循環、選択リスト、ハンドル表示の候補を比較する。 |
| UX-006 | Settings input focus | 右上ウィンドウの `Settings` で値を編集するとき、カーソルキーを使うとマップが動く。 | 入力欄にフォーカスがある間は、カーソルキーがマップ操作に伝播しない。 | High | New | IMGUI 入力中の key event 処理と camera/controller 側の入力抑制点を確認する。 |
| UX-007 | WebSocket display stability | WebSocket で情報を取るタイミングで画面がちらつくように見える。 | 状態更新時もパネルや画面表示が安定する。 | Medium | Needs investigation | 更新頻度、再描画範囲、パネル開閉状態、job history 保存タイミングを確認する。 |
| UX-008 | Map save/load | 作成したマップを保存して、後から開き直す導線がほしい。 | 壁、平面サイズ、スタート、ゴール、主要設定を保存し、後で同じマップを復元できる。 | High | New | Scenario Bundle 保存との関係、ユーザー向け保存形式、上書き/別名保存、読み込み導線を整理する。 |
| UX-009 | Map new/reset | 現在のマップを破棄して、新規マップを作る導線がない。 | 新規作成またはリセット操作で、平面、境界壁、追加壁、スタート、ゴール、主要設定を既定状態に戻せる。 | High | New | 保存済みマップやジョブ投入済み設定との関係、確認ダイアログ、リセット対象の範囲を整理する。 |
| UX-010 | Display labels | 右上ウィンドウの表示専用ラベルにマウスを乗せると色が変わり、操作できる要素のように見える。 | 表示専用ラベルは hover しても色が変わらず、ボタンや入力欄と見分けがつく。 | Low | New | IMGUI style の hover state、label と button の見た目、読み取り専用項目の表現を確認する。 |
| UX-011 | Replay controls | Replay の前後 step ボタンが縦線つきアイコンで、先頭・末尾へ移動する操作に見える。 | 1 step 前後移動だと分かるよう、現在のアイコンから縦線だけを外す。長い Replay Bundle の移動は、まず既存の episode 前後移動で足りるか確認する。 | Low | New | `NavigationReplayPlayer` の Previous / Next icon drawing で縦線だけを削除し、tooltip と実際の挙動が一致して見えるか確認する。 |
| CLOUD-001 | Cloud warning | Unity が `Result fetch failed ... Cannot resolve destination host ...` 警告を吐き続け、再起動しても消えない。 | 無効な接続先や不要な polling がある場合、警告が出続けない。 | High | Needs investigation | 保存済み API / WebSocket URL、job history、fallback URL、リトライ条件を確認する。 |
| OPS-001 | Cloud cleanup | クラウド側の不要なものを消して整理したい。 | 残す成果物と消すリソースを明確にしたうえで、安全に削除できる。 | Medium | Needs checklist | 削除前に `docs/implementation/cloud-result-retention.md` を消してはいけないものリストとして確認し、Cloud Run、Artifact Registry、Firestore、GCS の削除対象と照合する。 |
| QA-001 | Camera height verification | カメラの高さが設定変更や runtime 状態に応じてちゃんと変わっているか確認したい。 | カメラ高さの設定値、実際の transform、見た目の変化が一致していると確認できる。 | Medium | New | どのカメラを対象にするか、設定入力、保存値、runtime transform、表示結果の確認手順を定義する。 |
| QA-002 | Max episode definition | `max episode` の定義が期待と合っているか確認したい。 | UI 表示、Scenario Bundle、学習設定、Replay / 結果表示で `max episode` が同じ意味として扱われる。 | Medium | New | `max episode` が episode 数、最大 step 数、評価 episode 数、または別の上限値を指していないか、表示名とデータ契約を確認する。 |
| PHYS-001 | Robot-wall collision | ロボットと壁の衝突判定が甘いように見える。 | 見た目上ロボットが壁に接触した、または壁へ入り込んだ状態と、collision / failure 判定が自然に一致する。 | High | Needs investigation | ロボット collider、壁 collider、壁の厚さ、Rigidbody 設定、physics layer、Replay 表示、学習環境側の衝突判定との対応を確認する。 |

## 調査メモ

- `PHYS-001`: 衝突判定が甘く見える理由として、学習環境と EnvForge で壁の厚さが
  一致していない可能性がある。Scenario Bundle で壁をどの中心線、長さ、厚さとして
  表現しているか、EnvForge の表示 mesh / collider と EmbodiedLab 側の obstacle
  変換が同じ幾何として扱われているかを確認する。

## 優先順位の初期案

1. `UX-001`, `UX-004`, `UX-005`, `UX-006`, `UX-008`, `UX-009` を、編集体験を阻害する優先項目として扱う。
2. `CLOUD-001`, `UX-007` は、ログと状態更新の調査を先に行い、UI 修正と接続設定修正を分ける。
3. `OPS-001` は削除を伴うため、実行前に `docs/implementation/cloud-result-retention.md`
   を消してはいけないものリストとして確認し、保持対象を削除対象から除外する。
4. `PHYS-001` は見た目、Unity physics、Scenario / Replay contract のずれを疑い、調査を優先する。
5. `QA-001`, `QA-002` は、関連する修正や確認作業に入る前の検証タスクとして扱う。

## 保留事項

- 各項目の実装範囲はまだ確定していない。
- Unity 実行中のため、この記録時点ではコード変更、Unity 起動、runtime 検証は行っていない。
- `CLOUD-001` と `OPS-001` は EnvForge 側だけでなく EmbodiedLab / GCP 側の状態確認が必要になる。
