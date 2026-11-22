using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using bachhoaxanh.Models;

namespace bachhoaxanh.Controllers
{
    public class OrderController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // ==========================================
        // 1. MỞ TRANG XÁC NHẬN ĐƠN HÀNG & THANH TOÁN (GET)
        // ==========================================
        [HttpGet]
        public ActionResult Checkout()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var cart = Session["Cart"] as List<CartItem>;
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);
            var user = db.Users.Find(userId);

            if (user != null)
            {
                ViewBag.UserPhone = user.Phone;
                ViewBag.UserAddress = user.Address;
            }

            return View(cart);
        }

        // ==========================================
        // 2. XỬ LÝ ĐẶT HÀNG, LƯU TRẠNG THÁI QR/COD & TRỪ TỒN KHO (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Checkout(string shippingAddress, string phone, string paymentMethod)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var cart = Session["Cart"] as List<CartItem>;
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            int userId = Convert.ToInt32(Session["UserID"]);
            var user = db.Users.Find(userId);

            // Cập nhật số điện thoại mới vào hồ sơ khách hàng nếu có thay đổi
            if (user != null && !string.IsNullOrWhiteSpace(phone))
            {
                user.Phone = phone.Trim();
            }

            // Phân biệt trạng thái ban đầu: QR -> "Chờ thanh toán QR", Tiền mặt -> "Chờ xử lý"
            string initialStatus = (paymentMethod == "QR") ? "Chờ thanh toán QR" : "Chờ xử lý";

            // Bước A: Lưu thông tin chung vào bảng Orders
            Order newOrder = new Order
            {
                UserID = userId,
                OrderDate = DateTime.Now,
                TotalAmount = cart.Sum(c => c.TotalPrice),
                ShippingAddress = shippingAddress,
                Status = initialStatus
            };
            db.Orders.Add(newOrder);
            db.SaveChanges();

            // Bước B: Lưu từng món vào bảng OrderDetails và Cập nhật tồn kho
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

                var dbProduct = db.Products.Find(item.ProductID);
                if (dbProduct != null)
                {
                    dbProduct.StockQuantity -= item.Quantity;
                    if (dbProduct.StockQuantity < 0) dbProduct.StockQuantity = 0;

                    dbProduct.SoldQuantity += item.Quantity;
                }
            }

            db.SaveChanges();

            // Bước C: Xóa sạch giỏ hàng sau khi đặt hàng thành công
            Session["Cart"] = null;
            Session["CartCount"] = 0;

            return RedirectToAction("OrderDetail", new { id = newOrder.OrderID });
        }

        // ==========================================
        // 3. LỊCH SỬ GIAO DỊCH CỦA KHÁCH HÀNG
        // ==========================================
        public ActionResult History()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);

            var myOrders = db.Orders
                             .Where(o => o.UserID == userId)
                             .OrderByDescending(o => o.OrderDate)
                             .ToList();

            return View(myOrders);
        }

        // ==========================================
        // 4. CHI TIẾT HÓA ĐƠN & MÃ QR THANH TOÁN
        // ==========================================
        public ActionResult OrderDetail(int? id)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }
            if (id == null)
            {
                return RedirectToAction("History");
            }

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);

            var order = db.Orders.FirstOrDefault(o => o.OrderID == id && o.UserID == userId);
            if (order == null)
            {
                return HttpNotFound();
            }

            ViewBag.OrderDetails = db.OrderDetails.Where(d => d.OrderID == id).ToList();
            return View(order);
        }

        // ==========================================
        // 5. API KIỂM TRA TRẠNG THÁI CHUYỂN KHOẢN TỰ ĐỘNG (POLLING 3S)
        // ==========================================
        [HttpGet]
        public JsonResult CheckPaymentStatus(int id)
        {
            var order = db.Orders.Find(id);
            if (order == null)
            {
                return Json(new { paid = false }, JsonRequestBehavior.AllowGet);
            }

            bool isPaid = (order.Status == "Đã thanh toán");
            return Json(new { paid = isPaid, status = order.Status }, JsonRequestBehavior.AllowGet);
        }
        // ==========================================
        // 6. CỔNG NHẬN TÍN HIỆU NGÂN HÀNG (WEBHOOK SEPAY / CASSO)
        // ==========================================
        [HttpPost]
        public JsonResult BankWebhook()
        {
            try
            {
                // Đọc toàn bộ chuỗi JSON thô mà SePay gửi sang
                Request.InputStream.Position = 0;
                string jsonBody = "";
                using (var reader = new System.IO.StreamReader(Request.InputStream))
                {
                    jsonBody = reader.ReadToEnd();
                }

                if (!string.IsNullOrEmpty(jsonBody))
                {
                    // Dùng JavaScriptSerializer có sẵn của .NET để giải mã JSON của SePay
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    var data = serializer.Deserialize<Dictionary<string, object>>(jsonBody);

                    string content = data.ContainsKey("content") && data["content"] != null ? data["content"].ToString().ToUpper() : "";
                    decimal transferAmount = 0;
                    if (data.ContainsKey("transferAmount") && data["transferAmount"] != null)
                    {
                        decimal.TryParse(data["transferAmount"].ToString(), out transferAmount);
                    }

                    // Kiểm tra xem giao dịch là tiền vào ("in") hay không (nếu có trường transferType)
                    string transferType = data.ContainsKey("transferType") && data["transferType"] != null ? data["transferType"].ToString().ToLower() : "in";

                    if (transferType == "in" && !string.IsNullOrEmpty(content))
                    {
                        var pendingOrders = db.Orders.Where(o => o.Status == "Chờ thanh toán QR").ToList();
                        foreach (var ord in pendingOrders)
                        {
                            string expectedCode = "BHX" + ord.OrderID;
                            if (content.Contains(expectedCode) && transferAmount >= ord.TotalAmount)
                            {
                                ord.Status = "Đã thanh toán";
                                db.SaveChanges();
                                return Json(new { success = true, orderId = ord.OrderID });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }

            return Json(new { success = false });
        }

        // ==========================================
        // 7. KHÁCH HÀNG TỰ HỦY ĐƠN & HOÀN TRẢ TỒN KHO
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int id)
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = Convert.ToInt32(Session["UserID"]);
            var order = db.Orders.FirstOrDefault(o => o.OrderID == id && o.UserID == userId);

            if (order != null && (order.Status == "Chờ xử lý" || order.Status == "Chờ thanh toán QR"))
            {
                order.Status = "Khách hủy đơn";

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
                TempData["ErrorMessage"] = "Không thể hủy đơn hàng này.";
            }

            return RedirectToAction("History");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
