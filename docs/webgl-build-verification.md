# WebGL Build Verification

Issue: #55

Date: 2026-05-30

Unity: 6000.4.3f1

## Command

```sh
/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -nographics \
  -projectPath /private/tmp/AttackOnRasshiine-issue55 \
  -executeMethod AttackOnRasshiine.Editor.RasshiineSceneBuilder.BuildWebGL \
  -quit \
  -logFile /private/tmp/AttackOnRasshiine-issue55-webgl-build.log
```

## Result

- Build result: Success
- Output path: `Builds/WebGL`
- Reported build size: 13.7 MB
- Generated output size on disk: 15 MB

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
