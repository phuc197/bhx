using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace bachhoaxanh.Models
{
    public class CartItem
    {
        public int ProductID { get; set; }
        public string ProductName { get; set; }
        public string ImageURL { get; set; }
        public string Unit { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int StockQuantity { get; set; } // Lưu số tồn kho để chặn không cho mua lố

        // Tự động tính thành tiền của món này
        public decimal TotalPrice
        {
            get { return Price * Quantity; }
        }
    }
}