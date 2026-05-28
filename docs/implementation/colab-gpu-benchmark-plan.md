# Colab GPU Benchmark Plan

## 目的

EnvForgeの最終版ナビゲーション学習をGoogle Colab上で短時間だけ実行し、GPU環境への課金が実行速度にどの程度効くかを確認する。

今回の主な仮説は、高性能GPUにしても学習速度は大きく改善しない可能性が高い、というものである。理由は、現在の学習が小さなCNNを使う一方で、Unity headless側のシミュレーション、カメラ観測生成、ML-Agents通信が律速になりやすいためである。

この検証は、学習済みモデルの性能を比較するものではない。あくまで、クラウドでheadless実行する将来方針に向けて、GPU課金の優先度を判断するための速度観察である。

## 対象

対象は最終版ナビゲーション構成とする。

| 項目 | 値 |
| --- | --- |
| Unity project | `unity/EnvForge-local-first/` |
| BehaviorName | `NavigationFinal` |
| 学習設定 | `configs/ml-agents/navigation-final.yaml` |
| 学習入口 | `scripts/train-navigation-final.sh` |
| Python trainer | `python/envforge_mlagents/navigation_strict/` |
| 観測 | RGB segmentation image `84x112` stack 1 + 2-value goal vector |
| 行動 | continuous `[v, omega]` |
| 通常学習上限 | `1500000` trainer steps |
| ベンチ学習上限 | `100000` trainer steps |
| Colab学習用Unity引数 | `--envforge-train` |

初回ベンチ用の作業台は以下に置く。

| 用途 | ファイル |
| --- | --- |
| 10万step用ML-Agents設定 | `configs/ml-agents/navigation-final-bench-100k.yaml` |
| Colab notebook | `notebooks/envforge_colab_gpu_benchmark.ipynb` |

GitHubに10万step用configがまだ反映されていない場合でも、Colab notebook内で`configs/ml-agents/navigation-final.yaml`から`navigation-final-bench-100k.yaml`を生成する。生成内容は初回ベンチでは`max_steps: 1500000`を`max_steps: 100000`へ置き換えるだけに留め、他のtrainer設定は通常の`NavigationFinal`と揃える。

## 実行条件

まずはGoogle Colabだけで比較する。

| 条件 | 優先度 | 備考 |
| --- | --- | --- |
| CPUのみ | 必須 | GPUなしの基準値 |
| T4 | 必須 | Colabで取りやすいGPU基準 |
| L4 | 可能なら | premium GPUで取れた場合 |
| A100 | 可能なら | 高性能GPU課金の効果確認 |
| H100 | 任意 | 無理に狙わず、取れた場合のみ参考値 |

ColabではGPU種別、利用上限、VM寿命、idle timeoutが変動する。したがって、特定GPUが取れない場合はその時点で無理に待たず、取れた条件だけで一次判断する。

## ベンチ条件

各条件で`100000` trainer stepsだけ実行する。既存設定の`summary_freq: 5000`により、20点程度のログが得られる。

比較では起動直後の初期化やwarmupを混ぜすぎないため、可能なら最初の`10000` stepsを参考外とし、`10000`から`100000` stepsの区間を主な速度比較に使う。

run idは条件ごとに分ける。

| 条件 | run id例 |
| --- | --- |
| CPU | `bench-cpu-100k` |
| T4 | `bench-t4-100k` |
| L4 | `bench-l4-100k` |
| A100 | `bench-a100-100k` |
| H100 | `bench-h100-100k` |

## 記録する指標

最重要指標は、学習性能ではなく実行速度である。

| 指標 | 用途 |
| --- | --- |
| 10万step完了までのwall-clock時間 | GPU課金の効果を直接判断する |
| steps/sec | 条件間比較の主指標 |
| 5000stepごとの経過時間 | 途中で速度低下や詰まりがないかを見る |
| GPU model | Colabで実際に割り当てられたGPUを記録する |
| GPU utilization | GPUが使われているかを見る |
| GPU memory usage | VRAM不足や過剰余裕を確認する |
| CPU utilization | Unity headlessや環境生成が律速かを見る |
| system memory usage | Colab VMでメモリ不足がないか見る |
| reward mean推移 | 条件差で学習挙動が大きく崩れていないか確認する |

