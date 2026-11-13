-- ====================================================================
-- DỰ ÁN: POCKETLOG - MẠNG MÁY TÍNH (NT106) - NHÓM 10
-- KỊCH BẢN TẠO CƠ SỞ DỮ LIỆU CHUẨN 7 BẢNG TRÊN (localdb)\MSSQLLocalDB
-- ====================================================================

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PocketLogDB')
BEGIN
    CREATE DATABASE PocketLogDB;
END
GO

USE PocketLogDB;
GO

-- 1. BẢNG NGƯỜI DÙNG (Users)
IF OBJECT_ID('dbo.SplitBills', 'U') IS NOT NULL DROP TABLE dbo.SplitBills;
IF OBJECT_ID('dbo.ExpenseItems', 'U') IS NOT NULL DROP TABLE dbo.ExpenseItems;
IF OBJECT_ID('dbo.ExpensePosts', 'U') IS NOT NULL DROP TABLE dbo.ExpensePosts;
IF OBJECT_ID('dbo.GroupMembers', 'U') IS NOT NULL DROP TABLE dbo.GroupMembers;
IF OBJECT_ID('dbo.ExpenseGroups', 'U') IS NOT NULL DROP TABLE dbo.ExpenseGroups;
IF OBJECT_ID('dbo.Friendships', 'U') IS NOT NULL DROP TABLE dbo.Friendships;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    MonthlyBudget INT DEFAULT 3000000
);

-- 2. BẢNG MỐI QUAN HỆ BẠN BÈ (Friendships)
CREATE TABLE Friendships (
    FriendshipID INT IDENTITY(1,1) PRIMARY KEY,
    RequesterID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    AddresseeID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    Status NVARCHAR(20) DEFAULT 'Accepted',
    CreatedAt DATETIME DEFAULT GETDATE()
);

-- 3. BẢNG NHÓM CHI TIÊU (ExpenseGroups)
CREATE TABLE ExpenseGroups (
    GroupID INT IDENTITY(1,1) PRIMARY KEY,
    CreatedBy INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    GroupName NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);

-- 4. BẢNG THÀNH VIÊN NHÓM (GroupMembers)
CREATE TABLE GroupMembers (
    GroupID INT NOT NULL FOREIGN KEY REFERENCES ExpenseGroups(GroupID),
    UserID INT NOT NULL FOREIGN KEY REFERENCES Users(UserID),
    JoinedAt DATETIME DEFAULT GETDATE(),
    PRIMARY KEY (GroupID, UserID)
);

-- 5. BẢNG BÀI ĐĂNG KHOẢNH KHẮC & CHI TIÊU (ExpensePosts)
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

-- 6. BẢNG MÓN ĂN / KHOẢN CHI TIẾT (ExpenseItems)
CREATE TABLE ExpenseItems (
    ItemID INT IDENTITY(1,1) PRIMARY KEY,
    PostID INT NOT NULL FOREIGN KEY REFERENCES ExpensePosts(PostID),
    ItemName NVARCHAR(100) NOT NULL,
    Amount INT NOT NULL,
    Category NVARCHAR(50) NOT NULL,
    PosX FLOAT DEFAULT 0.5,
    PosY FLOAT DEFAULT 0.5
);

-- 7. BẢNG CHIA TIỀN HÓA ĐƠN (SplitBills)
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
GO

-- NẠP SẴN DỮ LIỆU TÀI KHOẢN MẪU CHO NHÓM (Tránh lỗi khóa ngoại khi chia tiền)
INSERT INTO Users (Username, PasswordHash, FullName, MonthlyBudget)
VALUES 
    ('huyhoang', 'hash123', N'Huy Hoàng (Bạn)', 3500000),
    ('giahuy', 'hash123', N'Gia Huy', 2500000),
    ('minhkhoa', 'hash123', N'Minh Khoa', 2000000);

INSERT INTO Friendships (RequesterID, AddresseeID, Status)
VALUES 
    (1, 2, 'Accepted'),
    (1, 3, 'Accepted');
GO