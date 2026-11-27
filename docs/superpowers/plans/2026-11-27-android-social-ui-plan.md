# PocketLog Client — Android Social UI ("Locket × Instagram Modern") Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade `PocketLog.Client` WinForms UI to a youthful Android Social aesthetic (`Locket × Instagram Modern`) with seamless Light/Dark mode switching, custom GDI+ photo & story cards without transparent halo artifacts, a double-ring camera shutter dock, and a floating capsule bottom navigation bar with unpaid bill badges.

**Architecture:** Extract theme palette management and state preservation into `Client.Theme.cs`, implement zero-halo double-buffered GDI+ custom controls (`SocialPhotoCanvas`, `StoryAvatarStrip`, `DoubleRingShutterButton`, `GradientPillButton`) in `Client.Controls.cs`, and update the shell plus five screens (`Client.cs`, `Client.Login.cs`, `Client.NewFeed.cs`, `Client.Camera.cs`, `Client.Expense.cs`, `Client.Friends.cs`, `Client.Network.cs`) without touching `PocketLog.Shared`, server projects, or protocol DTOs.

**Tech Stack:** `.NET 8.0-windows` WinForms, `GDI+` (`System.Drawing.Drawing2D`), `Guna.UI2.WinForms 2.0.5.1`.

**Spec:** [2026-11-27-android-social-ui-design.md](file:///D:/clonedoan/docs/superpowers/specs/2026-11-27-android-social-ui-design.md)

## Global Constraints
- Target only `PocketLog.Client` (`net8.0-windows`); do NOT modify `PocketLog.Shared`, `PocketLog.Server`, `PocketLog.Server.Linux`, or database schemas.
- Do NOT add external NuGet packages; use only built-in `.NET 8 WinForms/GDI+` and `Guna.UI2.WinForms 2.0.5.1`.
- Preserve `activeTabs`, `paneHosts`, `btnTabFeed`, `btnTabCamera`, `btnTabExpense`, `pnlHeader`, `pnlMainContainer`, `pnlBottomNav`, `pnlDrawerBackdrop`, `pnlFriendsDrawer`, `HandleBottomTabClick`, `OpenInitialAuthenticatedTab`, `RefreshDataDrivenPanesOnSync`, `OpenMyGalleryScreen`, `StopWebcam`.
- Keep all pill buttons measured with `MeasurePillBtnWidth(...)` and label line heights measured with `"Ág"` (`120 DPI` / 125% scaling safe); maintain `MinimumSize = (445, 580)` and 8-direction `ChildEdgeRedirector` edge resizing.
- Persist theme preference at `%LocalAppData%\PocketLog\theme_mode.txt` (`"dark"` or `"light"`, default `"light"`).
- Do NOT leave test projects or screenshots inside `D:\clonedoan`; keep automated UI harness tests in `%TEMP%` or scratch directory outside the repo.

## Review Focus
1. **Minimum width (`445px`) overlap/clipping on Header & Floating Bottom Nav:** With `btnThemeToggle`, `btnLogoutTop`, window controls, `lblBrand`, and `btnTabFeed/Camera/Expense` + unpaid badge, nothing may collide or clip at `445×680`.
2. **Mid-session Light/Dark toggle preserving unsaved `CAMERA` and `AUTH` state:** Switching themes while typing on `AUTH` or after pasting/snapping a photo + entering expense/tags on `CAMERA` must retain all inputs and `uploadedPhoto`.
3. **`SocialPhotoCanvas` edge cases & resize stability:** Must handle `CustomImage == null`, narrow container (`260px`) resizing up to `760px`, 0/1/many `TaggedFriends`, and long multi-word captions wrapping cleanly within 2 lines without throwing GDI+ errors or leaking bitmaps.
4. **125% DPI Vietnamese diacritic & Story Strip truncation safety:** All new buttons (`Chọn ảnh`, `Dán ảnh`, `+ Thêm bạn`, `Lưu hạn mức`), badges, and Story names (`Đỗ Huy Hoàng`, `Trần Gia Huy`) must render with zero overflow (`issues == 0`).
5. **Dual-pane Full-Screen (`Width >= 1100` / `Maximized`) & Sync badge updates:** Clicking tabs in dual mode, collapsing when shrinking below `1100px`, preserving `CAMERA` during background `SYNC_RES`, and updating the `btnTabExpense` unpaid dot count on `SYNC_RES` must all work in both themes.

---

### Task 1: Theme Palette System & Floating Android Shell (`Client.Theme.cs` + `Client.cs`)

**Files:**
- Create: `D:\clonedoan\PocketLog.Client\Client.Theme.cs`
- Modify: `D:\clonedoan\PocketLog.Client\Client.cs`
- Test: `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\Program.cs`

**Interfaces:**
- Produces (in `Client` partial `Client.Theme.cs` & `Client.cs`):
  - Nested class `AppPalette` (immutable record/class holding `AppBg`, `CardBg`, `InputBg`, `FloatingNavBg`, `BorderLight`, `BorderWindow`, `AccentPrimary`, `AccentSecondary`, `SoftAccentBg`, `SoftAccentBorder`, `TextMain`, `TextMuted`, `TextSecondary`, `Danger`, `SoftDangerBg`, `Success`, `StoryRingStart`, `StoryRingMid`, `StoryRingEnd`, `WalletGradStart`, `WalletGradEnd`, `HeroGradStart`, `HeroGradMid`, `HeroGradEnd`, `DrawerBackdrop`, `ScrimTop`, `ScrimBottom`)
  - Property / Field: `protected bool isDarkMode` & `protected AppPalette CurrentTheme`
  - Colors kept synchronized cleanly on `ApplyThemeColors()`: `clrAppBg`, `clrCardBg`, `clrInputBg`, `clrPrimaryGreen` (mapped to `AccentPrimary`), `clrAccentSecondary`, `clrSoftGreenBg`, `clrSoftGreenBorder`, `clrIndigo` (mapped to `TextSecondary`), `clrSoftIndigo`, `clrRedDanger`, `clrSoftRedBg`, `clrBorderLight`, `clrTextMain`, `clrTextMuted`
  - Methods: `protected void LoadThemePreference()`, `protected void SaveThemePreference(bool dark)`, `protected void ToggleThemeMode()`, `protected void ApplyShellTheme()`, `protected Bitmap CreateVectorIcon(UiIcon icon, Color color, int size = 18)` (expanded with `Moon`, `Sun`, `Eye`, `EyeOff`, transparent-cutout `UiIcon.Brand`)
  - Preserved hooks populated by Tasks 3 & 5: `private Func<(string Ip, string Port, string User, string Pass, bool ShowPass, bool IsRegister, string Budget)> authDraftCapture`, `private Action<(string Ip, string Port, string User, string Pass, bool ShowPass, bool IsRegister, string Budget)> authDraftRestore`, `private Func<CameraDraftState> cameraDraftCapture`, `private Action<CameraDraftState> cameraDraftRestore` (with internal `CameraDraftState` class), `protected int GetUnpaidSplitCount()`
- Consumes (after Task 2 defines `GradientPillButton`): `GradientPillButton` subclassing `Guna2Button` for `btnTabFeed`, `btnTabCamera`, `btnTabExpense` (or initially styled cleanly via custom paint hook on `Guna2Button` / `GradientPillButton` wired in Task 2).

- [ ] **Step 1: Add failing UI harness test for Theme persistence, new vector icons (`Moon`, `Sun`, `Eye`, `EyeOff`), and `445px` header/floating-nav fit**

Add assertions in `scratch/uiaudit/Program.cs` (behind a dedicated `--theme` or sequential test method) expecting `F("btnThemeToggle")`, `M("ToggleThemeMode")`, `M("ApplyThemeColors")`, `isDarkMode == true` toggling `clrAppBg == Color.FromArgb(11, 14, 20)` and reverting back to `Color.FromArgb(244, 246, 251)`, plus asserting zero control overlap at `Size(445, 680)`.

- [ ] **Step 2: Run harness to verify compilation fails because theme members do not yet exist**

Run: `dotnet build "C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\UiAudit.csproj"`
Expected: FAIL referencing missing `ToggleThemeMode` / `btnThemeToggle` / `UiIcon.Moon`.

- [ ] **Step 3: Implement `PocketLog.Client/Client.Theme.cs`**

Define `AppPalette` presets (`LightPalette` and `DarkPalette` matching Spec Section 4.1 RGB values), `CameraDraftState`, `LoadThemePreference()` / `SaveThemePreference(bool)` reading/writing `%LocalAppData%\PocketLog\theme_mode.txt`, `ApplyThemeColors()` syncing all `clr*` fields, `ApplyShellTheme()` re-coloring `this.BackColor`, `pnlHeader`, `pnlMainContainer`, `pnlBottomNav`, `pnlDrawerBackdrop`, `pnlFriendsDrawer`, `btnWinMin/Max/Close`, `btnLogoutTop`, `pnlHeaderUserChip`, `btnThemeToggle`, and `ToggleThemeMode()` capturing/restoring `AUTH` or `CAMERA` drafts while refreshing visible panes.

- [ ] **Step 4: Update `PocketLog.Client/Client.cs` shell, vector icons, and floating bottom nav**

Update `UiIcon` enum with `Moon, Sun, Eye, EyeOff`; update `CreateVectorIcon` (`Brand` using `CompositingMode.SourceCopy` transparent cutout + crisp `Moon`/`Sun`/`Eye`/`EyeOff` paths); convert `clr*` fields from `readonly` to theme-updated fields initialized via `LoadThemePreference()` + `ApplyThemeColors()` before `SetupPhoneShell()`; add `btnThemeToggle` (`34×34` rounded icon button) to `pnlHeader` and account for it cleanly in `LayoutHeaderControls()` (`445px` minimum width hides `pnlHeaderUserChip` below ~510px without ever colliding with `lblBrand` or `btnLogoutTop`); upgrade `pnlBottomNav` (`Height = 82`, seamless `clrAppBg` + owner-painted `Floating Capsule` shadow & `FloatingNavBg` border) and `UpdateBottomTabIcons()` (setting `unpaidCount` badge and active gradient state). Attach `AttachEdgeResizeRedirector` on the floating nav controls.

- [ ] **Step 5: Build solution and run Task 1 verification tests**

Run: `dotnet build D:\clonedoan\PocketLog.slnx` -> Expect `0 Error(s)`.
Run Task 1 test in `UiAudit` -> Expect PASS on Light/Dark switch, file persistence, header fit at `445×680`, and existing dual-pane tests.

---

### Task 2: Custom GDI+ Android Social Controls (`Client.Controls.cs`)

**Files:**
- Create: `D:\clonedoan\PocketLog.Client\Client.Controls.cs`
- Test: `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\Program.cs`

**Interfaces:**
- Produces (nested classes inside `partial class Client` in `PocketLog.Client/Client.Controls.cs`):
  - `GradientPillButton : Guna2Button` — properties: `bool IsGradientActive`, `Color GradientStart`, `Color GradientEnd`, `int BadgeCount`, `Color BadgeFillColor`; draws diagonal gradient background + optional top-right rose pill counter (`BadgeCount > 0`) inside `OnPaint`.
  - `SocialPhotoCanvas : Control` — constructor accepting `(Client owner, LocketPost post, bool isMyTopCard, bool compactThumbnail)`; features `26px` rounded clip (`16px` if `compactThumbnail`), `FitImageCover` bitmap cache regenerated on `>= 6px` size changes and disposed on `Dispose(bool)`, diagonal gradient + ambient bokeh circles + vector category icon when `CustomImage == null`, bottom vignette scrim, top-left frosted category/amount pill, top-right tag pill (`@Name` or `Tag +N bạn`), centered 1–2 line bottom frosted-glass caption pill (`Color.FromArgb(172, 18, 24, 38)` with `1px` `Color.FromArgb(85, 255, 255, 255)` border), hover accent border when `isMyTopCard`.
  - `StoryAvatarStrip : Control` (or double-buffered scrollable container) — accepts `(Client owner, List<FriendUser> friends, int suggestionsCount, Action onOpenFriendsDrawer)`; draws horizontal strip (`Height = 96`) with `+ Kết bạn` slot (suggestion count dot if `> 0`) followed by `friends` sorted `IsOnline` descending, each `72px` wide with `48×48px` circular avatar + 3-stop Instagram gradient ring (`StoryRingStart -> Mid -> End` for online / muted for offline) + `#10B981` online dot + centered 1-line truncated name + horizontal mouse-wheel scroll.
  - `DoubleRingShutterButton : Control` — circular shutter button (`Size(58, 58)`), property `bool IsCapturedMode`, event/click support, draws crisp outer ring (`Color.White` in live mode or diagonal `AccentPrimary -> AccentSecondary` gradient when captured) + animated press/hover inner disc shrink.
- Consumes: `AppPalette`, `CreateVectorIcon`, `CreateRoundedPath`, `FitImageCover`, `FontMedium`, `FontRegular`, `GetCategoryShortClean`, `GetCategoryIcon`, `AttachEdgeResizeRedirector` from `Client.cs` & `Client.Theme.cs`.

- [ ] **Step 1: Add unit/render harness checks for `SocialPhotoCanvas`, `StoryAvatarStrip`, `DoubleRingShutterButton`, and `GradientPillButton`**

Instantiate each control inside `UiAudit` covering: `CustomImage == null`, `CustomImage != null`, long Vietnamese caption forcing 2 lines at `Width = 260`, `TaggedFriends.Count == 0 / 1 / 3`, resizing `SocialPhotoCanvas` from `260px` to `760px`, `StoryAvatarStrip` with 0 friends and 6 friends (online + offline), `DoubleRingShutterButton` normal/captured toggle, and `GradientPillButton` with `BadgeCount = 3`. Render each via `DrawToBitmap` and assert `0` exceptions and non-empty rendered pixels.

- [ ] **Step 2: Run `UiAudit` to verify failure before `Client.Controls.cs` exists**

Run: `dotnet run --project "...\UiAudit.csproj"` -> Expect compile FAIL because custom controls do not exist yet.

- [ ] **Step 3: Implement `PocketLog.Client/Client.Controls.cs` & wire `GradientPillButton` into `Client.cs` bottom tabs**

Implement all 4 double-buffered GDI+ controls (`CreateRoundedPath` reuse, deterministic `Dispose()` of cached cover bitmaps/brushes/pens), switch `btnTabFeed`, `btnTabCamera`, `btnTabExpense` in `Client.cs` to `GradientPillButton`, and attach `AttachEdgeResizeRedirector` to `pnlBottomNav` & strip controls.

- [ ] **Step 4: Build solution and run Task 2 render verification**

Run: `dotnet build D:\clonedoan\PocketLog.slnx` -> Expect `0 Error(s)`.
Run `UiAudit` -> Expect all Task 2 control edge-case renders pass (`0` GDI+ exceptions) and verify rendered sample PNGs via `read_file`.

---

### Task 3: Auth Screen & Friends Drawer Redesign (`Client.Login.cs` + `Client.Friends.cs`)

**Files:**
- Modify: `D:\clonedoan\PocketLog.Client\Client.Login.cs`
- Modify: `D:\clonedoan\PocketLog.Client\Client.Friends.cs`
- Test: `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\Program.cs`

**Interfaces:**
- Consumes: `CurrentTheme`, `authDraftCapture` / `authDraftRestore`, `CreateVectorIcon(UiIcon.Eye / EyeOff)`, `Guna2GradientPanel`, `Guna2GradientButton`, `MeasurePillBtnWidth`, `MakeLabel`.
- Modifies `MakeTextBox` border/fill styling via `CurrentTheme` colors (ensuring Dark Mode inputs are readable `#1E2534` with `#F1F5F9` text and `#94A3B8` placeholder).

- [ ] **Step 1: Add UI checks for Auth Screen (Light & Dark, password eye toggle, theme-switch draft preservation, `445×680` fit) and Friends Drawer**

In `UiAudit`, test on initial Auth screen: type server host, username, password, toggle `isRegisterMode` & password eye visibility (`txtPassword.PasswordChar == '\0'`), invoke `ToggleThemeMode()`, and assert all text/mode/eye state remained intact! Open `FriendsDrawer` in both Light and Dark modes, verify `"Xóa bạn"` -> `"Chắc xóa?"` 2-step state styling and online status dots, and capture PNGs for visual inspection.

- [ ] **Step 2: Run harness to verify failure before Auth/Drawer redesign**

Expect FAIL on missing password visibility toggle button (`btnTogglePass`) / `authDraftCapture`.

- [ ] **Step 3: Upgrade `Client.Login.cs` and `Client.Friends.cs`**

In `Client.Login.cs`: upgrade Hero banner to diagonal `Guna2GradientPanel` (`HeroGradStart -> HeroGradMid -> HeroGradEnd`) with Cloud endpoint chip (`LoadSavedServerEndpoint()`), themed Server card (`Dò Server LAN`), segmented Login/Register switcher, integrated password eye toggle icon button inside/beside `txtPassword` (`btnTogglePass` with `UiIcon.Eye` / `EyeOff`), `Guna2GradientButton` primary CTA, and wire `authDraftCapture` / `authDraftRestore` (detaching cleanly on navigation away from Auth).
In `Client.Friends.cs`: apply `CurrentTheme` (`DrawerBackdrop`, `CardBg`, `InputBg`, subtle ring accent on connected friends + `#10B981`/`#34D399` online indicator, smooth theme sync on `ApplyShellTheme()`).

- [ ] **Step 4: Build solution and run Task 3 verification + visual screenshot check**

Run `dotnet build D:\clonedoan\PocketLog.slnx` and `UiAudit` -> Expect PASS with `0` layout overflows at `445×680`, `560×900`, and `1920×1020` in both themes; inspect Auth & Drawer PNGs via `read_file`.

---

### Task 4: Feed, Story Strip & Gallery Redesign (`Client.NewFeed.cs`)

**Files:**
- Modify: `D:\clonedoan\PocketLog.Client\Client.NewFeed.cs`
- Test: `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\Program.cs`

**Interfaces:**
- Consumes: `SocialPhotoCanvas`, `StoryAvatarStrip`, `CurrentTheme`, `OpenFriendsDrawer()`, `OpenMyGalleryScreen()`, `EnsureTabVisible("CAMERA")`.
- Preserves: `CreateLocketPostCard` overall role and click behavior (clicking top own card opens `OpenMyGalleryScreen()`; clicking `"Quay lại Bảng tin"` restores `FEED` in-place during dual-pane).

- [ ] **Step 1: Add `FEED` & `Gallery` Light/Dark + Empty/Populated render tests in `UiAudit`**

Verify `StoryAvatarStrip` is present at top of `BuildFeedScreen()`, verify `SocialPhotoCanvas` replaces legacy `pillCaption` button-over-picturebox (no child `Guna2Button` overlapping `Guna2PictureBox`), test both empty state (`posts.Count == 0`, `myFriends.Count == 0`) and rich feed (`CustomImage != null` + friend post), and test `Kho ảnh` 2-column grid in narrow & dual-pane modes across Light & Dark themes.

- [ ] **Step 2: Run `UiAudit` to verify failure against old `Client.NewFeed.cs`**

Expect FAIL checking for `StoryAvatarStrip` and `SocialPhotoCanvas`.

- [ ] **Step 3: Upgrade `Client.NewFeed.cs`**

Add `StoryAvatarStrip` right under TopBar (`Bảng tin`, `"Kho ảnh"` button with count if `> 0`, `"Bạn bè (N)"`); refactor `CreateLocketPostCard` to pair mini IG-gradient-ring avatar + author/time header with `SocialPhotoCanvas(this, post, isMyTopCard, compactThumbnail: false)`; polish empty-state hero cards & section divider with `CurrentTheme`; upgrade `BuildMyGalleryControl` mini photo preview to `SocialPhotoCanvas(this, item, false, compactThumbnail: true)` with themed text rows.

- [ ] **Step 4: Build solution and run Task 4 verification + visual screenshot check**

Run `dotnet build D:\clonedoan\PocketLog.slnx` and `UiAudit` -> Expect PASS (`0` overflows, dual-pane gallery tests pass); inspect Light & Dark Feed + Gallery screenshots via `read_file`.

---

### Task 5: Camera Screen & Double-Ring Shutter Dock Redesign (`Client.Camera.cs`)

**Files:**
- Modify: `D:\clonedoan\PocketLog.Client\Client.Camera.cs`
- Test: `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\Program.cs`

**Interfaces:**
- Consumes: `DoubleRingShutterButton`, `CurrentTheme`, `Guna2GradientButton`, `CameraDraftState`, `cameraDraftCapture` / `cameraDraftRestore`, `OpenFriendsDrawer()`.
- Preserves: `pasteImageToCameraAction`, `refreshCameraFriendsAction`, `txtNoteTop` placeholder `"Thêm tên món ăn"` (tested by existing `uiaudit`), `CREATE_POST_REQ` submission payload and post-submit navigation logic, background `SYNC_RES` preservation of live `CAMERA` inputs.

- [ ] **Step 1: Add Camera Dock & Theme-Switch Draft Preservation test in `UiAudit`**

Open `CAMERA` tab, inspect `DoubleRingShutterButton` + `"Chọn ảnh"` + `"Dán ảnh"` dock inside/below viewfinder + `"+ Thêm bạn"` shortcut near tag friends label, inject a test `Bitmap` into `uploadedPhoto` / select category `"⛽ Xăng cộ"`, toggle a friend chip, type `"Bánh mì"` / `"45000"` / `"Ăn sáng"`, call `ToggleThemeMode()` twice (Light -> Dark -> Light), and assert `uploadedPhoto != null`, category `"⛽ Xăng cộ"`, friend tag, and all 3 textboxes retain exact values without disposal crashes!

- [ ] **Step 2: Run `UiAudit` to verify failure before Camera redesign**

Expect FAIL checking for `DoubleRingShutterButton` and `cameraDraft` preservation across `ToggleThemeMode`.

- [ ] **Step 3: Upgrade `Client.Camera.cs`**

Enlarge `pnlViewfinder` to `390px` with dark cinema background, floating top status/switch-cam pills, and bottom translucent Camera Dock bar (`Height = 72px`) holding `"Chọn ảnh"` pill (left), `DoubleRingShutterButton` (center: captures frame from live webcam or restarts live webcam to re-shoot), and `"Dán ảnh"` pill (right); add compact `"+ Thêm bạn"` pill on the `"Gắn tag bạn bè"` header row; remove redundant 3-button bottom row in `pnlNoteCard` (`pnlNoteCard` shrinks cleanly to tighter modern height); replace `btnPostPhoto` with full-width `Guna2GradientButton` (`AccentPrimary -> AccentSecondary`); wire `cameraDraftCapture` and `cameraDraftRestore` safely cloning `uploadedPhoto` and cleaning up handlers on disposal.

- [ ] **Step 4: Build solution and run Task 5 verification + visual screenshot check**

Run `dotnet build D:\clonedoan\PocketLog.slnx` and `UiAudit` -> Expect PASS on draft preservation + camera dock + `0` overflows; inspect Light & Dark Camera screenshots via `read_file`.

---

### Task 6: Expense Gradient Dashboard, Network Sync Badge & Full Verification (`Client.Expense.cs` + `Client.Network.cs`)

**Files:**
- Modify: `D:\clonedoan\PocketLog.Client\Client.Expense.cs`
- Modify: `D:\clonedoan\PocketLog.Client\Client.Network.cs`
- Test: `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit\Program.cs`

**Interfaces:**
- Consumes: `CurrentTheme` (`WalletGradStart/End`, `AccentPrimary/Secondary`, `SoftAccentBg`, `Danger`, etc.), `GradientPillButton.BadgeCount`, `UpdateBottomTabIcons()`.
- Preserves: Split-bill upload/verify flows, category budget save, monthly budget save, CSV export, and dual-pane refresh.

- [ ] **Step 1: Add Expense Gradient card & Unpaid Badge sync assertions in `UiAudit`**

Assert that with 1 unpaid `TaggedDebtItem`, `btnTabExpense` renders `BadgeCount == 1`; mark `IsConfirmedPaid = true`, invoke `ApplySyncedDataToUI` / `RefreshDataDrivenPanesOnSync()`, and assert `BadgeCount == 0`! Render `EXPENSE` in both Light & Dark at `445×680`, `560×900`, and `1920×1020` (`[FEED,EXPENSE]` dual-pane) with `0` overflows.

- [ ] **Step 2: Run `UiAudit` to verify `BadgeCount` check fails before implementation**

Expect FAIL because `BadgeCount` / sync badge update is not yet wired in `Client.Expense.cs` / `Client.Network.cs`.

- [ ] **Step 3: Upgrade `Client.Expense.cs` and `Client.Network.cs`**

In `Client.Expense.cs`: upgrade `pnlBanner` to diagonal `Guna2GradientPanel` (`WalletGradStart -> WalletGradEnd`, `88px` tall) with high-contrast white/mint/rose figures; re-skin active split-bill card, unpaid bill dropdown rows, 6 category cards (gradient progress bars + `CurrentTheme`), total monthly budget highlight card, and CSV export pill.
In `Client.Network.cs`: update `ApplySyncedDataToUI` to pick harmonized `PhotoBg` colors for posts without image and call `UpdateBottomTabIcons()` so the floating nav's `Chi tiêu` unpaid dot/badge always reflects live `taggedDebts`.

- [ ] **Step 4: Run Full Build + Full End-to-End Visual & Dual-Pane Audit + Inspect Screenshots**

1. Run `dotnet build D:\clonedoan\PocketLog.slnx` -> Expect `0 Error(s)`.
2. Run full `UiAudit` covering all existing dual-pane assertions PLUS all new Light & Dark Android UI assertions (`445×680`, `560×900`, `1920×1020`) -> Expect `ALL TESTS PASSED 100%!` and `0` overflows.
3. Read final screenshots across Light & Dark (`Auth`, `Feed`, `Gallery`, `Camera`, `Expense`, `Dual_Pane`, `FriendsDrawer`) via `read_file` to confirm visual alignment, zero clipped diacritics, zero square halo around photo captions, crisp Story rings, and crisp Floating Bottom Nav.

---

## Self-Review Check
- **Spec coverage:** Theme system + persistence + state preservation (Task 1, 3, 5); `SocialPhotoCanvas`, `StoryAvatarStrip`, `DoubleRingShutterButton`, `FloatingBottomNav`/`GradientPillButton` (Task 2); `UiIcon.Brand` transparent cutout & `Moon/Sun/Eye/EyeOff` (Task 1); Login gradient hero + password toggle + Drawer (Task 3); Feed Stories + photo cards + Gallery (Task 4); Camera Dock + gradient CTA (Task 5); Expense gradient wallet + badge sync (Task 6). Zero unmapped sections.
- **Type & signature consistency:** `AppPalette`, `CurrentTheme`, `isDarkMode`, `CameraDraftState`, `GradientPillButton`, `SocialPhotoCanvas`, `StoryAvatarStrip`, `DoubleRingShutterButton`, `GetUnpaidSplitCount()`, `UpdateBottomTabIcons()` match identically across all 6 tasks.
- **Review Focus coverage:** All 5 failure modes have explicit assertions pinned to owning tasks (Task 1, Task 2, Task 3, Task 4, Task 5, Task 6).
