# MVP 3: Custom CNN Policy

## Goal

MVP
2で接続した画像観測とベクトル観測を使い、資料に近い構造のカスタム方策ネットワー
クを導入する。

MVP 3では、画像側の2層CNN、数値側の標準化、画像特徴と数値特徴の結合、sigmoid
gate、連続行動2値の出力を明示的に扱う。

初期実装では、ML-Agents 1.1.0の標準`vis_encode_type:
simple`が資料の画像側構造とほぼ一致することを確認したため、forkやvendor化を避け
た標準構成を採用する。明示的なsigmoid
gateまで完全一致させる実装は、学習接続と観測shapeが安定した後の追加作業とする。

このフェーズの主目的は、カスタムネットワークをML-Agents 1.1.0 / Unity ML-Agents
4.0.0の現在構成に安全に差し込めるかを確認し、要件どおりの観測shapeとネットワーク
shapeを固定することである。

## Non-goals

- 本格的な報酬shaping - カリキュラム学習 - 複雑なマップ生成 - LiDAR風レイ観測 -
  推論評価基盤の完成 - 学習済みモデルの性能最適化 -
  ML-AgentsやPyTorchのバージョン更新

## Required Architecture

資料上のネットワーク構造は以下である。

```text
image: batch x 3 x 84 x 112
  -> Conv(3 -> 16, kernel=8x8)
  -> LeakyReLU
  -> Conv(16 -> 32, kernel=4x4)
  -> LeakyReLU
  -> Flatten
  -> batch x 3456
  -> FC(256)
  -> batch x 256
  -> LeakyReLU

numeric: batch x 2
  -> standardization
  -> batch x 2

concat:
  image feature batch x 256 + numeric feature batch x 2
  -> batch x 258
  -> FC(256)
  -> batch x 256
  -> Gate: sigmoid
  -> FC(256)
  -> batch x 256
  -> Gate: sigmoid
  -> FC(2)
  -> batch x 2
  -> Clip
  -> Div(3)
  -> Deterministic output [-1, 1]
  -> continuous actions [v, omega]
```

`Flatten =
3456`は、画像サイズを`84x112`、畳み込みを`stride=4`と`stride=2`で行う前提だと自
然に一致する。

```text
84  -> Conv8 stride4 -> 20 -> Conv4 stride2 -> 9
112 -> Conv8 stride4 -> 27 -> Conv4 stride2 -> 12
32 x 9 x 12 = 3456
```

そのため、資料どおりのshapeを再現する場合は、MVP
2の`84x84`画像を`84x112`へ変更する必要がある。

## Observation Design

### Visual Observation

MVP 3では、前方Segmentation Cameraの解像度を資料に合わせて`84x112`にする。

| 項目 | MVP 2 | MVP 3 |
| --- | --- | --- |
| 解像度 | 84x84 | 84x112 |
| チャンネル | RGB | RGB |
| 画像スタック | 1 | 1 |
| 通行可能領域 | 緑 | 緑 |
| 通行不可能領域 | 青 | 青 |
| Goal | 画像に含めない | 画像に含めない |

画像は引き続き後処理で変換しない。Unityシーン側のマテリアル色をCamera
Sensorで直接撮影する。

### Vector Observation

資料上の数値入力は`batch x 2`であり、中身は`angle, distance`である。

MVP 3では、MVP 2の4値Vector
Observationから、カスタムネットワーク用の2値観測へ切り替えることを基本方針とする
。

| Index | Value | 備考 |
| --- | --- | --- |
| 0 | `angle` | Goalまでの相対角度 |
| 1 | `distance` | Goalまでの相対距離 |

標準化をどこで行うかは要確認である。候補は以下の3つ。

### Unity側で正規化済み値を渡す

- メリット: 観測値の意味が明確で、ONNX推論時も扱いやすい。
- デメリット: 資料のstandardizationとは厳密には異なる可能性がある。

### ML-Agentsの`normalize: true`に任せる

- メリット: 標準機能に沿う。
- デメリット: カスタムネットワーク内の標準化層とは分離される。

### Python側のカスタムモデルで標準化する

- メリット: 資料の構造に近い。
- デメリット: running mean/stdやONNX exportの扱いを慎重に設計する必要がある。

