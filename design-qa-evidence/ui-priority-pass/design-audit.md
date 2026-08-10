# Login UI audit — UI priority pass

## Audit scope

- Surface: Unity WebGL login screen
- User goal: understand the game world, enter credentials, and start without visual or interaction friction
- Fixed constraints: preserve the existing Blender environment geometry and camera composition
- Captures: current implementation at 808×570, 1440×1024, and 390×844

## Step health

1. **Desktop entry (808×570): needs work.** The scenic right side remains visible, but the title touches cloud silhouettes, labels render below a comfortable reading size, and the opaque olive card feels heavier than the world.
2. **Desktop entry (1440×1024): needs work.** The layout is contained, but input fields dominate the card, the status surface looks like a disabled control, and the low-key lighting makes the castle, trees, and ground read as one dark mass.
3. **Mobile entry (390×844): poor.** The card occupies nearly the full viewport, scenic context disappears, and labels/status copy become too small relative to the large empty form surfaces.

## Strengths

- The landscape card anchor leaves useful scenic space on the right.
- Inputs, status, and CTA remain inside the card in all captured sizes.
- The existing Zen Kaku Gothic and Zen Old Mincho font assets can support the intended Japanese fantasy hierarchy.

## Highest-impact issues

1. The frame sprite has an opaque cream center. Gold tint plus navy overlay produces the muddy brown surface; palette tweaks alone cannot fix it.
2. Canvas scaling reduces effective 808×570 label text to roughly 8 px and status copy to roughly 7 px.
3. The title uses the UI sans face rather than the available display serif, weakening the fantasy identity.
4. The card is visually overbuilt: oversized fields, multiple ornamental separators, and a status box that competes with the primary CTA.
5. Login failure rebuilds the form, losing entered values and focus; the in-flight state is not visible.
6. The login status parchment uses a 1024² texture at very low opacity, and runtime-baked button text adds avoidable WebGL memory/work.

## Direction decision

- Option 1 is consistent but visually heavy.
- Option 2 is readable, but the ivory panel creates a second design system that conflicts with the selected home direction.
- **Option 3 is recommended:** bright warm world, compact left-side navy card, thin gold frame, crest-led branding, and clear scenic priority.

## Accessibility and evidence limits

- Screenshot evidence confirms likely contrast, text-size, target-size, and responsive-reflow risks; it does not prove keyboard, screen-reader, zoom, or full WCAG behavior.
- The next implementation pass must test focus order, Enter-to-submit, loading/disabled state, error recovery, and minimum effective text/target sizes.

## Evidence

- `01-current-login-808x570.png`
- `02-current-login-1440x1024.png`
- `03-current-login-390x844.png`
- `option-1-navy-banner.png`
- `option-2-ivory-panel.png`
- `option-3-crest-navy.png`
