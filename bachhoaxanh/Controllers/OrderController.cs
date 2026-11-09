using bachhoaxanh.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace bachhoaxanh.Controllers
{
    public class OrderController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // ==========================================
        // 1. TRANG XÁC NHẬN THANH TOÁN (GET)
        // ==========================================
        [HttpGet]
        public ActionResult Checkout()
        {
            // Kiểm tra đăng nhập: Nếu chưa đăng nhập thì bắt buộc chuyển sang trang Login
            if (Session["UserID"] == null)
            {
                TempData["SuccessMessage"] = "Vui lòng đăng nhập để tiến hành thanh toán đơn hàng!";
                return RedirectToAction("Login", "Account");
            }

            // Kiểm tra giỏ hàng: Nếu rỗng thì đẩy về trang chủ
            var cart = Session["Cart"] as List<CartItem>;
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Categories = db.Categories.ToList();

            // Lấy thông tin khách hàng từ SQL để tự động điền vào Form thanh toán theo checklist
            int userId = Convert.ToInt32(Session["UserID"]);
            var user = db.Users.Find(userId);
            ViewBag.UserInfo = user;
            ViewBag.TotalAmount = cart.Sum(c => c.TotalPrice);

            return View(cart);
        }

        // ==========================================
        // 2. XỬ LÝ LƯU ĐƠN HÀNG & TRỪ TỒN KHO (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(string shippingAddress)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "Account");

            var cart = Session["Cart"] as List<CartItem>;
            if (cart == null || !cart.Any()) return RedirectToAction("Index", "Home");

            int userId = Convert.ToInt32(Session["UserID"]);

            // Bước A: Lưu thông tin chung vào bảng Orders
            Order newOrder = new Order
            {
                UserID = userId,
                OrderDate = DateTime.Now,
                TotalAmount = cart.Sum(c => c.TotalPrice),
                ShippingAddress = shippingAddress,
                Status = "Chờ xử lý"
            };
            db.Orders.Add(newOrder);
            db.SaveChanges(); // Lưu để lấy được mã OrderID tự động tăng

            // Bước B: Lưu từng món vào bảng OrderDetails và Cập nhật tồn kho bảng Products
            foreach (var item in cart)
            {
                OrderDetail detail = new OrderDetail
                {
                    OrderID = newOrder.OrderID,
                    ProductID = item.ProductID,
                    Quantity = item.Quantity,
                    Price = item.Price
                };
                db.OrderDetails.Add(detail);

                // Cập nhật số lượng tồn và số lượng đã bán (Điểm nâng cao Checklist)
                var dbProduct = db.Products.Find(item.ProductID);
                if (dbProduct != null)
                {
                    dbProduct.StockQuantity -= item.Quantity;
                    if (dbProduct.StockQuantity < 0) dbProduct.StockQuantity = 0;

                    dbProduct.SoldQuantity += item.Quantity;
                }
            }

            db.SaveChanges(); // Lưu toàn bộ chi tiết và cập nhật kho xuống SQL

            // Bước C: Xóa sạch giỏ hàng sau khi thanh toán thành công
            Session["Cart"] = null;
            Session["CartCount"] = 0;

            TempData["OrderSuccess"] = "Đặt hàng thành công! Cảm ơn bạn đã mua sắm tại Bách Hóa Xanh.";
            return RedirectToAction("OrderDetail", new { id = newOrder.OrderID });
        }

        // ==========================================
        // 3. LỊCH SỬ GIAO DỊCH CỦA KHÁCH HÀNG (Nâng cao)
        // ==========================================
        public ActionResult History()
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "Account");

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);

            // Chỉ lấy đúng danh sách đơn hàng của tài khoản đang đăng nhập
            var myOrders = db.Orders
                             .Where(o => o.UserID == userId)
                             .OrderByDescending(o => o.OrderDate)
                             .ToList();

            return View(myOrders);
        }

        // ==========================================
        // 4. CHI TIẾT HÓA ĐƠN MUA HÀNG (Nâng cao)
        // ==========================================
        public ActionResult OrderDetail(int? id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "Account");
            if (id == null) return RedirectToAction("History");

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);

            // Bảo mật: Kiểm tra đơn hàng phải thuộc về đúng người dùng đang đăng nhập
            var order = db.Orders.FirstOrDefault(o => o.OrderID == id && o.UserID == userId);
            if (order == null)
            {
                return HttpNotFound();
            }

            // Lấy danh sách món hàng trong hóa đơn này
            var details = db.OrderDetails.Where(d => d.OrderID == id).ToList();
            ViewBag.OrderDetails = details;

            return View(order);
        }
        // ==========================================
        // 5. HỦY ĐƠN HÀNG & HOÀN TRẢ TỒN KHO (CUSTOMER)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int id)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login", "Account");
            int userId = Convert.ToInt32(Session["UserID"]);

            // Tìm đúng đơn hàng của người dùng đang đăng nhập
            var order = db.Orders.FirstOrDefault(o => o.OrderID == id && o.UserID == userId);

            if (order != null && order.Status == "Chờ xử lý")
            {
                order.Status = "Khách hủy đơn"; // Đổi thành "Khách hủy đơn" để Admin dễ nhận biết

                // Hoàn trả lại số lượng tồn kho và giảm số lượng đã bán của từng món
                var details = db.OrderDetails.Where(d => d.OrderID == id).ToList();
                foreach (var item in details)
                {
                    var product = db.Products.Find(item.ProductID);
                    if (product != null)
                    {
                        product.StockQuantity += item.Quantity;
                        product.SoldQuantity -= item.Quantity;
                        if (product.SoldQuantity < 0) product.SoldQuantity = 0;
                    }
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = "Đã hủy đơn hàng #" + id + " và hoàn trả sản phẩm về kho thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể hủy đơn hàng này (đơn đã được xử lý hoặc đã hủy trước đó).";
            }

            return RedirectToAction("History");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}