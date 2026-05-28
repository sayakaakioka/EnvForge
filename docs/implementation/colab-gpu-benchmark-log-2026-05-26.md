# Colab GPU Benchmark Log 2026-05-26

## 概要

EnvForge `NavigationFinal`について、Google Colab上でCPU runtimeとT4
runtimeを使い、`100000` trainer
stepsの短時間ベンチを行った。目的は学習性能ではなく、クラウドGPU課金が実行速度に
どの程度効くかを粗く確認することだった。

## 実行前の修正

Colab上でLinux playerを動かすために、以下を確認・修正した。

- Linux buildはGoogle
  Drive上の`/content/drive/MyDrive/colab/EnvForgeNavigationFinal`からColab
  VMへコピーして使った。 -
  Unity起動は`-nographics`ではなく、CameraSensor/RenderTextureを動かすために`xvf
  b-run`と`-force-glcore`を使った。 - Sceneに推論用ONNX model
  assetが割り当てられている場合でもtrainerへ接続できるよう、Colab学習時はUnity起
  動引数に`--envforge-train`を付ける方針にした。 -
  trainerを先に起動し、その後Unity executableを起動する順序にした。 - Colab
  notebook内で`navigation-final-bench-100k.yaml`を生成し、`max_steps`を`100000`
  へ差し替えた。

Unity側では、Linux player上でruntime
`Shader.Find(...)`がすべて`null`になる問題が見えた。このため、NavigationFinalの
単色表示/セグメンテーション用MaterialはMaterial
assetとしてSceneから参照する方針に変更した。

## CPU runtime結果

| 項目 | 値 |
| --- | --- |
| run id | `bench-cpu-100k` |
| final step | `100000` |
| trainer elapsed | `5350.702s` |
| wall elapsed | `5355s` |
| trainer steps/sec | `18.689` |
| wall steps/sec | `18.674` |
| final mean reward | `28.835` |
| final reward std | `83.673` |

保存先はGoogle Driveの以下。

```text
/content/drive/MyDrive/EnvForge/colab-bench/bench-cpu-100k-summary.md
/content/drive/MyDrive/EnvForge/colab-bench/bench-cpu-100k-logs-*.tar.gz
/content/drive/MyDrive/EnvForge/colab-bench/bench-cpu-100k/
```

## T4 runtime結果

| 項目 | 値 |
| --- | --- |
| run id | `bench-t4-100k` |
| final step | `100000` |
| trainer elapsed | `3367.665s` |
| trainer steps/sec | `29.694` |
| CPU比 | 約`1.59x` |
| final mean reward | `21.745` |
| final reward std | `87.266` |

T4実行では、途中でforegroundから再起動を試した失敗ログが同じrun
idのログに混ざった。ただし、最初にbackgroundで起動していた実行は継続し、最終的に
`100000` stepへ到達した。解釈時はこの混入を注意点として扱う。

保存先はGoogle Driveの以下。

```text
/content/drive/MyDrive/EnvForge/colab-bench/bench-t4-100k-summary.md
/content/drive/MyDrive/EnvForge/colab-bench/bench-t4-100k-resource-usage.md
/content/drive/MyDrive/EnvForge/colab-bench/bench-t4-100k-logs-20260526.tar.gz
/content/drive/MyDrive/EnvForge/colab-bench/bench-t4-100k/
/content/drive/MyDrive/EnvForge/colab-bench/interrupted/
```

## T4 runtimeのリソース利用率

`bench-t4-100k-resource-usage.md`に以下の参考値を保存した。

| 指標 | 値 |
| --- | --- |
| GPU utilization | 平均`4.3%`、中央値`1.0%`、最小`1.0%`、最大`39.0%`、`n=26` |
| trainer Python CPU | 平均`78.6%`、中央値`78.7%`、最小`78.3%`、最大`79.2%`、`n=26` |
| Unity CPU all samples | 平均`30.2%`、中央値`58.8%`、最小`0.0%`、最大`59.6%`、`n=53` |
| Unity CPU active samples | 平均`59.2%`、中央値`59.2%`、最小`58.8%`、最大`59.6%`、`n=27` |

