# Udon 3D Pathfinding

[Compeito](https://github.com/phi16/Compeito) GPGPU カーネルを使用した VRChat ワールド向け GPU ベース 3D 経路探索。

[English](README_en.md)

## 機能

- **GPU ウェーブフロント BFS** — Compeito コンピュートシェーダーによる密な 3D ボクセルグリッド上の経路探索
- **UdonSharp 対応** — VRChat Udon ランタイムで完全動作
- **任意のコライダータイプ** — Box / Sphere / Capsule / Mesh コライダーすべて対応
- **OverlapBox ベースのボクセル塗り** — 壁ボクセルがコライダー体積を完全に覆う
- **パススムージング** — SphereCast による角切りで不要なウェイポイントを削除
- **インスタンス描画** — `VRCGraphics.DrawMeshInstanced` で壁・パス・マーカーを描画（GameObject 生成なし）
- **ステートレスなパスユーティリティ** — `Vector3[]` ウェイポイントを直接渡す、隠し状態なし
- **フォローターゲット API** — パス上の最近傍点にスナップ → ウェイポイント沿いに進む、簡易版とフル版あり
- **カスタムインスペクター** — 日英バイリンガル HelpBox、Scene ビューのグリッドハンドル、壁ボクセルプレビュー、リビルドボタン、マテリアル自動割り当て
- **PC 専用** — Quest 非対応（Compeito は GPU コンピュートが必要）

## 動作要件

| パッケージ | バージョン |
|---|---|
| `com.vrchat.worlds` | >=3.10.3 |
| `com.imaginantia.compeito` | >=1.0.1 |
| Unity | 2022.3 |

## インストール

VRChat Creator Companion でこのリポジトリを VPM リポジトリとして追加し、パッケージをインストールしてください:

```
https://github.com/nyakomechan/Udon-3D-Pathfinding.git
```

または、このフォルダをプロジェクトの `Packages/` ディレクトリ以下に手動で配置してください。

## クイックスタート

1. シーン内の GameObject に `UdonPathfindingManager` を追加
2. インスペクターで壁となる `Collider[]` を設定（任意のコライダータイプ・回転に対応）
3. グリッドサイズ（`gridSizeX/Y/Z`）、`cellSize`、`gridOrigin` を設定
4. `PathfindCompeito` マテリアルはインスペクターを開いたときに自動で割り当てられます
5. 任意の UdonSharpBehaviour から `RequestPath` を呼び出す:

```csharp
public UdonPathfindingManager manager;

void Start()
{
    manager.RequestPath(startPos, goalPos, this);
}

public void OnPathFound()
{
    Vector3[] wps = manager.waypoints;
    // パスを使用...
}
```

## API リファレンス

### パスリクエスト

| メソッド | 説明 |
|---|---|
| `RequestPath(Vector3 start, Vector3 goal)` | デフォルトのレシーバー/イベントでパス要求をキューに追加 |
| `RequestPath(Vector3 start, Vector3 goal, UdonSharpBehaviour receiver)` | カスタムレシーバーを指定してキューに追加 |
| `RequestPath(Vector3 start, Vector3 goal, UdonSharpBehaviour receiver, string foundEvent, string failedEvent)` | カスタムレシーバーとイベント名を指定してキューに追加 |

結果はレシーバーへ `SendCustomEvent` で通知されます。`OnPathFound` 後に `manager.waypoints`（`Vector3[]`）を読み取ってください。

### パス結果フィールド

| フィールド | 型 | 説明 |
|---|---|---|
| `waypoints` | `Vector3[]` | スムージング済みのパスウェイポイント |
| `pathFound` | `bool` | 最後の探索が成功したか |
| `pathError` | `string` | 失敗時のエラーメッセージ |
| `isBusy` | `bool` | 探索中かどうか |
| `startWorld` / `goalWorld` | `Vector3` | 実際に使用された開始/目標位置 |
| `searchIterationCount` | `int` | ウェーブフロントの総反復回数 |
| `searchFrameCount` | `int` | 経過フレーム数 |

### パスユーティリティ API（ステートレス — `Vector3[] wps` を引数に渡す）

| メソッド | 戻り値 | 説明 |
|---|---|---|
| `GetPathLength(Vector3[] wps)` | `float` | 全セグメントの距離合計 |
| `GetClosestPointOnPath(Vector3[] wps, Vector3 pos)` | `Vector3` | パス上の `pos` に最も近い点 |
| `GetClosestWaypointIndex(Vector3[] wps, Vector3 pos)` | `int` | `pos` に最も近いウェイポイントのインデックス |
| `ResamplePath(Vector3[] wps, float spacing)` | `Vector3[]` | 等間隔にリサンプリングしたパス |

### ウェイポイント進行 API（ステートレス — `int index` を引数に渡す）

| メソッド | 戻り値 | 説明 |
|---|---|---|
| `GetCurrentWaypoint(Vector3[] wps, int index)` | `Vector3` | `index` 番目のウェイポイント |
| `GetRemainingDistance(Vector3[] wps, int index)` | `float` | `index` から終点までの距離 |
| `GetProgress(Vector3[] wps, int index)` | `float` | 進行度 `0..1`（距離ベース） |
| `AdvanceWaypoint(int index, int count)` | `int` | 次のインデックス、終点で `-1` |
| `ResetWaypointProgress(int count)` | `int` | 初期目標インデックス（`count > 1` なら `1`、それ以外 `0`） |

### フォローターゲット API

**簡易版（ステートレス、インデックス管理不要）:**

```csharp
Vector3 target = manager.GetFollowTarget(wps, transform.position);
// 毎フレーム target に向かって移動
```

**簡易版 + ゴール判定:**

```csharp
Vector3 target = manager.GetFollowTarget(wps, transform.position, out bool reachedGoal);
if (reachedGoal) { /* 到達 */ }
```

**フル版（インデックス追跡あり、O(1) / フレーム）:**

```csharp
int followIndex = -1; // -1 = スナップ段階

void Update()
{
    Vector3 target = manager.GetFollowTarget(
        wps, transform.position,
        followIndex, snapDistance, reachDistance,
        out followIndex, out bool reachedGoal);

    if (reachedGoal) { /* 到達 */ }
    // target に向かって移動...
}
```

| `currentIndex` | 段階 | 動作 |
|---|---|---|
| `-1` | スナップ | パス上の最近傍点へ移動 |
| `>= 0` | 追跡 | `wps[currentIndex]` へ移動、到達で次へ進む |

### グリッド / ボクセル API

| メソッド | 戻り値 | 説明 |
|---|---|---|
| `WorldToLeaf(Vector3 worldPos)` | `int` | ワールド位置のリーフインデックス、または `-1` |
| `LeafToWorld(int leafIdx, bool center)` | `Vector3` | リーフのワールド位置 |
| `FindNearestEmptyLeaf(Vector3 worldPos)` | `int` | `worldPos` に最も近い歩行可能リーフ |
| `Rebuild()` | `void` | コライダーからグリッドを再構築し GPU を再初期化 |
| `GetCellSize()` | `float` | ボクセルセルサイズ |
| `GetGridOrigin()` | `Vector3` | グリッド原点 |
| `GetGridSizeX/Y/Z()` | `int` | グリッド寸法 |

## コンポーネント

| コンポーネント | 型 | 説明 |
|---|---|---|
| `UdonPathfindingManager` | UdonSharpBehaviour | 経路探索エンジン本体（GPU BFS、ボクセルグリッド、パス再構築） |
| `UdonPathfindDrawer` | UdonSharpBehaviour | `VRCGraphics.DrawMeshInstanced` による壁・パス・マーカーのインスタンス描画 |
| `UdonPathfindVisualizerMono` | MonoBehaviour | Editor モード用デバッグビジュアライザ（VRChat ランタイム非対象） |
| `UdonPathfindingManagerEditor` | Custom Editor | バイリンガル HelpBox、Scene ハンドル、壁プレビュー、リビルドボタン付きインスペクター |

## 仕組み

1. **ボクセル化** — 壁コライダーを `Physics.OverlapBoxNonAlloc` で密な 3D バイトグリッドにサンプリング。壁ボクセルがコライダー体積を完全に覆う。
2. **リーフマッピング** — 歩行可能ボクセルをリーフインデックスにマッピング。6 方向の隣接接続を事前計算。
3. **GPU BFS** — ウェーブフロント展開シェーダー（Compeito）が、テクスチャに格納された隣接グラフ上で開始ノードから伝播。各反復でゴール検出パスが到達をチェック。
4. **リードバック** — `VRCAsyncGPUReadback` が親ポインタテクスチャを取得。ゴールから開始点まで逆追跡でパスを再構築。
5. **スムージング** — `Physics.SphereCast` で壁が遮らない角を切り、不要なウェイポイントを削除。

## 制限事項

- **PC 専用** — Compeito は Quest で利用不可の GPU コンピュートを使用
- **グリッドサイズ** — 最大テクスチャ寸法に制限。`gridSizeX * gridSizeY * gridSizeZ` は妥当な範囲に収めること（32^3 = 32768 ボクセルまでテスト済み）
- **壁レイヤー** — `wallColliders` のレイヤーから自動検出。`SphereCast` スムージングでのみ使用
- **同時に1パスのみ** — リクエストはキューに入り順次処理される

## サンプル

Unity Package Manager から **Demo Scene** サンプルをインポートすると、フォロ移動の動作例を確認できます。

## ライセンス

MIT

## 作者

nyakomake — https://github.com/nyakomechan