初期実装では、Unity側から`angle`と`distance`を範囲正規化した2値として出し、ML-Ag
ents側は`network_settings.normalize: true`でrunning
mean/stdによる標準化を有効にする。ここでは、Unity側の処理を値域の安定化、ML-Agen
ts側の処理を学習用の標準化として分けて扱う。

## Action Design

出力は連続行動2値とする。

### Action 0

- 意味: `v`
- Unity側対応: 前進速度。Unity側`AgentMotor`で`[0, 1]`へ丸め、
  2倍にして移動へ入力する。負の出力は後退ではなく停止として扱う。

### Action 1

- 意味: `omega`
- Unity側対応: 角速度。ML-Agents出力は`[-1, 1]`で、Unity側では
  この範囲のまま角速度指令として扱う。

資料上は、最後に`Clip`してから`Div(3)`し、決定論的出力を`[-1, 1]`に収める。

これは、ネットワーク内部の出力を`[-3,
3]`程度に制限してから3で割り、Unity/ML-Agents側で扱いやすい連続行動範囲へ戻して
いる可能性が高い。

MVP 3標準trainerでは、Python側のpolicy
outputはML-Agents標準の連続値として扱う。Unity側では`v`を`[0,
1]`へ丸めてから2倍にしてロボット移動へ入力し、`omega`は`[-1,
1]`の標準化角速度指令として維持する。

strict trainerでは、ActionModelも差し替え、モデル側のaction mappingとして`v =
sigmoid(raw_v)`、`omega = clip(raw_omega, -3, 3) /
3`を適用する。これにより、strict版ではUnityへ渡る`v`とONNX
export用の`v`がモデル出力として`[0, 1]`に収まる。

## Gate Interpretation

資料の`Gate: sigmoid`は、実装上の意味がまだ曖昧である。

候補は2つある。

### Sigmoid activation

- 形: `x = sigmoid(fc(x))`
- メリット: 実装が単純。
- デメリット: 「特徴寄与を調整するgate」としては弱い。

### Sigmoid gate

- 形: `gate = sigmoid(fc_gate(x)); x = x * gate`
- メリット: 特徴ごとの寄与調整として自然。
- デメリット: 層構造が資料だけでは確定しない。

ユーザー説明の「入力特徴の寄与を調整」という意味には、sigmoid gateの解釈が近い。

ML-Agents
1.1.0の標準`LinearEncoder`は、全結合層の後にSwish活性化を使う。Swishは`x *
sigmoid(x)`であり、資料の`Gate:
sigmoid`と完全一致ではないが、sigmoidで特徴の通り方を調整する性質を持つ。そのた
め、MVP 3初期実装では標準`LinearEncoder`を使い、明示的な別gate層はOpen
Questionとして残す。

## Python Strategy

MVP 3の中心はPython側である。

現在の依存関係は以下で固定されている。

| 項目 | 値 |
| --- | --- |
| ML-Agents Python | `mlagents==1.1.0` |
| Unity ML-Agents package | `com.unity.ml-agents` `4.0.0` |
| PyTorch | `torch~=2.2.1` |
| Python | `>=3.10.1,<=3.10.12` |

最初の作業は、ML-Agents 1.1.0のTorch実装で、カスタムencoderまたはcustom
actor-criticをどの粒度で差し込めるか調査することである。

候補:

- ML-Agentsの既存visual encoder差し替え - actor/critic network moduleの差し替え
  - ML-Agentsをforkせず、最小限の追加moduleとして登録 -
  ML-Agentsをforkまたはvendor化して必要箇所を変更

推奨は、まずforkやvendor化を避け、追加moduleまたは設定差し替えで実現できるか調べ
ることである。これは、配布時にUnityプロジェクトとML-Agents環境を維持しやすくする
ためである。

調査結果として、`SimpleVisualEncoder`は以下の構造であり、資料の画像側と一致する
。

```text
Conv2d(initial_channels -> 16, kernel=8, stride=4)
LeakyReLU
Conv2d(16 -> 32, kernel=4, stride=2)
LeakyReLU
Flatten
Linear(flatten_size -> hidden_units)
LeakyReLU
```

`84x112`入力では`flatten_size = 32 x 9 x 12 = 3456`となる。`hidden_units:
256`を指定すれば、画像特徴は`batch x 256`になる。

また、ML-Agentsのcontinuous action export pathには、`[-3, 3]`へのclipと`/
3`に相当する処理がすでに含まれている。PPO学習中は探索のためstochastic
distributionを使うが、ONNX exportにはdeterministic continuous outputが含まれる。

