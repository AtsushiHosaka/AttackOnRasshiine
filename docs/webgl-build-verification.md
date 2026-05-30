# WebGL Build Verification

Issue: #118

Unity: 6000.4.3f1

## Production Build Contract

- Production scene: `Assets/Scenes/RasshiineProduction.unity`
- Prototype/demo scene excluded from production BuildSettings: `Assets/Scenes/RasshiineRaidPrototype.unity`
- WebGL output path: `Builds/WebGL`
- WebFront artifact mount path: `/unity`
- Runtime config source: `Assets/StreamingAssets/supabase-config.json`
- Runtime config example: `Assets/StreamingAssets/supabase-config.example.json`

## Commands

Generate the production scene, force BuildSettings to the production scene, and build WebGL:

```sh
zsh Tools/build_webgl_production.sh
```

Equivalent Unity batchmode command:

```sh
UNITY_BIN=/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity
"$UNITY_BIN" \
  -batchmode \
  -nographics \
  -projectPath "$PWD" \
  -executeMethod AttackOnRasshiine.Editor.RasshiineSceneBuilder.BuildWebGL \
  -quit \
  -logFile /private/tmp/AttackOnRasshiine-webgl-build.log
```

Copy artifacts to a WebFront checkout:

```sh
zsh Tools/prepare_webfront_unity_artifacts.sh /path/to/AttackOnRasshiineWebFront/public/unity
```

## Verification Checklist

- `ProjectSettings/EditorBuildSettings.asset` contains only `Assets/Scenes/RasshiineProduction.unity`.
- Asset demo scenes under `Assets/BackRock-NeonCity`, `Assets/Heat - Complete Modern UI`, and `Assets/Suggo Creations` are not enabled in BuildSettings.
- `Builds/WebGL/Build/WebGL.loader.js` exists after build.
- `Builds/WebGL/StreamingAssets/supabase-config.json` is injected by the deployment/WebFront pipeline, not committed with secrets.
- `UseDemoRepositoryFallback` is `false` for production deployment config.

## Output Smoke Check

The generated output was served locally with:

```sh
python3 -m http.server 8025 --bind 127.0.0.1 --directory Builds/WebGL
```

HTTP checks:

| Path | Result |
|---|---|
| `/index.html` | 200 OK |
| `/Build/WebGL.loader.js` | 200 OK |
| `/Build/WebGL.wasm.br` | 200 OK |

The generated `Builds/WebGL` directory is intentionally not committed because it is covered by `.gitignore`.
