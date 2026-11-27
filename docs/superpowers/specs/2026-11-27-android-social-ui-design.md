# Thiết kế Nâng cấp Giao diện PocketLog Client — Phong cách Mạng Xã Hội Android Hiện Đại (Locket × Instagram Modern)

**Ngày tạo:** 2026-11-27  
**Phạm vi:** Dự án `PocketLog.Client` (`.NET 8 WinForms`, `Guna.UI2.WinForms 2.0.5.1`) trong `D:\clonedoan`.

---

## 1. Mục tiêu & Ý định (Intent)

Biến giao diện `PocketLog.Client` từ phong cách thẻ WinForms phẳng, đơn sắc hiện tại thành giao diện **trẻ trung chuẩn ứng dụng mạng xã hội Android (Locket × Instagram Modern)** có chiều sâu, giàu tương tác, khắc phục triệt để các lỗi hiển thị WinForms (lộ viền hộp vuông quanh caption trên ảnh) và bổ sung **nút chuyển đổi nhanh Sáng / Tối (Daylight / Midnight)** ngay trên thanh tiêu đề.

### Giá trị mang lại cho người dùng
- **Trải nghiệm mạng xã hội ảnh thực thụ:** Thanh Story bạn bè viền chuyển sắc (Gradient Ring + chấm xanh Online) ở đầu Bảng tin; thẻ ảnh bo góc lớn `28px` có lớp phủ mờ chuyển sắc đáy ảnh và viên thuốc caption kính mờ tự vẽ GDI+ không bao giờ lộ viền vuông; khung Camera có nút chụp tròn viền kép (Double-ring Shutter) chuẩn Android.
- **Nhận diện tài chính rõ ràng & hiện đại:** Thẻ công nợ Gradient nổi bật trên tab Chi tiêu, huy hiệu đếm bill chưa tất toán trên thanh điều hướng đáy, và chế độ Sáng rõ nét khi kiểm tra con số chi tiêu / chuyển khoản song hành với chế độ Tối nổi bật ảnh kiểu Locket.
- **Giữ nguyên 100% logic & độ tin cậy kỹ thuật:** Không thay đổi giao thức mạng (`PocketLog.Shared`), Server (`PocketLog.Server`, `PocketLog.Server.Linux`), cấu hình IP Cloud (`20.239.65.18:8888`), kéo giãn cửa sổ 8 hướng (`ChildEdgeRedirector`), hiển thị chuẩn DPI 125% tiếng Việt, và chế độ chia đôi màn hình Full-Screen (`IsSplitModeCapable`).

---

## 2. Mục tiêu loại trừ (Non-Goals) & Ràng buộc

### Non-Goals (Không thực hiện trong phạm vi thiết kế này)
- **Không** sửa `PocketLog.Shared`, `PocketLog.Server`, `PocketLog.Server.Linux` hay cấu trúc bảng CSDL.
- **Không** thêm package NuGet bên ngoài; chỉ sử dụng `.NET 8 WinForms`, `GDI+` (`System.Drawing.Drawing2D`) và `Guna.UI2.WinForms 2.0.5.1` hiện có.
- **Không** tự động gửi gói mạng bổ sung hay thay đổi luồng `LOGIN_REQ` / `SYNC_REQ` / `CREATE_POST_REQ` / `UPLOAD_PROOF_REQ` / `VERIFY_BILL_REQ`.

