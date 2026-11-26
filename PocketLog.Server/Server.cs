using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;

namespace PocketLog.Server
{
    public partial class Server : Form
    {
        private TcpListener? tcpListener;
        private bool isRunning = false;

        private readonly string masterConnStr = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True";
        private readonly string dbConnStr = @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=PocketLogDB;Integrated Security=True";
        private readonly string storageFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ServerStorage");

        private readonly ConcurrentDictionary<TcpClient, int> connectedClients = new ConcurrentDictionary<TcpClient, int>();
        private readonly ConcurrentDictionary<TcpClient, SemaphoreSlim> clientSendLocks = new ConcurrentDictionary<TcpClient, SemaphoreSlim>();

        private Label lblStatus = null!;
        private Label lblIpInfo = null!;
        private Button btnToggleServer = null!;
        private ListBox lstClients = null!;
        private RichTextBox rtbLog = null!;

        public Server()
        {
            InitializeComponent();
            Directory.CreateDirectory(storageFolder);
            BuildServerDashboardUI();
            EnsureDatabaseSetup();
            StartTcpServer();

            this.FormClosing += (s, e) => StopTcpServer();
        }

        private void BuildServerDashboardUI()
        {
            this.Text = "PocketLog TCP Server & SQL Host (Port 8888)";
            var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1440, 900);
            int initW = Math.Min(900, Math.Max(640, workArea.Width - 80));
            int initH = Math.Min(580, Math.Max(420, workArea.Height - 80));
            this.Size = new Size(initW, initH);
            this.MinimumSize = new Size(560, 380);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 28, 32);
            this.ForeColor = Color.White;
            this.DoubleBuffered = true;

            string localIps = string.Join("  |  ", Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .Select(ip => ip.ToString()));

