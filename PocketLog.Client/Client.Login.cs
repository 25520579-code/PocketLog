using System;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
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
                Location = new Point(22, 18),
                Size = new Size(376, 96),
                BorderRadius = 22,
                FillColor = clrPrimaryGreen,
                UseTransparentBackground = true
            };
            pnlHero.Controls.Add(MakeLabel("📸 PocketLog Locket & Bill", 20, 18, 13.5F, FontStyle.Bold, Color.White));
            pnlHero.Controls.Add(MakeLabel("Chia sẻ khoảnh khắc & quản lý chi tiêu qua TCP Socket", 20, 52, 8.5F, FontStyle.Regular, clrSoftGreenBg));
            root.Controls.Add(pnlHero);

            Guna2Panel cardServer = new Guna2Panel
            {
                Location = new Point(22, 128),
                Size = new Size(376, 106),
                BorderRadius = 18,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            cardServer.Controls.Add(MakeLabel("🌐 Máy chủ kết nối (TCP Server Host)", 16, 12, 9.2F, FontStyle.Bold, clrPrimaryGreen));
            cardServer.Controls.Add(MakeLabel("Nhập 127.0.0.1 nếu cùng máy, hoặc IP LAN của Server:", 16, 34, 8F, FontStyle.Regular, clrTextMuted));

            Guna2TextBox txtServerIp = new Guna2TextBox
            {
                Text = currentServerIp,
                PlaceholderText = "IP Server (VD: 127.0.0.1)",
                Location = new Point(16, 58),
                Size = new Size(240, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = clrTextMain
            };

            Guna2TextBox txtServerPort = new Guna2TextBox
            {
                Text = currentServerPort.ToString(),
                PlaceholderText = "Port",
                Location = new Point(264, 58),
                Size = new Size(96, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = clrIndigo
            };
            cardServer.Controls.Add(txtServerIp);
            cardServer.Controls.Add(txtServerPort);
            root.Controls.Add(cardServer);

            Guna2Panel cardAuth = new Guna2Panel
            {
                Location = new Point(22, 248),
                Size = new Size(376, 348),
                BorderRadius = 20,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            var btnModeLogin = MakeBtn("Đăng nhập", 16, 16, 168, 38, 12, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 9F);
            var btnModeReg = MakeBtn("Tạo tài khoản mới", 192, 16, 168, 38, 12, clrAppBg, clrTextMain, clrBorderLight, 1, 9F);

            Label lblUserTitle = MakeLabel("Tên tài khoản / Username:", 16, 68, 8.8F, FontStyle.Bold, clrTextMain);
            Guna2TextBox txtUsername = new Guna2TextBox
            {
                Text = "",
                PlaceholderText = "Nhập tên tài khoản của bạn...",
                Location = new Point(16, 92),
                Size = new Size(344, 38),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F)
            };

            Label lblPassTitle = MakeLabel("Mật khẩu:", 16, 140, 8.8F, FontStyle.Bold, clrTextMain);
            Guna2TextBox txtPassword = new Guna2TextBox
            {
                Text = "",
                PlaceholderText = "Nhập mật khẩu...",
                PasswordChar = '●',
                Location = new Point(16, 164),
                Size = new Size(344, 38),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 9.5F)
            };

            Label lblInitBudget = MakeLabel("Hạn mức chi tiêu tháng (VNĐ):", 16, 210, 8.5F, FontStyle.Bold, clrIndigo);
            Guna2TextBox txtInitBudget = new Guna2TextBox
            {
                Text = "3500000",
                Location = new Point(16, 232),
                Size = new Size(344, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Visible = false
            };
            lblInitBudget.Visible = false;

            Label lblAuthStatus = MakeLabel("💡 Nhập tài khoản để đăng nhập hoặc chuyển sang Tạo tài khoản.", 16, 218, 8F, FontStyle.Italic, clrTextMuted, 344);

            var btnSubmitAuth = MakeBtn("Kết nối TCP & Đăng nhập", 16, 280, 344, 48, 14, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 10F);

            btnModeLogin.Click += (s, e) =>
            {
                isRegisterMode = false;
                btnModeLogin.FillColor = clrPrimaryGreen;
                btnModeLogin.ForeColor = Color.White;
                btnModeLogin.BorderColor = clrPrimaryGreen;

                btnModeReg.FillColor = clrAppBg;
                btnModeReg.ForeColor = clrTextMain;
                btnModeReg.BorderColor = clrBorderLight;

                lblInitBudget.Visible = false;
                txtInitBudget.Visible = false;
                lblAuthStatus.Location = new Point(16, 218);
                lblAuthStatus.Text = "💡 Nhập tài khoản đã có để đăng nhập vào hệ thống.";
                lblAuthStatus.ForeColor = clrTextMuted;
                btnSubmitAuth.Text = "Kết nối TCP & Đăng nhập";
            };

            btnModeReg.Click += (s, e) =>
            {
                isRegisterMode = true;
                btnModeReg.FillColor = clrPrimaryGreen;
                btnModeReg.ForeColor = Color.White;
                btnModeReg.BorderColor = clrPrimaryGreen;

                btnModeLogin.FillColor = clrAppBg;
                btnModeLogin.ForeColor = clrTextMain;
                btnModeLogin.BorderColor = clrBorderLight;

                lblInitBudget.Visible = true;
                txtInitBudget.Visible = true;
                lblAuthStatus.Location = new Point(16, 272);
                lblAuthStatus.Text = "";
                txtUsername.PlaceholderText = "Nhập tên tài khoản mới muốn tạo...";
                btnSubmitAuth.Text = "Tạo tài khoản & Kết nối Server";
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
                lblAuthStatus.ForeColor = clrPrimaryGreen;

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
                lblAuthStatus, btnSubmitAuth
            });

            root.Controls.Add(cardAuth);
            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(root);
        }

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
                    lblStatus.Text = $"❌ Không tìm thấy Server tại {ip}:{port}.";
                    lblStatus.ForeColor = clrRedDanger;
                    return false;
                }

                await connectTask;
                netStream = tcpClient.GetStream();
                isConnected = true;
                currentServerIp = ip;
                currentServerPort = port;

                _ = Task.Run(() => ListenFromServerLoopAsync());

                var loginReq = new LoginRequestDto
                {
                    Username = username,
                    Password = string.IsNullOrEmpty(password) ? "123" : password
                };
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