利用率確認に使った主なコマンドは以下。

```bash
nvidia-smi \
  --query-gpu=name,utilization.gpu,utilization.memory,memory.used,power.draw \
  --format=csv,noheader,nounits
nvidia-smi \
  --query-compute-apps=pid,process_name,used_memory \
  --format=csv,noheader,nounits
ps -eo pid,pcpu,pmem,comm,args |
  grep -E 'envforge_mlagents|EnvForgeNavigation|Xvfb|python3' |
  grep -v grep
grep -aiE \
  'Forcing GfxDevice|GfxDevice|OpenGL|Vulkan|Renderer:|Vendor:' \
  "$UNITY_LOG"
grep -aiE 'Mesa|llvmpipe|NVIDIA|graphics' "$UNITY_LOG"
```

## Unity GPU描画の確認

T4 runtime上で、UnityがNVIDIA GPUを描画に使っているか確認した。

確認結果は、Unity描画はGPUではなくMesa/llvmpipeのソフトウェアOpenGLだった。

```text
Forcing GfxDevice: OpenGL Core
Renderer: llvmpipe (LLVM 15.0.7, 256 bits)
Vendor: Mesa
Version: 4.5 (Core Profile) Mesa 23.2.1-1ubuntu3.1~22.04.3
```

`nvidia-smi --query-compute-apps=pid,process_name,used_memory
--format=csv,noheader,nounits`ではtrainer Pythonだけが表示され、Unity
processは表示されなかった。Unity processのfile descriptor確認でもNVIDIA
deviceを掴んでいる証跡は見えなかった。

追加probeとして`-force-vulkan`、`-force-glcore`、`xvfb-run`有無を変えた短時間起
動も試した。`-force-vulkan`は`Vulkan detection:
0`で失敗した。Colab環境では`NVIDIA_DRIVER_CAPABILITIES=compute,utility`が見えて
おり、OpenGL/Vulkan/X11で必要な`graphics`や`display`
capabilityが公開されていない可能性が高い。

このため、Colabでは「trainer側CUDAは使えるが、Unity描画GPU化はできない」と扱う。

## 判断

T4はCPU比で約`1.59x`速かったが、Unity描画はGPU化されていない。このため、Colab上
でA100など高性能GPUへ進んでも、UnityのCameraSensor/RenderTexture生成がGPUに載っ
た場合の効果は測れない。

次はGoogle Cloud Compute EngineのNVIDIA RTX Virtual
Workstation構成で、UnityがNVIDIA
rendererを掴めるかだけを先に確認する。成功条件は以下。

1. Unity logまたは`glxinfo`でrendererが`llvmpipe`ではなくNVIDIA系になる。 2.
   `nvidia-smi --query-compute-apps=pid,process_name,used_memory
   --format=csv,noheader,nounits`にUnity processが出る。 3.
   CameraSensor/RenderTextureがvirtual
   displayまたはheadless相当の構成で動作する。 4. この状態でだけ`100000` trainer
   stepsのGCEベンチへ進む。

## 次の作業

- Google Cloud Compute EngineでG2 + L4 vWS相当の最小VMを検討する。 -
  まず学習は回さず、Unity Linux playerのgraphics renderer確認だけを行う。 -
  GCEでUnityがNVIDIA rendererを掴めない場合は、AWS EC2 G4dn/G5 + NVIDIA GRID
  driver + Amazon DCV、またはAzure NV系を次候補にする。

## GCE試行メモ

Google Cloud
CLIはローカルに導入済みで、認証済みアカウントは`untamed.cat.0526@gmail.com`、対
象projectは`rosy-hangout-261702`だった。

Compute Engine APIは未有効だったため、`gcloud services enable
compute.googleapis.com`で有効化した。課金状態は`billingEnabled: true`だった。

`asia-northeast1-a`では`nvidia-l4`と`nvidia-l4-vws`が見え、`g2-standard-4`も利用
候補として表示された。