## Unity Strategy

MVP 3では、MVP 2を壊さず比較可能にする。

候補名:

- `Mvp3SceneBuilder` - BehaviorName: `Mvp3CustomCnnNavigation` - config:
  `configs/ml-agents/mvp3-custom-cnn-navigation.yaml` - script:
  `scripts/train-mvp3.sh`

Unity側の主な変更:

- 画像解像度を`84x112`にする - Vector Observationを2値にする - BehaviorNameをMVP
  2と分離する - Segmentation Preview Overlayを`84x112`表示に対応させる -
  `AgentMotor`の2次元連続行動は維持する

MVP 2は標準ML-Agents visual encoderの比較対象として残す。

## Reward Design

MVP 3のUnity側報酬は、MVP 1/2と同じ体系に揃える。

| 条件 | 報酬 |
| --- | --- |
| Goal到達 | `+100` |
| 壁衝突 | `-50` |
| 行動入力がある | `+0.01` |
| Goalまでの距離が前回判定より近づく | `+0.1` |
| Goal方向角が`<-90`または`>90` | `-0.1` |
| Goal方向角が`<-150`または`>150` | `-5` |
| 前進入力がほぼ0、または旋回入力が`-0.3`から`0.3`の範囲 | `-0.1` |

`AgentMotor`は`MovePosition`と`MoveRotation`で移動するため、報酬判定では物理速度
そのものではなく、`OnActionReceived`で受け取った連続行動入力を使う。ここでは前進
入力を速度指令、旋回入力を角度変化率の指令として扱う。

## Initial Implementation

MVP 3初期実装では、以下の方針で進める。

- Unity側に`Mvp3SceneBuilder`を追加する -
  BehaviorNameは`Mvp3CustomCnnNavigation`とする - Segmentation
  Cameraは`84x112`、RGB、stack 1とする - Vector Observationは`angle,
  distance`の2値にする - GoalはSegmentation Cameraから除外する -
  `configs/ml-agents/mvp3-custom-cnn-navigation.yaml`を追加する -
  `network_settings.vis_encode_type: simple`を使う -
  `network_settings.hidden_units: 256`、`num_layers: 2`を使う - 明示的なsigmoid
  gate層はまだ実装しない

この方針の理由は、資料のCNN側構造がML-Agents標準`simple`
encoderで再現でき、forkなしで配布・再現しやすいからである。

## Strict Implementation

資料のネットワーク構造へより厳密に合わせるため、ML-Agents本体は変更せず、起動前
に`NetworkBody`だけを差し替えるwrapperを追加する。

追加するPython module:

- `python/envforge_mlagents/navigation_strict/network.py` -
  `python/envforge_mlagents/navigation_strict/patch.py` -
  `python/envforge_mlagents/navigation_strict/train.py`

strict版の実行入口:

- config: `configs/ml-agents/mvp3-strict-custom-cnn-navigation.yaml` - script:
  `scripts/train-mvp3-strict.sh`

Unity側のBehaviorNameは、既存のMVP
3と同じ`Mvp3CustomCnnNavigation`を使う。これにより、Unityシーンや`Mvp3SceneBuild
er`を変更せず、trainer側だけをstrict版に差し替えられる。

strict版`NetworkBody`は、以下の観測仕様だけを受け付ける。

- visual observation: `3 x 84 x 112` - vector observation: `2` - hidden size:
  `256` - recurrent memory: なし

strict版のbody構造:

```text
image: batch x 3 x 84 x 112
  -> Conv2d(3 -> 16, kernel=8, stride=4)
  -> LeakyReLU
  -> Conv2d(16 -> 32, kernel=4, stride=2)
  -> LeakyReLU
  -> Flatten batch x 3456
  -> FC(256)
  -> LeakyReLU

numeric: batch x 2
  -> ML-Agents VectorInput normalizer

concat: batch x 258
  -> FC(256)
  -> sigmoid gate: x * sigmoid(x)
  -> FC(256)
  -> sigmoid gate: x * sigmoid(x)
```

連続行動の`FC(2)`とGaussian
distributionは、ML-Agents標準`ActionModel`と同じ分布を使う。strict版では、その後
段だけを差し替え、学習時にUnityへ送るactionとONNX
export用actionの両方で、1次元目を`sigmoid`により`[0,
1]`へ、2次元目を既存どおり`Clip -> Div(3)`により`[-1, 1]`へ写像する。

