using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
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
        }

        private void BuildServerDashboardUI()
        {
            this.Text = "PocketLog TCP Server & SQL Host (Port 8888)";
            this.Size = new Size(680, 480);
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
                Location = new Point(20, 44)
            };

            btnToggleServer = new Button
            {
                Text = "Đang chạy (Port 8888)",
                Location = new Point(490, 18),
                Size = new Size(155, 38),
                BackColor = Color.FromArgb(29, 99, 76),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            Label lblOnline = new Label
            {
                Text = "Các máy Client đang kết nối:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(20, 78),
                AutoSize = true
            };

            lstClients = new ListBox
            {
                Location = new Point(20, 102),
                Size = new Size(200, 320),
                BackColor = Color.FromArgb(34, 40, 46),
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 9.5F),
                BorderStyle = BorderStyle.FixedSingle
            };

            Label lblLogTitle = new Label
            {
                Text = "Nhật ký gói tin TCP & Truy vấn SQL Server:",
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(236, 78),
                AutoSize = true
            };

            rtbLog = new RichTextBox
            {
                Location = new Point(236, 102),
                Size = new Size(410, 320),
                BackColor = Color.FromArgb(16, 20, 24),
                ForeColor = Color.FromArgb(210, 220, 225),
                Font = new Font("Consolas", 9F),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            this.Controls.AddRange(new Control[] { lblStatus, lblIpInfo, btnToggleServer, lblOnline, lstClients, lblLogTitle, rtbLog });
        }

        private void Log(string msg)
        {
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
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshOnlineListUI));
                return;
            }
            lstClients.Items.Clear();
            foreach (var kv in connectedClients)
            {
                string ep = kv.Key.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                lstClients.Items.Add($"User#{kv.Value} ({ep})");
            }
        }

        // ====================================================================
        // 1. KHỞI TẠO CSDL 7 BẢNG & ĐẢM BẢO CÁC TÀI KHOẢN MẪU LUÔN CÓ SẴN
        // ====================================================================
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

                    -- Đảm bảo các tài khoản mẫu luôn tồn tại trong bảng Users dù bảng đã được tạo từ trước
                    IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'huyhoang')
                        INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget) VALUES ('huyhoang', '123', N'Huy Hoàng', 3500000);
                    IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'giahuy')
                        INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget) VALUES ('giahuy', '123', N'Gia Huy', 2500000);
                    IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'thehung')
                        INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget) VALUES ('thehung', '123', N'Thế Hưng', 2000000);
                    IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'thanhthao')
                        INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget) VALUES ('thanhthao', '123', N'Thanh Thảo', 3000000);
                    ";

                    using (SqlCommand cmd = new SqlCommand(schemaSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                Log("✓ Đã kết nối CSDL PocketLogDB và kiểm tra đầy đủ danh sách tài khoản.");
            }
            catch (Exception ex)
            {
                Log("❌ Lỗi khởi tạo SQL LocalDB: " + ex.Message);
            }
        }
    }
}