```bash
gcloud compute accelerator-types list \
  --filter='zone:asia-northeast1-a AND name~nvidia-l4' \
  --format='table(name,zone,description)'
```

結果:

```text
NAME           ZONE               DESCRIPTION
nvidia-l4      asia-northeast1-a  NVIDIA L4
nvidia-l4-vws  asia-northeast1-a  NVIDIA L4 Virtual Workstation
```

次のVM作成を試した。

```bash
gcloud compute instances create envforge-unity-gpu-smoke \
  --zone=asia-northeast1-a \
  --machine-type=g2-standard-4 \
  '--accelerator=type=nvidia-l4-vws,count=1' \
  --maintenance-policy=TERMINATE \
  --restart-on-failure \
  --provisioning-model=STANDARD \
  --boot-disk-size=100GB \
  --boot-disk-type=pd-balanced \
  --image-family=ubuntu-2204-lts \
  --image-project=ubuntu-os-cloud
```

ただし、project全体のGPU quotaが`0`で、VM作成は失敗した。

```text
Quota 'GPUS_ALL_REGIONS' exceeded. Limit: 0.0 globally.
metric name = compute.googleapis.com/gpus_all_regions
limit name = GPUS-ALL-REGIONS-per-project
```

次に進むには、Google Cloud
Consoleで`GPUS_ALL_REGIONS`と、必要に応じて`NVIDIA_L4_GPUS`または`NVIDIA_L4_VWS_
GPUS`相当のquota増枠申請が必要である。今回の最小検証では、まずglobal GPU
quotaを`1`、`asia-northeast1`のL4 vWS GPU quotaを`1`確保できれば足りる。

## GCE L4 vWS結果

Quota増枠後、Google Compute Engineに`g2-standard-4` + `nvidia-l4-vws`
x1のVMを作成し、NVIDIA RTX Virtual Workstation driverを導入した。

| 項目 | 値 |
| --- | --- |
| instance | `envforge-unity-gpu-smoke` |
| machine type | `g2-standard-4` |
| zone | `asia-northeast1-a` |
| CPU | Intel Xeon @ `2.20GHz`, `4` cores |
| memory | Unity log上 `15990 MB` |
| GPU | NVIDIA L4 vWS |
| VRAM | `23034 MiB` |
| driver | `595.71.05` |

XorgをNVIDIA driverで起動し、`glxinfo -B`で以下を確認した。

```text
OpenGL vendor string: NVIDIA Corporation
OpenGL renderer string: NVIDIA L4/PCIe/SSE2
OpenGL core profile version string: 4.6.0 NVIDIA 595.71.05
```

Unity logでも以下を確認した。

```text
Display 0 'NVIDIA VGX  32"': 1024x768 (primary device).
Renderer: NVIDIA L4/PCIe/SSE2
Vendor:   NVIDIA Corporation
Version:  4.5.0 NVIDIA 595.71.05
```

GCE上ではUnity standaloneがデフォルトではML-Agents
trainerへ接続せず、`--mlagents-port
5004`を明示する必要があった。最終的なUnity起動引数は以下。

```bash
./EnvForgeNavigationFinal.x86_64 \
  -batchmode \
  -force-glcore \
  --envforge-train \
  --mlagents-port 5004 \
  -logFile unity.log
```

`100000` trainer stepsの結果は以下。

| 項目 | 値 |
| --- | --- |
| run id | `bench-gce-l4-vws-100k-final` |
| final step | `100000` |
| trainer elapsed | `917.674s` |
| wall elapsed | `920s` |
| trainer steps/sec | `108.971` |
| wall steps/sec | `108.696` |
| final mean reward | `27.650` |
| final reward std | `82.916` |

参考リソース利用率は以下。

| 指標 | 値 |
| --- | --- |
| GPU utilization | 平均`5.129%`、最小`0%`、最大`16%`、`n=31` |
| GPU memory used | 平均`419.548 MiB` |
| power draw | 平均`31.609 W` |
| trainer CPU | 平均`94.626%` |
| Unity CPU | 平均`48.597%` |

