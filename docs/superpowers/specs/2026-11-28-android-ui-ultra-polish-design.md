# Thiết kế Nâng cấp Giao diện PocketLog Client — Đợt 2: Android Native Ultra-Polish

**Ngày tạo:** 2026-11-28  
**Trạng thái:** Đã được người dùng duyệt (chọn Phương án A cho thanh chân thẻ Bảng tin)  
**Phạm vi:** Chỉ chỉnh sửa dự án `PocketLog.Client` (`.NET 8.0-windows`, `GDI+`, `Guna.UI2.WinForms 2.0.5.1`) trong `D:\clonedoan`.

---

## 1. Mục tiêu & Lý do (Intent)

Sau đợt nâng cấp giao diện Android Social UI đầu tiên (`Locket × Instagram Modern` Light/Dark), kết quả kiểm tra thực tế và đối chiếu ảnh chụp 10 màn hình cho thấy app vẫn còn 5 điểm dễ lộ chất WinForms truyền thống:
1. Thanh cuộn dọc Win32 màu xám có mũi tên `^`/`v` xuất hiện nổi bật bên phải (đặc biệt chói mắt trên nền Dark Mode `#0B0E14`).
2. Các thẻ nội dung vẫn phẳng với viền `1px` đơn, thiếu độ nổi khối mềm (elevation shadow / OLED top rim).
3. Chip tên người dùng trên thanh tiêu đề (`"Đ Đỗ Huy..."`) và nhãn tên bạn bè trên thanh Story (`"Trần Gia..."`) bị cắt cụt; thẻ ảnh Bảng tin chưa vuông cân đối kiểu Locket và chưa có thanh tương tác nhanh bên dưới ảnh.
4. Thanh dock đáy khung Camera còn đường cắt ngang cứng và khe góc tối nhỏ ở viền bo dưới.
5. Tổng quan ngân sách tháng trên tab Chi tiêu đang nằm ở cuối trang dưới dạng thanh ngang nhỏ, chưa trực quan bằng biểu đồ vòng tròn (Donut Chart) đồng bộ màu danh mục.

Đợt **Ultra-Polish** này giải quyết đồng bộ cả 5 điểm trên để app đạt độ hoàn thiện thị giác tiệm cận ứng dụng Android/iOS bản địa, đồng thời giữ nguyên 100% tính ổn định của giao thức mạng, Server Azure `20.239.65.18:8888`, và chế độ Full-Screen Dual-Pane.

---

## 2. Ràng buộc toàn cục (Global Constraints)

- **Phạm vi tệp:** Chỉ chỉnh sửa/bổ sung mã nguồn bên trong `D:\clonedoan\PocketLog.Client\`. Không sửa `PocketLog.Shared`, `PocketLog.Server`, `PocketLog.Server.Linux` hay schema CSDL.
- **Không thêm NuGet:** Chỉ sử dụng `.NET 8.0-windows` WinForms, `System.Drawing.Drawing2D` (GDI+) và `Guna.UI2.WinForms 2.0.5.1` sẵn có.
- **Bảo toàn hợp đồng tương thích:** Giữ nguyên `activeTabs`, `paneHosts`, `btnTabFeed`, `btnTabCamera`, `btnTabExpense`, `pnlHeader`, `pnlMainContainer`, `pnlBottomNav`, `HandleBottomTabClick`, `OpenInitialAuthenticatedTab`, `RefreshDataDrivenPanesOnSync`, `OpenMyGalleryScreen`, `StopWebcam`, `MinimumSize = (445, 580)`, cơ chế kéo giãn 8 cạnh `ChildEdgeRedirector`, và công thức đo cỡ nút tiếng Việt 125% DPI (`MeasurePillBtnWidth`, `"Ág"`).
- **Bảo toàn tính trung thực của tính năng cục bộ:** Nút thả tim `❤️` trên thẻ Bảng tin (Phương án A) lưu cục bộ trên máy tại `%LocalAppData%\PocketLog\liked_posts.json` và gắn tooltip rõ ràng (`"Lưu yêu thích trên máy này"`) để không gây nhầm lẫn với dữ liệu đồng bộ server.
- **Tương phản WCAG & Quản lý tài nguyên GDI+:** Mọi chữ/icon nhỏ trên Dark Mode đạt tương phản cao (`#38BDF8` hoặc `#F1F5F9` trên `#161B26`/`#232B45`), mọi `Brush`/`Pen`/`GraphicsPath`/`Bitmap`/`Timer`/`IMessageFilter` đều được giải phóng sạch sẽ không rò rỉ.

---

## 3. Phương án kiến trúc đã xem xét (Approaches)

### Hướng 1 (Đã chọn): Thuần GDI+ Double-Buffered ngay trong `PocketLog.Client`
- Xây dựng 3 custom control dùng chung (`AndroidOverlayScrollPanel`, `ElevatedCardPanel`, `BudgetDonutCard`) trong `Client.Controls.cs`, gắn vào `Client.cs` và 5 màn hình (`Login`, `Friends`, `NewFeed`, `Camera`, `Expense`).
- **Ưu điểm:** Không tăng dung lượng `.exe`, không phát sinh phụ thuộc ngoài, tương thích tuyệt đối với DirectShow webcam và toàn bộ bộ kiểm thử `UiAudit`, vẽ chính xác 100% ở cả màn hình thật lẫn `DrawToBitmap`.
- **Đánh đổi:** Phải tự quản lý mouse-wheel routing (`IMessageFilter`), clip bán kính góc, và tự đo lại chiều cao thẻ khi thay đổi kích thước cửa sổ.