この実装は、資料のbody構造をstrictに近づける一方で、ML-Agentsのpolicy/export契約
は維持する折衷案である。

strict版を含む学習scriptは冒頭で、BehaviorName、観測、行動、strictモデル側のacti
on mapping、Unity側の速度2倍スケール、episode limit、training
limit、ML-Agents拡張の有無を出力する。

strict版wrapper自体は、固定文字列ではなく実際に生成されたモデルから以下を出力す
る。

- `networks.NetworkBody is StrictNavigationNetworkBody`の真偽 -
  `actor.network_body`の実class名 - `actor.action_model`の実class名 -
  `print(actor.network_body)`による実module構造 - forward smoke testの入力shape
  - forward smoke testの出力shapeとmemory - strict action smoke testの出力値

strict版`NetworkBody`は、学習中にML-Agents側へ渡った実際のVisual Observation
tensorからRGB平均、最小値、最大値を出力する。これにより、UnityのPreview
Overlayとは別に、Python側が黒画像や極端な値だけを受け取っていないかをざっくり確
認できる。既定では最初の実forwardと、その後1000
forwardごとに出力する。出力間隔は環境変数`ENVFORGE_IMAGE_STATS_INTERVAL`で変更で
きる。

## Unity Inference Check

strict版の学習済みONNXは、WSL2側の以下に出力された。

```text
~/src/ml-agents/results/mvp3-strict-custom-cnn-navigation/Mvp3CustomCnnNavigation.onnx
```

最終checkpointは`1500001` stepsで、training
status上のrewardは`86.0381145837172`である。ONNX
checkerは通過し、入出力shapeは以下である。

| 名前 | shape |
| --- | --- |
| `obs_0` | `batch x 3 x 84 x 112` |
| `obs_1` | `batch x 2` |
| `continuous_actions` | `batch x 2` |

Unity側では、このONNXを以下に配置する。

```text
unity/EnvForge-local-first/Assets/Resources/DevelopmentHistoryModels/Mvp3CustomCnnNavigation.onnx
```

`Mvp3SceneBuilder`は、`Mvp3Bootstrap`のInspector上の`Inference
Model`へ`ModelAsset`が割り当てられている場合だけ、`BehaviorParameters.Model`へそ
のモデルを設定する。この場合は`BehaviorType.InferenceOnly`、`DeterministicInfere
nce = true`として実行する。`Inference
Model`が空の場合は、従来の`BehaviorType.Default`のままにし、Python
trainerから接続して学習できる状態にする。

この方針を推奨する理由は、training時のBehaviorNameと観測仕様を保ったまま、Bootst
rapの有効/無効切り替えと同じ感覚で推論対象モデルを選べるためである。固定のモデル
名や起動時メニューに依存せず、モデルを割り当てれば推論、空にすれば学習という運用
にできる。

動作確認時は、`Mvp3Bootstrap`だけを有効にし、`Mvp0Bootstrap`、`Mvp1Bootstrap`、`
Mvp2Bootstrap`は無効にする。複数のbootstrapを同時に有効にすると、複数のエージェ
ントや環境が重なり、モデル推論結果の観察が不明確になる。

## Episode Initialization

各エピソード開始時に、ロボットの開始位置と開始向きをランダム化する。

- 開始位置はfloor内側の範囲からサンプリングする -
  開始向きはY軸まわり`0`から`360`度でサンプリングする -
  壁などのColliderと重なる候補位置は避ける -
  一定回数サンプリングして有効位置が見つからない場合は既定開始位置へ戻す

1エピソード上限は`Mvp3NavigationAgent.MaxStep =
1000`とする。学習上限はconfig側で`max_steps: 1500000`とする。

## Work Plan

### Step 1: Python側調査

- WSL2ミラー環境で`mlagents==1.1.0`のTorch network実装を確認する - visual
  encoder、actor、critic、ONNX exportの接続点を確認する -
  forkなしで差し替え可能か判断する

### Step 2: 観測shapeの固定

- Unity側の画像shapeを`84x112`に変更する案を検証する - Vector
  Observationを2値へ変更する - Python側で受け取るobservation
  specが期待どおりになるか確認する

### Step 3: 最小カスタムモデル

- 2層CNN + LeakyReLU + Flatten + FC(256)を実装する - 数値2値とconcatして`batch x
  258`を作る - sigmoid gateなしの最小actor/criticで学習が走るか確認する

