using System;
using System.Drawing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
                Size = new Size(w, 106),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = clrPrimaryGreen,
                UseTransparentBackground = true
            };
            pnlHero.Controls.Add(MakeSoftIconBadge(UiIcon.Camera, 20, 24, 52, 16, Color.FromArgb(88, 152, 250), Color.White));
            var lblHeroTitle = MakeLabel("PocketLog - Nhật ký & Chi tiêu", 84, 24, 14F, FontStyle.Bold, Color.White);
            lblHeroTitle.Font = FontBold(14F);
            pnlHero.Controls.Add(lblHeroTitle);
            pnlHero.Controls.Add(MakeLabel("Chia sẻ khoảnh khắc & Quản lý chi tiêu", 84, 58, 9.2F, FontStyle.Regular, clrSoftGreenBg, w - 100));
            col.Controls.Add(pnlHero);
            y += 122;

            Guna2Panel cardServer = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 136),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };
            cardServer.Controls.Add(MakeLabel("Máy chủ kết nối (TCP Server)", 18, 14, 10F, FontStyle.Bold, clrPrimaryGreen));
            cardServer.Controls.Add(MakeLabel("Nhập 127.0.0.1 hoặc bấm Dò Server LAN (UDP 8889)", 18, 42, 8.8F, FontStyle.Regular, clrTextMuted, w - 36));

            var btnDiscoverServer = MakeBtn("Dò Server LAN", w - 146, 12, 130, 30, 15, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.5F);
            btnDiscoverServer.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cardServer.Controls.Add(btnDiscoverServer);

            var gridServer = new BufferedTableLayout
            {
                Location = new Point(16, 74),
                Size = new Size(w - 32, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            gridServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
            gridServer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            gridServer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var txtServerIp = MakeTextBox("", "Thêm địa chỉ IP", 0, 0, 100, 42, clrCardBg, true);
            txtServerIp.Dock = DockStyle.Fill;
            txtServerIp.Margin = new Padding(2, 0, 6, 0);

            var txtServerPort = MakeTextBox("", "Thêm Port", 0, 0, 80, 42, clrCardBg, true);
            txtServerPort.Dock = DockStyle.Fill;
            txtServerPort.Margin = new Padding(4, 0, 2, 0);
            txtServerPort.ForeColor = clrIndigo;

            gridServer.Controls.Add(txtServerIp, 0, 0);
            gridServer.Controls.Add(txtServerPort, 1, 0);
            cardServer.Controls.Add(gridServer);
            col.Controls.Add(cardServer);
            y += 152;

            Guna2Panel cardAuth = new Guna2Panel
            {
                Location = new Point(0, y),
                Size = new Size(w, 440),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 24,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                UseTransparentBackground = true
            };

            Guna2Panel segTrack = new Guna2Panel
            {
                Location = new Point(18, 18),
                Size = new Size(w - 36, 46),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BorderRadius = 15,
                FillColor = clrAppBg,
                UseTransparentBackground = true
            };
            var gridSeg = new BufferedTableLayout
            {
                Location = new Point(3, 3),
                Size = new Size(segTrack.Width - 6, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1
            };
            gridSeg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridSeg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            gridSeg.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var btnModeLogin = MakeBtn("Đăng nhập", 0, 0, 100, 38, 12, clrPrimaryGreen, Color.White, Color.Transparent, 0, 9.8F);
            btnModeLogin.Dock = DockStyle.Fill;
            btnModeLogin.Margin = new Padding(1);

            var btnModeReg = MakeBtn("Tạo tài khoản", 0, 0, 100, 38, 12, Color.Transparent, clrIndigo, Color.Transparent, 0, 9.8F);
            btnModeReg.Dock = DockStyle.Fill;
            btnModeReg.Margin = new Padding(1);

            gridSeg.Controls.Add(btnModeLogin, 0, 0);
            gridSeg.Controls.Add(btnModeReg, 1, 0);
            segTrack.Controls.Add(gridSeg);

            Label lblUserTitle = MakeLabel("Tên tài khoản hoặc họ tên hiển thị", 18, 78, 9.8F, FontStyle.Bold, clrTextMain);
            var txtUsername = MakeTextBox("", "Nhập tên tài khoản...", 18, 106, w - 36, 44, clrCardBg);
            txtUsername.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblPassTitle = MakeLabel("Mật khẩu", 18, 162, 9.8F, FontStyle.Bold, clrTextMain);
            var txtPassword = MakeTextBox("", "Nhập mật khẩu...", 18, 190, w - 36, 44, clrCardBg);
            txtPassword.PasswordChar = '●';
            txtPassword.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Label lblInitBudget = MakeLabel("Hạn mức chi tiêu tháng (VNĐ)", 18, 246, 9.5F, FontStyle.Bold, clrIndigo);
            var txtInitBudget = MakeTextBox("3500000", "Ví dụ: 3500000", 18, 272, w - 36, 42, clrCardBg, true);
            txtInitBudget.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lblInitBudget.Visible = false;
            txtInitBudget.Visible = false;

            string defaultHint = string.IsNullOrEmpty(initialNotice)
                ? "Nhập tài khoản đã có để đăng nhập vào hệ thống."
                : initialNotice;

            Label lblAuthStatus = new Label
            {
                Text = defaultHint,
                Location = new Point(18, 248),
                Size = new Size(w - 36, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = FontRegular(9F),
                ForeColor = string.IsNullOrEmpty(initialNotice) ? clrTextMuted : clrRedDanger,
                BackColor = Color.Transparent
            };

            var btnSubmitAuth = MakeBtn("Đăng nhập", 18, 362, w - 36, 52, 26, clrPrimaryGreen, Color.White, clrPrimaryGreen, 0, 10.5F);
            btnSubmitAuth.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnDiscoverServer.Click += async (s, e) =>
            {
                btnDiscoverServer.Enabled = false;
                btnDiscoverServer.Text = "Đang dò...";
                lblAuthStatus.Text = "Đang dò tìm Server qua UDP Broadcast (Port 8889)...";
                lblAuthStatus.ForeColor = clrPrimaryGreen;

                var found = await DiscoverServerViaUdpAsync();
                if (found.Success)
                {
                    txtServerIp.Text = found.Ip;
                    txtServerPort.Text = found.Port.ToString();
                    lblAuthStatus.Text = $"✓ Tìm thấy Server ({found.HostName}) tại {found.Ip}:{found.Port}!";
                    lblAuthStatus.ForeColor = clrPrimaryGreen;
                }
                else
                {
                    lblAuthStatus.Text = "Không thấy Server phản hồi UDP 8889. Kiểm tra Server/Firewall!";
                    lblAuthStatus.ForeColor = clrRedDanger;
                }

                btnDiscoverServer.Enabled = true;
                btnDiscoverServer.Text = "Dò Server LAN";
            };

            btnModeLogin.Click += (s, e) =>
            {
                isRegisterMode = false;
                btnModeLogin.FillColor = clrPrimaryGreen;
                btnModeLogin.ForeColor = Color.White;

                btnModeReg.FillColor = Color.Transparent;
                btnModeReg.ForeColor = clrIndigo;

                lblInitBudget.Visible = false;
                txtInitBudget.Visible = false;
                lblAuthStatus.Location = new Point(18, 248);
                lblAuthStatus.Text = "Nhập tài khoản đã có để đăng nhập vào hệ thống.";
                lblAuthStatus.ForeColor = clrTextMuted;
                txtUsername.PlaceholderText = "Nhập tên tài khoản...";
                btnSubmitAuth.Text = "Đăng nhập";
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
                lblAuthStatus.Location = new Point(18, 318);
                lblAuthStatus.Text = "Điền tên hiển thị, mật khẩu và hạn mức chi tiêu tháng.";
                lblAuthStatus.ForeColor = clrTextMuted;
                txtUsername.PlaceholderText = "Nhập tên hiển thị / tài khoản mới...";
                btnSubmitAuth.Text = "Tạo tài khoản";
            };

            btnSubmitAuth.Click += async (s, e) =>
            {
                string ip = string.IsNullOrWhiteSpace(txtServerIp.Text) ? "127.0.0.1" : txtServerIp.Text.Trim();
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
            col.Height = y + 460;

            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(page);
        }

        private async Task<(bool Success, string Ip, int Port, string HostName)> DiscoverServerViaUdpAsync()
        {
            try
            {
                using var udp = new UdpClient();
                udp.EnableBroadcast = true;
                byte[] reqBytes = Encoding.UTF8.GetBytes("POCKETLOG_DISCOVER_REQ");

                try
                {
                    await udp.SendAsync(reqBytes, reqBytes.Length, new IPEndPoint(IPAddress.Broadcast, 8889));
                }
                catch { }

                try
                {
                    await udp.SendAsync(reqBytes, reqBytes.Length, new IPEndPoint(IPAddress.Loopback, 8889));
                }
                catch { }

                using var cts = new CancellationTokenSource(1500);
                while (!cts.IsCancellationRequested)
                {
                    var recv = await udp.ReceiveAsync(cts.Token);
                    string reply = Encoding.UTF8.GetString(recv.Buffer).Trim();
                    if (reply.StartsWith("POCKETLOG_DISCOVER_RES|"))
                    {
                        string[] parts = reply.Split('|');
                        int port = (parts.Length > 1 && int.TryParse(parts[1], out int p)) ? p : 8888;
                        string host = parts.Length > 2 ? parts[2] : "PocketLog Server";
                        string serverIp = recv.RemoteEndPoint.Address.ToString();
                        return (true, serverIp, port, host);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch { }

            return (false, "", 8888, "");
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