## 判断基準

今回の目的は、GPU課金に意味があるかどうかを粗く判断することである。最初の判断基準は以下とする。

| 結果 | 解釈 |
| --- | --- |
| A100がT4比で`1.2x`未満 | 高性能GPU課金の優先度は低い |
| A100がT4比で`1.2x`から`1.5x`程度 | 効果は限定的。Colab外の固定GPU環境で再確認する |
| A100がT4比で`2.0x`以上 | GPU課金に意味がある可能性が高い |
| CPUとGPUが近い | Unity headless、画像観測生成、ML-Agents通信が主な律速候補 |
| GPU利用率が低い | 高性能GPUより環境並列化を優先する |
| CPU利用率が高い | Unity側のstep生成、time scale、headless設定、並列環境数を見直す |

この基準では、A100がT4比で明確に速くならない場合、高性能GPUへの継続課金よりも、Unity環境の並列実行や観測生成コスト削減を次の検討対象にする。

## Colab実行の流れ

1. Colabのランタイムを選ぶ。
   - CPU条件ではHardware acceleratorをNoneにする。
   - GPU条件ではT4、L4、A100など、選べる範囲で対象GPUを選ぶ。
2. リポジトリを取得する。
3. Python `3.10.12`相当の環境を用意する。
4. `uv`を入れ、`pyproject.toml`に従って依存関係を同期する。
5. Unity Linux headless buildをGoogle DriveからColab VMへコピーする。
6. ベンチ用に`max_steps: 100000`の学習設定をColab VM内で生成して使う。
7. `NavigationFinal` trainerをrun id付きで先に起動し、ML-Agents communicatorを待ち受け状態にする。
8. Unity headless buildを`xvfb-run`、`-force-glcore`、`--envforge-train`付きで起動する。
9. 実行中に`nvidia-smi`、CPU、メモリ、経過時間を記録する。
10. 10万step完了後、結果フォルダとログをGoogle Driveへ保存する。

前回の`ColabSmokeTest.ipynb`では、Windows側UnityからLinux用にexportしたbuildフォルダをGoogle Drive上に置き、Colab内で以下の流れによりheadless起動とカメラ画像出力を確認した。

```bash
cp -r "/content/drive/MyDrive/colab/ColabSmokeTest" /content/unity_build
chmod +x /content/unity_build/ColabSmokeTest.x86_64
apt-get install -y xvfb
xvfb-run -s "-screen 0 1024x768x24" \
  /content/unity_build/ColabSmokeTest.x86_64 \
  -batchmode \
  -force-glcore \
  -logFile /content/unity.log
```

今回のベンチでも、CameraSensor / RenderTextureの安定性を優先してこの方式に寄せる。`-nographics`は使わない。なお、前回notebook内の`-batchmod`は`-batchmode`のtypoとみなし、今回の手順では`-batchmode`を使う。

NavigationFinalのSceneには通常の推論確認用に`NavigationFinal`のmodel assetを割り当てている。そのままLinux playerを起動すると`BehaviorType.InferenceOnly`になり、ML-Agents trainerへ接続しない。ColabベンチではUnity起動引数に`--envforge-train`を付け、model assetが割り当て済みでも`BehaviorType.Default`とcommunicator有効化を優先する。

また、外部Unity executableを使う場合は、trainerを先に起動してcommunicatorの待ち受けを作ってからUnityを起動する。Unityを先に起動すると、Unity側がAgent初期化までは進んでもtrainerへ参加できず、trainer側が`UnityTimeOutException`で待ち続ける場合がある。

## 2026-05-26時点のColab起動確認メモ

