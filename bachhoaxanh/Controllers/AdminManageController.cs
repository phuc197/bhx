using System;
using System.Linq;
using System.Web.Mvc;
using bachhoaxanh.Models; 

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


        // ==========================================
        // CẬP NHẬT TRẠNG THÁI ĐƠN HÀNG & XỬ LÝ LÝ DO HỦY (POST)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateOrderStatus(int orderId, string status, string cancelReason)
        {
            // 1. Kiểm tra quyền Quản trị viên
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Admin")
            {
                return RedirectToAction("Login", "Account");
            }

            // 2. Tìm đơn hàng trong SQL Server
            var order = db.Orders.Find(orderId);
            if (order == null)
            {
                return HttpNotFound();
            }

            string oldStatus = order.Status ?? "";

            // 3. Xử lý trường hợp Admin chọn "Khác" (Hủy đơn kèm lý do)
            if (status == "Khác" || status == "Admin hủy đơn")
            {
                string cleanReason = string.IsNullOrWhiteSpace(cancelReason)
                    ? "Cửa hàng tạm hết hàng"
                    : cancelReason.Trim();

                order.Status = "Đã hủy do: " + cleanReason;

                // Hoàn trả tồn kho nếu đơn này trước đó chưa từng bị hủy
                bool wasAlreadyCancelled = oldStatus.StartsWith("Đã hủy")
                                           || oldStatus == "Khách hủy đơn"
                                           || oldStatus == "Khác";

                if (!wasAlreadyCancelled)
                {
                    var details = db.OrderDetails.Where(d => d.OrderID == orderId).ToList();
                    foreach (var item in details)
                    {
                        var product = db.Products.Find(item.ProductID);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.SoldQuantity -= item.Quantity;
                            if (product.SoldQuantity < 0)
                            {
                                product.SoldQuantity = 0;
                            }
                        }
                    }
                }
            }
            else
            {
                // 4. Các trạng thái bình thường (Chờ xử lý, Đã thanh toán, Đang giao hàng, Đã giao thành công)
                order.Status = status;
            }

            // 5. Lưu thay đổi vào Database và quay lại trang chi tiết hóa đơn
            db.SaveChanges();
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái đơn hàng #" + orderId + " thành: " + order.Status;

            // Chuyển hướng chuẩn xác về lại trang Chi tiết hóa đơn Admin (không bao giờ bị lỗi 404)
            return RedirectToAction("Orders");

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
            // 1. Kiểm tra Họ và tên phải bắt đầu bằng chữ cái (tối thiểu 2 ký tự)
            if (string.IsNullOrWhiteSpace(admin.FullName) || !System.Text.RegularExpressions.Regex.IsMatch(admin.FullName.Trim(), @"^[\p{L}][\p{L}\s]{1,49}$"))
            {
                ViewBag.Error = "Họ và tên không hợp lệ! Tên phải bắt đầu bằng chữ cái và tối thiểu 2 ký tự.";
                return View(admin);
            }

            // 2. Kiểm tra Email hợp lệ và chưa tồn tại
            if (string.IsNullOrWhiteSpace(admin.Email) || !System.Text.RegularExpressions.Regex.IsMatch(admin.Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                ViewBag.Error = "Địa chỉ Email đăng nhập không đúng định dạng!";
                return View(admin);
            }

            string cleanEmail = admin.Email.Trim().ToLower();
            if (db.Users.Any(u => u.Email.ToLower() == cleanEmail))
            {
                ViewBag.Error = "Email quản trị này đã tồn tại!";
                ModelState.AddModelError("Email", "Email quản trị này đã tồn tại!");
                return View(admin);
            }

            // 3. Kiểm tra Mật khẩu tối thiểu 6 ký tự và tuyệt đối không có dấu cách
            if (string.IsNullOrEmpty(admin.PasswordHash) || admin.PasswordHash.Length < 6 || admin.PasswordHash.Any(char.IsWhiteSpace))
            {
                ViewBag.Error = "Mật khẩu phải có độ dài tối thiểu 6 ký tự và không được chứa dấu cách!";
                return View(admin);
            }

            // 4. Kiểm tra Số điện thoại đúng 10 số, bắt đầu bằng duy nhất 1 số 0
            if (string.IsNullOrWhiteSpace(admin.Phone) || !System.Text.RegularExpressions.Regex.IsMatch(admin.Phone.Trim(), @"^0[1-9][0-9]{8}$"))
            {
                ViewBag.Error = "Số điện thoại phải bắt đầu bằng duy nhất 1 số 0 và gồm đúng 10 chữ số (VD: 0912345678)!";
                return View(admin);
            }

            // 5. Lưu tài khoản Admin mới xuống SQL Server và quay về trang Admins
            admin.FullName = admin.FullName.Trim();
            admin.Email = cleanEmail;
            admin.Phone = admin.Phone.Trim();
            admin.Role = "Admin";

            db.Users.Add(admin);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Thêm tài khoản Quản trị viên mới thành công!";
            return RedirectToAction("Admins");
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