`nvidia-smi`では、trainer Pythonがcompute process `C`、Unity playerがgraphics
process `G`として表示された。これにより、Colab T4と異なり、Unity描画がNVIDIA
GPUに載っていることを確認できた。

保存先は以下。

```text
G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-100k-final-20260526T105543Z\
G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-100k-final-20260526T105543Z.tar.gz
G:\マイドライブ\EnvForge\docs\implementation\cloud-benchmark-environment-summary-2026-05-26.md
```

比較すると、GCE L4 vWSはColab T4の`29.694 steps/sec`に対して約`3.67x`、Colab
CPUの`18.689
steps/sec`に対して約`5.83x`だった。ただしこの差はGPU種類だけではなく、CPU
core数、VM基盤、Unity描画がNVIDIA OpenGLに載ったことの差を含む。

## GCE L4 vWS g2-standard-16追加結果

L4より高性能なGPUとして`g4-standard-12` / NVIDIA RTX PRO
6000系も候補にしたが、`GPUS_PER_GPU_FAMILY`の`gpu_family: NVIDIA_RTX_PRO_6000`
quotaが`0`で、増枠申請は一度拒否された。拒否理由は、新規projectまたはbilling
accountの利用履歴不足のため、48時間後の再申請またはbilling
historyの蓄積を待つよう案内されたものだった。

その待ち時間で、同じL4 vWS
1枚のままCPU/メモリだけを増やす比較として、`g2-standard-16`を実行した。比較意図
は、GPUを変えずにCPU側を増やしたときの伸びを見ることである。`g2-standard-4`との
主な違いは、CPUが4 coreから16 core、メモリがUnity log上`15990 MB`から`64298
MB`になった点で、Unity build、`navigation-final-bench-100k.yaml`、`time-scale
10`、NVIDIA Xorg/OpenGL、`--mlagents-port 5004`は同じにした。

| 項目 | 値 |
| --- | --- |
| run id | `bench-gce-l4-vws-g2-16-100k-20260526T115528Z` |
| machine type | `g2-standard-16` |
| GPU | NVIDIA L4 vWS, driver `595.71.05` |
| CPU | Intel Xeon @ `2.20GHz`, `16` cores |
| memory | Unity log上 `64298 MB` |
| final step | `100000` |
| trainer elapsed | `845.964s` |
| wall elapsed | `851s` |
| trainer steps/sec | `118.208` |
| wall steps/sec | `117.509` |
| final mean reward | `24.618` |
| final reward std | `88.708` |

Unity logでは以下を確認した。

```text
Display 0 'NVIDIA VGX  32"': 1024x768 (primary device).
Renderer: NVIDIA L4/PCIe/SSE2
Vendor:   NVIDIA Corporation
Version:  4.5.0 NVIDIA 595.71.05
```

参考リソース利用率は以下。

| 指標 | 値 |
| --- | --- |
| GPU utilization | 平均`5.690%`、中央値`5.000%`、最小`0%`、最大`18%`、`n=29` |
| GPU memory used | 平均`418.586 MiB`、中央値`432.000 MiB` |
| power draw | 平均`31.317 W` |
| trainer main CPU active | 平均`99.750%` |
| trainer child CPU active | 平均`24.971%` |
| Unity CPU | 平均`66.571%` |

保存先は以下。

```text
G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-g2-16-100k-20260526T115528Z\
G:\マイドライブ\EnvForge\gce-bench\bench-gce-l4-vws-g2-16-100k-20260526T115528Z.tar.gz
G:\マイドライブ\EnvForge\docs\implementation\cloud-benchmark-environment-summary-2026-05-26.md
```

`g2-standard-4`の`108.971 steps/sec`に対して、`g2-standard-16`は`118.208
steps/sec`で、約`1.08x`の改善だった。CPU
core数を4から16へ増やしても改善幅は小さく、今回の条件では、単にCPU
core数を増やすよりも、Unity描画がNVIDIA
OpenGLに載るかどうかの方が支配的に見える。
