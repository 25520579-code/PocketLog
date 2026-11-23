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
            this.Size = new Size(860, 560);
            this.MinimumSize = new Size(760, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(24, 28, 32);
            this.ForeColor = Color.White;

            string localIps = string.Join("  |  ", Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                .Select(ip => ip.ToString()));

            lblStatus = new Label
            {
                Text = "● SERVER ĐANG KHỞI ĐỘNG...",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(46, 204, 113),
                AutoSize = true,
                Location = new Point(20, 16)
            };

            lblIpInfo = new Label
            {
                Text = $"IP LAN cho các máy khác kết nối:  {localIps}   (Port: 8888)",
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(241, 196, 15),
                AutoSize = true,
                Location = new Point(20, 58)
            };

            btnToggleServer = new Button
            {
                Text = "⏹ Dừng Server",
                Location = new Point(this.ClientSize.Width - 170, 14),
                Size = new Size(150, 36),
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnToggleServer.Click += (s, e) =>
            {
                if (isRunning) StopTcpServer();
                else StartTcpServer();
            };

            Label lblOnline = new Label
            {
                Text = "Các máy Client đang kết nối:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 96),
                AutoSize = true
            };

            lstClients = new ListBox
            {
                Location = new Point(20, 122),
                Size = new Size(240, this.ClientSize.Height - 142),
                BackColor = Color.FromArgb(34, 40, 46),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left
            };

            Label lblLogTitle = new Label
            {
                Text = "Nhật ký gói tin TCP & Truy vấn SQL Server:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(280, 96),
                AutoSize = true
            };

            rtbLog = new RichTextBox
            {
                Location = new Point(280, 122),
                Size = new Size(this.ClientSize.Width - 300, this.ClientSize.Height - 142),
                BackColor = Color.FromArgb(16, 20, 24),
                ForeColor = Color.FromArgb(210, 220, 225),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            this.Controls.AddRange(new Control[] { lblStatus, lblIpInfo, btnToggleServer, lblOnline, lstClients, lblLogTitle, rtbLog });
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

                string defaultPassHash = HashPassword("123");

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

                    // Chỉ khởi tạo tài khoản mẫu nếu bảng Users hoàn toàn trống
                    string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Users)
BEGIN
    INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
    VALUES 
        ('huyhoang', @defHash, N'Huy Hoàng', 3500000),
        ('giahuy', @defHash, N'Gia Huy', 2500000),
        ('minhkhoa', @defHash, N'Minh Khoa', 2000000);

    INSERT INTO Friendships (RequesterID, AddresseeID, Status)
    VALUES 
        (1, 2, 'Accepted'),
        (1, 3, 'Accepted');
END";
                    using (SqlCommand seedCmd = new SqlCommand(seedSql, conn))
                    {
                        seedCmd.Parameters.AddWithValue("@defHash", defaultPassHash);
                        seedCmd.ExecuteNonQuery();
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