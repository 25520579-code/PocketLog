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

        private void ShowAuthScreen(string initialNotice = "")
        {
            StopWebcam();
            activeTabName = "AUTH";
            pnlBottomNav.Visible = false;
            pnlDrawerBackdrop.Visible = false;
            UpdateHeaderUserUI("");

            Panel page = CreateResponsiveScrollPage(out Panel col);
            int w = col.Width;
            int y = 16;
            bool isRegisterMode = false;

            Guna2Panel pnlHero = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 96),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrPrimaryGreen,
                UseTransparentBackground = true
            };
            pnlHero.Controls.Add(MakeSoftIconBadge(UiIcon.Camera, 18, 22, 46, 15, Color.FromArgb(88, 152, 250), Color.White));
            var lblHeroTitle = MakeLabel("PocketLog Locket & Bill", 76, 22, 13F, FontStyle.Bold, Color.White);
            lblHeroTitle.Font = FontBold(13F);
            pnlHero.Controls.Add(lblHeroTitle);
            pnlHero.Controls.Add(MakeLabel("Chia sẻ khoảnh khắc & chia tiền nhóm tức thì", 76, 52, 8.8F, FontStyle.Regular, clrSoftGreenBg, w - 92));
            col.Controls.Add(pnlHero);
            y += 110;

            Guna2Panel cardServer = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 108),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 20,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            cardServer.Controls.Add(MakeLabel("Máy chủ kết nối (TCP Server)", 16, 12, 9.2F, FontStyle.Bold, clrPrimaryGreen));
            cardServer.Controls.Add(MakeLabel("Nhập 127.0.0.1 nếu chạy cùng máy hoặc IP LAN của Server", 16, 34, 8.2F, FontStyle.Regular, clrTextMuted, w - 32));

            var gridServer = new BufferedTableLayout
            {
                Location = new Point(14, 58),
                Size = new Size(w - 28, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            gridServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
            gridServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            gridServer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtServerIp = MakeTextBox(currentServerIp, "IP Server (127.0.0.1)", 0, 0, 100, 36, clrCardBg, true);
            txtServerIp.Dock = DockStyle.Fill;
            txtServerIp.Margin = new Padding(2, 0, 6, 0);

            var txtServerPort = MakeTextBox(currentServerPort.ToString(), "Port", 0, 0, 80, 36, clrCardBg, true);
            txtServerPort.Dock = DockStyle.Fill;
            txtServerPort.Margin = new Padding(4, 0, 2, 0);
            txtServerPort.ForeColor = clrIndigo;

            gridServer.Controls.Add(txtServerIp, 0, 0);
            gridServer.Controls.Add(txtServerPort, 1, 0);
            cardServer.Controls.Add(gridServer);
            col.Controls.Add(cardServer);
            y += 122;

            Guna2Panel cardAuth = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 384),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            // Thanh chuyển tab dạng Segmented Pill chuẩn Android
            Guna2Panel segTrack = new Guna2Panel
            {
                Location = new Point(16, 16),
                Size = new Size(w - 32, 42),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 14,
                FillColor = clrAppBg,
                UseTransparentBackground = true
            };
            var gridSeg = new BufferedTableLayout
            {
                Location = new Point(3, 3),
                Size = new Size(segTrack.Width - 6, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            gridSeg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridSeg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridSeg.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var btnModeLogin = MakeBtn("Đăng nhập", 0, 0, 100, 34, 11, clrPrimaryGreen, Color.White, Color.Transparent, 0, 9F);
            btnModeLogin.Dock = DockStyle.Fill;
            btnModeLogin.Margin = new Padding(1);

            var btnModeReg = MakeBtn("Tạo tài khoản", 0, 0, 100, 34, 11, Color.Transparent, clrIndigo, Color.Transparent, 0, 9F);
            btnModeReg.Dock = DockStyle.Fill;
            btnModeReg.Margin = new Padding(1);

            gridSeg.Controls.Add(btnModeLogin, 0, 0);
            gridSeg.Controls.Add(btnModeReg, 1, 0);
            segTrack.Controls.Add(gridSeg);

            Label lblUserTitle = MakeLabel("Tên tài khoản hoặc họ tên hiển thị", 16, 70, 8.8F, FontStyle.Bold, clrTextMain);
            var txtUsername = MakeTextBox("", "Nhập tên tài khoản...", 16, 94, w - 32, 40, clrCardBg);
            txtUsername.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblPassTitle = MakeLabel("Mật khẩu", 16, 144, 8.8F, FontStyle.Bold, clrTextMain);
            var txtPassword = MakeTextBox("", "Nhập mật khẩu...", 16, 168, w - 32, 40, clrCardBg);
            txtPassword.PasswordChar = '●';
            txtPassword.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblInitBudget = MakeLabel("Hạn mức chi tiêu tháng (VNĐ)", 16, 216, 8.6F, FontStyle.Bold, clrIndigo);
            var txtInitBudget = MakeTextBox("3500000", "Ví dụ: 3500000", 16, 238, w - 32, 38, clrCardBg, true);
            txtInitBudget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblInitBudget.Visible = false;
            txtInitBudget.Visible = false;

            string defaultHint = string.IsNullOrEmpty(initialNotice)
                ? "Nhập tài khoản đã có để đăng nhập vào hệ thống."
                : initialNotice;

            Label lblAuthStatus = new Label
            {
                Text = defaultHint,
                Location = new Point(16, 222),
                Size = new Size(w - 32, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = FontRegular(8.4F),
                ForeColor = string.IsNullOrEmpty(initialNotice) ? clrTextMuted : clrRedDanger,
                BackColor = Color.Transparent
            };

            var btnSubmitAuth = MakeBtn("Kết nối & Đăng nhập", 16, 320, w - 32, 46, 23, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 9.8F);
            btnSubmitAuth.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnModeLogin.Click += (s, e) =>
            {
                isRegisterMode = false;
                btnModeLogin.FillColor = clrPrimaryGreen;
                btnModeLogin.ForeColor = Color.White;

                btnModeReg.FillColor = Color.Transparent;
                btnModeReg.ForeColor = clrIndigo;

                lblInitBudget.Visible = false;
                txtInitBudget.Visible = false;
                lblAuthStatus.Location = new Point(16, 222);
                lblAuthStatus.Text = "Nhập tài khoản đã có để đăng nhập vào hệ thống.";
                lblAuthStatus.ForeColor = clrTextMuted;
                txtUsername.PlaceholderText = "Nhập tên tài khoản...";
                btnSubmitAuth.Text = "Kết nối & Đăng nhập";
            };

            btnModeReg.Click += (s, e) =>
            {
                isRegisterMode = true;
                btnModeReg.FillColor = clrPrimaryGreen;
                btnModeReg.ForeColor = Color.White;

                btnModeLogin.FillColor = Color.Transparent;
                btnModeLogin.ForeColor = clrIndigo;

                lblInitBudget.Visible = true;
                txtInitBudget.Visible = true;
                lblAuthStatus.Location = new Point(16, 280);
                lblAuthStatus.Text = "Điền tên hiển thị, mật khẩu và hạn mức chi tiêu tháng.";
                lblAuthStatus.ForeColor = clrTextMuted;
                txtUsername.PlaceholderText = "Nhập tên hiển thị / tài khoản mới...";
                btnSubmitAuth.Text = "Tạo tài khoản & Kết nối";
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
                    lblAuthStatus.Text = "Vui lòng nhập tên tài khoản!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                    return;
                }

                if (string.IsNullOrEmpty(pass))
                {
                    lblAuthStatus.Text = "Vui lòng nhập mật khẩu!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                    return;
                }

                btnSubmitAuth.Enabled = false;
                lblAuthStatus.Text = $"Đang kết nối tới {ip}:{port}...";
                lblAuthStatus.ForeColor = clrPrimaryGreen;

                onAuthFailedUI = (errMsg) =>
                {
                    lblAuthStatus.Text = errMsg;
                    lblAuthStatus.ForeColor = clrRedDanger;
                    btnSubmitAuth.Enabled = true;
                };

                bool ok = await ConnectAndAuthenticateAsync(ip, port, user, pass, isRegisterMode, txtInitBudget.Text, lblAuthStatus);
                if (!ok)
                {
                    btnSubmitAuth.Enabled = true;
                }
            };

            cardAuth.Controls.AddRange(new Control[]
            {
                segTrack, lblUserTitle, txtUsername,
                lblPassTitle, txtPassword,
                lblInitBudget, txtInitBudget,
                lblAuthStatus, btnSubmitAuth
            });

            col.Controls.Add(cardAuth);
            col.Height = y + 404;

            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(page);
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
                    CloseCurrentSocket();
                    lblStatus.Text = $"Không tìm thấy Server tại {ip}:{port}.";
                    lblStatus.ForeColor = clrRedDanger;
                    return false;
                }

                await connectTask;
                netStream = tcpClient.GetStream();
                isConnected = true;
                currentServerIp = ip;
                currentServerPort = port;

                int.TryParse(initBudgetStr.Replace(".", "").Replace(",", ""), out int customBudget);
                if (customBudget <= 0) customBudget = 3500000;

                _ = Task.Run(() => ListenFromServerLoopAsync());

                var loginReq = new LoginRequestDto
                {
                    Username = username,
                    FullName = username,
                    Password = password,
                    IsRegister = isRegister,
                    InitialBudget = customBudget
                };
                await SendPacketSafeAsync(TcpPacket.Create(PacketType.LOGIN_REQ, loginReq));

                return true;
            }
            catch (Exception ex)
            {
                CloseCurrentSocket();
                lblStatus.Text = "Lỗi kết nối: " + ex.Message;
                lblStatus.ForeColor = clrRedDanger;
                return false;
            }
        }

        private void DisconnectAndLogout()
        {
            StopWebcam();
            CloseCurrentSocket();
            DisposeCachedImages();
            InitializeDefaultBudgets();
            currentUserId = 0;
            currentUsername = "";
            currentFullName = "";
            ShowAuthScreen();
        }
    }
}