# Screen Specifications

All screens inherit the Waypoint Terrace tokens and keep one primary task per view. Desktop secondary workflows use a right-side or centered focused panel over a dimmed 3D scene. Mobile uses one full-screen panel and a single persistent menu affordance.

## Entry

### Login

- Scene: distant terrace at dawn, no character control.
- Content: logo/crest, login ID, password, one `ログイン` action.
- Errors: one inline sentence under the affected control; no toast duplication.
- No signup, connection status, sync status, or public battle data.

### First password change

- Content: current temporary password, new password, confirmation, one `変更する` action.
- Explain the rule in one short line. Logout is secondary.

## Member

### Home

- Exact layout follows `reference-option-3.png`.
- Top-left: curved menu rail (`ホーム`, `チーム`, `目標`, `記録`, `設定`).
- Top-right: avatar/crest, nickname, level, EXP, today's one-line state.
- World: controllable/idle RPG Tiny Hero on the left staging platform; same-team members in the middle distance; waypoint at right.
- Bottom-center: one primary `開発をはじめる` compass action.
- Lower-right: next raid date/time.

### Development goal

- Focused panel title: `今日の目標`.
- One multiline goal field and one primary `開始する` action.
- A recent-next-task suggestion may appear as one selectable row.

### Active development

- Scene remains visible and calm.
- Panel contains elapsed time, goal, `終了して振り返る`.
- Prevent parallel sessions; recovery affordance appears only for an incomplete session.

### Reflection

- Step 1: achievement slider/selection.
- Step 2: reflection text.
- Step 3: next task.
- Back preserves input. Submit action is visible without scrolling on mobile.

### AI result / approval state

- Show rank, provisional EXP, three short evaluation axes, one feedback paragraph.
- Approval state is one chip (`AI評価待ち`, `承認待ち`, `承認済み`, `要確認`, `却下`).
- No raw model/API terminology or scores without explanation.

### History

- One grouped list with lightweight dividers, date, duration, goal, state.
- Details open as a focused sheet; rows are not individual floating cards.

## Growth

### Character / cosmetics

- Live 3D Tiny Hero preview occupies at least 50% desktop width.
- Slots: head, hair/hat, cloak/body, main hand, off hand.
- Owned items use a compact grid in a single panel. Equip state is immediate and reversible.

### Weapon gacha

- Show ticket/credit balance, a single draw action, and concise odds access.
- Draw result is a dedicated reveal state with the real weapon model/thumbnail, rarity, duplicate compensation, and `装備する`.
- Never imply purchasable currency; credits come from approved development time.

## Community

### Team

- Same-team members only, with nickname, level, this-week approved time, raid role.
- The user's row is emphasized. No private reflection content.

### Ranking

- Tabs: `個人`, `班`; period switch: `今週`, `累計`.
- One ranked list, not metric cards. Respect ranking visibility preference.

### Products

- Public products: name, creator, short description, external-link action.
- Registration is one focused form (`名前`, `URL`, `説明`).

### Achievements

- Earned and pending grouped separately.
- Application form is opened only when requested; special reward is visible before submission.

## Raid

### Scheduled / lobby

- Boss, scheduled time, team readiness, chosen role/weapon.
- One `準備完了` action. Do not expose action controls before the battle opens.

### Active member HUD

- Boss HP/top center, turn and time near it, member HP/MP lower left.
- Bottom command opens one focused command wheel/sheet: attack, skill, guard, support.
- Role/weapon selection is locked for the current turn after action acceptance.
- One action per user/turn; accepted state is unmistakable.

### Raid result

- Outcome, team damage, personal contribution, support contribution, rewards.
- One return action. Rankings are secondary.

### Front display

- No member controls or account chrome.
- Giant boss, team damage, turn/time, highlighted contributor, next highlight.
- Names and short contribution only; never show private development content.

## Mentor

### Overview

- Primary queue count, suspicious count, raid state, and one next operational action.
- Navigation hides all detailed tables until opened.

### Review queue

- Filters: pending, suspicious, AI pending.
- A selected session shows goal, duration, reflection, next task, AI evaluation, flags.
- Actions: approve, correct-and-approve, reject; comment required for reject/correction.

### Accounts / teams

- Account create and issued accounts are separate views.
- Temporary password is displayed once.
- Team assignment is explicit; no random mentor/team inference.

### Raid configuration

- Date/time, boss identity, HP, status. Destructive reset is visually separated.
- Front-display launch is available only from the raid section.

## System states

- Loading: crest pulse and one short verb.
- Empty: one sentence and one relevant action.
- Error: human-readable cause and recovery; implementation/vendor names stay hidden.
- Offline: preserve local input, clearly label unsent state only when it occurs.
