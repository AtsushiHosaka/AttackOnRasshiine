# AttackOnRasshiine Design System

SDD 5.2の「3D協力レイドバトル / 未来的 / ネオンブルー・マゼンタ・パープル / 視認性重視」に合わせたUnity用の初期デザインシステムです。

## Assets

- `../Suggo Creations/RETROWAVE SKIES Lite/Skybox Materials/Vapor_Skybox.mat`: 実行時に使うRETROWAVE SKIES LiteのSkyboxマテリアル。
- `Materials/Skybox/M_CyberRaid_PanoramicSkybox.mat`: 旧ボス戦背景のPanoramic skybox用マテリアル。
- `Textures/Skybox/T_CyberRaid_PanoramicSkybox.png`: 暗いサイバー空間、ホログラム、レイド床グリッドを持つ2:1背景テクスチャ。
- `Textures/UI/T_UI_Button_*.png`: Primary / Secondary / Dangerの9-slice前提ボタン。
- `Textures/UI/T_UI_Panel_*.png`: レイド演出用パネル、開発ログ用パネル、ステータスカード。
- `Textures/UI/T_UI_Progress_*.png`: HP/MP/進捗ゲージ用の枠とフィル。
- `Textures/UI/T_UI_HexBadge_Frame.png`: 評価・ランク・役割アイコンを載せる六角形フレーム。
- `DesignTokens/RasshiineDesignTokens.json`: 色、9-slice境界、利用方針のトークン。

## Usage Notes

- UIスプライトはSprite設定済みです。Image Typeは `Sliced` にして、ボタンは左右64px/上下42px、パネルは72px、進捗枠は左右42px/上下24pxを基準にしてください。
- 背景はLighting SettingsのSkybox Material、またはScene内のVolume/Camera背景設定から `Vapor_Skybox` を参照してください。
- 文字は白〜薄青を基本にし、画像の明部には直接重ねず、暗いパネル上に配置してください。
- 開発ログ画面はTone/3.pngとTone/4.png同様、情報密度を上げすぎず、主要操作をシアン/マゼンタの発光境界で誘導してください。

## Regeneration

`node Tools/DesignSystem/generate_design_system_assets.mjs` で同じGUIDを保ったまま素材を再生成できます。