### Step 4: 資料準拠モデル

- sigmoid gateを追加する - `Clip -> Div(3)`の出力整形を追加する -
  deterministic出力とML-Agentsのcontinuous action分布の関係を確認する

### Step 5: Export / 推論確認

- ONNX exportが通ることを確認する -
  Unity側でONNX推論に使う場合の入力/出力shapeを確認する -
  学習時と推論時の標準化が一致することを確認する

## Done

- MVP 3のネットワーク要件がMarkdownで明文化されている - `84x112`画像と`batch x
  3456` Flattenの関係が説明されている - Vector
  Observationを2値へ変更する方針が整理されている -
  Python側カスタムネットワーク調査項目が整理されている - MVP 2を壊さず、MVP
  3を別Behaviorとして進める方針が整理されている - ML-Agents
  1.1.0の`SimpleVisualEncoder`が資料のCNN側構造と一致することを確認した -
  Unity側にMVP
  3用の`Mvp3SceneBuilder`、`Mvp3NavigationAgent`、2値観測providerを追加した -
  MVP 3用のML-Agents configとtrain scriptを追加した -
  初期実装ではforkなしの標準`simple` encoder構成を採用した -
  strict版として、ML-Agents起動前に`NetworkBody`を差し替えるwrapperを追加した -
  strict版では、`FC -> x * sigmoid(x)`のsigmoid gateを2層追加した -
  strict版用のconfigとtrain scriptを追加した - MVP 3
  Agentに指定された報酬体系を実装した - `v`をUnity側で`[0,
  1]`へ丸め、2倍にしてロボット移動へ入力するようにした - `omega`は`[-1,
  1]`の標準化角速度指令として維持した - 1エピソード上限を1000 stepにした -
  学習上限を150万stepにした -
  エピソード開始時のロボット位置と向きをランダム化した - 学習scriptとstrict
  wrapperでモデルと設定を出力するようにした - strict
  wrapperは実際に生成された`actor.network_body`からmodule構造とforward smoke
  test結果を出力する - strict版では`ActionModel`も差し替え、`v =
  sigmoid(raw_v)`、`omega = clip(raw_omega, -3, 3) / 3`を学習時actionとONNX
  export actionに適用するようにした -
  strict版`NetworkBody`で、実入力画像tensorのRGB平均・最小値・最大値を節目ごとに
  出力するようにした -
  ロボット頭上の矢印を、ロボット正面方向に伸びる非対称な矢印形状へ変更した -
  動作確認用マップに短い内壁を2つ追加し、難度を大きく上げずに曲がり方の変化を増
  やした -
  strict版ONNXを`Assets/Resources/DevelopmentHistoryModels/Mvp3CustomCnnNavigati
  on.onnx`へ履歴モデルとして退避した -
  `Mvp3SceneBuilder`でInspector上の`Inference
  Model`に割り当てたONNXを`InferenceOnly`で実行できるようにした - `Inference
  Model`が空の場合は、従来どおり`BehaviorType.Default`でPython
  trainerから学習接続できるようにした - strict版ONNXの入出力shapeがMVP
  3のUnity観測仕様と一致することを確認した

## Open Questions

- `Gate: sigmoid`を`x * sigmoid(x)`と解釈してよいか、別gate branchが必要か -
  Unity側の範囲正規化とML-Agents側のrunning standardizationの組み合わせで十分か
  - strict版のcriticもactorと同じbody構造でよいか - action
  distributionまで完全に決定論的にする必要があるか - ONNX
  export時に`torch~=2.2.1`と`onnx==1.15.0`の制約内で問題が出ないか -
  速度・角度変化率の報酬判定を入力指令ではなくRigidbody実測値に切り替える必要が
  あるか - strict版の画像統計ログをTensorBoardやCSVへ保存する必要があるか -
  Unity
  Editor上でstrict版ONNX推論を複数episode観察し、成功率・衝突率・停滞を記録する
  必要がある

## Next

- Unity
  Editorで`Mvp3Bootstrap`のみを有効にし、strict版ONNX推論でロボットが動くことを
  確認する - 可能なら数episode観察し、Goal到達、壁衝突、停滞の傾向を記録する -
  推論挙動が不安定な場合は、`999974`
  stepなどrewardが高かったcheckpointとの比較を行う
