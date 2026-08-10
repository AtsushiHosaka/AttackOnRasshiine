# AttackOnRasshiine — Waypoint Terrace Design System

## Source of truth

- Reference: `reference-option-3.png`
- Native reference viewport: 1487 × 1058
- Unity/WebGL design viewport: 1440 × 1024 (Scale With Screen Size, match 0.5)
- Mobile verification viewport: 390 × 844
- Direction: premium low-poly fantasy, scene-first HUD, restrained information density

## Visual hierarchy

1. The 3D world and RPG Tiny Hero are the primary content.
2. The current action is a single compass command at the bottom center.
3. Player progression stays in a thin top-right overlay.
4. Navigation uses a small curved icon rail at the upper left.
5. The next raid uses one lower-right ribbon.
6. Secondary workflows open as focused full-height menu panels; they never stack on the home HUD.

## Palette

| Token | Value | Use |
| --- | --- | --- |
| `Ink/Night` | `#112844` | Deep HUD surfaces and text on light panels |
| `Ink/Blue` | `#1C3D5E` | Navigation and secondary surfaces |
| `Sky/Cyan` | `#66D8F2` | Waypoint energy, focus, progress |
| `Sky/Pale` | `#BDECF7` | Soft glow and selected state |
| `Gold/Main` | `#D8BC72` | Primary keylines and icons |
| `Gold/Bright` | `#F1DE9D` | Active highlights |
| `Ivory` | `#F6F0DF` | Text and light menu surfaces |
| `Stone` | `#87909A` | Environment accents |
| `Success` | `#75B989` | Approved and complete |
| `Warning` | `#D6A657` | Pending and attention |
| `Danger` | `#C76D72` | Rejected and destructive |

## Typography

- UI/body: Zen Kaku Gothic New Medium/Bold.
- Display/action: Zen Old Mincho SemiBold (OFL) when added; fallback to Zen Kaku Gothic New Bold.
- Japanese body baseline: 16 px at 1440 × 1024, 14 px minimum on mobile.
- HUD labels: 13–15 px, 0.06 em letter spacing.
- Primary CTA: 30–36 px.
- Never use more than two font families on a screen.

## Reference measurements at 1440 × 1024

- Safe area: 32 px desktop; 16 px mobile.
- Top navigation arc: x 32–520, y 24–188.
- Player status: x 1015–1410, y 36–205.
- World label: x 48–335, y 254–310.
- Hero visual zone: x 185–615, y 300–775.
- Waypoint visual zone: x 1015–1280, y 220–640.
- Primary compass CTA: 292 × 292, centered near x 720, y 878.
- Next raid ribbon: x 965–1410, y 846–982.
- Toast/message: x 32–360, y 954–1004.

## Responsive behavior

- Desktop/tablet keeps the cinematic HUD composition.
- Under 900 px, navigation becomes a bottom menu button; player status collapses to avatar, level, and EXP.
- Mobile opens each workflow as a full-screen focused panel over a paused/lightweight scene.
- The primary action remains above the bottom safe area and never competes with the mobile menu.
- Front-display mode has no interaction chrome and keeps the 3D battle, boss HP, turn, timer, and contributor highlight only.

## Screen families

- Entry: login, first-password change.
- Member: home, development start, active timer, completion reflection, AI result, history.
- Growth: character, equipment, cosmetics, weapon gacha, reward result.
- Community: personal/team ranking, products, achievements.
- Raid: scheduled, role/weapon/action choice, active HUD, result.
- Front display: scheduled, active, contributor highlight, result.
- Mentor: overview, log review, suspicious flags, achievements, accounts, teams, raid configuration.
- System: help, account, error/loading/empty states.

## Non-negotiable QA rules

- No clipped Japanese text, horizontal scroll, overlapping persistent controls, or off-screen primary action.
- No placeholder geometry in shipped scenes.
- No neon-magenta cyber UI from the legacy theme.
- No screen exposes Supabase, sync, online/offline, or implementation-status text.
- Core controls must work; visible static chrome is not accepted for the primary flow.
- Every P0/P1/P2 mismatch against the reference blocks handoff.