### Ràng buộc kỹ thuật bắt buộc
1. **Giữ tương thích kiểm thử & API nội bộ:** Giữ nguyên các field/method cốt lõi mà bài kiểm thử & logic hiện tại dựa vào (`activeTabs`, `paneHosts`, `btnTabFeed`, `btnTabCamera`, `btnTabExpense`, `pnlHeader`, `pnlMainContainer`, `pnlBottomNav`, `pnlDrawerBackdrop`, `pnlFriendsDrawer`, `HandleBottomTabClick`, `OpenInitialAuthenticatedTab`, `RefreshDataDrivenPanesOnSync`, `OpenMyGalleryScreen`, `StopWebcam`).
2. **Đo chữ chuẩn DPI 125% tiếng Việt:** Mọi nút pill và label tiếp tục dùng `MeasurePillBtnWidth(...)` và chiều cao `"Ág"` trong `MakeLabel(...)`; giữ `MinimumSize = (445, 580)` không tràn/cắt chữ ở mọi kích thước cửa sổ (`445px` -> `1920px`).
3. **Kéo giãn 8 hướng & Full-Screen 2-pane:** Toàn bộ custom control mới đều gắn `AttachEdgeResizeRedirector(...)` nếu lọt vào vùng viền/góc hoặc nằm trong container đã gắn. Chế độ chia đôi màn hình (`Width >= 1100` hoặc `Maximized`) hoạt động đồng bộ cho cả Light Mode và Dark Mode.
4. **Không mất dữ liệu đang nhập khi đổi Theme hoặc nhận `SYNC_RES`:** Đổi Sáng/Tối khi đang đứng ở tab `CAMERA` phải bảo toàn ảnh đã chụp/dán (`uploadedPhoto`) và nội dung ô nhập món/số tiền/caption/tag bạn bè đang chọn.

---

## 3. Các hướng tiếp cận đã cân nhắc (Approaches)

| Hướng tiếp cận | Đặc điểm | Đánh đổi | Quyết định |
|---|---|---|---|
| **Hướng 1: Locket × Instagram Modern (Dual Theme: Daylight Sáng + Midnight Tối)** | Nâng cấp đồng bộ hệ màu động 2 chế độ, custom GDI+ photo card xóa hộp vuông, Story bar bạn bè viền gradient, nút chụp tròn viền kép, Floating Nav Pill nổi | Cần cấu trúc hóa bảng màu qua `AppTheme` và hỗ trợ `ApplyTheme()` động không làm mất trạng thái màn hình đang mở | **Đã chọn** (khớp cả chất MXH ảnh lẫn tính dễ đọc của sổ chi tiêu) |
| **Hướng 2: Thuần Locket Dark Mode cố định** | Nền tối `#0B0E14` cố định, nhanh hơn khi không cần quản lý `ApplyTheme` | Màn hình Chi tiêu có nhiều thanh hạn mức + bill ngân hàng nền trắng sẽ hơi gắt và khó nhìn lâu ban ngày | Loại |
| **Hướng 3: Thuần Instagram Sáng cố định** | Chỉ tinh chỉnh nền sáng, giữ cơ chế sơn tĩnh | Thiếu chất Locket nổi ảnh trên nền tối buổi tối và thiếu tính cá nhân hóa như app Android thật | Loại |

---

## 4. Kiến trúc Giao diện & Bảng màu Chi tiết (Design Spec)

### 4.1. Hệ thống Theme Động (Light & Dark Mode) — `Client.Theme.cs`

Trạng thái `isDarkMode` được lưu bền vững tại `%LocalAppData%\PocketLog\theme_mode.txt` (`"dark"` hoặc `"light"`, mặc định `"light"` khi chưa có file).

| Token vai trò | Light Mode (`Daylight Social`) | Dark Mode (`Midnight Locket`) |
|---|---|---|
| `AppBg` (nền app) | `#F4F6FB` (`244, 246, 251`) | `#0B0E14` (`11, 14, 20`) |
| `CardBg` (bề mặt thẻ) | `#FFFFFF` (`255, 255, 255`) | `#161B26` (`22, 27, 38`) |
| `InputBg` / `SurfaceAlt` | `#EEF2F9` (`238, 242, 249`) | `#1E2534` (`30, 37, 52`) |
| `FloatingNavBg` (thanh tab nổi) | `#FFFFFF` (`255, 255, 255`) | `#1A202E` (`26, 32, 46`) |
| `BorderLight` (viền mảnh `1px`) | `#E2E8F4` (`226, 232, 244`) | `#2A3346` (`42, 51, 70`) |
| `BorderWindow` (khung form) | `#D6DFEE` (`214, 223, 238`) | `#283144` (`40, 49, 68`) |
| `AccentPrimary` (`clrPrimaryGreen`) | `#4F46E5` Electric Indigo (`79, 70, 229`) | `#6366F1` Bright Indigo (`99, 102, 241`) |
| `AccentSecondary` | `#0EA5E9` Sky Cyan (`14, 165, 233`) | `#38BDF8` Neon Sky (`56, 189, 248`) |
| `StoryRingGradient` | 3 màu: `#F59E0B` -> `#EC4899` -> `#6366F1` | 3 màu: `#FBBF24` -> `#F472B6` -> `#818CF8` |
| `SoftAccentBg` | `#EEF2FF` (`238, 242, 255`) | `#232B45` (`35, 43, 69`) |
| `SoftAccentBorder` | `#C7D2FE` (`199, 210, 254`) | `#4F46E5` (`79, 70, 229`) |
| `TextMain` | `#0F172A` (`15, 23, 42`) | `#F1F5F9` (`241, 245, 249`) |
| `TextMuted` | `#64748B` (`100, 116, 139`) | `#94A3B8` (`148, 163, 184`) |
| `TextSecondary` (`clrIndigo`) | `#334155` (`51, 65, 85`) | `#CBD5E1` (`203, 213, 225`) |
| `Danger` / `SoftDangerBg` | `#F43F5E` / `#FFE4EB` | `#FB7185` / `#3E1826` |
| `Success` / `OnlineDot` | `#10B981` | `#34D399` |