CPUランタイム上でLinux buildを再作成して配置し直したところ、`xvfb-run`上でUnityはOpenGL 4.5 graphics deviceの初期化まで到達した。したがって、前回詰まっていたrenderer初期化問題は、Linux build出力フォルダの混在を避け、必要なLinux出力物をまとめて配置することで解消できる見込みである。

一方で、`NavigationSceneBuilder.Start()`内でランタイムに`Shader.Find("Universal Render Pipeline/Unlit")`、`Shader.Find("Unlit/Color")`、`Shader.Find("Standard")`を順に呼ぶ実装では、Linux player上で全て`null`になり、Material生成時に例外が出た。前回の`ColabSmokeTest`はCamera、RenderTexture、ReadPixelsの確認であり、ランタイムshader lookupやML-Agents接続までは含んでいなかったため、この問題は今回のNavigationFinal側で初めて見えた。

このため、NavigationFinalの単色表示/セグメンテーション用Materialは、ランタイム生成ではなくMaterial assetとしてSceneから参照する方針に変更する。これによりUnityのbuild依存追跡にMaterialとshaderを載せ、Colab headless buildでのshader strippingや名前解決差分に依存しない構成を優先する。

Material asset化後のLinux buildでは、Colab CPU runtime上で`OPENGL LOG: Creating OpenGL 4.5 graphics device`、`Navigation inference model assigned: NavigationFinal.`、`Registered Communicator in Agent.`まで到達することを確認した。残ったtrainer接続待ち問題は、Sceneに推論model assetが入っていることで`InferenceOnly`になっていたことが原因である。これに対して、Colab学習時のみ`--envforge-train`で学習接続を明示する方針にする。

## 2026-05-26時点のColabベンチ結果

CPU runtimeとT4 runtimeで`NavigationFinal`をそれぞれ`100000` trainer stepsまで実行した。どちらもColab上のLinux playerをGoogle Driveからコピーし、trainerを先に起動してからUnityを`xvfb-run`、`-force-glcore`、`--envforge-train`付きで起動した。

| 条件 | run id | 10万step到達時間 | steps/sec | 備考 |
| --- | --- | --- | --- | --- |
| CPU | `bench-cpu-100k` | trainer elapsed `5350.702s` / wall `5355s` | trainer `18.689` / wall `18.674` | 基準値 |
| T4 | `bench-t4-100k` | trainer elapsed `3367.665s` | trainer `29.694` | CPU比で約`1.59x` |

T4実行中の参考リソース利用率は以下だった。

| 指標 | 値 |
| --- | --- |
| GPU utilization | 平均`4.3%`、中央値`1.0%`、最大`39.0%` |
| trainer Python CPU | 平均`78.6%`、中央値`78.7%` |
| Unity CPU active samples | 平均`59.2%`、中央値`59.2%` |

ただし、このT4結果は「Unity描画もGPUに載った結果」ではない。`nvidia-smi --query-compute-apps=pid,process_name,used_memory --format=csv,noheader,nounits`ではtrainer Pythonだけが見え、Unity processは見えなかった。またUnity logでは以下のようにMesa/llvmpipe rendererが使われていた。

```text
Forcing GfxDevice: OpenGL Core
Renderer: llvmpipe (LLVM 15.0.7, 256 bits)
Vendor: Mesa
Version: 4.5 (Core Profile) Mesa 23.2.1-1ubuntu3.1~22.04.3
```

Colab T4 runtime上で`-force-vulkan`、`-force-glcore`、`xvfb-run`有無を変えた短いprobeも行ったが、`-force-vulkan`は`Vulkan detection: 0`で失敗し、OpenGL CoreはMesa/llvmpipe経路になった。Colab環境では`NVIDIA_DRIVER_CAPABILITIES=compute,utility`が見えており、OpenGL/Vulkan/X11向けの`graphics`や`display` capabilityが公開されていない。このため、Colab上でUnityのGPU描画を有効化するのは、Unity起動引数の調整だけでは難しいと判断する。

この結果の解釈は以下である。

