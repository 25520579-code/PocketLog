#!/usr/bin/env bash
set -e

echo "=========================================================="
echo "   CÀI ĐẶT TỰ ĐỘNG POCKETLOG SERVER TRÊN UBUNTU LINUX VM  "
echo "=========================================================="

# 1. Đặt múi giờ Việt Nam (UTC+7)
sudo timedatectl set-timezone Asia/Ho_Chi_Minh || true

# 2. Tạo 1.5GB Swap nếu máy ảo Free Tier 1GB RAM chưa có Swap (chống tràn RAM khi build Docker)
if [ "$(swapon --show | wc -l)" -eq 0 ] && [ ! -f /swapfile ]; then
    echo "-> Tạo 1.5GB Swap cho máy ảo RAM nhỏ..."
    sudo fallocate -l 1536M /swapfile || sudo dd if=/dev/zero of=/swapfile bs=1M count=1536
    sudo chmod 600 /swapfile
    sudo mkswap /swapfile
    sudo swapon /swapfile
    if ! grep -q '/swapfile' /etc/fstab; then
        echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
    fi
fi

# 3. Cài Docker (có chờ khóa apt tự động trên máy ảo mới khởi tạo)
if ! command -v docker >/dev/null 2>&1; then
    echo "-> Đang cài đặt Docker Engine..."
    sudo apt-get -o DPkg::Lock::Timeout=180 update -y
    sudo DEBIAN_FRONTEND=noninteractive apt-get -o DPkg::Lock::Timeout=180 install -y ca-certificates curl docker.io docker-compose-v2 || \
    (sudo DEBIAN_FRONTEND=noninteractive apt-get -o DPkg::Lock::Timeout=180 install -y ca-certificates curl && curl -fsSL https://get.docker.com | sudo sh)
fi

# 4. Đảm bảo Docker tự khởi động cùng máy ảo mỗi sáng khi VM bật nguồn
sudo systemctl enable --now docker

# 5. Mở cổng 8888 trên tường lửa UFW của Ubuntu (nếu UFW đang bật)
if command -v ufw >/dev/null 2>&1; then
    if sudo ufw status | grep -q "Status: active"; then
        sudo ufw allow 8888/tcp
        sudo ufw allow 8889/udp
    fi
fi

# 6. Build và chạy ngầm PocketLog Server (restart: unless-stopped)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

echo "-> Đang đóng gói và khởi chạy PocketLog Linux Server..."
if sudo docker compose version >/dev/null 2>&1; then
    sudo docker compose up -d --build
else
    sudo DEBIAN_FRONTEND=noninteractive apt-get -o DPkg::Lock::Timeout=180 install -y docker-compose-v2 || \
    sudo DEBIAN_FRONTEND=noninteractive apt-get -o DPkg::Lock::Timeout=180 install -y docker-compose
    sudo docker compose up -d --build || sudo docker-compose up -d --build
fi

PUB_IP=$(curl -s --max-time 4 https://ifconfig.me || hostname -I | awk '{print $1}')

echo ""
echo "=========================================================="
echo " ✓ HOÀN TẤT! POCKETLOG SERVER ĐANG CHẠY NGẦM 24/7"
echo " -> Địa chỉ IP nhập vào PocketLog.Client : $PUB_IP"
echo " -> Port nhập vào PocketLog.Client       : 8888"
echo " -> Xem log trực tiếp: sudo docker logs -f pocketlog-server"
echo "=========================================================="
