using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Form1 : Form
    {
        private Guna2BorderlessForm borderlessForm;
        private Guna2DragControl dragControl;
        private Guna2Panel pnlHeader, pnlMainContainer, pnlBottomNav;
        private Guna2Button btnTabFeed, btnTabCamera, btnTabExpense, btnLogoutTop;
        private Label lblHeaderUser;

        // Ngăn kéo Bạn bè trượt ngang
        private Panel pnlDrawerBackdrop;
        private Guna2Panel pnlFriendsDrawer;
        private System.Windows.Forms.Timer drawerTimer;
        private int drawerTargetX;

        // Kết nối mạng TCP Client & Thông tin phiên đăng nhập
        private TcpClient? tcpClient;
        private NetworkStream? netStream;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private bool isConnected = false;

        private string currentServerIp = "127.0.0.1";
        private int currentServerPort = 8888;
        private int currentUserId = 0;
        private string currentUsername = "";
        private string currentFullName = "";

        // Bảng màu Clean Light UI
        private readonly Color clrAppBg = Color.FromArgb(245, 247, 246);
        private readonly Color clrCardBg = Color.White;
        private readonly Color clrPrimaryGreen = Color.FromArgb(29, 99, 76);
        private readonly Color clrSoftGreenBg = Color.FromArgb(232, 245, 239);
        private readonly Color clrSoftGreenBorder = Color.FromArgb(132, 190, 168);
        private readonly Color clrIndigo = Color.FromArgb(57, 66, 167);
        private readonly Color clrSoftIndigo = Color.FromArgb(234, 236, 252);
        private readonly Color clrRedDanger = Color.FromArgb(166, 58, 58);
        private readonly Color clrBadgeOrangeBg = Color.FromArgb(253, 238, 228);
        private readonly Color clrBadgeOrangeTxt = Color.FromArgb(168, 85, 34);
        private readonly Color clrBorderLight = Color.FromArgb(218, 224, 221);
        private readonly Color clrTextMain = Color.FromArgb(28, 32, 30);
        private readonly Color clrTextMuted = Color.FromArgb(110, 118, 114);

        private readonly (string Name, string Emoji)[] standardCategories = new[]
        {
            ("🍜 Đồ ăn/Thức uống", "🍜"),
            ("⛽ Xăng cộ", "⛽"),
            ("👕 Quần áo", "👕"),
            ("🏠 Tiền trọ", "🏠"),
            ("🛒 Mua sắm", "🛒"),
            ("📚 Học tập/Khác", "📚")
        };

        private class FriendUser
        {
            public int UserId { get; set; }
            public string Name { get; set; } = "";
            public string Handle { get; set; } = "";
            public bool IsOnline { get; set; }
        }

        private class LocketPost
        {
            public int PostId { get; set; }
            public bool IsMine { get; set; }
            public string Author { get; set; } = "";
            public bool HasTag { get; set; }
            public List<string> TaggedFriends { get; set; } = new List<string>();
            public string Category { get; set; } = "";
            public string NoteTop { get; set; } = "";
            public string CaptionInside { get; set; } = "";
            public int Amount { get; set; }
            public string Time { get; set; } = "";
            public string Emoji { get; set; } = "📷";
            public Color PhotoBg { get; set; }
            public Image? CustomImage { get; set; }
        }

        private class TaggedDebtItem
        {
            public int SplitId { get; set; }
            public int CreditorId { get; set; }
            public string CreditorName { get; set; } = "";
            public int DebtorId { get; set; }
            public string DebtorName { get; set; } = "";
            public string Title { get; set; } = "";
            public string Category { get; set; } = "";
            public int TotalAmount { get; set; }
            public int MyShare { get; set; }
            public string SplitDetail { get; set; } = "";
            public string Time { get; set; } = "";
            public bool IsSentProof { get; set; }
            public bool IsConfirmedPaid { get; set; }
            public Image? ProofImage { get; set; }
            public string StatusNote { get; set; } = "";
        }

        private readonly List<FriendUser> myFriends = new List<FriendUser>();
        private readonly List<FriendUser> friendSuggestions = new List<FriendUser>();
        private readonly List<LocketPost> posts = new List<LocketPost>();
        private readonly List<TaggedDebtItem> taggedDebts = new List<TaggedDebtItem>();
        private readonly Dictionary<string, int> categoryBudgets = new Dictionary<string, int>();

        private int totalBudgetLimit = 3500000;
        private bool isUnpaidDropdownOpen = false;
        private int selectedSplitId = 0;
        private string activeTabName = "AUTH";

        public Form1()
        {
            InitializeComponent();
            InitDefaultCategoryBudgets();
            SetupPhoneShell();
            ShowAuthScreen();
        }

        private void InitDefaultCategoryBudgets()
        {
            categoryBudgets["🍜 Đồ ăn/Thức uống"] = 1200000;
            categoryBudgets["⛽ Xăng cộ"] = 300000;
            categoryBudgets["👕 Quần áo"] = 500000;
            categoryBudgets["🏠 Tiền trọ"] = 1500000;
            categoryBudgets["🛒 Mua sắm"] = 400000;
            categoryBudgets["📚 Học tập/Khác"] = 300000;
        }

        private Label MakeLabel(string text, int x, int y, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                AutoSize = true,
                UseMnemonic = false,
                BackColor = Color.Transparent
            };
        }

        private Guna2Button MakeBtn(string text, int x, int y, int w, int h, int radius, Color fill, Color fore, Color border, int borderThick = 1, float fontSize = 8.5F)
        {
            return new Guna2Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BorderRadius = radius,
                FillColor = fill,
                ForeColor = fore,
                BorderColor = border,
                BorderThickness = borderThick,
                Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
        }

        private void SetupPhoneShell()
        {
            this.Controls.Clear();
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(420, 820);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = clrAppBg;

            borderlessForm = new Guna2BorderlessForm
            {
                ContainerControl = this,
                BorderRadius = 28,
                ResizeForm = false,
                HasFormShadow = true
            };

            pnlHeader = new Guna2Panel { Dock = DockStyle.Top, Height = 52, FillColor = clrCardBg };
            pnlHeader.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = clrBorderLight });
            pnlHeader.Controls.Add(MakeLabel("● PocketLog", 16, 13, 12.5F, FontStyle.Bold, clrPrimaryGreen));

            lblHeaderUser = MakeLabel("", 138, 17, 8.5F, FontStyle.Bold, clrIndigo);
            pnlHeader.Controls.Add(lblHeaderUser);

            btnLogoutTop = MakeBtn("Đăng xuất", 248, 12, 76, 28, 8, clrBadgeOrangeBg, clrBadgeOrangeTxt, Color.Transparent, 0, 7.8F);
            btnLogoutTop.Visible = false;
            btnLogoutTop.Click += (s, e) => DisconnectAndLogout();
            pnlHeader.Controls.Add(btnLogoutTop);

            Guna2ControlBox btnClose = new Guna2ControlBox
            {
                Location = new Point(372, 11),
                Size = new Size(34, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8,
                HoverState = { FillColor = Color.FromArgb(232, 17, 35), IconColor = Color.White }
            };
            Guna2ControlBox btnMin = new Guna2ControlBox
            {
                ControlBoxType = Guna.UI2.WinForms.Enums.ControlBoxType.MinimizeBox,
                Location = new Point(332, 11),
                Size = new Size(34, 30),
                FillColor = clrAppBg,
                IconColor = clrTextMain,
                BorderRadius = 8
            };
            pnlHeader.Controls.Add(btnMin);
            pnlHeader.Controls.Add(btnClose);
            dragControl = new Guna2DragControl { TargetControl = pnlHeader };

            pnlBottomNav = new Guna2Panel { Dock = DockStyle.Bottom, Height = 74, FillColor = clrCardBg, Visible = false };
            pnlBottomNav.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = clrBorderLight });

            btnTabFeed = MakeBtn("Bảng tin", 14, 14, 114, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.8F);
            btnTabCamera = MakeBtn("Chụp ảnh", 136, 14, 114, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.8F);
            btnTabExpense = MakeBtn("Quản lý chi tiêu", 258, 14, 148, 46, 23, clrCardBg, clrTextMain, clrBorderLight, 1, 8.8F);

            btnTabFeed.Click += (s, e) => { activeTabName = "FEED"; ActivateMenu(btnTabFeed, BuildFeedScreen()); };
            btnTabCamera.Click += (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
            btnTabExpense.Click += (s, e) => { activeTabName = "EXPENSE"; ActivateMenu(btnTabExpense, BuildExpenseScreen()); };

            pnlBottomNav.Controls.AddRange(new Control[] { btnTabFeed, btnTabCamera, btnTabExpense });

            pnlMainContainer = new Guna2Panel { Dock = DockStyle.Fill, FillColor = clrAppBg };

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlBottomNav);

            SetupFriendsDrawer();
        }

        private void ActivateMenu(Guna2Button selectedBtn, Control screenContent)
        {
            foreach (var btn in new[] { btnTabFeed, btnTabCamera, btnTabExpense })
            {
                btn.FillColor = clrCardBg;
                btn.ForeColor = clrTextMain;
                btn.BorderColor = clrBorderLight;
                btn.BorderThickness = 1;
            }
            selectedBtn.FillColor = clrPrimaryGreen;
            selectedBtn.ForeColor = Color.White;
            selectedBtn.BorderColor = clrSoftGreenBorder;
            selectedBtn.BorderThickness = 2;

            pnlMainContainer.Controls.Clear();
            screenContent.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(screenContent);
        }

        // ====================================================================
        // MÀN HÌNH 0: ĐĂNG NHẬP / ĐĂNG KÝ & NHẬP IP SERVER TCP
        // ====================================================================
        private void ShowAuthScreen()
        {
            activeTabName = "AUTH";
            pnlBottomNav.Visible = false;
            btnLogoutTop.Visible = false;
            lblHeaderUser.Text = "";

            Panel root = new Panel { BackColor = clrAppBg, Dock = DockStyle.Fill };
            bool isRegisterMode = false;

            // Banner giới thiệu đầu trang
            Guna2Panel pnlHero = new Guna2Panel
            {
                Location = new Point(22, 16),
                Size = new Size(376, 92),
                BorderRadius = 20,
                FillColor = clrPrimaryGreen
            };
            pnlHero.Controls.Add(MakeLabel("📸 PocketLog Locket & Bill", 18, 16, 13.5F, FontStyle.Bold, Color.White));
            pnlHero.Controls.Add(MakeLabel("Chia sẻ khoảnh khắc & quản lý chi tiêu qua TCP Socket", 18, 48, 8.5F, FontStyle.Regular, clrSoftGreenBg));
            root.Controls.Add(pnlHero);

            // Thẻ Cấu hình mạng TCP Server (IP + Port)
            Guna2Panel cardServer = new Guna2Panel
            {
                Location = new Point(22, 120),
                Size = new Size(376, 102),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 1
            };
            cardServer.Controls.Add(MakeLabel("🌐 Kết nối Máy chủ (TCP Server Host)", 14, 10, 9.2F, FontStyle.Bold, clrPrimaryGreen));
            cardServer.Controls.Add(MakeLabel("Nhập 127.0.0.1 nếu cùng máy, hoặc IP LAN của máy chủ:", 14, 31, 8F, FontStyle.Regular, clrTextMuted));

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

            // Thẻ Đăng nhập / Đăng ký
            Guna2Panel cardAuth = new Guna2Panel
            {
                Location = new Point(22, 234),
                Size = new Size(376, 366),
                BorderRadius = 18,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            // 2 nút chuyển tab: Đăng nhập | Đăng ký
            var btnModeLogin = MakeBtn("Đăng nhập", 14, 14, 170, 38, 12, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 9.2F);
            var btnModeReg = MakeBtn("Đăng ký tài khoản", 192, 14, 170, 38, 12, clrAppBg, clrTextMain, clrBorderLight, 1, 9.2F);

            Label lblUserTitle = MakeLabel("Tên đăng nhập / Hiển thị:", 14, 64, 8.8F, FontStyle.Bold, clrTextMain);
            Guna2TextBox txtUsername = new Guna2TextBox
            {
                Text = "Huy Hoàng",
                PlaceholderText = "Nhập tên tài khoản (VD: Huy Hoàng)...",
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

            // Ô ngân sách khởi tạo (Hiện thêm khi chọn tab Đăng ký)
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

            // Bộ chọn nhanh tài khoản demo (Hiện khi ở tab Đăng nhập)
            Label lblQuickPick = MakeLabel("⚡ Chọn nhanh tài khoản test nhiều cửa sổ:", 14, 202, 8.2F, FontStyle.Bold, clrIndigo);
            var btnPickHoang = MakeBtn("Huy Hoàng", 14, 224, 110, 32, 10, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8.2F);
            var btnPickHuy = MakeBtn("Gia Huy", 132, 224, 110, 32, 10, clrAppBg, clrTextMain, clrBorderLight, 1, 8.2F);
            var btnPickKhoa = MakeBtn("Thế Hưng", 250, 224, 112, 32, 10, clrAppBg, clrTextMain, clrBorderLight, 1, 8.2F);

            Action<Guna2Button, string> selectQuickAccount = (btnPicked, accName) =>
            {
                txtUsername.Text = accName;
                txtPassword.Text = "123";
                foreach (var b in new[] { btnPickHoang, btnPickHuy, btnPickKhoa })
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
            btnPickKhoa.Click += (s, e) => selectQuickAccount(btnPickKhoa, "Thế Hưng");

            Label lblAuthStatus = MakeLabel("💡 Hãy bật PocketLog.Server trước khi bấm Kết nối.", 14, 268, 8.2F, FontStyle.Italic, clrTextMuted);

            var btnSubmitAuth = MakeBtn("🚀 Kết nối TCP & Đăng nhập", 14, 298, 348, 50, 14, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 2, 10.5F);

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
                btnPickKhoa.Visible = true;
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
                btnPickKhoa.Visible = false;
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
                lblQuickPick, btnPickHoang, btnPickHuy, btnPickKhoa,
                lblAuthStatus, btnSubmitAuth
            });

            root.Controls.Add(cardAuth);
            pnlMainContainer.Controls.Clear();
            pnlMainContainer.Controls.Add(root);
        }

        // ====================================================================
        // XỬ LÝ KẾT NỐI TCP CLIENT & LẮNG NGHE GÓI TIN TỪ SERVER
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

                // Bắt đầu luồng lắng nghe gói tin từ Server trả về
                _ = Task.Run(() => ListenFromServerLoopAsync());

                // Gửi gói tin LOGIN_REQ (Server sẽ tự tạo tài khoản mới nếu chưa tồn tại)
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

        private async Task SendPacketSafeAsync(TcpPacket packet)
        {
            if (!isConnected || netStream == null) return;
            await sendLock.WaitAsync();
            try
            {
                await TcpPacketHelper.SendPacketAsync(netStream, packet);
            }
            catch
            {
                // Nếu mất kết nối
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task ListenFromServerLoopAsync()
        {
            try
            {
                while (isConnected && netStream != null && tcpClient != null && tcpClient.Connected)
                {
                    TcpPacket? packet = await TcpPacketHelper.ReadPacketAsync(netStream);
                    if (packet == null) break;

                    switch (packet.Type)
                    {
                        case PacketType.LOGIN_RES:
                            {
                                var res = packet.GetPayload<LoginResponseDto>();
                                if (res != null && res.Success)
                                {
                                    currentUserId = res.UserId;
                                    currentUsername = res.Username;
                                    currentFullName = res.FullName;
                                    totalBudgetLimit = res.MonthlyBudget;

                                    BeginInvoke(new Action(() =>
                                    {
                                        lblHeaderUser.Text = $"👤 {currentFullName}";
                                        btnLogoutTop.Visible = true;
                                        pnlBottomNav.Visible = true;
                                        activeTabName = "FEED";
                                        ActivateMenu(btnTabFeed, BuildFeedScreen());
                                    }));

                                    // Xin ngay dữ liệu đồng bộ từ CSDL của Server
                                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
                                }
                                break;
                            }

                        case PacketType.SYNC_RES:
                            {
                                var syncData = packet.GetPayload<SyncDataResponseDto>();
                                if (syncData != null)
                                {
                                    BeginInvoke(new Action(() => ApplySyncedDataToUI(syncData)));
                                }
                                break;
                            }

                        case PacketType.REFRESH_BROADCAST:
                            {
                                // Khi có bất kỳ máy nào đăng ảnh, kết bạn hoặc xác nhận bill -> tự động xin dữ liệu mới
                                if (currentUserId > 0)
                                {
                                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
                                }
                                break;
                            }
                    }
                }
            }
            catch
            {
                // Socket đóng
            }
        }

        private void ApplySyncedDataToUI(SyncDataResponseDto data)
        {
            totalBudgetLimit = data.MonthlyBudget > 0 ? data.MonthlyBudget : totalBudgetLimit;

            myFriends.Clear();
            foreach (var f in data.MyFriends)
            {
                myFriends.Add(new FriendUser
                {
                    UserId = f.UserId,
                    Name = f.FullName,
                    Handle = f.Username,
                    IsOnline = f.IsOnline
                });
            }

            friendSuggestions.Clear();
            foreach (var s in data.SuggestedUsers)
            {
                friendSuggestions.Add(new FriendUser
                {
                    UserId = s.UserId,
                    Name = s.FullName,
                    Handle = s.Username,
                    IsOnline = s.IsOnline
                });
            }

            posts.Clear();
            foreach (var p in data.Posts)
            {
                bool mine = (p.AuthorId == currentUserId);
                posts.Add(new LocketPost
                {
                    PostId = p.PostId,
                    IsMine = mine,
                    Author = mine ? $"{p.AuthorName} (Bạn)" : p.AuthorName,
                    HasTag = p.HasTag,
                    TaggedFriends = p.TaggedFriends ?? new List<string>(),
                    Category = p.Category,
                    NoteTop = p.NoteTop,
                    CaptionInside = p.CaptionInside,
                    Amount = p.Amount,
                    Time = p.Time,
                    Emoji = GetCategoryEmoji(p.Category),
                    PhotoBg = mine ? Color.FromArgb(82, 122, 106) : Color.FromArgb(148, 128, 114),
                    CustomImage = Base64ToImage(p.ImageBase64)
                });
            }

            taggedDebts.Clear();
            foreach (var b in data.SplitBills)
            {
                taggedDebts.Add(new TaggedDebtItem
                {
                    SplitId = b.SplitId,
                    CreditorId = b.CreditorId,
                    CreditorName = b.CreditorName,
                    DebtorId = b.DebtorId,
                    DebtorName = b.DebtorName,
                    Title = b.Title,
                    Category = b.Category,
                    TotalAmount = b.TotalAmount,
                    MyShare = b.AmountOwed,
                    SplitDetail = b.SplitDetail,
                    Time = b.Time,
                    IsSentProof = b.IsSentProof,
                    IsConfirmedPaid = b.IsPaid,
                    ProofImage = Base64ToImage(b.ProofImageBase64),
                    StatusNote = b.StatusNote
                });
            }

            // Vẽ lại màn hình đang mở (trừ màn hình Chụp ảnh để không làm mất chữ người dùng đang gõ)
            if (activeTabName == "FEED")
                ActivateMenu(btnTabFeed, BuildFeedScreen());
            else if (activeTabName == "EXPENSE")
                ActivateMenu(btnTabExpense, BuildExpenseScreen());

            if (pnlDrawerBackdrop.Visible)
                RenderFriendsDrawerContent("");
        }

        private void DisconnectAndLogout()
        {
            CloseCurrentSocket();
            currentUserId = 0;
            currentFullName = "";
            ShowAuthScreen();
        }

        private void CloseCurrentSocket()
        {
            isConnected = false;
            try { netStream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            netStream = null;
            tcpClient = null;
        }

        // ====================================================================
        // NGĂN KÉO DANH SÁCH BẠN BÈ & TÌM KIẾM KẾT BẠN QUA TCP
        // ====================================================================
        private void SetupFriendsDrawer()
        {
            pnlDrawerBackdrop = new Panel
            {
                Location = new Point(0, 52),
                Size = new Size(420, 768),
                BackColor = Color.FromArgb(222, 230, 226),
                Visible = false
            };
            pnlDrawerBackdrop.Click += (s, e) => CloseFriendsDrawer();

            pnlFriendsDrawer = new Guna2Panel
            {
                Location = new Point(420, 0),
                Size = new Size(344, 768),
                FillColor = clrCardBg,
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2
            };
            pnlDrawerBackdrop.Controls.Add(pnlFriendsDrawer);
            this.Controls.Add(pnlDrawerBackdrop);

            drawerTimer = new System.Windows.Forms.Timer { Interval = 12 };
            drawerTimer.Tick += (s, e) =>
            {
                int cur = pnlFriendsDrawer.Left;
                if (cur > drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Max(drawerTargetX, cur - Math.Max(20, (cur - drawerTargetX) / 2));
                }
                else if (cur < drawerTargetX)
                {
                    pnlFriendsDrawer.Left = Math.Min(drawerTargetX, cur + Math.Max(20, (drawerTargetX - cur) / 2));
                    if (pnlFriendsDrawer.Left >= 420)
                    {
                        pnlDrawerBackdrop.Visible = false;
                        drawerTimer.Stop();
                    }
                }
                else drawerTimer.Stop();
            };
        }

        private void OpenFriendsDrawer()
        {
            RenderFriendsDrawerContent("");
            pnlDrawerBackdrop.Visible = true;
            pnlDrawerBackdrop.BringToFront();
            pnlFriendsDrawer.Left = 420;
            drawerTargetX = 76;
            drawerTimer.Start();
        }

        private void CloseFriendsDrawer()
        {
            drawerTargetX = 420;
            drawerTimer.Start();
        }

        private void RenderFriendsDrawerContent(string keyword)
        {
            pnlFriendsDrawer.Controls.Clear();
            pnlFriendsDrawer.Controls.Add(MakeLabel($"👥 Danh sách bạn bè ({myFriends.Count})", 16, 16, 11.5F, FontStyle.Bold, clrTextMain));

            var btnClose = MakeBtn("✕", 294, 12, 34, 32, 10, clrAppBg, clrTextMain, clrBorderLight);
            btnClose.Click += (s, e) => CloseFriendsDrawer();
            pnlFriendsDrawer.Controls.Add(btnClose);

            Guna2TextBox txtSearch = new Guna2TextBox
            {
                Text = keyword,
                PlaceholderText = "🔍 Nhập tên bạn bè để kết bạn...",
                Location = new Point(16, 54),
                Size = new Size(216, 36),
                BorderRadius = 10,
                FillColor = clrAppBg,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnAddNew = MakeBtn("+ Kết bạn", 238, 54, 90, 36, 10, clrPrimaryGreen, Color.White, clrSoftGreenBorder);
            Panel listScroll = new Panel { Location = new Point(16, 100), Size = new Size(312, 650), AutoScroll = true };

            Action<string> refreshList = (kw) =>
            {
                listScroll.Controls.Clear();
                string q = (kw ?? "").Trim().ToLower();
                int y = 4;

                listScroll.Controls.Add(MakeLabel("Gợi ý người dùng trên Server", 2, y, 8.8F, FontStyle.Bold, clrIndigo));
                y += 22;

                var sugs = friendSuggestions
                    .Where(u => string.IsNullOrEmpty(q) || u.Name.ToLower().Contains(q) || u.Handle.ToLower().Contains(q))
                    .ToList();

                foreach (var sUser in sugs)
                {
                    var card = BuildFriendItemRow(sUser, false, async () =>
                    {
                        await SendPacketSafeAsync(TcpPacket.Create(PacketType.ADD_FRIEND_REQ, new AddFriendRequestDto
                        {
                            RequesterId = currentUserId,
                            TargetKeyword = sUser.Name
                        }));
                    });
                    card.Location = new Point(0, y);
                    listScroll.Controls.Add(card);
                    y += 56;
                }

                y += 8;
                listScroll.Controls.Add(MakeLabel("Bạn bè đã kết nối (Có thể tag chia tiền)", 2, y, 8.8F, FontStyle.Bold, clrPrimaryGreen));
                y += 22;

                foreach (var fUser in myFriends.Where(f => string.IsNullOrEmpty(q) || f.Name.ToLower().Contains(q) || f.Handle.ToLower().Contains(q)))
                {
                    var card = BuildFriendItemRow(fUser, true, null);
                    card.Location = new Point(0, y);
                    listScroll.Controls.Add(card);
                    y += 56;
                }
            };

            txtSearch.TextChanged += (s, e) => refreshList(txtSearch.Text);
            btnAddNew.Click += async (s, e) =>
            {
                string raw = txtSearch.Text.Trim();
                if (string.IsNullOrEmpty(raw)) return;
                await SendPacketSafeAsync(TcpPacket.Create(PacketType.ADD_FRIEND_REQ, new AddFriendRequestDto
                {
                    RequesterId = currentUserId,
                    TargetKeyword = raw
                }));
                txtSearch.Text = "";
            };

            refreshList(keyword);
            pnlFriendsDrawer.Controls.AddRange(new Control[] { txtSearch, btnAddNew, listScroll });
        }

        private Guna2Panel BuildFriendItemRow(FriendUser u, bool isFriend, Action? onAdd)
        {
            Guna2Panel row = new Guna2Panel
            {
                Size = new Size(308, 50),
                BorderRadius = 12,
                FillColor = isFriend ? clrAppBg : clrSoftIndigo,
                BorderColor = isFriend ? clrBorderLight : Color.FromArgb(180, 188, 240),
                BorderThickness = 1
            };

            Guna2CircleButton avt = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(u.Name) ? "U" : u.Name.Substring(0, 1).ToUpper(),
                Size = new Size(32, 32),
                Location = new Point(10, 9),
                FillColor = isFriend ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            row.Controls.Add(avt);
            row.Controls.Add(MakeLabel(u.Name, 48, 7, 9F, FontStyle.Bold, clrTextMain));
            row.Controls.Add(MakeLabel($"{u.Handle} • {(u.IsOnline ? "🟢 Đang online" : "⚪ Offline")}", 48, 26, 7.8F, FontStyle.Regular, u.IsOnline ? clrPrimaryGreen : clrTextMuted));

            if (!isFriend && onAdd != null)
            {
                var btn = MakeBtn("+ Kết bạn", 218, 10, 80, 30, 8, clrIndigo, Color.White, clrIndigo, 1, 7.8F);
                btn.Click += (s, e) => onAdd();
                row.Controls.Add(btn);
            }
            else
            {
                row.Controls.Add(MakeBtn("✓ Bạn bè", 222, 11, 76, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F));
            }
            return row;
        }

        // ====================================================================
        // MÀN HÌNH 1: BẢNG TIN (ẢNH GẦN NHẤT CỦA BẢN THÂN + BẢNG TIN BẠN BÈ)
        // ====================================================================
        private Control BuildFeedScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            root.Controls.Add(MakeLabel("Ảnh gần đây nhất", 20, y + 4, 11.5F, FontStyle.Bold, clrTextMain));

            var btnGallery = MakeBtn("Kho ảnh ›", 174, y, 88, 28, 14, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);
            btnGallery.Click += (s, e) => OpenMyGalleryScreen();

            var btnFriendsTop = MakeBtn($"👥 Bạn bè ({myFriends.Count}) ›", 268, y, 126, 28, 14, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8F);
            btnFriendsTop.Click += (s, e) => OpenFriendsDrawer();

            root.Controls.Add(btnGallery);
            root.Controls.Add(btnFriendsTop);
            y += 36;

            LocketPost? latestMine = posts.FirstOrDefault(p => p.IsMine);
            if (latestMine != null)
            {
                var hero = CreateLocketPostCard(latestMine, true);
                hero.Location = new Point(20, y);
                root.Controls.Add(hero);
                y += hero.Height + 16;
            }
            else
            {
                Guna2Panel emptyMine = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(374, 96),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1,
                    Cursor = Cursors.Hand
                };
                emptyMine.Controls.Add(MakeLabel("📷 Bạn chưa đăng tấm ảnh nào", 82, 24, 9.5F, FontStyle.Bold, clrPrimaryGreen));
                emptyMine.Controls.Add(MakeLabel("Bấm sang tab 'Chụp ảnh' để đăng khoảnh khắc đầu tiên!", 46, 50, 8.2F, FontStyle.Regular, clrTextMuted));
                emptyMine.Click += (s, e) => { activeTabName = "CAMERA"; ActivateMenu(btnTabCamera, BuildCameraScreen()); };
                root.Controls.Add(emptyMine);
                y += 110;
            }

            Guna2Panel divider = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 34),
                BorderRadius = 10,
                FillColor = clrSoftIndigo,
                BorderColor = Color.FromArgb(180, 188, 240),
                BorderThickness = 1
            };
            divider.Controls.Add(new Label
            {
                Text = "⬇ Lướt xuống • Bảng tin của bạn bè",
                Font = new Font("Segoe UI", 8.8F, FontStyle.Bold),
                ForeColor = clrIndigo,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            });
            root.Controls.Add(divider);
            y += 44;

            foreach (var fp in posts.Where(p => !p.IsMine))
            {
                var card = CreateLocketPostCard(fp, false);
                card.Location = new Point(20, y);
                root.Controls.Add(card);
                y += card.Height + 16;
            }

            root.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            return root;
        }

        private Guna2Panel CreateLocketPostCard(LocketPost post, bool isMyTopCard)
        {
            Guna2Panel card = new Guna2Panel
            {
                Size = new Size(374, 356),
                BorderRadius = 22,
                FillColor = clrCardBg,
                BorderColor = isMyTopCard ? clrSoftGreenBorder : clrBorderLight,
                BorderThickness = isMyTopCard ? 2 : 1,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            Guna2CircleButton avatar = new Guna2CircleButton
            {
                Text = string.IsNullOrEmpty(post.Author) ? "U" : post.Author.Substring(0, 1).ToUpper(),
                Size = new Size(34, 34),
                Location = new Point(14, 12),
                FillColor = isMyTopCard ? clrPrimaryGreen : clrIndigo,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            string tagFriendsStr = (post.TaggedFriends != null && post.TaggedFriends.Count > 0)
                ? $"👥 Tag: {string.Join(", ", post.TaggedFriends)}"
                : "🔒 Cá nhân";

            string topText = post.HasTag
                ? $"{post.Category} — {post.NoteTop} ({tagFriendsStr})"
                : "📷 Ảnh khoảnh khắc (Không gắn tag)";

            card.Controls.Add(avatar);
            card.Controls.Add(MakeLabel($"{post.Author} • {post.Time}", 56, 10, 9.2F, FontStyle.Bold, clrTextMain));
            card.Controls.Add(MakeLabel(topText, 56, 30, 8F, FontStyle.Bold, post.HasTag ? clrPrimaryGreen : clrTextMuted));

            Guna2Panel photoFrame = new Guna2Panel
            {
                Location = new Point(14, 56),
                Size = new Size(346, 284),
                BorderRadius = 20,
                FillColor = post.PhotoBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1,
                Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
            };

            if (post.CustomImage != null)
            {
                PictureBox pic = new PictureBox { Dock = DockStyle.Fill, Image = post.CustomImage, SizeMode = PictureBoxSizeMode.StretchImage };
                if (isMyTopCard) pic.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(pic);
            }
            else
            {
                Label art = new Label
                {
                    Text = isMyTopCard ? $"{post.Emoji}\n(Bấm vào ảnh để mở Kho ảnh cá nhân)" : post.Emoji,
                    Font = new Font("Segoe UI Emoji", isMyTopCard ? 12.5F : 40F, FontStyle.Bold),
                    ForeColor = Color.White,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = isMyTopCard ? Cursors.Hand : Cursors.Default
                };
                if (isMyTopCard) art.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Controls.Add(art);
            }

            var pillCaption = MakeBtn(post.CaptionInside, 23, 224, 300, 44, 18, Color.FromArgb(32, 38, 35), Color.White, Color.FromArgb(180, 210, 195), 1, 8.4F);
            if (isMyTopCard) pillCaption.Click += (s, e) => OpenMyGalleryScreen();
            photoFrame.Controls.Add(pillCaption);
            pillCaption.BringToFront();

            if (isMyTopCard)
            {
                card.Click += (s, e) => OpenMyGalleryScreen();
                photoFrame.Click += (s, e) => OpenMyGalleryScreen();
            }

            card.Controls.Add(photoFrame);
            return card;
        }

        private void OpenMyGalleryScreen()
        {
            Panel galleryRoot = new Panel { BackColor = clrAppBg, AutoScroll = true };
            var btnBack = MakeBtn("‹ Quay lại Bảng tin", 20, 14, 150, 36, 12, clrCardBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.8F);
            btnBack.Click += (s, e) => ActivateMenu(btnTabFeed, BuildFeedScreen());

            galleryRoot.Controls.Add(btnBack);
            galleryRoot.Controls.Add(MakeLabel($"Kho ảnh chụp của {currentFullName}", 20, 58, 12F, FontStyle.Bold, clrTextMain));

            var myPosts = posts.Where(p => p.IsMine).ToList();
            for (int i = 0; i < myPosts.Count; i++)
            {
                var item = myPosts[i];
                int col = i % 2, row = i / 2;
                Guna2Panel cell = new Guna2Panel
                {
                    Location = new Point(20 + col * 192, 92 + row * 212),
                    Size = new Size(182, 198),
                    BorderRadius = 16,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                Guna2Panel mini = new Guna2Panel { Location = new Point(8, 8), Size = new Size(166, 122), BorderRadius = 12, FillColor = item.PhotoBg };
                if (item.CustomImage != null)
                    mini.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = item.CustomImage, SizeMode = PictureBoxSizeMode.StretchImage });
                else
                    mini.Controls.Add(new Label { Text = item.Emoji, Font = new Font("Segoe UI Emoji", 24F), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });

                string tagTxt = (item.TaggedFriends != null && item.TaggedFriends.Count > 0) ? $"Tag: {string.Join(", ", item.TaggedFriends)}" : "Cá nhân";
                cell.Controls.Add(mini);
                cell.Controls.Add(MakeLabel(item.HasTag ? item.Category : "📷 Không gắn tag", 10, 136, 8F, FontStyle.Bold, item.HasTag ? clrPrimaryGreen : clrTextMuted));
                cell.Controls.Add(MakeLabel(item.HasTag ? $"{item.NoteTop}\n({tagTxt})" : item.CaptionInside, 10, 154, 7.6F, FontStyle.Regular, clrTextMain));
                galleryRoot.Controls.Add(cell);
            }

            pnlMainContainer.Controls.Clear();
            galleryRoot.Dock = DockStyle.Fill;
            pnlMainContainer.Controls.Add(galleryRoot);
        }

        // ====================================================================
        // MÀN HÌNH 2: CHỤP ẢNH & BẮN GÓI TIN QUA TCP SERVER
        // ====================================================================
        private Control BuildCameraScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();

            string? selectedCategory = "🍜 Đồ ăn/Thức uống";
            HashSet<string> taggedFriendNames = new HashSet<string>();
            Image? uploadedPhoto = null;

            var badgeLiveTop = MakeBtn("🍜 Đồ ăn/Thức uống — Bún bò Huế • 45.000đ (🔒 Cá nhân)", 20, 10, 374, 32, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8F);

            Guna2Panel pnlViewfinder = new Guna2Panel
            {
                Location = new Point(20, 46),
                Size = new Size(374, 212),
                BorderRadius = 22,
                FillColor = Color.FromArgb(46, 62, 56),
                BorderColor = clrSoftGreenBorder,
                BorderThickness = 2,
                Cursor = Cursors.Hand
            };

            PictureBox picPreview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.StretchImage, Visible = false };
            Label lblPrompt = new Label
            {
                Text = "📷\nBấm vào khung để chọn/chụp ảnh",
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(215, 230, 224),
                Size = new Size(340, 65),
                Location = new Point(17, 46),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };

            var pillLiveCaption = MakeBtn("Bữa trưa ngon bổ rẻ cùng đồng đội ✨", 32, 158, 310, 40, 18, Color.FromArgb(28, 34, 31), Color.White, Color.FromArgb(180, 210, 195), 1, 8.4F);

            pnlViewfinder.Controls.AddRange(new Control[] { lblPrompt, picPreview, pillLiveCaption });
            pillLiveCaption.BringToFront();

            EventHandler pickImg = (s, e) =>
            {
                using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                {
                    if (ofd.ShowDialog() == DialogResult.OK)
                    {
                        uploadedPhoto = Image.FromFile(ofd.FileName);
                        picPreview.Image = uploadedPhoto;
                        picPreview.Visible = true;
                        lblPrompt.Visible = false;
                        pillLiveCaption.BringToFront();
                    }
                }
            };
            pnlViewfinder.Click += pickImg;
            lblPrompt.Click += pickImg;
            picPreview.Click += pickImg;

            root.Controls.Add(MakeLabel("Mục chi tiêu (Bấm lại để bỏ tag):", 20, 266, 8.8F, FontStyle.Bold, clrTextMain));
            var btnNoTag = MakeBtn("🚫 Không gắn tag", 278, 262, 116, 26, 8, clrCardBg, clrRedDanger, clrRedDanger, 1, 7.8F);

            List<Guna2Button> catButtons = new List<Guna2Button>();

            Guna2Panel pnlNoteCard = new Guna2Panel
            {
                Location = new Point(20, 370),
                Size = new Size(374, 224),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            Guna2TextBox txtNoteTop = new Guna2TextBox
            {
                Text = "Bún bò Huế",
                PlaceholderText = "Ghi chú món...",
                Location = new Point(12, 10),
                Size = new Size(230, 34),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            Guna2TextBox txtAmountNum = new Guna2TextBox
            {
                Text = "45000",
                PlaceholderText = "Số tiền (đ)",
                Location = new Point(250, 10),
                Size = new Size(112, 34),
                BorderRadius = 10,
                BorderColor = clrSoftGreenBorder,
                ForeColor = clrPrimaryGreen,
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold)
            };

            Label lblTagFriends = MakeLabel("👥 Tag bạn bè chia tiền cùng (Không chọn = Cá nhân):", 12, 50, 8.2F, FontStyle.Bold, clrIndigo);

            FlowLayoutPanel flowFriends = new FlowLayoutPanel
            {
                Location = new Point(10, 70),
                Size = new Size(354, 46),
                AutoScroll = true,
                WrapContents = false,
                BackColor = Color.Transparent
            };

            Guna2TextBox txtCaption = new Guna2TextBox
            {
                Text = "Bữa trưa ngon bổ rẻ cùng đồng đội ✨",
                PlaceholderText = "Caption lọt trong ảnh (giữa phía dưới)...",
                Location = new Point(12, 124),
                Size = new Size(350, 36),
                BorderRadius = 10,
                BorderColor = clrBorderLight,
                Font = new Font("Segoe UI", 8.8F)
            };

            var btnChooseFile = MakeBtn("🖼️ Chọn ảnh từ máy tính", 12, 168, 220, 36, 10, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 8.2F);
            btnChooseFile.Click += pickImg;

            var btnAddMoreFriend = MakeBtn("+ Thêm bạn", 240, 168, 122, 36, 10, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8.2F);
            btnAddMoreFriend.Click += (s, e) => OpenFriendsDrawer();

            Action updatePreview = () =>
            {
                if (string.IsNullOrEmpty(selectedCategory))
                {
                    badgeLiveTop.Text = "📷 Ảnh khoảnh khắc (Không gắn tag chi tiêu)";
                    badgeLiveTop.FillColor = Color.FromArgb(235, 238, 236);
                    badgeLiveTop.ForeColor = clrTextMuted;
                    txtNoteTop.Enabled = false;
                    txtAmountNum.Enabled = false;
                    flowFriends.Enabled = false;
                }
                else
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out int amt);
                    if (amt <= 0) amt = 45000;
                    string splitInfo = taggedFriendNames.Count == 0
                        ? "🔒 Cá nhân"
                        : $"👥 Tag {string.Join(", ", taggedFriendNames)} ({amt / (taggedFriendNames.Count + 1):N0}đ/người)";

                    badgeLiveTop.Text = $"{selectedCategory} — {txtNoteTop.Text} • {amt:N0}đ ({splitInfo})";
                    badgeLiveTop.FillColor = clrSoftGreenBg;
                    badgeLiveTop.ForeColor = clrPrimaryGreen;
                    txtNoteTop.Enabled = true;
                    txtAmountNum.Enabled = true;
                    flowFriends.Enabled = true;
                }
            };

            foreach (var f in myFriends)
            {
                var fBtn = MakeBtn($"+ {f.Name}", 0, 0, 102, 32, 14, clrCardBg, clrTextMain, clrBorderLight, 1, 8F);
                fBtn.Margin = new Padding(2, 2, 6, 2);
                fBtn.Click += (s, e) =>
                {
                    if (taggedFriendNames.Contains(f.Name))
                    {
                        taggedFriendNames.Remove(f.Name);
                        fBtn.Text = $"+ {f.Name}";
                        fBtn.FillColor = clrCardBg;
                        fBtn.ForeColor = clrTextMain;
                        fBtn.BorderColor = clrBorderLight;
                        fBtn.BorderThickness = 1;
                    }
                    else
                    {
                        taggedFriendNames.Add(f.Name);
                        fBtn.Text = $"✓ {f.Name}";
                        fBtn.FillColor = clrSoftIndigo;
                        fBtn.ForeColor = clrIndigo;
                        fBtn.BorderColor = clrIndigo;
                        fBtn.BorderThickness = 2;
                    }
                    updatePreview();
                };
                flowFriends.Controls.Add(fBtn);
            }

            for (int i = 0; i < standardCategories.Length; i++)
            {
                var cat = standardCategories[i];
                var btnCat = MakeBtn(cat.Name, 20 + (i % 3) * 127, 292 + (i / 3) * 36, 120, 32, 10,
                    i == 0 ? clrSoftIndigo : clrCardBg, i == 0 ? clrIndigo : clrTextMain, i == 0 ? clrIndigo : clrBorderLight, i == 0 ? 2 : 1, 7.8F);

                btnCat.Click += (s, e) =>
                {
                    if (selectedCategory == cat.Name)
                    {
                        selectedCategory = null;
                        btnCat.FillColor = clrCardBg;
                        btnCat.ForeColor = clrTextMain;
                        btnCat.BorderColor = clrBorderLight;
                        btnCat.BorderThickness = 1;
                    }
                    else
                    {
                        foreach (var b in catButtons)
                        {
                            b.FillColor = clrCardBg;
                            b.ForeColor = clrTextMain;
                            b.BorderColor = clrBorderLight;
                            b.BorderThickness = 1;
                        }
                        btnCat.FillColor = clrSoftIndigo;
                        btnCat.ForeColor = clrIndigo;
                        btnCat.BorderColor = clrIndigo;
                        btnCat.BorderThickness = 2;
                        selectedCategory = cat.Name;
                    }
                    updatePreview();
                };
                catButtons.Add(btnCat);
                root.Controls.Add(btnCat);
            }

            btnNoTag.Click += (s, e) =>
            {
                selectedCategory = null;
                foreach (var b in catButtons)
                {
                    b.FillColor = clrCardBg;
                    b.ForeColor = clrTextMain;
                    b.BorderColor = clrBorderLight;
                    b.BorderThickness = 1;
                }
                updatePreview();
            };

            txtNoteTop.TextChanged += (s, e) => updatePreview();
            txtAmountNum.TextChanged += (s, e) => updatePreview();
            txtCaption.TextChanged += (s, e) => pillLiveCaption.Text = txtCaption.Text;

            pnlNoteCard.Controls.AddRange(new Control[] { txtNoteTop, txtAmountNum, lblTagFriends, flowFriends, txtCaption, btnChooseFile, btnAddMoreFriend });

            var btnPostPhoto = MakeBtn("Đăng ảnh", 20, 602, 374, 48, 16, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 2, 11F);
            btnPostPhoto.Click += async (s, e) =>
            {
                btnPostPhoto.Enabled = false;
                btnPostPhoto.Text = "⏳ Đang gửi lên Server...";

                bool hasTag = !string.IsNullOrEmpty(selectedCategory);
                int parsedAmt = 0;
                if (hasTag)
                {
                    int.TryParse(txtAmountNum.Text.Replace(".", "").Replace(",", ""), out parsedAmt);
                    if (parsedAmt <= 0) parsedAmt = 45000;
                }

                string cleanItemName = string.IsNullOrWhiteSpace(txtNoteTop.Text) ? "Khoản chi mới" : txtNoteTop.Text.Trim();
                string formattedNoteTop = hasTag ? $"{cleanItemName} • {parsedAmt:N0}đ" : "";

                var postDto = new PostDto
                {
                    AuthorId = currentUserId,
                    AuthorName = currentFullName,
                    HasTag = hasTag,
                    TaggedFriends = hasTag ? taggedFriendNames.ToList() : new List<string>(),
                    Category = hasTag ? selectedCategory! : "",
                    NoteTop = formattedNoteTop,
                    CaptionInside = string.IsNullOrWhiteSpace(txtCaption.Text) ? "Khoảnh khắc mới ✨" : txtCaption.Text.Trim(),
                    Amount = hasTag ? parsedAmt : 0,
                    ImageBase64 = ImageToBase64(uploadedPhoto)
                };

                await SendPacketSafeAsync(TcpPacket.Create(PacketType.CREATE_POST_REQ, postDto));

                activeTabName = "FEED";
                ActivateMenu(btnTabFeed, BuildFeedScreen());
            };

            root.Controls.AddRange(new Control[] { badgeLiveTop, pnlViewfinder, btnNoTag, pnlNoteCard, btnPostPhoto });
            return root;
        }

        // ====================================================================
        // MÀN HÌNH 3: QUẢN LÝ CHI TIÊU (GỬI ẢNH CK & XÁC NHẬN QUA TCP)
        // ====================================================================
        private Control BuildExpenseScreen()
        {
            Panel root = new Panel { BackColor = clrAppBg, AutoScroll = true };
            root.MouseEnter += (s, e) => root.Focus();
            int y = 12;

            var unpaidList = taggedDebts.Where(d => !d.IsConfirmedPaid).ToList();
            int totalUnpaidShare = unpaidList.Sum(d => d.MyShare);

            TaggedDebtItem? currentDebt = taggedDebts.FirstOrDefault(d => d.SplitId == selectedSplitId)
                                          ?? unpaidList.FirstOrDefault()
                                          ?? taggedDebts.FirstOrDefault();

            Guna2Panel pnlBanner = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 46),
                BorderRadius = 12,
                FillColor = Color.FromArgb(31, 41, 79)
            };
            pnlBanner.Controls.Add(MakeLabel($"Chia tiền bạn bè • Chưa tất toán: {totalUnpaidShare:N0}đ", 14, 13, 9.8F, FontStyle.Bold, Color.White));
            root.Controls.Add(pnlBanner);
            y += 54;

            Guna2Panel cardActiveDebt = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 218),
                BorderRadius = 16,
                FillColor = clrCardBg,
                BorderColor = clrBorderLight,
                BorderThickness = 1
            };

            if (currentDebt != null)
            {
                cardActiveDebt.Controls.Add(MakeLabel($"Khoản chia: {currentDebt.Title}", 14, 10, 9.5F, FontStyle.Bold, clrTextMain));
                cardActiveDebt.Controls.Add(MakeLabel($"{currentDebt.SplitDetail} • Phần chia: {currentDebt.MyShare:N0}đ", 14, 30, 8.2F, FontStyle.Bold, clrPrimaryGreen));

                var badgeState = MakeBtn(
                    currentDebt.IsConfirmedPaid ? "✓ Đã hoàn thành" : (currentDebt.IsSentProof ? "⏳ Chờ xác nhận" : "Chưa thanh toán"),
                    14, 52, 136, 24, 8,
                    currentDebt.IsConfirmedPaid ? clrSoftGreenBg : clrBadgeOrangeBg,
                    currentDebt.IsConfirmedPaid ? clrPrimaryGreen : clrBadgeOrangeTxt,
                    Color.Transparent, 0, 7.8F);
                cardActiveDebt.Controls.Add(badgeState);

                Label lblNote = MakeLabel(currentDebt.StatusNote, 156, 56, 7.8F, FontStyle.Italic, clrTextMuted);
                cardActiveDebt.Controls.Add(lblNote);

                Guna2Panel pnlBillImg = new Guna2Panel
                {
                    Location = new Point(14, 84),
                    Size = new Size(136, 118),
                    BorderRadius = 12,
                    FillColor = Color.FromArgb(238, 242, 240),
                    BorderColor = clrSoftGreenBorder,
                    BorderThickness = 1
                };

                PictureBox picProof = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = currentDebt.ProofImage,
                    Visible = currentDebt.ProofImage != null
                };
                Label lblNoImg = new Label
                {
                    Text = "🧾\nẢnh bill CK\nđính kèm",
                    Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
                    ForeColor = clrTextMuted,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Visible = currentDebt.ProofImage == null
                };
                pnlBillImg.Controls.Add(lblNoImg);
                pnlBillImg.Controls.Add(picProof);
                cardActiveDebt.Controls.Add(pnlBillImg);

                var btnUploadProof = MakeBtn("📤 Gửi ảnh CK của bản thân", 162, 86, 198, 50, 12, clrSoftIndigo, clrIndigo, clrIndigo, 2, 8.5F);
                var btnFriendVerify = MakeBtn($"✓ Xác nhận hoàn thành", 162, 146, 198, 54, 12, clrPrimaryGreen, Color.White, clrSoftGreenBorder, 1, 8.5F);

                btnUploadProof.Click += async (s, e) =>
                {
                    using (OpenFileDialog ofd = new OpenFileDialog { Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp" })
                    {
                        if (ofd.ShowDialog() == DialogResult.OK)
                        {
                            using var img = Image.FromFile(ofd.FileName);
                            string b64 = ImageToBase64(img);
                            await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPLOAD_PROOF_REQ, new UploadProofRequestDto
                            {
                                SplitId = currentDebt.SplitId,
                                UserId = currentUserId,
                                ProofImageBase64 = b64
                            }));
                        }
                    }
                };

                btnFriendVerify.Click += async (s, e) =>
                {
                    if (!currentDebt.IsSentProof)
                    {
                        lblNote.Text = "Vui lòng gửi ảnh CK trước!";
                        lblNote.ForeColor = clrRedDanger;
                        return;
                    }
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.VERIFY_BILL_REQ, new VerifyBillRequestDto
                    {
                        SplitId = currentDebt.SplitId,
                        VerifierName = currentFullName
                    }));
                };

                cardActiveDebt.Controls.Add(btnUploadProof);
                cardActiveDebt.Controls.Add(btnFriendVerify);
            }
            else
            {
                cardActiveDebt.Controls.Add(MakeLabel("Chưa có khoản chia tiền nào được gắn tag.", 42, 92, 9.5F, FontStyle.Bold, clrTextMuted));
            }
            root.Controls.Add(cardActiveDebt);
            y += 226;

            // Nút thả xuống các khoản chi được gắn tag chưa thanh toán
            string arrow = isUnpaidDropdownOpen ? "▲" : "▼";
            var btnToggleUnpaid = MakeBtn(
                $"{arrow} Các khoản chi gắn tag với bạn bè chưa thanh toán ({unpaidList.Count})",
                20, y, 374, 40, 12, clrSoftIndigo, clrIndigo, clrIndigo, 1, 8.4F);

            btnToggleUnpaid.Click += (s, e) =>
            {
                isUnpaidDropdownOpen = !isUnpaidDropdownOpen;
                ActivateMenu(btnTabExpense, BuildExpenseScreen());
            };
            root.Controls.Add(btnToggleUnpaid);
            y += 46;

            if (isUnpaidDropdownOpen)
            {
                foreach (var debt in unpaidList)
                {
                    bool isSelected = currentDebt != null && debt.SplitId == currentDebt.SplitId;
                    Guna2Panel itemRow = new Guna2Panel
                    {
                        Location = new Point(20, y),
                        Size = new Size(374, 68),
                        BorderRadius = 12,
                        FillColor = isSelected ? clrSoftGreenBg : clrCardBg,
                        BorderColor = isSelected ? clrPrimaryGreen : clrBorderLight,
                        BorderThickness = isSelected ? 2 : 1
                    };

                    itemRow.Controls.Add(MakeLabel($"{debt.Category} • {debt.Title}", 12, 8, 8.8F, FontStyle.Bold, clrTextMain));
                    itemRow.Controls.Add(MakeLabel($"{debt.SplitDetail} • Phần chia: {debt.MyShare:N0}đ", 12, 28, 8F, FontStyle.Bold, clrPrimaryGreen));
                    itemRow.Controls.Add(MakeLabel($"{debt.Time} • {(debt.IsSentProof ? "Đã đính kèm bill" : "Chưa gửi bill")}", 12, 46, 7.6F, FontStyle.Regular, clrTextMuted));

                    var btnSelectDebt = MakeBtn(
                        isSelected ? "Đang chọn" : "Chọn trả ›",
                        272, 16, 90, 34, 10,
                        isSelected ? clrPrimaryGreen : clrSoftIndigo,
                        isSelected ? Color.White : clrIndigo,
                        isSelected ? clrPrimaryGreen : clrIndigo, 1, 8F);

                    btnSelectDebt.Click += (s, e) =>
                    {
                        selectedSplitId = debt.SplitId;
                        ActivateMenu(btnTabExpense, BuildExpenseScreen());
                    };

                    itemRow.Controls.Add(btnSelectDebt);
                    root.Controls.Add(itemRow);
                    y += 74;
                }
            }

            y += 6;
            root.Controls.Add(MakeLabel("Chi tiêu theo danh mục Tag (Tự đặt hạn mức):", 20, y, 10F, FontStyle.Bold, clrTextMain));
            y += 26;

            foreach (var cat in standardCategories)
            {
                string catName = cat.Name;
                int spent = posts.Where(p => p.HasTag && p.Category == catName).Sum(p => p.Amount);
                int limit = categoryBudgets.ContainsKey(catName) ? categoryBudgets[catName] : 500000;

                Guna2Panel catCard = new Guna2Panel
                {
                    Location = new Point(20, y),
                    Size = new Size(374, 84),
                    BorderRadius = 14,
                    FillColor = clrCardBg,
                    BorderColor = clrBorderLight,
                    BorderThickness = 1
                };

                catCard.Controls.Add(MakeLabel(catName, 12, 10, 9.2F, FontStyle.Bold, clrTextMain));
                catCard.Controls.Add(MakeLabel($"Đã tiêu: {spent:N0}đ / Mức: {limit:N0}đ", 12, 32, 8F, FontStyle.Bold, spent > limit ? clrRedDanger : clrPrimaryGreen));

                int pct = limit > 0 ? Math.Min(100, (int)((long)spent * 100 / limit)) : 100;
                Guna2ProgressBar prg = new Guna2ProgressBar
                {
                    Location = new Point(12, 56),
                    Size = new Size(210, 12),
                    BorderRadius = 6,
                    Value = pct,
                    FillColor = Color.FromArgb(232, 236, 234),
                    ProgressColor = spent > limit ? clrRedDanger : clrPrimaryGreen,
                    ProgressColor2 = spent > limit ? Color.OrangeRed : clrSoftGreenBorder
                };

                Guna2TextBox txtLim = new Guna2TextBox
                {
                    Text = limit.ToString(),
                    Location = new Point(234, 12),
                    Size = new Size(126, 28),
                    BorderRadius = 8,
                    BorderColor = clrBorderLight,
                    Font = new Font("Segoe UI", 8.5F)
                };

                var btnSetLim = MakeBtn("Set hạn mức", 234, 46, 126, 28, 8, clrSoftGreenBg, clrPrimaryGreen, clrSoftGreenBorder, 1, 7.8F);
                btnSetLim.Click += (s, e) =>
                {
                    if (int.TryParse(txtLim.Text.Replace(".", "").Replace(",", ""), out int nLim) && nLim > 0)
                    {
                        categoryBudgets[catName] = nLim;
                        ActivateMenu(btnTabExpense, BuildExpenseScreen());
                    }
                };

                catCard.Controls.AddRange(new Control[] { prg, txtLim, btnSetLim });
                root.Controls.Add(catCard);
                y += 92;
            }

            int grandTotal = posts.Where(p => p.HasTag).Sum(p => p.Amount);

            Guna2Panel cardTotal = new Guna2Panel
            {
                Location = new Point(20, y),
                Size = new Size(374, 132),
                BorderRadius = 18,
                FillColor = clrSoftGreenBg,
                BorderColor = clrPrimaryGreen,
                BorderThickness = 2
            };

            cardTotal.Controls.Add(MakeLabel("TỔNG CHI TIÊU TẤT CẢ CÁC MỤC", 14, 10, 9.2F, FontStyle.Bold, clrPrimaryGreen));
            cardTotal.Controls.Add(MakeLabel($"{grandTotal:N0}đ  /  Tổng hạn mức: {totalBudgetLimit:N0}đ", 14, 32, 10.5F, FontStyle.Bold, grandTotal > totalBudgetLimit ? clrRedDanger : clrTextMain));

            int totalPct = totalBudgetLimit > 0 ? Math.Min(100, (int)((long)grandTotal * 100 / totalBudgetLimit)) : 100;
            Guna2ProgressBar prgTotal = new Guna2ProgressBar
            {
                Location = new Point(14, 58),
                Size = new Size(346, 14),
                BorderRadius = 7,
                Value = totalPct,
                FillColor = Color.White,
                ProgressColor = grandTotal > totalBudgetLimit ? clrRedDanger : clrPrimaryGreen,
                ProgressColor2 = grandTotal > totalBudgetLimit ? Color.OrangeRed : clrIndigo
            };

            Guna2TextBox txtTotalIn = new Guna2TextBox
            {
                Text = totalBudgetLimit.ToString(),
                Location = new Point(14, 82),
                Size = new Size(210, 34),
                BorderRadius = 10,
                FillColor = Color.White,
                BorderColor = clrSoftGreenBorder,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var btnSetTotal = MakeBtn("Set tổng hạn mức", 232, 82, 128, 34, 10, clrPrimaryGreen, Color.White, clrPrimaryGreen, 1, 8.2F);
            btnSetTotal.Click += async (s, e) =>
            {
                if (int.TryParse(txtTotalIn.Text.Replace(".", "").Replace(",", ""), out int nTot) && nTot > 0)
                {
                    totalBudgetLimit = nTot;
                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.UPDATE_BUDGET_REQ, new UpdateBudgetRequestDto
                    {
                        UserId = currentUserId,
                        NewMonthlyBudget = nTot
                    }));
                    ActivateMenu(btnTabExpense, BuildExpenseScreen());
                }
            };

            cardTotal.Controls.AddRange(new Control[] { prgTotal, txtTotalIn, btnSetTotal });
            root.Controls.Add(cardTotal);
            y += 148;

            root.Controls.Add(new Label { Location = new Point(0, y), Size = new Size(10, 20) });
            return root;
        }

        // ====================================================================
        // TIỆN ÍCH CHUYỂN ĐỔI ẢNH <-> BASE64 NHẸ MƯỢT QUA MẠNG TCP
        // ====================================================================
        private string GetCategoryEmoji(string category)
        {
            foreach (var c in standardCategories)
            {
                if (c.Name == category) return c.Emoji;
            }
            return "📷";
        }

        private string ImageToBase64(Image? img)
        {
            if (img == null) return "";
            try
            {
                int maxSide = 600;
                int w = img.Width, h = img.Height;
                if (w > maxSide || h > maxSide)
                {
                    if (w > h) { h = (int)((double)h * maxSide / w); w = maxSide; }
                    else { w = (int)((double)w * maxSide / h); h = maxSide; }
                }
                using Bitmap resized = new Bitmap(img, new Size(Math.Max(1, w), Math.Max(1, h)));
                using MemoryStream ms = new MemoryStream();
                resized.Save(ms, ImageFormat.Jpeg);
                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return "";
            }
        }

        private Image? Base64ToImage(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return null;
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                using MemoryStream ms = new MemoryStream(bytes);
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }
    }
}