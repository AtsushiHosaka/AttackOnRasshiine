# AttackOnRasshiine Design System

仕様書と選定済み生成案に合わせた、明るいファンタジーRPG調のUnity用デザインシステムです。

## Assets

- `Materials/Skybox/M_CyberRaid_PanoramicSkybox.mat`: 草原・山・青空を持つPanoramic skybox用マテリアル。
- `Textures/Skybox/T_CyberRaid_PanoramicSkybox.png`: 明るい草原レイド画面のための2:1背景テクスチャ。
- `Textures/UI/T_UI_Button_*.png`: Primary / Secondary / Dangerの9-slice前提ボタン。
- `Textures/UI/T_UI_Input_Field.png`: ログイン・開発ログ・申請フォーム用の入力欄。
- `Textures/UI/T_UI_Panel_*.png`: レイド演出用パネル、開発ログ用パネル、ステータスカード。
- `Textures/UI/T_UI_Progress_*.png`: HP/MP/進捗ゲージ用の枠とフィル。
- `Textures/UI/T_UI_HexBadge_Frame.png`: 評価・ランク・役割アイコンを載せる六角形フレーム。
- `DesignTokens/RasshiineDesignTokens.json`: 色、9-slice境界、利用方針のトークン。

## Usage Notes

- UIスプライトはSprite設定済みです。Image Typeは `Sliced` にして、ボタンは左右64px/上下42px、入力欄は左右56px/上下38px、パネルは72px、進捗枠は左右42px/上下24pxを基準にしてください。
- 背景はLighting SettingsのSkybox Material、またはScene内のVolume/Camera背景設定から `M_CyberRaid_PanoramicSkybox` を参照してください。
- 文字色は濃紺を基本にし、背景が明るい画面では淡いアイボリー面の上に配置してください。
- 開発ログやボス戦HUDは最小限の情報だけを初期表示し、詳細はボタンで展開してください。

## Regeneration

`node Tools/DesignSystem/generate_design_system_assets.mjs` で同じGUIDを保ったまま素材を再生成できます。