#### Cơ chế chuyển Theme (`ToggleThemeMode`)
- Lưu ngay `"dark"` / `"light"` vào `%LocalAppData%\PocketLog\theme_mode.txt`.
- Cập nhật bộ token màu, gọi lại `ApplyShellTheme()` cho Form chính, `pnlHeader`, `pnlMainContainer`, `pnlBottomNav`, `pnlDrawerBackdrop`, `pnlFriendsDrawer`, `btnWinMin/Max/Close`, nút đổi theme `btnThemeToggle`.
- Nếu đang ở màn Auth (`activeTabName == "AUTH"`): chụp nhanh giá trị đang gõ (`serverIp`, `serverPort`, `username`, `password`, `isPasswordShown`, `isRegisterMode`, `initBudget`) -> dựng lại Auth screen -> điền trả toàn bộ giá trị đang gõ để không mất ký tự nào.
- Nếu đã đăng nhập: trước khi gọi `RefreshPaneContent` trên các tab trong `activeTabs`, nếu tab `"CAMERA"` đang mở thì lưu tạm trạng thái nháp (`cameraDraft`: `uploadedPhoto` clone, `selectedCategory`, `taggedFriends`, `noteText`, `amountText`, `captionText`) vào trường của `Client` và khôi phục ngay sau khi dựng lại pane `"CAMERA"`; đồng thời cập nhật lại `btnTabFeed`, `btnTabCamera`, `btnTabExpense` và Friends Drawer nếu đang mở.

---

### 4.2. Custom Controls GDI+ Chuẩn Android — `Client.Controls.cs`

Thay thế các chi tiết thô của WinForms bằng control tự vẽ `DoubleBuffered` + `SmoothingMode.AntiAlias`:

1. **`SocialPhotoCanvas` (Khung ảnh Locket / Instagram Post Card):**
   - Bo tròn toàn khối theo bán kính `26px`, vẽ **trực tiếp trên 1 bề mặt Paint duy nhất** nên loại bỏ vĩnh viễn lỗi viền hộp vuông quanh caption trên ảnh.
   - **Thứ tự vẽ trong `OnPaint`:**
     1. Vẽ ảnh `FitImageCover` (hoặc nền `LinearGradientBrush` dịu + họa tiết tròn mờ + vector icon danh mục nếu bài đăng không kèm ảnh).
     2. Vẽ **dải tối mờ đáy ảnh (Bottom Scrim)** từ 56% chiều cao đến đáy (`alpha 0 -> 185`) để mọi chữ/badge màu trắng luôn nổi rõ trên cả ảnh sáng lẫn ảnh tối.
     3. Vẽ **Top-Left Category/Amount Pill** (`12px` từ góc trên trái): nền kính tối bán trong suốt (`Color.FromArgb(168, 15, 23, 42)`), viền mảnh `Color.FromArgb(75, 255, 255, 255)`, icon danh mục + nhãn ngắn (ví dụ `Ăn uống • 360k`) hoặc `Khoảnh khắc Pocket`.
     4. Nếu có tag bạn bè (`TaggedFriends.Count > 0`): vẽ **Top-Right Tag Pill** (tối đa hiện `@Tên` nếu 1 người hoặc `Tag +N bạn`).
     5. Vẽ **Bottom Frosted Caption Pill**: viên thuốc kính mờ nằm giữa sát đáy ảnh (`Bottom - 14px`), nền `Color.FromArgb(172, 18, 24, 38)`, viền sáng `1px` `Color.FromArgb(85, 255, 255, 255)`, chữ trắng font Medium hỗ trợ tự ngắt tối đa 2 dòng khi câu dài hoặc màn hình hẹp.
     6. Khi di chuột hover lên bài `isMyTopCard`: vẽ viền nhấn nhẹ `2px` màu `AccentPrimary`.
   - Quản lý bộ nhớ chặt chẽ: cache bitmap đã resize khớp `ClientSize`, chỉ tái tạo khi kích thước thay đổi `>= 6px`, tự `Dispose()` khi control bị hủy.

