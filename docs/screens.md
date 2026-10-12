# Makdous (مكدوس) — screen descriptions

A weekly family meal planner (mobile-first PWA, max content width 600px, centred on desktop).
Arabic-first with full RTL support; also English and German. This document describes every screen
as it is built today, plus where I think the design is weakest, so you can paste a screen's section
into a design tool and ask for an improved version.

---

## 1. Global design system

**Personality:** calm, clean, family-friendly, "radio-app" inspired (shared with two sibling apps via the
Nxt.UI component library). Large rounded surfaces, black pill primary buttons, one hot pink-red accent.

| Token | Light | Dark |
|---|---|---|
| Page background | `#f7f7f9` | `#000000` |
| Card / sheet | `#ffffff` | `#1c1c1e` |
| Inset (inputs, chips, pills) | `#f2f2f7` | `#2c2c2e` |
| Text / secondary text | `#1c1c1e` / `#8e8e93` | `#f2f2f7` / `#98989f` |
| Accent (pink-red) | `#ff2d55` | `#ff375f` |
| Primary control ("ink") | `#1c1c1e` black | `#3a3a3c` |
| Border | `#e5e5ea` | `#38383a` |
| Success / warning / danger | `#22a85a` / `#b7791f` / `#e11d48` | `#30d158` / `#ffd60a` / `#ff375f` |

- **Theme:** user chooses System / Light / Dark in Settings.
- **Font:** Cairo (Arabic-friendly), system sans for Latin in LTR.
- **Radii:** 12 / 16 / 28 / 32 px, fully round pills (999px) for buttons and chips.
- **Shadows:** very soft; cards rely on a 1px hairline border plus a light shadow.
- **Icons:** thin 2px-stroke line icons (24px) mixed with emoji (🍽️ 🛒 ✨ 👋 🔁 🔒) in headers and empty states.
- **Recurring parts:** page header (icon + big title + grey subtitle), white rounded cards, black pill buttons,
  grey pill chips (active chip = black), bottom sheets (grabber handle, rounded top corners, dimmed backdrop),
  toasts, empty states (emoji/illustration + title + hint + button).
- **Meal pictures:** no photos. Every meal gets a flat SVG plate illustration (pasta, soup, rice, burger,
  pizza, fish, salad, etc., 15 total) on a light rounded tile.
- **Direction:** layout mirrors in RTL (default). Day names, dates and months are localised.

### Bottom navigation (on every main screen, hidden on sign-in)
Fixed to the bottom, white/dark bar with rounded top corners, five **icon-only** tabs (no text labels):
1. **Week** – house icon
2. **Meals** – fork-and-knife icon
3. **AI Assistant** – a "+" icon (centre tab)
4. **Shopping** – shopping-bag icon
5. **Friends** – two-people icon

Active tab = icon inside a filled black rounded square (white glyph). Inactive = grey glyph.

---

## 2. Screens

### 2.1 Week (home, `/`)
**Purpose:** see and edit the seven days of the current week at a glance.

**Layout, top to bottom**
1. **Header block** (white, rounded bottom corners):
   - Top row: person icon (opens Settings; a small red dot appears for guests who haven't chosen a
     username) and a share icon (exports the whole week as a PNG image via the native share sheet).
   - Small pink uppercase eyebrow "Weekly plan", huge wordmark **مكدوس**, grey date range
     ("Mon 21 Sep – Sun 27 Sep").
   - **Day strip**: round prev/next-week arrows on both ends and seven pill buttons (short day name over date
     number). Today = black pill with a small pink "needle" at the bottom, like a radio tuner. Tapping a day
     smooth-scrolls to its card.
   - Full-width outlined pink pill button with a dice icon: **"Surprise My Week"** (fills only empty days).
2. **Seven day cards** in a vertical list:
   - *Planned day:* square illustration tile on the leading side; date line ("Monday 21 Sep");
     meal name with a type emoji (🥩 🍗 🐟 🥦 🌱); optional ⭐ Favorite badge; a row of four round icon buttons:
     edit, "suggest another" (dice), ingredients (bulleted list), favourite (heart).
   - *Empty day:* dashed pink-tinted card with the date and a dice icon + "Surprise me" — one tap fills it.
   - *Past days:* faded and slightly desaturated.

**Bottom sheets from this screen**
- **Day actions** (tap a card or the edit icon): Change meal → opens Meals picker for that day;
  Surprise me; Move to another day (list of the other days); Remove from day (red).
- **Ingredients sheet:** dish illustration tile, title, "Ingredients" bullet list, "Side dishes" chips.
- **Surprise-my-week confirmation:** title, one-line reassurance, confirm button.

**States:** loading text; toast "✅ Added X to Monday" after returning from Meals; card "flip" animation
when a meal changes; selected-day highlight ring while navigating to the picker.

**Design pain points:** header is tall and busy (two icon buttons, eyebrow, wordmark, range, day strip, CTA);
each card repeats four same-weight icon buttons so nothing is primary; the day strip duplicates the list
below it; no obvious way to jump to a month view any more.

