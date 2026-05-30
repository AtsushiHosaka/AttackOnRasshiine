# AttackOnRasshiine Runtime

SDDに沿ったUnity WebGL向けの軽量プロトタイプ実装です。

## Structure

- `Runtime/Data`: ユーザー、開発ログ、AI評価、成長、ボス戦のドメインモデル。
- `Runtime/Services`: 現在はローカルモックのリポジトリ。Supabase / Edge Functions / Gemini連携はここを差し替える。
- `Runtime/Battle`: 3Dレイド表示と軽量演出。物理同期ではなく、3ターン制の安定進行を優先する。
- `Runtime/Scene`: 本番Sceneカタログ、Boot初期化、Scene遷移ルーター。
- `Runtime/UI`: ログイン、メンバーホーム、開発ログ、ボス戦、前画面、メンターダッシュボード。
- `Editor`: シーン、マテリアル、Build Settingsを再生成するEditorユーティリティ。

## Placeholder Models

メンターとメンバーの3Dモデルは `RasshiineTheme` の `MentorPlaceholderPrefab` と `MemberPlaceholderPrefab` から参照します。
今はBanana Man FBXを代替にしていますが、後から独自モデルのPrefabへ差し替えられます。
参照がない場合は、WebGLビルドが壊れないように軽量プリミティブモデルへフォールバックします。

## WebGL Notes

- 通信・AI評価はクライアント直叩きにせず、Supabase Edge Function を経由します。demo fixture は明示設定時のみ使います。
- Supabase Edge Function との本番API契約は `docs/supabase-api-contract.md` を参照してください。
- 3D演出はLineRendererと少数ライトに抑え、WebGL埋め込みでも重くなりにくい構成。
- 本番WebGLは `RasshiineBoot` から起動し、`Login` / `MemberHome` / `DevLog` / `MentorDashboard` / `Battle` / `FrontDisplay` の順にBuildSettingsへ登録します。
- `AttackOnRasshiine/Build Production Scene` または `zsh Tools/build_webgl_production.sh` で本番 Scene / WebGL build を再生成できます。
