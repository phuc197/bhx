CREATE DATABASE BachHoaXanhDB;
GO

USE BachHoaXanhDB;
GO

-- 2. Bảng Danh mục sản phẩm (Rau củ, Thịt cá, Sữa, Đồ gia dụng...)
CREATE TABLE Categories (
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255) NULL
);

-- 3. Bảng Sản phẩm (Mỗi sản phẩm thuộc 1 danh mục)
CREATE TABLE Products (
    ProductID INT IDENTITY(1,1) PRIMARY KEY,
    ProductName NVARCHAR(200) NOT NULL,
    CategoryID INT FOREIGN KEY REFERENCES Categories(CategoryID),
    Price DECIMAL(18,2) NOT NULL,
    StockQuantity INT NOT NULL, -- Số lượng tồn kho (rất quan trọng với hàng bách hóa)
    ImageURL NVARCHAR(500) NULL,
    Unit NVARCHAR(50) NOT NULL, -- Đơn vị tính: kg, bó, chai, hộp...
    Description NVARCHAR(MAX) NULL,
    IsActive BIT DEFAULT 1 -- Còn bán hay ngừng bán
);

-- 4. Bảng Người dùng / Khách hàng
CREATE TABLE Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email VARCHAR(100) UNIQUE NOT NULL,
    PasswordHash VARCHAR(255) NOT NULL, -- Lưu mật khẩu đã mã hóa
    Phone VARCHAR(15) NULL,
    Address NVARCHAR(255) NULL,
    Role VARCHAR(20) DEFAULT 'Customer' -- Quyền: Admin hoặc Customer
);

-- 5. Bảng Đơn hàng
CREATE TABLE Orders (
    OrderID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT FOREIGN KEY REFERENCES Users(UserID),
    OrderDate DATETIME DEFAULT GETDATE(),
    TotalAmount DECIMAL(18,2) NOT NULL,
    ShippingAddress NVARCHAR(255) NOT NULL,
    Status NVARCHAR(50) DEFAULT N'Chờ xử lý' -- Chờ xử lý, Đang giao, Đã giao, Đã hủy
);

-- 6. Bảng Chi tiết đơn hàng (Lưu các món hàng trong đơn)
CREATE TABLE OrderDetails (
    OrderDetailID INT IDENTITY(1,1) PRIMARY KEY,
    OrderID INT FOREIGN KEY REFERENCES Orders(OrderID),
    ProductID INT FOREIGN KEY REFERENCES Products(ProductID),
    Quantity INT NOT NULL,
    Price DECIMAL(18,2) NOT NULL -- Lưu giá tại thời điểm mua
);
ALTER TABLE Products 
ADD SoldQuantity INT NOT NULL DEFAULT 0;
GO