2. **`StoryAvatarStrip` (Dải Avatar Bạn bè kiểu Instagram Stories):**
   - Control cuộn ngang (hỗ trợ kéo lăn chuột MouseWheel chuyển cuộn ngang) đặt ngay dưới thanh tiêu đề của tab `Bảng tin` (`Height = 96px`).
   - Phần tử đầu tiên luôn là chip **`+ Kết bạn`** với vòng nét đứt / icon `UserPlus` màu `AccentPrimary` (kèm chấm đếm gợi ý mới nếu `friendSuggestions.Count > 0`).
   - Tiếp theo là danh sách `myFriends` sắp xếp ưu tiên người `IsOnline == true` đứng trước:
     - Mỗi item rộng `72px`: avatar tròn `48×48px`, bên ngoài là vòng tròn `3px`: dùng **Gradient 3 màu (`#F59E0B -> #EC4899 -> #6366F1`)** cho bạn đang Online (hoặc viền xám mảnh cho bạn Offline), thêm chấm tròn xanh `#10B981` viền nền thẻ ở góc dưới phải khi `IsOnline`.
     - Bên dưới là tên ngắn (`Trim`/`AutoEllipsis` 1 dòng) căn giữa chuẩn `"Ág"`.
   - Nhấn vào bất kỳ item nào trong dải sẽ mở `OpenFriendsDrawer()`.

3. **`DoubleRingShutterButton` (Nút bấm chụp chuẩn Camera điện thoại):**
   - Nút tròn `56×56px` tự vẽ: vòng ngoài đường kính `52px` nét `3px` màu trắng (khi live cam) hoặc Gradient `AccentPrimary -> AccentSecondary` (khi đã chụp giữ ảnh), vòng tròn lõi bên trong thu nhỏ `40×40px` co nhẹ hiệu ứng nhấn khi Hover/MouseDown.

4. **`FloatingBottomNav` & `GradientPillButton`:**
   - Thanh `pnlBottomNav` cao `82px` đồng màu nền `AppBg` (bỏ đường kẻ ngang cứng ngắc toàn chiều rộng).
   - Bên trong vẽ 1 **viên thuốc nổi (`Floating Capsule`, `BorderRadius = 28`)** kích thước bằng phần cụm 3 tab + padding `6px` xung quanh, tô màu `FloatingNavBg`, viền `BorderLight` + bóng đổ mềm 2 lớp (`alpha 16` và `8`) vẽ trực tiếp xuống `pnlBottomNav`.
   - `GradientPillButton` kế thừa `Guna2Button` (giữ nguyên tương thích `btnTabFeed`, `btnTabCamera`, `btnTabExpense` và các kiểm thử `FillColor`), ghi đè `OnPaint` để vẽ nền **Gradient chéo (`AccentPrimary -> AccentSecondary`)** khi nút đang active; riêng `btnTabExpense` vẽ thêm chấm/badge tròn nhỏ màu `Danger` hiển thị số bill chưa thanh toán (`unpaidCount`) ở góc trên phải khi `unpaidCount > 0`.
   - Giữ `lblSplitModeHint` hiển thị gọn bên trái viên thuốc nổi khi màn hình đủ rộng (`IsSplitModeCapable`).

