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
| UX-003 | Wall handles | 壁の拡大マーカーと縮小マーカーらしき表示の違いが分かりづらい。 | マーカーごとの操作結果が見た目と操作感で分かる。 | Medium | Done | 左右の端点はいずれも、反対側を固定して長さを変える同じ操作だった。選択中の壁は矩形アウトラインで実形状を示し、リサイズ用マーカーは実端点より内側に置いて外壁近くでも操作しやすくした。ユーザー壁の配置 clamp は外壁の内側面まで寄せられるようにし、直角に接続したいときの不要な隙間を避ける。 |
| UX-004 | Wall placement | `Place Wall` で壁を設置すると既存の壁に重なり、操作しづらい。 | 新規壁が既存壁を邪魔しない位置に置かれる、または配置直後に動かしやすい。 | High | Done | 新規壁の中心候補を周辺へ探索し、既存ユーザー壁と重ならない位置を優先して配置するようにした。配置後は新規壁を選択状態にする。 |
| UX-005 | Wall selection | 壁が近い間隔で設置されていると、意図した壁を選択状態にしづらい。 | 密集した壁でも狙った壁を安定して選択できる。 | High | New | クリック判定、選択候補の循環、選択リスト、ハンドル表示の候補を比較する。 |
| UX-006 | Settings input focus | 右上ウィンドウの `Settings` で値を編集するとき、カーソルキーを使うとマップが動く。 | 入力欄にフォーカスがある間は、カーソルキーがマップ操作に伝播しない。 | High | Done | Cloud settings の text field focus を camera controller が見るようにし、Settings 入力中は pan 入力を抑制する。 |
| UX-007 | WebSocket display stability | WebSocket で情報を取るタイミングで画面がちらつくように見える。 | 状態更新時もパネルや画面表示が安定する。 | Medium | Needs investigation | 更新頻度、再描画範囲、パネル開閉状態、job history 保存タイミングを確認する。 |
| UX-008 | Map save/load | 作成したマップを保存して、後から開き直す導線がほしい。 | 壁、平面サイズ、スタート、ゴール、主要設定を保存し、後で同じマップを復元できる。 | High | Done | World 詳細パネルに `Save Map` / `Load Map` を追加し、Unity の persistent data 配下に Scenario Bundle JSON として `latest-map.json` を保存/復元する。 |
| UX-009 | Map new/reset | 現在のマップを破棄して、新規マップを作る導線がない。 | 新規作成またはリセット操作で、平面、境界壁、追加壁、スタート、ゴール、主要設定を既定状態に戻せる。 | High | New | 保存済みマップやジョブ投入済み設定との関係、確認ダイアログ、リセット対象の範囲を整理する。 |
| UX-010 | Display labels | 右上ウィンドウの表示専用ラベルにマウスを乗せると色が変わり、操作できる要素のように見える。 | 表示専用ラベルは hover しても色が変わらず、ボタンや入力欄と見分けがつく。 | Low | New | IMGUI style の hover state、label と button の見た目、読み取り専用項目の表現を確認する。 |
| UX-011 | Replay controls | Replay の前後 step ボタンが縦線つきアイコンで、先頭・末尾へ移動する操作に見える。 | 1 step 前後移動だと分かるよう、現在のアイコンから縦線だけを外す。長い Replay Bundle の移動は、まず既存の episode 前後移動で足りるか確認する。 | Low | Done | `NavigationReplayPlayer` の Previous / Next icon drawing から縦線だけを削除し、tooltip と 1 step 挙動を維持した。 |
| UX-012 | Replay camera view | リプレイ時にも上からの視点を選べるようにしたい。 | Replay 再生中も、斜め view と top-down view を切り替えて軌跡、壁、ゴールとの位置関係を確認できる。 | Medium | Done | Replay compact / details overlay に `Top` / `Angle` を追加し、再生中も既存の camera controller を切り替えられるようにした。 |
| UX-013 | Artifact selection | マップ、学習結果、学習済モデルを選択してダウンロード・ロードできる導線がない。 | 保存済みマップ、Result Bundle / Replay Bundle、`policy.onnx` を一覧から選び、必要なものをダウンロードまたはローカルロードできる。 | High | New | `job-history.json`、ローカル保存済み map、GCS artifact metadata、手動 file picker のどれを正本にするか整理し、選択 UI、再取得、ロード済み状態表示を設計する。 |
| UX-014 | Wall deselection | 最後に選択した壁の黄色いマーカーが出っぱなしになる。 | 何もない床をクリックしたら壁の選択状態が解除され、黄色いマーカーがどの壁にも出ない。 | Medium | New | 床クリックと壁クリック/ハンドルクリックを判別し、`selectedWallIndex` と drag state を安全に解除する。Replay / Cloud / World panel 上のクリックでは解除しない。 |
| VIS-001 | Wall visual consistency | 壁の見え方にばらつきがあり、角度や厚さが同じに見えない。 | 同じ設定の壁は、角度、厚さ、端点、影、選択状態にかかわらず一貫した形に見える。 | Medium | Done | 壁の影を無効化して角度ごとの見え方の差を抑えた。選択中の表示は中心線ではなく壁の矩形アウトラインにし、回転時も実際の厚みと向きが直感的に分かるようにした。さらに `Top` view は perspective ではなく orthographic に切り替え、壁を上下に動かしたときに板が倒れるように見える投影歪みを避ける。 |
| VIS-002 | Angled wall visual consistency | 真上視点の壁表示は改善したが、アングル視点では壁の大きさや厚みがまだおかしく見える。 | アングル視点でも壁の高さ、厚み、奥行き、端点が自然に見え、編集時の実寸感と一致する。 | Medium | New | perspective camera、field of view、wall mesh scale、material / outline、選択中 overlay の描画位置、外壁との見え方を切り分ける。 |
| CLOUD-001 | Cloud warning | Unity が `Result fetch failed ... Cannot resolve destination host ...` 警告を吐き続け、再起動しても消えない。 | 無効な接続先や不要な polling がある場合、警告が出続けない。 | High | Needs investigation | 保存済み API / WebSocket URL、job history、fallback URL、リトライ条件を確認する。 |
| OPS-001 | Cloud cleanup | クラウド側の不要なものを消して整理したい。 | 残す成果物と消すリソースを明確にしたうえで、安全に削除できる。 | Medium | Needs checklist | 削除前に `docs/implementation/cloud-result-retention.md` を消してはいけないものリストとして確認し、Cloud Run、Artifact Registry、Firestore、GCS の削除対象と照合する。 |
| MAINT-001 | Code maintainability | コードが増えてきて、人間が中身を追ったり確認したりしづらくなる可能性がある。 | 定期的にコードをリファクタリングし、責務、命名、重複を整理して、人間が追いやすく冗長なコードが少ない状態を保つ。 | Medium | New | 機能変更を目的にせず、読みやすさ、責務分離、重複削除、古い暫定コード削除、テストや検証のしやすさを観点に点検する。 |
| QA-001 | Camera height verification | カメラの高さが設定変更や runtime 状態に応じてちゃんと変わっているか確認したい。 | カメラ高さの設定値、実際の transform、見た目の変化が一致していると確認できる。 | Medium | Done | ユーザー目視で概ね問題ないことを確認済み。設定値、range、Replay 表示は既存 UI に表示される。 |
| QA-002 | Max episode definition | `max episode` の定義が期待と合っているか確認したい。 | UI 表示、Scenario Bundle、学習設定、Replay / 結果表示で `max episode` が同じ意味として扱われる。 | Medium | Done | UI ラベルを `max steps/ep` に変更し、Scenario Bundle の `training.max_episode_steps` と同じ意味だと分かるようにした。 |
| PHYS-001 | Robot-wall collision | ロボットと壁の衝突判定が甘いように見える。 | 見た目上ロボットが壁に接触した、または壁へ入り込んだ状態と、collision / failure 判定が自然に一致する。 | High | Done | `robot.radius` を Scenario Bundle の正本にし、EnvForge の collider と EmbodiedLab の obstacle 膨張 collision 判定を同じ値に揃える。 |

