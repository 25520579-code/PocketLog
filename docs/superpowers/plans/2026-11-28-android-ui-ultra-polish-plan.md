# PocketLog Client — Android Native Ultra-Polish (Wave 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Elevate `PocketLog.Client` from styled WinForms to native-grade Android/iOS visual polish with custom slim overlay scrollbars, multi-layer soft elevation cards, non-truncating Header/Story names, 1:1 Locket square photo cards with a quick action footer, a seamless feathered Camera dock, and a Fintech multi-segment Donut Budget Chart.

**Architecture:** Build three reusable double-buffered GDI+ controls (`AndroidOverlayScrollPanel`, `ElevatedCardPanel`, `BudgetDonutCard`) in `Client.Controls.cs` backed by shared helpers (`MouseWheelScrollFilter`, `GetShortVietnameseName`, `GetCategoryAccentColor`, local `liked_posts.json` store) in `Client.cs`/`Client.Theme.cs`, then progressively adopt them across Auth, Friends Drawer, Feed/Gallery, Camera, and Expense without altering shared DTOs or server code.

**Tech Stack:** `.NET 8.0-windows` WinForms, `System.Drawing.Drawing2D` (GDI+), `Guna.UI2.WinForms 2.0.5.1`.

**Spec:** [2026-11-28-android-ui-ultra-polish-design.md](file:///D:/clonedoan/docs/superpowers/specs/2026-11-28-android-ui-ultra-polish-design.md)

## Global Constraints

- Modify ONLY files inside `PocketLog.Client/` (`net8.0-windows`); do NOT modify `PocketLog.Shared`, `PocketLog.Server`, `PocketLog.Server.Linux`, or database schemas.
- Do NOT add NuGet packages; use only built-in `.NET 8 WinForms/GDI+` and `Guna.UI2.WinForms 2.0.5.1`.
- Preserve all shell & dual-pane contracts: `activeTabs`, `paneHosts`, `btnTabFeed`, `btnTabCamera`, `btnTabExpense`, `pnlHeader`, `pnlMainContainer`, `pnlBottomNav`, `HandleBottomTabClick`, `OpenInitialAuthenticatedTab`, `RefreshDataDrivenPanesOnSync`, `OpenMyGalleryScreen`, `StopWebcam`, `MinimumSize = (445, 580)`, 8-way `ChildEdgeRedirector`, and 125% DPI sizing (`MeasurePillBtnWidth`, `"Ág"`).
- Keep Dark-Mode small text/icons at WCAG >= 4.5:1 (`#38BDF8` or `#F1F5F9` on `#161B26`/`#232B45`) and dispose every GDI+ object (`GraphicsPath`, `Brush`, `Pen`, `Font`, `Bitmap`, `Timer`, `IMessageFilter`) cleanly.
- Local heart reactions (Option A) persist ONLY to `%LocalAppData%\PocketLog\liked_posts.json` and display tooltip `"Lưu yêu thích trên máy này"`.
- External test harness path (outside repo): `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\UiAudit.csproj` (`Program.cs`, screenshots in `dual_shots\`).

## Review Focus

1. **Mouse-wheel pane & backdrop isolation:** In Dual-Pane `1920×1020`, wheel input over the left `AndroidOverlayScrollPanel` must NOT scroll the right pane; when `pnlDrawerBackdrop.Visible == true`, wheel input over the backdrop or drawer must NEVER scroll any feed/expense pane underneath (pinned in Task 1 & Task 2).
2. **Right-edge 8-way resize compatibility:** Hovering/clicking within the outer 8px window border next to `OverlayScrollTrack` in `WindowState.Normal` must still pass through `ChildEdgeRedirector` without being stolen by scrollbar thumb drag (pinned in Task 1).
3. **Feed dynamic `1:1` square relayout & `"Xem chia bill"` routing:** Resizing Feed across `445×680` → `1920×1020` → `445×680` must recalculate `SocialPhotoCanvas.Height`, quick-footer `Top`, `card.Height`, sibling `card.Top`, and `col.Height` with zero overlap or stale dead whitespace; clicking `"Xem chia bill"` on a tagged post must focus the matching `SplitId` and switch cleanly to `EXPENSE` (pinned in Task 3).
4. **Camera feathered vignette live frame repaint & corner clipping:** Swapping captured photos or painting live webcam frames under the transparent-top feathered `CameraBottomDockPanel` must never retain stale pixels from the previous photo or expose unclipped corner slivers (pinned in Task 4).
5. **BudgetDonutCard numeric & narrow-viewport edge cases:** `grandTotal == 0`, `totalBudgetLimit == 0`, `grandTotal > totalBudgetLimit` (over budget), single-category 100%, and `445×680` 125% DPI layout must render cleanly with zero divide-by-zero, zero GDI+ zero-sweep `DrawArc` exceptions, and zero legend overlap in both Light and Dark modes (pinned in Task 5).

---

### Task 1: Slim Android Overlay Scrollbar, Elevated Card Control & Header Helpers

**Files:**
- Modify: `PocketLog.Client/Client.Controls.cs` (add `AndroidOverlayScrollPanel`, `ElevatedCardPanel`)
- Modify: `PocketLog.Client/Client.cs` (update `CreateResponsiveScrollPage`, register/unregister `MouseWheelScrollFilter`, add `GetShortVietnameseName`, `GetCategoryAccentColor`, `UiIcon.Heart`/`HeartFilled`/`ArrowRight`, upgrade `LayoutHeaderControls`)
- Test (external): `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\Program.cs`

**Interfaces:**
- Consumes: `AppPalette`, `CurrentTheme`, `isDarkMode`, `SmoothScrollPanel`, `AttachEdgeResizeRedirector`, `CreateRoundedPath`, `BlendControlColor` (existing in `Client.cs`, `Client.Theme.cs`, `Client.Controls.cs`).
- Produces:
  - `public class AndroidOverlayScrollPanel : SmoothScrollPanel` (nested in `Client`):
    - Properties: `public Panel ContentHost { get; }`, `public int ScrollOffsetY { get; }`, `public int MaxScrollOffset { get; }`, `public bool IsScrollable { get; }`, `public Rectangle ThumbBounds { get; }`, `public float ThumbOpacity { get; }`, `public bool CenterColumnMode { get; set; }`, `public int MaxContentWidth { get; set; }`
    - Methods: `public void SetScrollOffset(int y, bool animate = false)`, `public void ScrollByDelta(int deltaY, bool animate = true)`, `public void ScrollControlToVisible(Control target)`, `public void SyncLayout()`, `public void SyncTheme()`
  - `public class ElevatedCardPanel : Panel` (nested in `Client`):
    - Properties: `public int CornerRadius { get; set; }`, `public Color CardFillColor { get; set; }`, `public Color CardBorderColor { get; set; }`, `public int CardBorderThickness { get; set; }`, `public int ElevationLevel { get; set; }`, `public Rectangle InnerCardBounds { get; }`
    - Constructor: `public ElevatedCardPanel(Client owner = null)` (sets `Padding = new Padding(4, 2, 4, 6)` matching shadow margins, double-buffered, attaches `AttachEdgeResizeRedirector(this)`)
  - `protected static string GetShortVietnameseName(string fullName)` in `Client.cs`
  - `protected Color GetCategoryAccentColor(string categoryKey)` in `Client.cs` (6 exact jewel colors from Spec 4.5)
  - `UiIcon.Heart`, `UiIcon.HeartFilled`, `UiIcon.ArrowRight` in `Client.cs` (`CreateVectorIcon`)

- [ ] **Step 1: Write failing tests in `scratch\uiaudit_wt\Program.cs` (`RunWave2Task1Tests`)**

Add `RunWave2Task1Tests()` verifying:
- `CreateResponsiveScrollPage(out Panel col)` returns an `AndroidOverlayScrollPanel` with `AutoScroll == false`, `VerticalScroll.Visible == false`, `HorizontalScroll.Visible == false`.
- Growing `col.Height` from `400` to `1400` inside `450×700` flips `IsScrollable == true` with `MaxScrollOffset == 700`, `ThumbBounds.Width` between `4` and `7`, and changes `col.Width` at most once (zero width bounce).
- `SetScrollOffset(320, animate: false)` sets `ScrollOffsetY == 320` and `col.Top == -320`; `ScrollByDelta(120, animate: false)` advances to `440`; `ScrollControlToVisible(bottomChild)` brings the child inside `[0, ClientSize.Height]`.
- Dual-pane wheel routing helper (`RouteMouseWheelAtPoint(Point screenPt, int wheelDelta)`) scrolls ONLY the left `AndroidOverlayScrollPanel` when pointed at left pane, leaves right pane at `ScrollOffsetY == 0`, and blocks background scroll when `pnlDrawerBackdrop.Visible == true`.
- `ElevatedCardPanel` renders cleanly via `DrawToBitmap` in both Light and Dark mode (`InnerCardBounds` inset by `(4, 2, 4, 6)`, non-uniform shadow pixels below `InnerCardBounds.Bottom` in Light Mode and specular top highlight pixels in Dark Mode).
- `GetShortVietnameseName("Trần Gia Huy") == "Gia Huy"`, `GetShortVietnameseName("Đỗ Huy Hoàng") == "Huy Hoàng"`, `GetShortVietnameseName("Lê  Minh  Anh") == "Minh Anh"`, `GetShortVietnameseName("An") == "An"`.
- `GetCategoryAccentColor` returns the 6 exact spec colors (`#F97316`, `#0EA5E9`, `#8B5CF6`, `#10B981`, `#EC4899`, `#F59E0B`).
- At `560×900`, `pnlHeaderUserChip.Visible == true` and `lblHeaderUser.Text` fits cleanly inside `lblHeaderUser.Width` (`TextRenderer.MeasureText(lblHeaderUser.Text, lblHeaderUser.Font).Width <= lblHeaderUser.Width`) without `"..."` truncation.
- `CreateVectorIcon` succeeds for `UiIcon.Heart`, `UiIcon.HeartFilled`, `UiIcon.ArrowRight` with non-transparent pixels.
- Update existing scroll checks in `Program.cs` so both `AndroidOverlayScrollPanel` (`!AutoScroll`, `!VerticalScroll.Visible`, `!HorizontalScroll.Visible`, `ScrollControlToVisible`, `SetScrollOffset(0)`) and any remaining `ScrollableControl` checks pass cleanly.

- [ ] **Step 2: Run build/test to verify RED failure**

Run: `dotnet run --project "C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\UiAudit.csproj"`
Expected: FAIL (compile error: `AndroidOverlayScrollPanel`, `ElevatedCardPanel`, `GetShortVietnameseName`, `GetCategoryAccentColor`, `UiIcon.Heart` not yet defined).

- [ ] **Step 3: Implement `AndroidOverlayScrollPanel`, `ElevatedCardPanel`, `MouseWheelScrollFilter`, and Header/Icon helpers**

- In `PocketLog.Client/Client.Controls.cs`:
  - Implement `AndroidOverlayScrollPanel : SmoothScrollPanel`:
    - `AutoScroll = false`, double-buffered.
    - Contains `ContentHost` (`Panel`, transparent background) and a docked right-edge `OverlayScrollTrack` (`Width = 10`, `Dock = DockStyle.Right`, `AttachEdgeResizeRedirector`, paints `4px` idle / `6px` hover-or-drag pill thumb centered horizontally with rounded ends; mouse down on thumb starts smooth drag; mouse down on track jumps smoothly; `fadeTimer` + `animTimer` smoothly animate eased wheel momentum & idle fade-out after `1200ms`, all timers disposed cleanly in `Dispose(bool)`).
    - When `CenterColumnMode == true`, `SyncLayout()` computes `availW = ClientSize.Width - (IsScrollable ? 10 : 0)` (reserving the constant `10px` right track cleanly when tall or always keeping `sidePad = availW < 450 ? 10 : 14` with invariant column width `targetW = Math.Clamp(ClientSize.Width - 10 - sidePad * 2, 260, MaxContentWidth)` so `col.Width` NEVER bounces when `IsScrollable` flips!), clamps `ScrollOffsetY` to `[0, MaxScrollOffset]`, and sets `ContentHost.Location = new Point(targetX, -ScrollOffsetY)`.
    - When `CenterColumnMode == false` (for Friends Drawer), `ContentHost` spans `ClientSize.Width - 12` at `X = 0` and `Y = -ScrollOffsetY`.
  - Implement `ElevatedCardPanel : Panel`:
    - `Padding = new Padding(4, 2, 4, 6)` (so child controls placed at `Location(InnerCardBounds.X + x, ...)` or `Dock` inside `Padding` never clip the outer soft shadow).
    - `OnPaintBackground` fills parent-matched backdrop (`BackColor = Color.Transparent` or owner `clrAppBg` fallback), paints 3-pass soft rounded shadow (`y+1`, `y+3`, `y+5` with calibrated low alpha in Light Mode; deeper `rgba(0,0,0,alpha)` in Dark Mode), fills rounded `InnerCardBounds` with `CardFillColor`, draws `1px` Dark-Mode top inner specular rim (`Color.FromArgb(26, 255, 255, 255)`) clipped to top half, and draws `CardBorderThickness` border.
- In `PocketLog.Client/Client.cs`:
  - Add `UiIcon.Heart`, `UiIcon.HeartFilled`, `UiIcon.ArrowRight` and vector drawing paths in `CreateVectorIcon`.
  - Add `GetShortVietnameseName(string fullName)` and `GetCategoryAccentColor(string categoryKey)`.
  - Implement private `MouseWheelScrollFilter : IMessageFilter` registered via `Application.AddMessageFilter` in `Client` constructor and removed in `Dispose(bool)` (`override void Dispose(bool disposing)`), delegating to `internal bool RouteMouseWheelAtPoint(Point screenPt, int wheelDelta)` so unit tests can invoke wheel routing deterministically without OS cursor injection.
  - Update `CreateResponsiveScrollPage(out Panel colPanel)` to create and return `new AndroidOverlayScrollPanel(this) { CenterColumnMode = true, MaxContentWidth = MaxColWidth, BackColor = clrAppBg, Dock = DockStyle.Fill }` with `colPanel = scrollRoot.ContentHost`.
  - Update `LayoutHeaderControls()` to measure `lblBrand` width cleanly and switch `lblHeaderUser.Text` between `currentFullName` (when it fits) and `GetShortVietnameseName(currentFullName)` (when `availForChip` is tight at `510–610px`), keeping `avtHeaderUser` crisp and preventing `"Đỗ Huy..."` truncation.

- [ ] **Step 4: Run build and full `UiAudit` to verify GREEN**

Run:
1. `dotnet build "D:\clonedoan\PocketLog.slnx"` → Expect `0 Error(s)`.
2. `dotnet run --project "C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\UiAudit.csproj"` → Expect `ALL WAVE 2 TASK 1 TESTS PASSED 100%!` and `exit code 0`.
3. Inspect rendered `T1_445x680_Dark.png` and `T4_Feed_560x900_Dark.png` via `read_file` to confirm the gray Win32 scrollbar is GONE and header `"Huy Hoàng"` / `"Đỗ Huy Hoàng"` renders without ellipsis.

- [ ] **Step 5: Commit**

```bash
git add PocketLog.Client/Client.Controls.cs PocketLog.Client/Client.cs
git commit -m "feat(ui): add Android overlay scrollbar, elevated card control, and header name polish"
```

---

### Task 2: Auth Screen & Friends Drawer Ultra-Polish

**Files:**
- Modify: `PocketLog.Client/Client.Login.cs` (elevated `cardServer` & `cardAuth`, Hero glass specular rim + soft drop shadow)
- Modify: `PocketLog.Client/Client.Friends.cs` (switch drawer list to `AndroidOverlayScrollPanel`, left-edge drawer drop shadow, elevated friend item cards)
- Test (external): `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\Program.cs`

**Interfaces:**
- Consumes: `AndroidOverlayScrollPanel`, `ElevatedCardPanel` from Task 1.
- Produces: Zero Win32 scrollbar in Friends Drawer & Auth; elevated Auth/Friends surfaces with preserved `authDraftCapture`/`Restore`, `btnTogglePass`, `IsDisposed` guards, and 2-step friend deletion.

- [ ] **Step 1: Write failing tests in `scratch\uiaudit_wt\Program.cs` (`RunWave2Task2Tests`)**

Add assertions verifying:
- Auth screen contains `ElevatedCardPanel` instances for `cardServer` and `cardAuth` in both Light and Dark modes, with zero native `AutoScroll == true` panels and zero overflows at `445×680`, `560×900`, `1920×1020`.
- Friends Drawer uses `AndroidOverlayScrollPanel` (`AutoScroll == false`, `VerticalScroll.Visible == false`) for its friend list, contains `ElevatedCardPanel` rows for friends/suggestions, scrolls when populated with 12+ friends via `RouteMouseWheelAtPoint` over the drawer while leaving the Feed behind at `ScrollOffsetY == 0`.
- Existing Auth & Friends Drawer behaviors (`btnTogglePass`, `authDraftCapture`/`Restore`, `"Xóa bạn"` → `"Chắc xóa?"`) remain 100% green.

- [ ] **Step 2: Run test to verify RED failure**

Run: `dotnet run --project "C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\UiAudit.csproj"`
Expected: FAIL (`cardServer`/`cardAuth` not `ElevatedCardPanel`, Friends Drawer still uses native `AutoScroll == true`).

- [ ] **Step 3: Upgrade `Client.Login.cs` and `Client.Friends.cs`**

- In `PocketLog.Client/Client.Login.cs`:
  - Replace `cardServer` and `cardAuth` `Guna2Panel` containers with `ElevatedCardPanel` (`CornerRadius = 24`, `ElevationLevel = 2`, `CardFillColor = clrCardBg`, `CardBorderColor = clrBorderLight`, `BackColor = clrAppBg`), offsetting inner child coordinates cleanly within `InnerCardBounds` so zero controls touch the shadow margin.
  - Add subtle ambient drop shadow + crisp `1px` top inner specular sheen to `HeroBannerPanel` (rendering inside a `4px`/`6px` shadow margin).
- In `PocketLog.Client/Client.Friends.cs`:
  - Replace `listScroll = new SmoothScrollPanel { AutoScroll = true }` with `new AndroidOverlayScrollPanel(this) { CenterColumnMode = false, BackColor = clrCardBg }` adding section labels and rows into `listScroll.ContentHost`, setting `listScroll.ContentHost.Height = y + 20`, and calling `listScroll.SyncLayout()`.
  - Paint a soft 4-step left-edge elevation shadow along `pnlFriendsDrawer.Left - 14..pnlFriendsDrawer.Left` inside `pnlDrawerBackdrop.Paint` so the drawer visibly floats over the dimmed screen.
  - Upgrade `BuildFriendItemRow` to return `ElevatedCardPanel` (`CornerRadius = 18`, `ElevationLevel = 1`, `CardFillColor = clrInputBg`, `CardBorderColor = isAlreadyFriend ? clrSoftGreenBorder : clrBorderLight`, `BackColor = clrCardBg`), keeping avatar rings, online emerald dots, and 2-step `"Xóa bạn"` → `"Chắc xóa?"` intact.

- [ ] **Step 4: Run build, full `UiAudit`, and inspect screenshots**

Run:
1. `dotnet build "D:\clonedoan\PocketLog.slnx"` → `0 Error(s)`.
2. `dotnet run --project "...\UiAudit.csproj"` → `ALL WAVE 2 TASK 2 TESTS PASSED 100%!`.
3. Inspect `T3_Auth_560x900_Light.png`, `T3_Auth_560x900_Dark.png`, `T3_FriendsDrawer_560x900_Light.png`, and `T3_FriendsDrawer_560x900_Dark.png` via `read_file`.

- [ ] **Step 5: Commit**

```bash
git add PocketLog.Client/Client.Login.cs PocketLog.Client/Client.Friends.cs
git commit -m "feat(ui): elevate auth cards and upgrade friends drawer with overlay scrollbar"
```

---

### Task 3: Feed, Story Strip & Gallery Ultra-Polish (`1:1` Square, Short Names & Quick Action Footer)

**Files:**
- Modify: `PocketLog.Client/Client.Theme.cs` (add local `likedPostIds` persistence at `%LocalAppData%\PocketLog\liked_posts.json`: `LoadLikedPosts()`, `SaveLikedPosts()`, `IsPostLiked(int postId)`, `TogglePostLiked(int postId)`)
- Modify: `PocketLog.Client/Client.Controls.cs` (upgrade `StoryAvatarStrip` with `GetShortVietnameseName`, crisp `TextRenderer` labels, soft card elevation backdrop)
- Modify: `PocketLog.Client/Client.NewFeed.cs` (upgrade Feed & Gallery cards to `ElevatedCardPanel`, `1:1` square responsive `SocialPhotoCanvas`, dynamic card-stack relayout on resize, Option A Quick Action Footer Bar with local Heart pill + `"Xem chia bill"` / `"Mở Kho ảnh"` navigation)
- Test (external): `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\Program.cs`

**Interfaces:**
- Consumes: `ElevatedCardPanel`, `GetShortVietnameseName`, `UiIcon.Heart`, `UiIcon.HeartFilled`, `UiIcon.ArrowRight` from Task 1.
- Produces:
  - `protected bool IsPostLiked(int postId)` and `protected bool TogglePostLiked(int postId)` in `Client.Theme.cs`
  - `protected static string FormatStoryDisplayName(string fullName)` on `StoryAvatarStrip` in `Client.Controls.cs` (delegating to `GetShortVietnameseName`)
  - Quick Action Footer inside every Feed card with local Heart toggle + real `"Xem chia bill"` (`EnsureTabVisible("EXPENSE")` + matching `selectedSplitId`) / `"Mở Kho ảnh"` (`OpenMyGalleryScreen()`) routing.

- [ ] **Step 1: Write failing tests in `scratch\uiaudit_wt\Program.cs` (`RunWave2Task3Tests`)**

Add assertions verifying:
- `StoryAvatarStrip.FormatStoryDisplayName("Trần Gia Huy") == "Gia Huy"` and `FormatStoryDisplayName("Lê Minh Anh") == "Minh Anh"`.
- `TogglePostLiked(99901)` persists to `%LocalAppData%\PocketLog\liked_posts.json`, survives reload (`LoadLikedPosts`), and toggles back cleanly (clean up test post ID afterward).
- Feed cards are `ElevatedCardPanel` containing `SocialPhotoCanvas` whose aspect ratio is `1:1` at `445×680` (`Math.Abs(canvas.Width - canvas.Height) <= 2`) and capped `<= 380px` height at `1920×1020`, with zero vertical overlap between stacked cards after resizing `445×680` → `1920×1020` → `445×680`.
- Every Feed card has a Quick Footer below `SocialPhotoCanvas` containing the local Heart pill (`"Thích"` → clicks to `"Đã thích"`) and the right action pill (`"Xem chia bill"` on tagged post clicks through to activate `"EXPENSE"` tab and selects matching `SplitId`; `"Mở Kho ảnh"` on untagged own post opens Gallery).
- Gallery cards (`BuildMyGalleryControl`) are `ElevatedCardPanel` with `1:1` square `SocialPhotoCanvas` thumbnails (`Math.Abs(thumb.Width - thumb.Height) <= 2`) and zero overflow at `445×680`, `560×900`, `1920×1020`.

- [ ] **Step 2: Run test to verify RED failure**

Run: `dotnet run --project "...\UiAudit.csproj"`
Expected: FAIL (`TogglePostLiked`, `FormatStoryDisplayName`, Feed Quick Footer, `1:1` square proportions not yet implemented).

- [ ] **Step 3: Implement local likes store, Story Strip polish, and Feed/Gallery Ultra-Polish**

- In `PocketLog.Client/Client.Theme.cs`:
  - Add `private readonly HashSet<int> likedPostIds = new HashSet<int>();` and `LoadLikedPosts()` (called from constructor alongside `LoadThemePreference()`), `SaveLikedPosts()`, `IsPostLiked(int postId)`, `TogglePostLiked(int postId)` storing simple JSON integer array at `%LocalAppData%\PocketLog\liked_posts.json` with silent IO fallback.
- In `PocketLog.Client/Client.Controls.cs`:
  - Add `public static string FormatStoryDisplayName(string fullName) => GetShortVietnameseName(fullName);` on `StoryAvatarStrip`.
  - Render story names with `TextRenderer.DrawText(..., TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine)` (eliminating GDI+ double-space kerning gap!) and give `StoryAvatarStrip` a subtle elevated soft shadow inside a `4px/6px` inset so it harmonizes seamlessly with `ElevatedCardPanel`.
- In `PocketLog.Client/Client.NewFeed.cs`:
  - Upgrade `CreateLocketPostCard` and `CreateEmptyHeroCard` to `ElevatedCardPanel` (`CornerRadius = 28`, `ElevationLevel = 2`, `CardFillColor = clrCardBg`, `CardBorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight`, `BackColor = clrAppBg`).
  - Inside `CreateLocketPostCard`: compute `int canvasW = Math.Max(180, inner.Width - 24); int canvasH = Math.Clamp(canvasW, 260, 380);` and place the `42px` Quick Footer below `photoCanvas`:
    - Left `btnLike` pill (`"Thích"` / `"Đã thích"`, `UiIcon.Heart` / `UiIcon.HeartFilled`, `AttachControlImageDisposal`, `ToolTip` `"Lưu yêu thích trên máy này"`, updates state & color live on click without triggering `openGallery` on `isMyTopCard`).
    - Right context pill (`btnAction`): if `post.HasTag`, `"Xem chia bill"` with `UiIcon.ArrowRight` (finds matching `SplitDebtItem` by `PostId`/title/amount in `taggedDebts`, sets `selectedSplitId` if found, calls `EnsureTabVisible("EXPENSE")` and `RefreshPaneContent("EXPENSE")`); else if `isMyTopCard || post.IsMine`, `"Mở Kho ảnh"` with `UiIcon.ArrowRight` calling `OpenMyGalleryScreen()`; else quiet status chip `"Khoảnh khắc"`.
  - In `BuildFeedScreen`, attach a responsive `RelayoutFeedStack()` callback wired to `col.Resize` so resizing between `445×680` and `1920×1020` updates each card's square `photoCanvas.Height`, footer `Top`, `card.Height`, sequential `y` offsets of all cards/sections, and `col.Height` smoothly with zero overlap or stale whitespace!
  - In `BuildMyGalleryControl`, upgrade each tile to `ElevatedCardPanel` (`CornerRadius = 22`, `ElevationLevel = 2`) with `1:1` square `SocialPhotoCanvas(compactThumbnail: true)` (`thumbH = Math.Clamp(tileInnerW - 20, 128, 220)`), recomputed inside `LayoutGalleryGrid()`.

- [ ] **Step 4: Run build, full `UiAudit`, and inspect screenshots**

Run:
1. `dotnet build "D:\clonedoan\PocketLog.slnx"` → `0 Error(s)`.
2. `dotnet run --project "...\UiAudit.csproj"` → `ALL WAVE 2 TASK 3 TESTS PASSED 100%!`.
3. Inspect `T4_Feed_560x900_Light.png`, `T4_Feed_560x900_Dark.png`, `T4_Feed_445x680_Dark.png`, and `T4_Gallery_560x900_Dark.png` via `read_file` to verify crisp short story names, `1:1` square photos, soft elevation shadows, and Quick Action Footer alignment.

- [ ] **Step 5: Commit**

```bash
git add PocketLog.Client/Client.Theme.cs PocketLog.Client/Client.Controls.cs PocketLog.Client/Client.NewFeed.cs
git commit -m "feat(ui): polish feed square cards, story short names, local reactions, and gallery elevation"
```

---

### Task 4: Camera Feathered Glass Dock & Elevated Composer

**Files:**
- Modify: `PocketLog.Client/Client.Camera.cs` (4-stop feathered transparent-top vignette `CameraBottomDockPanel` without hard top hairline or corner sliver, square-balanced elevated viewfinder, elevated expense-tag composer card)
- Test (external): `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\Program.cs`

**Interfaces:**
- Consumes: `ElevatedCardPanel` from Task 1.
- Produces: Seamless feathered Camera dock (`Height = 84`, zero top hairline, zero corner sliver), responsive square-balanced elevated viewfinder & composer preserving `DoubleRingShutterButton`, `InvalidateViewfinderChildren`, `cameraDraftCapture`/`Restore`, `suppressAutoCameraStart`, and `StopWebcam`.

- [ ] **Step 1: Write failing tests in `scratch\uiaudit_wt\Program.cs` (`RunWave2Task4Tests`)**

Add assertions verifying:
- Camera screen wraps both viewfinder and tag-composer form inside `ElevatedCardPanel`.
- Sampling pixels along the top edge (`y = 0..1`) of `CameraBottomDockPanel` over a bright solid green test photo shows smooth feathered blend close to the photo color (no harsh bright white hairline or abrupt opaque alpha step!).
- Outer bottom-left/bottom-right pixels of `pnlViewfinder` outside the rounded arc match `clrAppBg` / `#0F172A` cleanly with zero unclipped blue corner sliver (`#4F46E5`/`#6366F1` border does not detach from the inner dock corner).
- Swapping photo 1 (green) → photo 2 (magenta) while `IsCapturedMode == true` cleanly repaints the feathered dock with zero stale green pixels.
- Viewfinder height scales responsively (`Math.Clamp(innerW, 290, 380)`) with zero overflow at `445×680`, `560×900`, `1920×1020`.

- [ ] **Step 2: Run test to verify RED failure**

Run: `dotnet run --project "...\UiAudit.csproj"`
Expected: FAIL (Camera containers not `ElevatedCardPanel`, top-edge hairline/abrupt alpha present on `CameraBottomDockPanel`).

- [ ] **Step 3: Upgrade `Client.Camera.cs`**

- In `CameraBottomDockPanel.OnPaintBackground`:
  - Draw `previewBox.Image` cleanly clipped to the exact bottom rounded radius (`22f`, matching `picPreview.BorderRadius = 22`).
  - Replace the single abrupt `148 → 228` gradient + `topLinePen` with a smooth 4-stop `ColorBlend` vertical vignette (`alpha 0` at `0.0f` → `alpha 88` at `0.30f` → `alpha 176` at `0.68f` → `alpha 226` at `1.0f` over `Color.FromArgb(..., 11, 14, 20)`), removing `topLinePen` entirely so the dock melts invisibly into the photo!
  - Set dock `Height = 84` and position `DoubleRingShutterButton` + `"Chọn ảnh"` / `"Dán ảnh"` cleanly in the lower `64px` band.
- Wrap `pnlViewfinder` inside an `ElevatedCardPanel` (`CornerRadius = 28`, `ElevationLevel = 2`, `CardFillColor = Color.FromArgb(15, 23, 42)`, `CardBorderColor = isDarkMode ? Color.FromArgb(46, 56, 78) : Color.FromArgb(30, 41, 59)`, `BackColor = clrAppBg`) with `picPreview` inset seamlessly (`Padding` inside `InnerCardBounds`, `BorderRadius = 22`) so ZERO corner sliver ever shows between frame and dock!
- Wrap the tag-composer form (`noteAmountRow`, `lblFriends`, `btnQuickAddFriend`, `flowFriends`) in `ElevatedCardPanel` (`CornerRadius = 22`, `ElevationLevel = 2`), keeping dynamic height relayout on `col.Resize` so viewfinder stays balanced square (`Math.Clamp(innerW, 290, 380)`) and all controls below shift cleanly without overlap.

- [ ] **Step 4: Run build, full `UiAudit`, and inspect screenshots**

Run:
1. `dotnet build "D:\clonedoan\PocketLog.slnx"` → `0 Error(s)`.
2. `dotnet run --project "...\UiAudit.csproj"` → `ALL WAVE 2 TASK 4 TESTS PASSED 100%!`.
3. Inspect `T5_Camera_445x680_Light.png`, `T5_Camera_560x900_Dark.png`, and `T5_Camera_Dual_1920x1020_Dark.png` via `read_file` to confirm the hard line & corner sliver are completely gone.

- [ ] **Step 5: Commit**

```bash
git add PocketLog.Client/Client.Camera.cs
git commit -m "feat(ui): upgrade camera dock with feathered vignette and elevated viewfinder"
```

---

### Task 5: Fintech Budget Donut Chart, Category Color Sync, Elevated Wallet & Full Verification

**Files:**
- Modify: `PocketLog.Client/Client.Controls.cs` (add `BudgetDonutCard : Control` + `BudgetSlice` record/struct)
- Modify: `PocketLog.Client/Client.Expense.cs` (elevate `WalletGradientPanel` with ambient shadow & glass sheen, promote elevated `BudgetDonutCard` + inline total-budget editor above category list, upgrade split card/rows/6 category cards to `ElevatedCardPanel`, sync category colors & add `%` badges)
- Test (external): `C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\Program.cs`

**Interfaces:**
- Consumes: `ElevatedCardPanel`, `AndroidOverlayScrollPanel`, `GetCategoryAccentColor` from Task 1.
- Produces:
  - `public readonly struct BudgetSlice` (`Label`, `Amount`, `Color`) and `public class BudgetDonutCard : Control` (nested in `Client`, `Client.Controls.cs`):
    - Properties/Methods: `public void SetData(int totalSpent, int totalLimit, IReadOnlyList<BudgetSlice> slices)`, `public int UsedPercent { get; }`, `public bool IsOverBudget { get; }`, `public Rectangle DonutBounds { get; }`
  - Elevated Expense dashboard with Donut Chart overview, synced 6-category jewel colors, `%` chips, and zero Win32 scrollbars.

- [ ] **Step 1: Write failing tests in `scratch\uiaudit_wt\Program.cs` (`RunWave2Task5Tests`)**

Add assertions verifying:
- `BudgetDonutCard` renders cleanly via `DrawToBitmap` without exceptions across ALL 5 edge cases (Review Focus #5):
  1. `totalSpent = 0, totalLimit = 2_400_000` (`UsedPercent == 0`, `IsOverBudget == false`)
  2. `totalSpent = 0, totalLimit = 0` (`UsedPercent == 0`, no divide-by-zero)
  3. `totalSpent = 575_000, totalLimit = 2_400_000` with 6 slices (`UsedPercent == 24`, non-track jewel colors painted on the donut ring!)
  4. `totalSpent = 2_850_000, totalLimit = 2_400_000` (`IsOverBudget == true`, `UsedPercent == 118`, Danger color painted on ring)
  5. Single-category 100% (`totalSpent = 1_200_000, totalLimit = 1_200_000`, `UsedPercent == 100`)
- Expense screen mounts `BudgetDonutCard` inside an `ElevatedCardPanel` ABOVE the 6 category cards (`donutCard.Top < firstCatCard.Top`), contains `txtTotalIn` + `"Lưu tổng hạn mức"` inside that overview card, gives all 6 category cards `ElevatedCardPanel` framing + matching `GetCategoryAccentColor` progress color (or Danger when over) + `%` pill label, and keeps `0` overflows at `445×680`, `560×900`, `1920×1020` in both Light & Dark modes.

- [ ] **Step 2: Run test to verify RED failure**

Run: `dotnet run --project "...\UiAudit.csproj"`
Expected: FAIL (`BudgetDonutCard` / `BudgetSlice` not defined yet).

- [ ] **Step 3: Implement `BudgetDonutCard` and upgrade `Client.Expense.cs`**

- In `PocketLog.Client/Client.Controls.cs`:
  - Implement `public readonly struct BudgetSlice` (`string Label`, `int Amount`, `Color Color`).
  - Implement `public class BudgetDonutCard : Control` (double-buffered, `AttachEdgeResizeRedirector(this)`):
    - Responsive layout: when `Width >= 376`, draws `116×116` Donut ring on the left (`x = 4`) and hero total (`575,000đ`), `/ Hạn mức`, remaining/over status pill (`"Còn lại: ..."` / `"Vượt: ..."`), plus compact 2-column colored category legend dots on the right; when `Width < 376` (very tight screens), centers a `104×104` Donut ring above the hero text & 2-column legend and exposes `PreferredHeight` (`GetRequiredHeight(int width)`) so the host `ElevatedCardPanel` adjusts smoothly without clipping!
    - Donut ring rendering:
      - Full 360° background track ring (`13px` stroke, muted `InputBg` / subtle track alpha).
      - When `totalSpent > 0`: for each slice with `Amount > 0`, compute `sweepAngle` proportionally against `Math.Max(totalLimit, totalSpent)` (clamped `>= 4f` deg so tiny non-zero slices stay visible, total sum capped `<= 360f`), skipping any slice with `sweepAngle < 0.5f` so GDI+ `DrawArc` never throws. When `IsOverBudget`, draw a subtle outer Danger glow halo (`2.5px`) around the ring.
      - Center readout: bold `"{UsedPercent}%"` (`15F` bold) + muted `"đã dùng"` (`7.6F`).
- In `PocketLog.Client/Client.Expense.cs`:
  - Enhance `WalletGradientPanel` with a `4px/6px` bottom ambient elevation shadow (colored subtly with `WalletGradStart` at low alpha) and a subtle diagonal translucent glass highlight arc (`Color.FromArgb(22, 255, 255, 255)`).
  - Upgrade active split-bill card, empty debt card, and unpaid dropdown rows to `ElevatedCardPanel` (`CornerRadius = 22` / `18`).
  - Build the **Monthly Budget Overview `ElevatedCardPanel`** right above the `"Chi tiêu thực tế theo danh mục"` section header:
    - Top: `BudgetDonutCard` populated from `standardCategories` (`GetCategoryAccentColor(cat.Key)`), `grandTotal`, and `totalBudgetLimit`.
    - Bottom inside same card: `totalActionGrid` with `txtTotalIn` and gradient `btnSetTotal` (`"Lưu tổng hạn mức"`).
  - Upgrade all 6 category cards to `ElevatedCardPanel` (`CornerRadius = 18`, `ElevationLevel = 1`), using `Color catAccent = isOver ? clrRedDanger : GetCategoryAccentColor(catName);` for:
    - Soft icon badge tinted gently from `catAccent` (`BlendControlColor(catAccent, clrCardBg, isDarkMode ? 0.80f : 0.88f)`),
    - Progress bar `ProgressColor = catAccent`, `ProgressColor2 = BlendControlColor(catAccent, Color.White, isDarkMode ? 0.18f : 0.25f)`,
    - Crisp inline **`%` pill** (`"{rawPct}%"`) beside the category title,
    - Keeping `txtLim` and `"Lưu hạn mức"` cleanly aligned and 100% functional.
  - Keep `"Xuất báo cáo chi tiêu (.CSV / Excel)"` pill cleanly styled below the category list.

- [ ] **Step 4: Full clean build, full `UiAudit` regression run & complete visual inspection**

Run:
1. `dotnet build "D:\clonedoan\PocketLog.slnx" --no-incremental` → Expect `0 Error(s)`.
2. `dotnet run --project "C:\Users\Thai Tu Thieu Tien\.gemini\antigravity\brain\5540703e-a554-424b-a50a-f76baf35c42e\scratch\uiaudit_wt\UiAudit.csproj"` → Expect ALL legacy Dual-Pane + Task 1–6 + Wave 2 Task 1–5 tests PASSED 100% (`exit code 0`).
3. Visually inspect final rendered screenshots across Light & Dark (`T3_Auth_560x900_Light`, `T3_FriendsDrawer_560x900_Dark`, `T4_Feed_560x900_Light`, `T4_Feed_560x900_Dark`, `T4_Gallery_560x900_Dark`, `T5_Camera_560x900_Dark`, `T6_Expense_560x900_Light`, `T6_Expense_445x680_Dark`, `FS_02_Dual_Feed_Expense`) via `read_file` to confirm native Android polish with zero visual defects.

- [ ] **Step 5: Commit**

```bash
git add PocketLog.Client/Client.Controls.cs PocketLog.Client/Client.Expense.cs
git commit -m "feat(ui): add fintech budget donut chart, synced category colors, and elevated wallet"
```

---

## Self-Review Check

- **Spec coverage:**
  - Slim Android Overlay Scrollbar + `MouseWheelScrollFilter` + Dual-Pane/Drawer isolation → Task 1 & Task 2.
  - Multi-layer `ElevatedCardPanel` (Light soft shadow + Dark OLED top rim) → Task 1, applied across Tasks 2, 3, 4, 5.
  - Non-truncating Header user chip + Story short Vietnamese names → Task 1 & Task 3.
  - Feed `1:1` square cards + Quick Footer (local Heart Option A + real `"Xem chia bill"` / `"Mở Kho ảnh"`) + `1:1` Gallery → Task 3.
  - Camera 4-stop feathered vignette dock (zero top hairline / zero corner sliver) + square-balanced viewfinder → Task 4.
  - Fintech `BudgetDonutCard` + synced 6-category colors + `%` chips + elevated Wallet → Task 5.
- **Type & signature consistency:** `AndroidOverlayScrollPanel` (`ContentHost`, `ScrollOffsetY`, `MaxScrollOffset`, `IsScrollable`, `ThumbBounds`, `ThumbOpacity`, `CenterColumnMode`, `MaxContentWidth`, `SetScrollOffset`, `ScrollByDelta`, `ScrollControlToVisible`, `SyncLayout`), `ElevatedCardPanel` (`CornerRadius`, `CardFillColor`, `CardBorderColor`, `CardBorderThickness`, `ElevationLevel`, `InnerCardBounds`), `GetShortVietnameseName`, `GetCategoryAccentColor`, `UiIcon.Heart`/`HeartFilled`/`ArrowRight`, `IsPostLiked`/`TogglePostLiked`, `BudgetSlice`, `BudgetDonutCard` match identically across every task.
- **Review Focus coverage:** All 5 failure modes are explicitly tested in their owning tasks (Task 1, Task 2, Task 3, Task 4, Task 5).
