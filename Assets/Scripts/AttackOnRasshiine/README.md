# AttackOnRasshiine Runtime

SDDに沿ったUnity WebGL向けの軽量プロトタイプ実装です。

## Structure

- `Runtime/Data`: ユーザー、開発ログ、AI評価、成長、ボス戦のドメインモデル。
- `Runtime/Services`: 現在はローカルモックのリポジトリ。Supabase / Edge Functions / Gemini連携はここを差し替える。
- `Runtime/Battle`: 3Dレイド表示と軽量演出。物理同期ではなく、3ターン制の安定進行を優先する。
- `Runtime/Scene`: 本番の Login / Battle / FrontDisplay / MentorDashboard scene 定義。
- `Runtime/UI`: ログイン、メンバーホーム、開発ログ、ボス戦、前画面、メンターダッシュボード。
- `Editor`: シーン、マテリアル、Build Settingsを再生成するEditorユーティリティ。

## Production Scenes

- `Assets/Scenes/Battle.unity`: メンバー操作用。Supabase 設定が有効な本番ではログインから開始し、ローカル未設定時はメンバーで直接プレビューできます。
- `Assets/Scenes/FrontDisplay.unity`: 教室前面表示用。ログイン不要の表示専用 mode で起動し、Supabase 設定が有効な場合は `front-display-snapshot` を polling します。
- `ProductionSceneCatalog` が scene path、ログイン要否、読み取り専用、同期 interval を一元管理します。

## Placeholder Models

メンターとメンバーの3Dモデルは `RasshiineTheme` の `MentorPlaceholderPrefab` と `MemberPlaceholderPrefab` から参照します。
今はBanana Man FBXを代替にしていますが、後から独自モデルのPrefabへ差し替えられます。
参照がない場合は、WebGLビルドが壊れないように軽量プリミティブモデルへフォールバックします。

## WebGL Notes

- 通信・AI評価はクライアント直叩きにしない前提で、現状はローカルルール評価のみ。
- 3D演出はLineRendererと少数ライトに抑え、WebGL埋め込みでも重くなりにくい構成。
- `AttackOnRasshiine/Build Production Scenes` メニュー、または `RasshiineSceneBuilder.BuildProductionScenes` で本番 scene と Build Settings を再生成できます。