## 調査メモ

- `PHYS-001`: 衝突判定が甘く見える理由として、学習環境と EnvForge で壁の厚さが
  一致していない可能性がある。Scenario Bundle で壁をどの中心線、長さ、厚さとして
  表現しているか、EnvForge の表示 mesh / collider と EmbodiedLab 側の obstacle
  変換が同じ幾何として扱われているかを確認する。

## 優先順位の初期案

1. `UX-005`, `UX-009`, `UX-013`, `UX-014`, `VIS-002` を、編集体験と確認体験を阻害する残りの優先項目として扱う。
2. `CLOUD-001`, `UX-007` は、ログと状態更新の調査を先に行い、UI 修正と接続設定修正を分ける。
3. `OPS-001` は削除を伴うため、実行前に `docs/implementation/cloud-result-retention.md`
   を消してはいけないものリストとして確認し、保持対象を削除対象から除外する。
4. `MAINT-001` は、機能追加や不具合修正の節目で定期点検として扱い、仕様変更ではなく読みやすさと冗長コード削除を目的にする。
5. `UX-001`, `UX-003`, `UX-004`, `UX-006`, `UX-008`, `UX-011`, `UX-012`, `QA-001`, `QA-002`, `PHYS-001`, `VIS-001` は完了済みとして扱う。

## 保留事項

- 各項目の実装範囲はまだ確定していない。
- Unity 実行中のため、この記録時点ではコード変更、Unity 起動、runtime 検証は行っていない。
- `CLOUD-001` と `OPS-001` は EnvForge 側だけでなく EmbodiedLab / GCP 側の状態確認が必要になる。