            Panel pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Size = new Size(this.ClientSize.Width, 88),
                BackColor = Color.FromArgb(24, 28, 32)
            };

            btnToggleServer = new Button
            {
                Text = "⏹ Dừng Server",
                Location = new Point(pnlTop.Width - 186, 14),
                Size = new Size(168, 38),
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            btnToggleServer.FlatAppearance.BorderSize = 0;
            btnToggleServer.Click += (s, e) =>
            {
                if (isRunning) StopTcpServer();
                else StartTcpServer();
            };

            lblStatus = new Label
            {
                Text = "● SERVER ĐANG KHỞI ĐỘNG...",
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 204, 113),
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(18, 14),
                Size = new Size(Math.Max(180, btnToggleServer.Left - 30), 34)
            };

            lblIpInfo = new Label
            {
                Text = $"IP LAN cho các máy khác kết nối:  {localIps}   (Port: 8888)",
                Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 196, 15),
                AutoSize = false,
                AutoEllipsis = true,
                UseMnemonic = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(18, 54),
                Size = new Size(Math.Max(200, pnlTop.Width - 36), 28)
            };

            void LayoutServerTopBar()
            {
                int tw = pnlTop.ClientSize.Width;
                if (tw <= 0) return;
                btnToggleServer.Location = new Point(tw - 186, 14);
                lblStatus.SetBounds(18, 14, Math.Max(160, btnToggleServer.Left - 30), 34);
                lblIpInfo.SetBounds(18, 54, Math.Max(180, tw - 36), 28);
            }
            pnlTop.Resize += (s, e) => LayoutServerTopBar();

            ToolTip tip = new ToolTip();
            tip.SetToolTip(lblIpInfo, lblIpInfo.Text);

            pnlTop.Controls.AddRange(new Control[] { lblStatus, lblIpInfo, btnToggleServer });
            LayoutServerTopBar();

            Panel pnlBody = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18, 4, 18, 16),
                BackColor = Color.FromArgb(24, 28, 32)
            };

            SplitContainer splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 10,
                BackColor = Color.FromArgb(24, 28, 32)
            };

            Label lblOnline = new Label
            {
                Text = "Các máy Client đang kết nối:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 28,
                UseMnemonic = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            lstClients = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(34, 40, 46),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                IntegralHeight = false
            };

            splitMain.Panel1.Controls.Add(lstClients);
            splitMain.Panel1.Controls.Add(lblOnline);

            Label lblLogTitle = new Label
            {
                Text = "Nhật ký gói tin TCP & Truy vấn SQL Server:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 28,
                UseMnemonic = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            rtbLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(16, 20, 24),
                ForeColor = Color.FromArgb(210, 220, 225),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            splitMain.Panel2.Controls.Add(rtbLog);
            splitMain.Panel2.Controls.Add(lblLogTitle);

            pnlBody.Controls.Add(splitMain);
            this.Controls.Add(pnlBody);
            this.Controls.Add(pnlTop);

            this.Shown += (s, e) =>
            {
                try
                {
                    splitMain.Panel1MinSize = 160;
                    splitMain.Panel2MinSize = 240;
                    int desired = Math.Clamp((int)(splitMain.Width * 0.30f), 200, Math.Max(200, splitMain.Width - 260));
                    splitMain.SplitterDistance = desired;
                }
                catch { }
            };
        }

        private void Log(string msg)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Log(msg)));
                return;
            }
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            rtbLog.ScrollToCaret();
        }

        private void RefreshOnlineListUI()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshOnlineListUI));
                return;
            }
            lstClients.Items.Clear();
            foreach (var kv in connectedClients)
            {
                string ep = "Disconnected";
                try { ep = kv.Key.Client?.RemoteEndPoint?.ToString() ?? "Unknown"; } catch { }
                lstClients.Items.Add($"User#{kv.Value} ({ep})");
            }
        }

        private void EnsureDatabaseSetup()
        {
            try
            {
                using (SqlConnection masterConn = new SqlConnection(masterConnStr))
                {
                    masterConn.Open();
                    string checkDb = "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PocketLogDB') CREATE DATABASE PocketLogDB;";
                    using (SqlCommand cmd = new SqlCommand(checkDb, masterConn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                using (SqlConnection conn = new SqlConnection(dbConnStr))
                {
                    conn.Open();
                    string schemaSql = @"
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE Users (
        UserID INT IDENTITY(1,1) PRIMARY KEY,
        Username NVARCHAR(50) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(255) NOT NULL,
        FullName NVARCHAR(100) NOT NULL,
        MonthlyBudget INT DEFAULT 3500000
    );
    CREATE TABLE Friendships (
        FriendshipID INT IDENTITY(1,1) PRIMARY KEY,
        RequesterID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        AddresseeID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        Status NVARCHAR(20) DEFAULT 'Accepted',
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    CREATE TABLE ExpenseGroups (
        GroupID INT IDENTITY(1,1) PRIMARY KEY,
        CreatedBy INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        GroupName NVARCHAR(100) NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    CREATE TABLE GroupMembers (
        GroupID INT NOT NULL FOREIGN KEY REFERENCES ExpenseGroups(GroupID),
        UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        JoinedAt DATETIME DEFAULT GETDATE(),
        PRIMARY KEY (GroupID, UserID)
    );
    CREATE TABLE ExpensePosts (
        PostID INT IDENTITY(1,1) PRIMARY KEY,
        UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        GroupID INT NULL FOREIGN KEY REFERENCES ExpenseGroups(GroupID),
        ImagePath NVARCHAR(255) NOT NULL,
        Caption NVARCHAR(255),
        IsPublicToFriends BIT DEFAULT 1,
        IsExpenseTracked BIT DEFAULT 1,
        TotalAmount INT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
    CREATE TABLE ExpenseItems (
        ItemID INT IDENTITY(1,1) PRIMARY KEY,
        PostID INT NOT NULL FOREIGN KEY REFERENCES ExpensePosts(PostID),
        ItemName NVARCHAR(100) NOT NULL,
        Amount INT NOT NULL,
        Category NVARCHAR(50) NOT NULL,
        PosX FLOAT DEFAULT 0.5,
        PosY FLOAT DEFAULT 0.5
    );
    CREATE TABLE SplitBills (
        SplitID INT IDENTITY(1,1) PRIMARY KEY,
        PostID INT NOT NULL FOREIGN KEY REFERENCES ExpensePosts(PostID),
        DebtorID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        AmountOwed INT NOT NULL,
        IsPaid BIT DEFAULT 0,
        ProofImagePath NVARCHAR(255) NULL,
        StatusNote NVARCHAR(255) DEFAULT N'Chưa đính kèm ảnh chuyển khoản',
        PaidAt DATETIME NULL,
        CreatedAt DATETIME DEFAULT GETDATE()
    );
END

IF OBJECT_ID('dbo.UserCategoryBudgets', 'U') IS NULL
BEGIN
    CREATE TABLE UserCategoryBudgets (
        UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
        Category NVARCHAR(50) NOT NULL,
        BudgetLimit INT NOT NULL,
        PRIMARY KEY (UserID, Category)
    );
END";

                    using (SqlCommand cmd = new SqlCommand(schemaSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                Log("✓ Đã kết nối CSDL PocketLogDB (đầy đủ 8 bảng, bảo toàn dữ liệu hiện có).");
            }
            catch (Exception ex)
            {
                Log("❌ Lỗi khởi tạo SQL LocalDB: " + ex.Message);
            }
        }
    }
}