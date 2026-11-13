using System.Net.Sockets;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        // ====================================================================
        // MÀN HÌNH ĐĂNG NHẬP (LOGIN) / ĐĂNG KÝ & KẾT NỐI MÁY CHỦ
        // ====================================================================
        private void ShowLoginScreen() => ShowAuthScreen();

        private void ShowAuthScreen()
        {
            StopWebcam();
            activeTabName = "AUTH";
            pnlBottomNav.Visible = false;
            btnLogoutTop.Visible = false;
            lblHeaderUser.Text = "";

            Panel root = new Panel { BackColor = clrAppBg, Dock = DockStyle.Fill };
            bool isRegisterMode = false;

            Guna2Panel pnlHero = new Guna2Panel
            {
                Location = new Point(22, 16),
                Size = new Size(376, 92),
                BorderRadius = 20,
                FillColor = clrPrimaryGreen
            };
            pnlHero.Controls.Add(MakeLabel("📸 PocketLog Locket & Bill", 18, 16, 13.5F, FontStyle.Bold, Color.White));
            pnlHero.Controls.Add(MakeLabel("Chia sẻ khoảnh khắc & quản lý chi tiêu qua TCP Socket", 18, 48, 8.2F, FontStyle.Regular, clrSoftGreenBg));
            root.Controls.Add(pnlHero);

            Guna2Panel cardServer = new Guna2Panel
            {
                Location = new Point(22, 120),
                Size = new Size(376, 102),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1
            };
            cardServer.Controls.Add(MakeLabel("🌐 Kết nối Máy chủ (TCP Server Host)", 14, 10, 9F, FontStyle.Bold, clrPrimaryGreen));
            cardServer.Controls.Add(MakeLabel("Nhập 127.0.0.1 nếu cùng máy, hoặc IP LAN của máy chủ:", 14, 31, 7.8F, FontStyle.Regular, clrTextMuted));

            Guna2TextBox txtServerIp = new Guna2TextBox
            {
                Text = currentServerIp,
                PlaceholderText = "IP Server (VD: 192.168.1.15)",
                Location = new Point(14, 54),
                Size = new Size(242, 36),
                BorderRadius = 10,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = clrTextMain
            };

            Guna2TextBox txtServerPort = new Guna2TextBox
            {
                Text = currentServerPort.ToString(),
                PlaceholderText = "Port",
                Location = new Point(266, 54),
                Size = new Size(96, 36),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = clrIndigo
            };
            cardServer.Controls.Add(txtServerIp);
            cardServer.Controls.Add(txtServerPort);
            root.Controls.Add(cardServer);

            Guna2Panel cardAuth = new Guna2Panel
            {
                Location = new Point(22, 234),
                Size = new Size(376, 366),
                BorderRadius = 18,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            var btnModeLogin = MakeBtn("Đăng nhập", 14, 14, 170, 38, 12, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 9F);
            var btnModeReg = MakeBtn("Đăng ký tài khoản", 192, 14, 170, 38, 12, clrAppBg, clrTextMain, clrBorderLight, 1, 9F);

            Label lblUserTitle = MakeLabel("Tên đăng nhập / MSSV:", 14, 64, 8.8F, FontStyle.Bold, clrTextMain);
            Guna2TextBox txtUsername = new Guna2TextBox
            {
                Text = "Huy Hoàng",
                PlaceholderText = "Nhập tên hoặc MSSV (VD: 25520579)...",
                Location = new Point(14, 86),
                Size = new Size(348, 38),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F)
            };

            Label lblPassTitle = MakeLabel("Mật khẩu:", 14, 132, 8.8F, FontStyle.Bold, clrTextMain);
            Guna2TextBox txtPassword = new Guna2TextBox
            {
                Text = "123",
                PlaceholderText = "Nhập mật khẩu...",
                PasswordChar = '●',
                Location = new Point(14, 154),
                Size = new Size(348, 38),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F)
            };

            Label lblInitBudget = MakeLabel("Hạn mức chi tiêu tháng (VNĐ):", 14, 200, 8.5F, FontStyle.Bold, clrIndigo);
            Guna2TextBox txtInitBudget = new Guna2TextBox
            {
                Text = "3500000",
                Location = new Point(14, 222),
                Size = new Size(348, 36),
                BorderRadius = 10,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Visible = false
            };
            lblInitBudget.Visible = false;

            Label lblQuickPick = MakeLabel("⚡ Chọn nhanh tài khoản test nhiều cửa sổ:", 14, 202, 8.2F, FontStyle.Bold, clrIndigo);
            var btnPickHoang = MakeBtn("Huy Hoàng", 14, 224, 110, 32, 10, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8F);
            var btnPickHuy = MakeBtn("Gia Huy", 132, 224, 110, 32, 10, clrAppBg, clrTextMain, clrBorderLight, 1, 8F);
            var btnPickHung = MakeBtn("Thế Hưng", 250, 224, 112, 32, 10, clrAppBg, clrTextMain, clrBorderLight, 1, 8F);

            Action<Guna2Button, string> selectQuickAccount = (btnPicked, accName) =>
            {
                txtUsername.Text = accName;
                txtPassword.Text = "123";
                foreach (var b in new[] { btnPickHoang, btnPickHuy, btnPickHung })
                {
                    b.FillColor = clrAppBg;
                    b.ForeColor = clrTextMain;
                    b.BorderColor = clrBorderLight;
                }
                btnPicked.FillColor = clrSoftIndigo;
                btnPicked.ForeColor = clrIndigo;
                btnPicked.BorderColor = clrIndigo;
            };

            btnPickHoang.Click += (s, e) => selectQuickAccount(btnPickHoang, "Huy Hoàng");
            btnPickHuy.Click += (s, e) => selectQuickAccount(btnPickHuy, "Gia Huy");
            btnPickHung.Click += (s, e) => selectQuickAccount(btnPickHung, "Thế Hưng");

            Label lblAuthStatus = MakeLabel("💡 Hãy bật PocketLog.Server trước khi bấm Kết nối.", 14, 268, 8F, FontStyle.Italic, clrTextMuted);

            var btnSubmitAuth = MakeBtn("🚀 Kết nối TCP & Đăng nhập", 14, 298, 348, 50, 14, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 2, 10F);

            btnModeLogin.Click += (s, e) =>
            {
                isRegisterMode = false;
                btnModeLogin.FillColor = clrPrimaryGreen;
                btnModeLogin.ForeColor = Color.White;
                btnModeReg.FillColor = clrAppBg;
                btnModeReg.ForeColor = clrTextMain;

                lblInitBudget.Visible = false;
                txtInitBudget.Visible = false;
                lblQuickPick.Visible = true;
                btnPickHoang.Visible = true;
                btnPickHuy.Visible = true;
                btnPickHung.Visible = true;
                btnSubmitAuth.Text = "🚀 Kết nối TCP & Đăng nhập";
            };

            btnModeReg.Click += (s, e) =>
            {
                isRegisterMode = true;
                btnModeReg.FillColor = clrIndigo;
                btnModeReg.ForeColor = Color.White;
                btnModeLogin.FillColor = clrAppBg;
                btnModeLogin.ForeColor = clrTextMain;

                lblQuickPick.Visible = false;
                btnPickHoang.Visible = false;
                btnPickHuy.Visible = false;
                btnPickHung.Visible = false;
                lblInitBudget.Visible = true;
                txtInitBudget.Visible = true;
                txtUsername.Text = "";
                txtUsername.PlaceholderText = "Nhập tên tài khoản mới muốn tạo...";
                btnSubmitAuth.Text = "✨ Tạo tài khoản & Kết nối Server";
            };

            btnSubmitAuth.Click += async (s, e) =>
            {
                string ip = txtServerIp.Text.Trim();
                int.TryParse(txtServerPort.Text.Trim(), out int port);
                if (port <= 0) port = 8888;
                string user = txtUsername.Text.Trim();
                string pass = txtPassword.Text.Trim();

                if (string.IsNullOrEmpty(user))
                {
                    lblAuthStatus.Text = "⚠ Vui lòng nhập tên tài khoản!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                    return;
                }

                btnSubmitAuth.Enabled = false;
                lblAuthStatus.Text = $"⏳ Đang kết nối tới {ip}:{port}...";
                lblAuthStatus.ForeColor = clrIndigo;

                bool ok = await ConnectAndAuthenticateAsync(ip, port, user, pass, isRegisterMode, txtInitBudget.Text, lblAuthStatus);
                if (!ok)
                {
                    btnSubmitAuth.Enabled = true;
                }
            };

            cardAuth.Controls.AddRange(new Control[]
            {
                btnModeLogin, btnModeReg,
                lblUserTitle, txtUsername,
                lblPassTitle, txtPassword,
                lblInitBudget, txtInitBudget,
                lblQuickPick, btnPickHoang, btnPickHuy, btnPickHung,
                lblAuthStatus, btnSubmitAuth
            });

            root.Controls.Add(cardAuth);
            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(root);
        }

        // ====================================================================
        // KẾT NỐI TCP CLIENT & NHẬN DỮ LIỆU ĐỒNG BỘ
        // ====================================================================
        private async Task<bool> ConnectAndAuthenticateAsync(string ip, int port, string username, string password, bool isRegister, string initBudgetStr, Label lblStatus)
        {
            try
            {
                CloseCurrentSocket();

                tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(3500);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    lblStatus.Text = $"❌ Không tìm thấy Server tại {ip}:{port}. Kiểm tra IP/Firewall!";
                    lblStatus.ForeColor = clrRedDanger;
                    return false;
                }

                await connectTask;
                netStream = tcpClient.GetStream();
                isConnected = true;
                currentServerIp = ip;
                currentServerPort = port;

                _ = Task.Run(() => ListenFromServerLoopAsync());

                var loginReq = new LoginRequestDto { Username = username, Password = password };
                await SendPacketSafeAsync(TcpPacket.Create(PacketType.LOGIN_REQ, loginReq));

                if (isRegister && int.TryParse(initBudgetStr.Replace(".", "").Replace(",", ""), out int customBudget) && customBudget > 0)
                {
                    totalBudgetLimit = customBudget;
                }

                return true;
            }
            catch (Exception ex)
            {
                lblStatus.Text = "❌ Lỗi kết nối: " + ex.Message;
                lblStatus.ForeColor = clrRedDanger;
                return false;
            }
        }

        private void DisconnectAndLogout()
        {
            StopWebcam();
            CloseCurrentSocket();
            currentUserId = 0;
            currentFullName = "";
            ShowAuthScreen();
        }
    }
}