5. **Gradient Icon Mark (`UiIcon.Brand`, `UiIcon.Moon`, `UiIcon.Sun`, `UiIcon.Eye`, `UiIcon.EyeOff`):**
   - Bổ sung 4 vector icon mới (`Moon`, `Sun`, `Eye`, `EyeOff`) trong `UiIcon` enum.
   - Sửa `UiIcon.Brand` dùng cutout trong suốt (`CompositingMode.SourceCopy`) thay vì vẽ đè bằng màu `clrCardBg` cố định, giúp logo sạch sẽ trên cả nền gradient lẫn nền tối.

---

### 4.3. Thiết kế chi tiết từng màn hình

1. **Header cửa sổ (`pnlHeader`, `Height = 58px`):**
   - Trái: Logo Badge nền Gradient + nhãn chữ `"PocketLog"` đậm.
   - Phải (sắp xếp tự động từ phải sang trái trong `LayoutHeaderControls()`):
     - Window Controls (`Minimize`, `Maximize`, `Close`) bo tròn `10px`.
     - **`btnThemeToggle` (`34×34px` pill):** Hiển thị vector icon **Mặt trăng** (khi đang Sáng -> bấm để sang Tối) hoặc **Mặt trời** (khi đang Tối -> bấm để sang Sáng), kèm `ToolTip`.
     - Khi đã đăng nhập: nút `Đăng xuất` (`btnLogoutTop`) + chip Avatar người dùng (`pnlHeaderUserChip`) — tự ẩn chip nếu chiều rộng `< 510px` để không bao giờ đụng chữ `"PocketLog"`.

2. **Màn Đăng nhập / Kết nối (`Client.Login.cs`):**
   - **Hero Banner Gradient (`118px`):** Phủ Gradient 3 điểm `Indigo -> Violet -> Sky`, góc phải trên có badge nhỏ trạng thái Server mặc định (`20.239.65.18:8888`), tên app & slogan *"Chia sẻ khoảnh khắc • Chia bill sòng phẳng"*.
   - **Card Máy chủ (gọn `124px`):** Giữ nút `"Dò Server LAN"` tự đo chiều rộng `MeasurePillBtnWidth`, 2 ô nhập IP/Domain và Port bo `14px`.
   - **Card Xác thực:** Segmented Toggle `Đăng nhập` / `Tạo tài khoản` mượt mà, ô Mật khẩu tích hợp nút vector hình con mắt **`btnTogglePass`** ở bên phải để bật/tắt ẩn hiện mật khẩu (`PasswordChar`), nút CTA chính `Đăng nhập` / `Tạo tài khoản` dùng `Guna2GradientButton` bo `24px`.

3. **Tab Bảng tin & Kho ảnh (`Client.NewFeed.cs`):**
   - **Top Bar:** Tiêu đề `"Bảng tin"`, nút pill `"Kho ảnh"` (hiện số ảnh của tôi nếu > 0 hoặc nhãn `"Kho ảnh"`) và nút `"Bạn bè (N)"`.
   - **Dải Story Bạn bè (`StoryAvatarStrip`):** Hiển thị ngay bên dưới Top Bar.
   - **Khoảnh khắc mới nhất của bạn + Feed bạn bè:** Sử dụng `SocialPhotoCardCanvas` thay thế hoàn toàn `photoFrame + pillCaption`. Nếu chưa có ảnh nào, thẻ trống bo `26px` có icon badge Gradient thân thiện chạm vào để mở tab `Chụp ảnh`.
   - **Màn Kho ảnh (`BuildMyGalleryControl`):** Lưới 2 cột các thẻ bo `22px`, ảnh thumbnail phủ bằng `SocialPhotoCanvas` (ở chế độ thu gọn ẩn caption/tag pill để ảnh thoáng sạch) kèm 2 dòng thông tin phân cấp màu rõ ràng.

