# Battle visual design QA

## Selected reference

- Source: `/var/folders/bt/mhqn437d2hq5szj628j440080000gn/T/codex-clipboard-36bce78f-e65e-4d85-8445-e54116b9606b.png`
- Reference viewport: `1672 x 941`
- Target state: active boss battle
- Target qualities: saturated cyan sky/water, warm coral canyon, lime/violet accents, matte faceted low-poly geometry, one large right-side boss, three readable foreground heroes, white/purple command controls, dark compact boss HP bar.

## Functional and visual QA inventory

| Claim or state | Functional check | Visual evidence |
| --- | --- | --- |
| Active battle | Normal attack and guard submit through the authoritative battle action path; skill opens the complete command deck | Active battle at reference, desktop, portrait, and compact viewports |
| Scheduled battle | Member refresh/log and mentor start controls remain usable | Member and mentor scheduled states at three viewports |
| Expanded commands | Role, weapon, action, close, and result controls remain real 44 px targets | Expanded command state at three viewports |
| Cooperative turn | Expanded state summary does not cover boss HP or action controls | Cooperative turn at three viewports |
| Result | Summary and focused result details are mutually exclusive, not stacked | Result state at three viewports |
| Responsive fit | Canvas exactly matches viewport; no page scrolling, clipping, or text overflow | `1440x1024`, `844x390`, `390x844` |
| WebGL budget | Shared meshes, no battle post-processing, no extra point lights, bounded heap | Runtime metrics and release artifact verification |

## Comparison history

- Pass 1: `reference-vs-built__1672x941.png`
  - Rejected: upright boss, muted brown terrain, thin lagoon, central six-member crowd, oversized translucent cloud facet, duplicate result HUD.
- Pass 2: `reference-vs-built__1672x941-pass2.png`
  - Improved: low-poly beetle boss, clearer coral/violet palette, right-shifted composition, turn-order strip, mutually exclusive result HUD.
  - Rejected: boss and heroes remained too small; six visible heroes still crowded the arena; procedural cloud artifact remained.
- Pass 3: `reference-vs-built__1672x941-pass3.png`
  - Improved: three visible party members while retaining the six-member battle state, larger horizontal beetle boss, Tiny Hero role variants, bounded mesh clouds.
  - Rejected: camera was too close, sky read as flat cyan, and the bridge/waterway depth was absent.
- Pass 4: `reference-vs-built__1672x941-pass4.png`
  - Improved: pulled-back camera, central cyan waterway, seven-part shared-mesh bridge, brighter coral ground, softer short shadows.
  - Rejected: boss, heroes, and action HUD were still materially smaller than the selected reference.
- Pass 5-6: `reference-vs-built__1672x941-pass5.png`, `reference-vs-built__1672x941-pass6.png`
  - Improved: boss and heroes reached the intended visual weight; three camera-relative white faceted cloud groups; compact upper-left boss bar; readable stat cards; enlarged command controls.
  - Remaining correction: deepen the sky blue and increase the visible hex-command footprint without changing constrained layouts.
- Pass 7: `reference-vs-built__1672x941-pass7.png`
  - Rejected on senior review: the palette was directionally aligned, but the boss, heroes, environment depth, and HUD scale remained materially below the selected reference.
- Pass 12: `reference-vs-built__1672x941-pass12.png`
  - Improved: readable white stat cards and larger hex commands.
  - Rejected: box-shaped oversized boss, repeated heroes, missing action-order portraits, and weak bridge/water depth.
- Pass 13: `battle-active__reference-1672x941-pass13.png`
  - Improved: blue/red/green party separation, lower beetle stance, continuous visual hierarchy, and action-order structure.
  - Rejected: segmented bridge reads as disconnected debris; boss shell/horns still read as stacked bars; HP/MP values are absent; action-order portraits are code-native approximations instead of authored assets; boss HUD material and spacing diverge from the reference.
