using System;
using System.Linq;
using System.Web.Mvc;
using bachhoaxanh.Models; // Đảm bảo khớp namespace dự án của bạn

namespace bachhoaxanh.Controllers
{
    public class AdminManageController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // Bảo mật: Chặn khách hàng gõ link lén vào trang Admin
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Admin")
            {
                filterContext.Result = new RedirectResult("/Account/Login");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        // ==========================================
        // 1. QUẢN LÝ KHO HÀNG (Nâng cao: Chia 2 phần Tồn kho & Cần nhập)
        // ==========================================
        public ActionResult Inventory()
        {
            // Hàng tồn kho (Số lượng > 0)
            ViewBag.InStockProducts = db.Products.Where(p => p.StockQuantity > 0).OrderBy(p => p.StockQuantity).ToList();

            // Hàng cần nhập (Số lượng = 0)
            ViewBag.OutOfStockProducts = db.Products.Where(p => p.StockQuantity == 0).ToList();

            return View();
        }

        // ==========================================
        // 2. QUẢN LÝ ĐƠN HÀNG (Chỉ xem & Chi tiết dạng Hóa đơn)
        // ==========================================
        public ActionResult Orders()
        {
            var orders = db.Orders.OrderByDescending(o => o.OrderDate).ToList();
            return View(orders);
        }

        public ActionResult OrderDetails(int? id)
        {
            if (id == null) return RedirectToAction("Orders");
            var order = db.Orders.Find(id);
            if (order == null) return HttpNotFound();

            ViewBag.OrderDetails = db.OrderDetails.Where(d => d.OrderID == id).ToList();
            return View(order);
        }

        // Cập nhật trạng thái giao hàng (Chờ xử lý -> Đang giao -> Đã giao)
        [HttpPost]
        public ActionResult UpdateOrderStatus(int orderId, string status)
        {
            var order = db.Orders.Find(orderId);
            if (order != null)
            {
                order.Status = status;
                db.SaveChanges();
                TempData["SuccessMessage"] = "Đã cập nhật trạng thái đơn hàng #" + orderId + " thành: " + status;
            }
            return RedirectToAction("OrderDetails", new { id = orderId });
        }

        // ==========================================
        // 3. QUẢN LÝ KHÁCH HÀNG (Chỉ xem & Chi tiết nhúng Partial View đơn hàng)
        // ==========================================
        public ActionResult Customers()
        {
            var customers = db.Users.Where(u => u.Role == "Customer").OrderByDescending(u => u.UserID).ToList();
            return View(customers);
        }

        public ActionResult CustomerDetails(int? id)
        {
            if (id == null) return RedirectToAction("Customers");
            var customer = db.Users.FirstOrDefault(u => u.UserID == id && u.Role == "Customer");
            if (customer == null) return HttpNotFound();

            // Lấy danh sách đơn hàng của khách này để truyền vào Partial View
            ViewBag.CustomerOrders = db.Orders.Where(o => o.UserID == id).OrderByDescending(o => o.OrderDate).ToList();
            return View(customer);
        }
        // Xóa tài khoản khách hàng (Có kiểm tra ràng buộc khóa ngoại đơn hàng)
        public ActionResult DeleteCustomer(int id)
        {
            var customer = db.Users.FirstOrDefault(u => u.UserID == id && u.Role == "Customer");
            if (customer != null)
            {
                // Lấy toàn bộ đơn hàng của khách hàng này
                var customerOrders = db.Orders.Where(o => o.UserID == id).ToList();

                // Kiểm tra xem có đơn hàng nào hợp lệ (không phải đơn hủy) không
                bool hasValidOrders = customerOrders.Any(o => o.Status != "Khách hủy đơn" && o.Status != "Đã hủy");

                if (hasValidOrders)
                {
                    TempData["ErrorMessage"] = "Không thể xóa khách hàng '" + customer.FullName + "' vì đang có đơn hàng hợp lệ hoặc đang giao!";
                }
                else
                {
                    // Xóa lần lượt từ bảng con (OrderDetails) -> bảng cha (Orders) -> bảng Users để không bị lỗi khóa ngoại
                    foreach (var order in customerOrders)
                    {
                        var details = db.OrderDetails.Where(d => d.OrderID == order.OrderID).ToList();
                        db.OrderDetails.RemoveRange(details);
                    }
                    db.Orders.RemoveRange(customerOrders);
                    db.Users.Remove(customer);

                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Đã xóa sạch tài khoản phá hoại '" + customer.FullName + "' cùng toàn bộ lịch sử đơn hủy!";
                }
            }
            return RedirectToAction("Customers");
        }


        // ==========================================
        // 4. QUẢN LÝ NGƯỜI DÙNG QUẢN TRỊ (Admin: Cho phép Thêm/Sửa/Xóa)
        // ==========================================
        public ActionResult Admins()
        {
            var admins = db.Users.Where(u => u.Role == "Admin").ToList();
            return View(admins);
        }

        [HttpGet]
        public ActionResult CreateAdmin()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAdmin(User admin)
        {
            if (db.Users.Any(u => u.Email.ToLower() == admin.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Email quản trị này đã tồn tại!");
                return View(admin);
            }

            if (ModelState.IsValid)
            {
                admin.Role = "Admin"; // Gán cứng quyền Quản trị viên
                db.Users.Add(admin);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Thêm tài khoản Quản trị viên mới thành công!";
                return RedirectToAction("Admins");
            }
            return View(admin);
        }

        public ActionResult DeleteAdmin(int id)
        {
            int currentUserId = Convert.ToInt32(Session["UserID"]);
            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "Không thể tự xóa tài khoản Admin đang đăng nhập!";
                return RedirectToAction("Admins");
            }

            var admin = db.Users.FirstOrDefault(u => u.UserID == id && u.Role == "Admin");
            if (admin != null)
            {
                db.Users.Remove(admin);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Đã xóa tài khoản quản trị viên!";
            }
            return RedirectToAction("Admins");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