- T4 runtimeの高速化は、主にtrainer側のCUDA利用やVM差分による可能性が高い。
- UnityのCameraSensor/RenderTexture生成は、Colab上ではソフトウェアOpenGL経路で動いている。
- 高性能GPU課金の判断をColabだけで続けると、Unity GPU描画の効果を測れない。
- Unity GPU描画込みの評価は、OpenGL/Vulkan graphics workloadを想定したGPU VMで別途確認する必要がある。

## Unity GPU描画を確認する次の環境候補

次の検証はGoogle Cloud Compute EngineのNVIDIA RTX Virtual Workstation構成を第一候補にする。理由は、Google Cloudの公式ドキュメントでRTX Virtual WorkstationがVulkan、OpenGL、Direct3Dを使うworkload向けとされており、Linux GPU workstation手順も用意されているためである。

| 候補 | 評価 | 理由 |
| --- | --- | --- |
| Google Cloud Compute Engine G2 + L4 vWS | 第一候補 | OpenGL/Vulkan workload向けのvWS driver手順があり、Colab/Drive/GCSからの移行がしやすい |
| AWS EC2 G4dn/G5 + NVIDIA GRID driver + Amazon DCV | 第二候補 | G系GPUとDCVでhardware OpenGLの実績があり、選択肢として堅い |
| Azure NVadsA10 v5 / NV系 | 第三候補 | NV familyがgraphics rendering / simulation / virtual desktop向け |
| RunPod / Lambda / Paperspace等 | 慎重枠 | 学習用GPUとしては便利だが、containerに`graphics,display` capabilityを出せるか事前確認が必要 |

GCEでの最初の成功条件は、学習を回すことではなくUnityの描画経路を確認することに限定する。

1. `glxinfo`またはUnity logでrendererが`llvmpipe`ではなくNVIDIA系になる。
2. `nvidia-smi --query-compute-apps=pid,process_name,used_memory --format=csv,noheader,nounits`にUnity processが出る。
3. Unity logに`Renderer: NVIDIA ...`相当が出る。
4. その状態でheadlessまたはvirtual display上のCameraSensor/RenderTextureが動作する。

この4点が揃った場合のみ、GCE上で`100000` trainer stepsの比較ベンチに進む。揃わない場合は、GCE設定を見直すか、Unity描画GPU化より先にColab/CPU上の環境並列化や観測軽量化へ戻る。

## Colabでの注意

Colabでは実行環境が途中で切れることがある。結果はローカルランタイムだけに置かず、Google Driveに保存する。

GPUランタイムに接続しているだけでもcompute unitが消費される可能性があるため、ベンチが終わったらランタイムを切断する。

A100やH100は利用可能性が変動する。今回の検証では、取れないGPUのために長時間待つより、CPUとT4を先に取り、必要ならL4またはA100を追加する。

## 次の分岐

### 高性能GPUで速くならない場合

この場合は、GPU選定よりも以下を優先する。

- Unity headlessの`time-scale`とフレーム制約を見直す
- Unity環境を複数並列で起動できるか確認する
- 画像観測の生成コストを分離して測る
- CameraSensorなし、vector observationのみの速度比較を追加する
- RunPodなどの固定GPU環境ではなく、CPUが強いVMも比較候補に入れる

### T4からA100で明確に速くなる場合

この場合は、Colabだけで判断せず、固定GPU環境でも再確認する。

- RunPodや類似環境でL4、RTX 4090、A100のうち1から2種類を試す
- Dockerまたは起動スクリプトで再現性を上げる
- 10万stepではなく50万step程度に伸ばして、長時間時の安定性を見る

## 保留事項

- Unity 6.3 LTSのLinux headless buildは、普段のWindows作業環境からexportし、Google Driveに配置して使う。
- Unity EditorをColab上で直接使う運用は、初回ベンチでは採用しない。
- ML-Agentsの複数Unity環境並列接続を使うかどうかは、この初回ベンチの結果を見て判断する。
- GPU利用率やCPU利用率を自動集計する専用スクリプトはまだ作らない。初回はColab notebook内の簡易ログで十分とする。