- Pass 14: `reference-vs-built__1672x941-pass14.png`
  - Improved: authored portrait sprites, numeric stat values, clearer selected command scale.
  - Rejected: boss remained a long block assembly; heroes were too small and right-shifted; bridge and rear canyon were too low.
- Pass 15-17: `battle-active__reference-1672x941-pass15-valid2.png`, `battle-active__reference-1672x941-pass16.png`, `battle-active__reference-1672x941-pass17.png`
  - Improved: direct latest-build QA path, full-size heroes, connected suspension bridge, low-poly ellipsoid shell, tapered coral horn, cyan eyes, and reference-sized boss.
  - Continued correction: raised the rear canyon layers and bridge, reduced dorsal-spike height, expanded the boss and moved the party left.
- Pass 18-20: `battle-active__reference-1672x941-pass18.png`, `battle-active__reference-1672x941-pass19.png`, `battle-active__reference-1672x941-pass20.png`
  - Passed: saturated coral/cyan/violet palette, large readable beetle boss, continuous forward horn, three distinct Tiny Heroes, dark enemy pill, authored four-step portraits, unclipped HP/MP cards, aligned white/purple commands, and layered bridge/water/canyon composition all match the selected reference's intended visual hierarchy.

## Final evidence

- Exact reference comparison: `five/AttackOnRasshiineWebFront/output/playwright/visual-qa-bright-battle-2026-07-14/reference-vs-built__1672x941-pass20.png`
- Final implementation capture: `five/AttackOnRasshiineWebFront/output/playwright/visual-qa-bright-battle-2026-07-14/battle-active__reference-1672x941-pass20.png`
- Battle matrix: 6 states x 3 viewports = 18/18 successful captures, no failed state, no document scrolling, exact canvas sizing.
- Full product matrix: 36 states x 3 viewports = 108/108 successful captures at `1440x1024`, `390x844`, and `844x390`.
- Contact sheets:
  - `five/AttackOnRasshiineWebFront/output/playwright/visual-qa-final-2026-07-14/contact-all-desktop-pass7.png`
  - `five/AttackOnRasshiineWebFront/output/playwright/visual-qa-final-2026-07-14/contact-all-portrait-pass7.png`
  - `five/AttackOnRasshiineWebFront/output/playwright/visual-qa-final-2026-07-14/contact-all-compact-pass7.png`
- Unity EditMode: 410/410 passed after the final visual correction.
- Final direct Visual-QA browser runtime: exact `1672x941` canvas and viewport, zero page/console errors; the six Chromium internal-format warnings are the known raw-Unity-template probes already filtered by the production shell guard.
- Final captured JavaScript heap: `37,635,913` bytes; the authored boss reuses three shared low-poly meshes and stays under the enforced 2,600-triangle aggregate budget.
- Visual-QA WebGL heap observed in every capture: `173,670,400` bytes; canvas bitmap equals CSS viewport in all captures.
- Production release `unity-26b1783cab78`: `22.4 MB` compressed build, all four Unity artifact headers passed, and the 64-client/320-request web smoke passed at `89 ms` p95 client latency.
- Production WebAssembly contract: `32 MB` initial heap, `512 MB` hard maximum, geometric growth. The live heap stabilized at `170,196,992` bytes and was byte-identical after more than three minutes; the GC-normalized JS heap remained under `8 MB`.
- Local production backend gate: contract `2026-07-14.1`, DB lint clean, pgTAP `156/156`, Edge unit `38/38`, Edge integration `9/9`, security/target guards `19/19`, and 60 simultaneous logins plus 600 authenticated snapshot reads with zero failures (`441 ms` login p95, `229 ms` snapshot p95).
- Dependency/security gate: production and full `npm audit` both reported zero vulnerabilities; release configuration rejects secret/service-role keys, local fixture identities, unsafe endpoints, and demo fallbacks.
- Hosted backend gate: the currently deployed `game-api-v2` still reports contract `2026-07-12`, so the production health probe correctly fails closed with HTTP `409 contract_mismatch`. Deploying the verified local migrations/functions is the only remaining release integration action and requires access to that Supabase project.

final result: passed