---

### 2.2 Meals (`/meals`)
**Purpose:** browse the meal library, favourite meals, add new ones, or pick a meal for a specific day.

**Two modes**
- *Browse mode:* normal header "Meals — browse, save favourites, pick a day".
- *Pick mode* (arrived from "Change meal" on a Week card): a pink-tinted header reading
  "Add meal to / Monday 21 Sept / Choose meal", and the card buttons say "Select this meal".

**Layout**
1. Search field (rounded, inset): "Search meals or ingredients…".
2. Row: outlined **＋ New meal** button and a chip toggling "current language only / all languages".
3. "Type:" label and chip row wrapping over 2–3 lines: All · 🥩 Meat · 🍗 Chicken · 🐟 Fish & Seafood ·
   🥦 Vegetarian · 🌱 Vegan (active chip = black).
4. Optional success banner (pink tint) after editing/hiding a meal.
5. **Meal cards** (list): illustration tile, meal name, small type badge, then three actions in a row —
   ingredients icon, heart (favourite), and a text pill button ("Use this meal" outlined in browse mode,
   black "Select this meal" in pick mode). Tapping the card body in browse mode opens the edit sheet.
6. Empty state when filters match nothing (🍳 "No meals found — try changing your search or filters").

**Sheets:** day picker ("Add this meal to which day?" – list of weekdays); **New meal** (name field,
type chips, "Save and select"); **Edit meal** (name, type chips, Save changes, Use this meal, red
"Hide meal from library"); the shared Ingredients sheet.

**Design pain points:** the filter area takes about a third of the first screen before any meal appears;
chips wrap unevenly; cards crowd five controls into a small area; the library has about 90 meals across three languages with no
grouping, sections or images beyond the generic plate; spacing in this screen is inline-styled and less
consistent than the rest.

---

### 2.3 AI Assistant (`/ai-plan`, centre tab)
**Purpose:** talk to Gemini to plan meals and manage the shopping list in natural language
("what can I cook with rice and chicken?", "add pizza Friday", "take milk off my list").
It **discusses first**: concrete changes are proposed as a list and only saved after the user agrees
in the chat.

**Layout**
1. Page header: ✨ **AI Assistant** — "Describe your week in your own words".
2. **Empty state:** an animated glowing orb with a sparkle (pulsing rings, an orbiting dot, twinkling
   stars), hint text "Tell me what to add and I'll take care of it", and three **suggestion chips**
   (ingredients idea, plan my week, shopping list).
3. **Conversation thread:** user messages are black right-aligned bubbles; assistant replies are
   white/card bubbles on the other side. A reply can contain headed lists: *Meals* (name + date),
   *Add to shopping list*, *Remove from shopping list*, followed by a hint to confirm. After confirming,
   a "Done! Here's what I did" bubble lists what was saved (and anything not found). Errors are red-tinted
   bubbles, with a "Go to settings" link when no API key is set. While waiting: a bubble with three
   animated dots + "Thinking…".
4. **Composer** pinned above the nav bar: rounded text box with a microphone (dictation) icon and a round
   send arrow inside the box.

**Design pain points:** the proposal list is plain text rows with no per-item remove/edit; no visual
distinction between "proposal" and "applied"; the centre tab uses a "+" icon although it opens the AI;
the thread has no timestamps or day grouping.

---

### 2.4 Shopping (`/shopping`)
**Purpose:** shopping lists organised **by store**.

**Layout**
1. Header: basket icon tile + "Shopping" + "Organise your shopping by store".
2. Full-width black pill **+ Add store**.
3. One **card per store**: store icon + name, "N items", red-outline trash icon; an add-item row
   (text field + "Add" button); the item list — each row is a rounded grey strip with a round check
   toggle, the item name (strikethrough + faded when bought; bought items sink to the bottom) and a
   delete icon.
4. **Empty state:** 🛒 "Shopping list is ready — add a store and list what you need" with an
   "Add first store" button.

**Sheets:** New store (name field, e.g. "REWE"); Delete store confirmation (red button naming the store).

**Design pain points:** one level of grouping only (no categories/aisles); no quantities; no way to add
all ingredients of a planned meal; every store card is the same height-heavy white box; bought items
are not collapsed.

---

### 2.5 Friends (`/friends`)
**Purpose:** share plans with friends and copy a friend's week.

