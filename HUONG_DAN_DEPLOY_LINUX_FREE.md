# Thông tin vận hành & Hướng dẫn PocketLog Linux Cloud Server

Hệ thống **`PocketLog.Server.Linux`** đã được triển khai thực tế và đang hoạt động trực tuyến trên gói **Azure for Students** (`25520579@ms.uit.edu.vn`, miễn phí 100%).

---

## 1. Thông tin máy chủ đang chạy thực tế (Live Production)

- **Địa chỉ IP Tĩnh (Static Public IP):** `20.239.65.18` *(không bao giờ đổi kể cả khi máy tự tắt ban đêm và bật lại buổi sáng)*
- **Cổng kết nối TCP:** `8888` *(đã cấu hình sẵn trong `PocketLog.Client/App.config` — mở app lên là tự điền sẵn!)*
- **Vùng máy chủ (Region):** `eastasia` (Hong Kong — độ trễ thấp về Việt Nam)
- **Tên Resource Group:** `pocketlog-eas-rg`
- **Tên Máy ảo (VM):** `pocketlog-vm` (Ubuntu 24.04 LTS Gen2, `Standard_B2ats_v2` Free Services Tier + 1.5GB Swap)
- **Cơ sở dữ liệu:** SQLite tích hợp sẵn lưu bền vững trong Docker Volume `pocketlog_pocketlog_data` (`/data/PocketLogDB.sqlite` & `/data/ServerStorage`)
- **Lịch tự động tiết kiệm giờ/credit (Giờ Việt Nam UTC+7):**
  - **00:00 Đêm:** Tự động tắt & giải phóng phí tính toán (`Auto-shutdown: shutdown-computevm-pocketlog-vm`).
  - **07:00 Sáng:** Tự động bật nguồn lại thông qua **Azure Logic App (`pocketlog-autostart-07am`)** gắn Managed Identity — ngay khi máy bật, Docker tự động khởi chạy lại `pocketlog-server` sau ~5 giây (`restart: unless-stopped`).

---

## 2. Cách kết nối từ `PocketLog.Client` (Cho bạn & các bạn trong nhóm)

1. Mở ứng dụng **`PocketLog.Client`** (bấm `F5` trong Visual Studio hoặc mở file `PocketLog.Client.exe`).
2. Ô **IP / Domain** đã được điền sẵn **`20.239.65.18`**, ô **Port** điền sẵn **`8888`**.
   *(Nếu máy thành viên trong nhóm dùng bản code cũ chưa pull mới, chỉ cần gõ `20.239.65.18` và `8888` ở lần đầu tiên, ứng dụng sẽ tự lưu lại).*
3. Chuyển sang tab **Tạo tài khoản mới** ở lần đầu sử dụng trên Server mới, sau đó đăng nhập, kết bạn, chụp ảnh và chia tiền bình thường qua Internet!

---

## 3. Các lệnh quản trị nhanh qua SSH (Khi cần xem Log hoặc cập nhật Code)

Khóa SSH quản trị đã được lưu sẵn trên máy bạn tại `C:\Users\Thai Tu Thieu Tien\.ssh\pocketlog_azure_ed25519`. Mở PowerShell và chạy:

```powershell
# Kết nối vào máy ảo Ubuntu
ssh -i "$env:USERPROFILE\.ssh\pocketlog_azure_ed25519" azureuser@20.239.65.18
```

Sau khi vào trong máy ảo Ubuntu:
```bash
# Xem nhật ký (log) các máy đang kết nối theo thời gian thực
sudo docker logs -f pocketlog-server

# Khởi động lại Server (dữ liệu tài khoản & hình ảnh vẫn giữ nguyên 100%)
cd ~/PocketLog && sudo docker compose restart
```

---

## 4. Phương án dự phòng 100% Free khác (GitHub Codespaces + Playit.gg)

Trong repo vẫn giữ đầy đủ `.devcontainer/devcontainer.json`, `Dockerfile` và `deploy/setup-linux-vm.sh` để bạn có thể chạy Server trên bất kỳ máy ảo Linux nào khác hoặc mở trực tiếp trên **GitHub Codespaces** bất cứ lúc nào:
1. Trên GitHub repo -> **`<> Code`** -> **`Codespaces`** -> **`Create codespace`**.
2. Chạy `dotnet run --project PocketLog.Server.Linux -c Release &` và dùng `playit` mở TCP Tunnel cổng `8888`.