### Hướng 2 (Loại bỏ): Nhúng WebView2 HTML/CSS Frontend vào WinForms
- **Lý do loại:** Đòi hỏi viết lại toàn bộ 6 màn hình sang HTML/JS + cầu nối C#, làm hỏng tích hợp webcam DirectShow hiện có, tăng nặng RAM và dung lượng triển khai khi gửi file `.exe` cho bạn bè.

---

## 4. Chi tiết thiết kế từng hạng mục

### 4.1. Thanh cuộn mảnh kiểu Android (`AndroidOverlayScrollPanel`)
- Kế thừa `SmoothScrollPanel : Panel`, đặt `AutoScroll = false` để triệt tiêu hoàn toàn thanh cuộn Win32 xám dọc/ngang ở tất cả 5 màn hình (`CreateResponsiveScrollPage`) và Drawer Bạn bè (`Client.Friends.cs`).
- Cung cấp bề mặt cuộn dọc mượt (`ScrollOffsetY`, `MaxScrollOffset`, `IsScrollable`, `SetScrollOffset(int, bool)`, `ScrollByDelta(int, bool)`, `ScrollControlToVisible(Control)`) kết hợp dải overlay mảnh `10px` neo cạnh phải tự vẽ viên thuốc **`4px`** (mở rộng `6px` khi hover/kéo), tự làm mờ dần sau `~1.2s` không cuộn, hỗ trợ kéo thumb và click rãnh cuộn.
- Đăng ký duy nhất một `IMessageFilter` (`MouseWheelScrollFilter`) ở cấp `Form` (gỡ bỏ trong `Dispose(bool)`) để cuộn mượt bằng con lăn chuột ở mọi vùng con, tôn trọng lớp phủ `pnlDrawerBackdrop.Visible` (khi mở Drawer chỉ cuộn Drawer) và cuộn độc lập từng pane trái/phải ở chế độ Dual-Pane `1920×1020`.
- Gắn `AttachEdgeResizeRedirector` để không cản trở kéo viền phải của cửa sổ ở chế độ Normal.

### 4.2. Độ nổi khối đa lớp cho thẻ (`ElevatedCardPanel`)
- Custom `Panel` double-buffered (`CornerRadius`, `CardFillColor`, `CardBorderColor`, `ElevationLevel`, `InnerCardBounds`) chừa đệm bóng mỏng (`4px` hai bên, `2px` trên, `6px` dưới).
- **Light Mode:** Vẽ 3 lớp bóng tỏa mềm màu xanh-xám ấm (`rgba(15, 23, 42, alpha)`) + mặt thẻ trắng bo tròn + viền `1px` dịu.
- **Dark Mode:** Bóng đáy sâu hơn (`rgba(0, 0, 0, alpha)`) + đường highlight kính sáng mảnh `1px` cạnh trên thẻ (`rgba(255, 255, 255, 26)`) tạo chiều sâu OLED tinh tế.
- Áp dụng đồng nhất cho thẻ Auth (`cardServer`, `cardAuth`), thẻ bài đăng Feed & Empty Hero, thẻ Gallery 2 cột, khung Camera & form gắn tag, thẻ chia tiền & 6 thẻ danh mục Chi tiêu, cùng hàng bạn bè trong Drawer.

### 4.3. Header, Story Strip, Bảng tin `1:1` & Thanh chân thẻ (Phương án A)
- **Header (`Client.cs`):** Đo chính xác độ rộng chữ `"PocketLog"`, tối ưu khoảng cách cụm nút bên phải; khi bề rộng cửa sổ ở khoảng `510–610px`, tự động hiển thị tên ngắn gọn (`GetShortVietnameseName`, ví dụ `"Huy Hoàng"` thay vì cắt cụt `"Đỗ Huy..."`), mở rộng cửa sổ hiển thị lại đầy đủ `"Đỗ Huy Hoàng"`.
- **Story Strip (`Client.Controls.cs`):** Hiển thị tên ngắn tiếng Việt tự nhiên (`"Gia Huy"`, `"Minh Anh"`, `"Thế Hưng"`) bằng `TextRenderer` sắc nét (xóa khoảng trắng lệch chữ và xóa `"..."`), đặt trên bề mặt nổi nhẹ.
- **Bảng tin & Kho ảnh (`Client.NewFeed.cs`):**
  - Tỉ lệ ảnh `SocialPhotoCanvas` vuông cân đối **`1:1`** theo bề rộng thẻ (`Math.Clamp(cardWidth - 36, 260, 380)`), tự động cập nhật chiều cao thẻ và đẩy tọa độ các thẻ bên dưới khi đổi cỡ cửa sổ.
  - **Thanh chân thẻ (`Height = 42`)** ngay dưới ảnh:
    - Trái: Nút tim cục bộ (`UiIcon.Heart` / `UiIcon.HeartFilled`, `"Thích"` / `"Đã thích"`, màu hồng Rose khi bật, lưu `%LocalAppData%\PocketLog\liked_posts.json`, tooltip `"Lưu yêu thích trên máy này"`).
    - Phải: Nút hành động ngữ cảnh thật với `UiIcon.ArrowRight`: bài có gắn tag chi tiêu hiển thị **"Xem chia bill"** (chọn đúng khoản nợ tương ứng nếu còn chưa tất toán và chuyển sang tab `EXPENSE`); bài cá nhân của mình hiển thị **"Mở Kho ảnh"** (mở `MyGallery`); bài bạn bè không tag hiển thị nhãn trạng thái nhẹ `"Khoảnh khắc"`.
  - **Kho ảnh (`BuildMyGalleryControl`):** Nâng cấp ô lưới 2 cột thành `ElevatedCardPanel` kèm thumbnail vuông **`1:1`**.

