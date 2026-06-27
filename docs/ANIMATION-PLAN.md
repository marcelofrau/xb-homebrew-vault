# Animation & Polish Plan — XBVault

Tracking visual polish work across the app. Each section = one stage. Stages implemented sequentially, each tested before next.

---

## Stage 1 — Page transitions (Carousel) ✅

**Status:** Done  
**File:** `MainWindow.axaml:281-286`  
**What changed:** `CrossFade` → `CompositePageTransition(CrossFade + PageSlide Horizontal)`  
**Effect:** Views slide left/right + fade when switching sidebar tabs. Direction follows tab index (forward slide left, back slide right).

---

## Stage 2 — Sidebar hover + active + brand pulse ✅

**Status:** Done  
**File:** `MainWindow.axaml`, `BladesTheme.axaml`  
**Tasks:**
- [x] Nav items: `:pointerover` with `Opacity` transition (Background removed — not animatable with DoubleTransition)
- [x] Active tab: smooth opacity transition
- [x] Brand logo: subtle pulse keyframe on load

---

## Stage 3 — Connect button pulse ✅

**Status:** Done  
**Files:** `MainViewModel.cs`, `MainWindow.axaml`  
**Tasks:**
- [x] Add `IsConnecting` property to MainViewModel, set before/after modal dialog
- [x] Pulse keyframe on connect button while scanning/discovering (opacity 1→0.4→1, 1.5s loop)
- [x] Normal button hidden during connecting, pulsing button shown with "CONNECTING" label

---

## Stage 4 — Dialog fade+scale ✅

**Status:** Done  
**Files:** `XBVault/Assets/Themes/BladesTheme.axaml`, `XBVault/Views/*.axaml`  
**Tasks:**
- [x] Each modal window: fade-in animation (opacity 0→1, 200ms) via `Window.dialogWindow` style + behavior
- [x] Fade-out on close (opacity 1→0, 200ms) via `DialogFadeBehavior` attached to `Closing` event

---

## Stage 5 — Progress bar shimmer ❌

**Status:** Skipped — testado, descartado pelo usuário  
**File:** `FileExplorerView.axaml`  
**Tasks:**
- [ ] Animated `LinearGradientBrush` sliding across progress bar during active transfer

---

## Stage 6 — Status bar + micro-interactions ✅

**Status:** Done  
**Files:** `MainWindow.axaml`, `BladesTheme.axaml`  
**Tasks:**
- [x] Connection status dot: pulse keyframe when connected (`Image.statusDot` style, opacity 1→0.5→1, 2s loop)
- [x] Global button hover/press transitions (scale 1.02 / 0.98, 100ms `TransformOperationsTransition`)