**Layout**
1. Header: 👋 Friends — "Plan your week together".
2. **No username yet:** locked empty state (🔒) + "Go to settings".
3. Otherwise:
   - **Your username card:** pink gradient card with ✨, "Your username", the name in bold, copy icon.
   - **Add a friend:** username input + round "+" button; inline success/error line below.
   - **Friend requests** (only if any): rows with avatar initial, name, username and ✓ / × buttons.
   - **Your friends:** collapsible rows (avatar initial, name, username, chevron). Expanding shows a dark
     panel with the friend's week: title "Week of <name>", date range, prev/next arrows, a two-column
     grid of seven small day tiles (day label + meal name or "—"), and a full-width pink pill
     **"Sync my week"**. It opens a confirmation sheet ("This will replace your plan for this week with
     <name>'s plan"), then copies their week onto yours.
   - **People on Makdous:** discoverable users list with a black round "+" to send a request.

**Design pain points:** several strings here are still Arabic-only (not yet localised); the friend week
panel is a heavy dark block inside a light page; avatars are only initials; no way to see a friend's
meal illustrations; copying a week has no preview of what will be overwritten.

---

### 2.6 Settings (`/settings`)
Reached from the person icon on the Week screen (not in the bottom nav). Built from the shared family
settings shell: a stack of white rounded cards, each with an emoji + title + description.
1. **Username** (👤): current name in a mono code box with copy icon; "change username" input + round
   black check button; live availability message ("Username is available ✓").
2. **Recover my plan** (🔁): explanation + link to the recover screen.
3. **Language** (🌐): segmented control — 🇸🇾 العربية · 🇬🇧 English · 🇩🇪 Deutsch.
4. **Appearance** (🌓): segmented control — System · Light · Dark.
5. **AI Assistant** (✨): Gemini API key (password field) with "get a free key" link, model dropdown with a
   refresh icon button, save button.
6. **Install on home screen** (📲): link to the illustrated install guide.
7. **About:** app logo, name, short description, version, developer link, and a "More apps" row of the
   sibling apps with their logos.

---

### 2.7 Secondary screens

**Recover my plan** (`/settings/recover`) — header 🔁; intro paragraph in a tinted card; "Previous
username" input with a search button; if found: avatar + name card, amber warning ("This device will switch
to user …"), black "Recover my plan now" + ghost cancel; back button.

**Install guide** (`/settings/install`) — header 📲; a 3-step **screenshot carousel** (phone screenshots
with a step badge and caption "Tap the share button" → "Add to Home Screen" → "Add"), Back/Next buttons and
"Step 1 of 3" progress; below it a "works on Android and iPhone/iPad" card and a green tip card.

**First-launch install wizard** — modal over the first screen: close ✕, title "Add Makdous to your home
screen", the same carousel, "Skip setup for now". On iOS Safari a separate smaller sheet shows a looping
GIF explaining "Share → Add to Home Screen" with Replay / Got it.

**Sign-in** (`/signin`) — centred card with floating food emoji (🍳🥑🍕🍓), "Quick sign-in", an email field
and a "Continue 🚀" button. Rarely seen: new visitors are signed in automatically as a guest. Still
Arabic-only and visually older than the rest of the app.

**Email-link confirmation** — small modal asking for the email address used for a sign-in link.

**Meal details** (`/meal/{id}`) — "Close" at the top, a hero card (large illustration tile, meal name,
type badge, ⭐ badge), sections "Ingredients" (bullet list) and "Side dishes" (chips), and a Favourite
toggle button. Currently reachable only from the month view / direct links.

**Month calendar** (`/month`) — header with prev/next round buttons and the month name; Monday-first
weekday row; 7-column grid where each cell shows the day number and a truncated meal name (today outlined,
⭐ for favourites); tapping a cell opens meal details. Currently **not linked from anywhere** in the app.

**Not found** — plain message page.

**Legacy/unused screens** (kept in code, not linked): Edit meal wizard (`/edit/{id}`, three steps for
name, type, ingredients and favourite), simple ingredients checklist (`/list`), history import and an admin
reset page. Safe to ignore when redesigning.

---

## 3. Cross-cutting issues worth a design pass
1. **Bottom nav has no labels** and the centre "+" no longer adds anything — it opens the AI. A sparkle
   icon and/or labels would fit better.
2. **Inconsistent density:** Week and Friends are dense; Shopping and Settings are airy.
3. **Illustrations are generic:** all meals of one kind share the same plate image; no photography.
4. **Too many equally weighted icon buttons** on Week and Meals cards.
5. **No onboarding/first-run content** beyond the install wizard: an empty new account auto-fills two
   weeks with random meals, which can feel surprising.
6. **Month view and meal details are orphaned** — decide whether to bring them back (e.g. a month toggle on Week).
7. **Emoji + line icons mix** in headers and chips.
8. **Dark theme** exists via tokens but was mainly checked on a few screens.

## 4. Suggested prompts for a design tool
- "Redesign the Week screen of a family meal-planning app (RTL Arabic, also LTR). Keep: day strip,
  seven day cards with illustration, per-day actions in a bottom sheet. Goal: calmer header, one primary
  action per card, clearer empty days."
- "Redesign the Meals library: sticky search, horizontally scrolling type chips, 2-column grid of meal
  cards with large illustration, heart and an 'add to day' button."
- "Redesign the AI chat for meal planning: proposals shown as an editable card (each meal/item removable)
  with Apply / Cancel buttons, then a success summary."
- "Redesign shopping lists by store with collapsible stores, bought items collapsed, quick-add, and
  an 'add ingredients from this week's meals' action."