### 4.4. Khung Camera mượt viền (`Client.Camera.cs`)
- Thay đường cắt ngang `1px` cứng và lớp phủ tối đột ngột trên `CameraBottomDockPanel` bằng dải mờ chuyển sắc 4 điểm mượt (`alpha 0 → 88 → 176 → 224`, cao `84px`), bo khít chính xác bán kính bên trong khung ngắm (`22px`) để triệt tiêu hoàn toàn khe hở màu xanh ở hai góc dưới.
- Cân lại chiều cao viewfinder theo tỉ lệ vuông Locket (`Math.Clamp(w - 12, 290, 380)`), bọc khung ngắm và khung form gắn tag chi tiêu trong `ElevatedCardPanel`.

### 4.5. Tab Chi tiêu chuẩn Fintech & Biểu đồ Donut (`Client.Controls.cs`, `Client.Expense.cs`)
- **Bảng màu danh mục dùng chung (`GetCategoryAccentColor`):**
  - `Ăn uống`: `#F97316` · `Xăng cộ`: `#0EA5E9` · `Quần áo`: `#8B5CF6` · `Tiền trọ`: `#10B981` · `Mua sắm`: `#EC4899` · `Học tập`: `#F59E0B`.
- **`BudgetDonutCard`:** Đặt nổi bật phía trên danh sách 6 danh mục (thay cho thẻ thanh ngang ở cuối trang cũ). Vẽ vòng tròn **Donut Chart đa sắc** chống răng cưa (`118×118`, nét dày `14px` đầu tròn) hiển thị tỉ lệ từng danh mục + tổng `%` ở tâm, tổng đã chi / hạn mức, nhãn `"Còn lại"` (Xanh lục) hoặc `"Vượt"` (Đỏ hồng), chú thích màu 2 cột tự thu gọn ở `445px`, cùng ô nhập & nút gradient `"Lưu tổng hạn mức"` ngay trong cùng thẻ.
- **Ví Gradient (`WalletGradientPanel`):** Thêm bóng đổ mềm dưới chân thẻ và vệt sáng kính chéo tinh tế.
- **6 thẻ danh mục:** Đồng bộ màu chấm/icon badge và thanh tiến độ với màu danh mục trên Donut Chart, bổ sung viên thuốc **`%`** (`20%`, `0%`, `105%`), giữ nguyên đầy đủ ô nhập hạn mức và nút `"Lưu hạn mức"`.

---

## 5. Phạm vi tệp & Chiến lược kiểm chứng

- **Chỉnh sửa (chỉ trong `D:\clonedoan\PocketLog.Client\`):**
  - `Client.Controls.cs` (`AndroidOverlayScrollPanel`, `ElevatedCardPanel`, `BudgetDonutCard`, nâng cấp `StoryAvatarStrip`)
  - `Client.cs` (`CreateResponsiveScrollPage`, `MouseWheelScrollFilter`, `GetShortVietnameseName`, `GetCategoryAccentColor`, `LayoutHeaderControls`, `UiIcon.Heart`/`HeartFilled`/`ArrowRight`)
  - `Client.Theme.cs` (lưu/đọc `liked_posts.json`, cập nhật `ApplyShellTheme` nếu cần)
  - `Client.Login.cs`, `Client.Friends.cs`, `Client.NewFeed.cs`, `Client.Camera.cs`, `Client.Expense.cs`
- **Kiểm chứng:**
  1. `dotnet build D:\clonedoan\PocketLog.slnx --no-incremental` đạt `0 Error(s)`.
  2. Cập nhật và chạy toàn bộ bộ kiểm thử `UiAudit` (Light & Dark, `445×680`, `560×900`, `1920×1020` Dual-Pane) xác minh `0` Win32 scrollbar (`AutoScroll == false`, `VerticalScroll.Visible == false`, `HorizontalScroll.Visible == false`), `0` overflow, mouse-wheel isolation, Donut edge cases, local heart persistence, và **kiểm tra trực quan từng ảnh PNG sau nâng cấp**.