4. **Tab Chụp ảnh (`Client.Camera.cs`):**
   - **Khung ngắm Camera (`pnlViewfinder`, cao `390px`):**
     - Phía trên góc trái: Badge trạng thái Live/Ảnh (`"Cam trước"` / `"Đã chụp"` / `"Đã dán ảnh"`); góc phải: Nút `"Đổi cam"`.
     - **Camera Bottom Dock (`68px` ngay đáy khung ngắm):** Căn 3 thành phần như app Camera Android: trái = nút pill `"Chọn ảnh"`, giữa = **`DoubleRingShutterButton`** (bấm để chụp ảnh hoặc bấm chụp lại), phải = nút pill `"Dán ảnh"` (`Ctrl+V`).
     - Hỗ trợ lưu nháp trạng thái `cameraDraft` khi đổi Theme hoặc đồng bộ nền.
   - **Thẻ Chi tiêu & Tag bạn bè:** Lưới 6 danh mục chi tiêu bo `16px` (`Ăn uống`, `Xăng cộ`, `Quần áo`, `Tiền trọ`, `Mua sắm`, `Học tập`), hàng tiêu đề `"Gắn tag bạn bè"` đặt kèm nút tắt **"+ Thêm bạn"** ở góc phải (mở `OpenFriendsDrawer()` nhanh khi `Chọn ảnh` và `Dán ảnh` đã chuyển lên Camera Dock), chip bạn bè dạng `+ Tên` / `✓ Tên`, ô nhập món & số tiền nổi bật, nút `"Đăng lên Bảng tin Pocket"` nền **Gradient toàn chiều ngang** cao `54px`.

5. **Tab Chi tiêu & Chia bill (`Client.Expense.cs`):**
   - **Thẻ Công nợ Gradient (`88px`):** Guna2GradientPanel tông Indigo–Violet–Blue (`#312E81` -> `#4338CA`), chia 2 cột cân đối `"Bạn đang nợ"` (số màu hồng neon nếu `> 0`) và `"Bạn bè nợ bạn"` (số xanh ngọc nếu `> 0`).
   - **Thẻ Khoản chia đang chọn, Danh sách bill chưa tất toán (`unpaidList`), 6 thanh Progress Hạn mức theo danh mục, Thẻ Tổng hạn mức tháng & Nút xuất CSV:** Toàn bộ đồng bộ theo `AppTheme` Sáng/Tối, viền bo `20–24px`, tương phản cao trên cả hai nền.

6. **Drawer Bạn bè (`Client.Friends.cs`):**
   - Trượt mượt từ cạnh phải trên nền `pnlDrawerBackdrop` đồng tông dịu theo theme, header bo gọn, thanh tìm kiếm & nút `"+ Kết bạn"`, các hàng bạn bè hiển thị avatar có chấm xanh Online/Offline và xác nhận xóa 2 bước `"Xóa bạn"` -> `"Chắc xóa?"` như cũ.

---

## 5. Phạm vi tệp thay đổi & Chiến lược kiểm thử

### Tệp dự kiến tạo mới / chỉnh sửa (chỉ trong `D:\clonedoan\PocketLog.Client\`)
- **Tạo mới:** `Client.Theme.cs` (định nghĩa bảng màu Light/Dark, lưu/đọc preference, `ApplyTheme` & lưu giữ `cameraDraft`).
- **Tạo mới:** `Client.Controls.cs` (các custom GDI+ controls: `SocialPhotoCanvas`, `StoryAvatarStrip`, `DoubleRingShutterButton`, helper vẽ gradient/scrim/pill).
- **Chỉnh sửa:** `Client.cs`, `Client.Login.cs`, `Client.NewFeed.cs`, `Client.Camera.cs`, `Client.Expense.cs`, `Client.Friends.cs`.

### Phương án kiểm chứng (Verification Plan)
1. `dotnet build D:\clonedoan\PocketLog.slnx` đạt `0 Error(s)`.
2. Mở rộng bộ kiểm thử tự động ngoài workspace (trong `scratch\uiaudit`) để xác minh cả hành vi lẫn **chụp ảnh kiểm tra bằng mắt (visual inspection)** trên:
   - Light Mode & Dark Mode ở cửa sổ hẹp (`560×900`, `445×680` kích thước tối thiểu) và Full-Screen 2-pane (`1920×1020`) cho cả 5 màn (`Auth`, `Feed + Story Strip`, `Gallery`, `Camera + DoubleRingShutter`, `Expense + Gradient Banner`, `Friends Drawer`).
   - Kiểm tra 0 control tràn viền (`overflow == 0`), 0 cắt chữ tiếng Việt ở 125% DPI, đổi Theme giữa chừng không mất dữ liệu đang nhập trên tab `Camera`, và toàn bộ bài test Full-Screen Dual-Pane cũ tiếp tục đạt 100%.
