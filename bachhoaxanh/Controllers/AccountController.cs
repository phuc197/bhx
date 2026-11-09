using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using bachhoaxanh.Models;
using System.Text.RegularExpressions;


namespace BachHoaXanhDB.Controllers
{
    public class AccountController : Controller
    {
        // Khởi tạo đối tượng kết nối SQL Server của Entity Framework Db First
        // Hãy thay "BachHoaXanhDBEntities" bằng tên thật trong file Model .edmx của bạn
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // ==========================================
        // 1. ĐĂNG KÝ TÀI KHOẢN (CUSTOMER)
        // ==========================================
        [HttpGet]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Chống hack dập form liên tục
        public ActionResult Register(User user)
        {
            // 1. Kiểm tra Họ tên & Địa chỉ không được trống hoặc bắt đầu bằng khoảng trắng
            if (string.IsNullOrWhiteSpace(user.FullName) || user.FullName.StartsWith(" "))
            {
                ModelState.AddModelError("FullName", "Họ tên không được để trống hoặc bắt đầu bằng khoảng trắng!");
            }

            if (string.IsNullOrWhiteSpace(user.Address) || user.Address.StartsWith(" "))
            {
                ModelState.AddModelError("Address", "Địa chỉ không được để trống hoặc bắt đầu bằng khoảng trắng!");
            }

            // 2. Kiểm tra Email phải bắt đầu bằng chữ cái (a-z, A-Z)
            if (string.IsNullOrEmpty(user.Email) || !System.Text.RegularExpressions.Regex.IsMatch(user.Email, @"^[a-zA-Z][a-zA-Z0-9._%+-]*@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"))
            {
                ModelState.AddModelError("Email", "Địa chỉ Email phải bắt đầu bằng chữ cái và đúng định dạng!");
            }

            // 3. Kiểm tra Mật khẩu từ 6 ký tự và không bắt đầu/kết thúc bằng khoảng trắng
            if (string.IsNullOrEmpty(user.PasswordHash) || user.PasswordHash.Length < 6 || user.PasswordHash.StartsWith(" ") || user.PasswordHash.EndsWith(" "))
            {
                ModelState.AddModelError("PasswordHash", "Mật khẩu phải từ 6 ký tự trở lên và không được có khoảng trắng ở đầu!");
            }

            // 4. Kiểm tra Số điện thoại phải bắt đầu bằng số 0 và có đúng 10 chữ số
            if (string.IsNullOrEmpty(user.Phone) || !System.Text.RegularExpressions.Regex.IsMatch(user.Phone, @"^0[0-9]{9}$"))
            {
                ModelState.AddModelError("Phone", "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số!");
            }

            // 5. Kiểm tra trùng Email trong SQL Server
            if (!string.IsNullOrEmpty(user.Email) && db.Users.Any(u => u.Email.ToLower() == user.Email.ToLower()))
            {
                ModelState.AddModelError("Email", "Địa chỉ Email này đã được đăng ký trong hệ thống!");
            }

            if (ModelState.IsValid)
            {
                user.FullName = user.FullName.Trim();
                user.Address = user.Address.Trim();
                user.Role = "Customer";

                db.Users.Add(user);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Đăng ký tài khoản Bách Hóa Xanh thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }

            return View(user);
        }

        // ==========================================
        // 2. ĐĂNG NHẬP (CUSTOMER)
        // ==========================================
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng điền đầy đủ thông tin đăng nhập.";
                return View();
            }

            // Truy vấn kiểm tra tài khoản bằng LINQ xuống SQL Server
            var user = db.Users.FirstOrDefault(u => u.Email == email && u.PasswordHash == password);

            if (user != null)
            {
                // Đăng nhập thành công: Lưu thông tin vào Session chuẩn .NET Framework
                Session["UserID"] = user.UserID;
                Session["UserEmail"] = user.Email;
                Session["UserFullName"] = user.FullName;
                Session["UserRole"] = user.Role;

                // Phân quyền: Nếu là Admin thì bế thẳng vào trang quản trị
                // Phân quyền: Nếu là Admin thì chuyển thẳng vào trang Quản lý hàng hóa Admin
                if (user.Role == "Admin")
                {
                    return RedirectToAction("Index", "AdminProduct");
                }

                // Nếu là Khách hàng thì quay lại trang chủ mua hàng bách hóa
                return RedirectToAction("Index", "Home");
            }

            // Đăng nhập thất bại, có hiển thị thông báo theo checklist
            ViewBag.Error = "Tài khoản hoặc Mật khẩu bách hóa không chính xác!";
            return View();
        }

        // ==========================================
        // 3. ĐĂNG XUẤT TÀI KHOẢN
        // ==========================================
        public ActionResult Logout()
        {
            Session.Clear(); // Xóa sạch bộ nhớ Session của tài khoản
            Session.Abandon();
            return RedirectToAction("Index", "Home");
        }
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email, string phone, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ Email và Số điện thoại đã đăng ký!";
                return View();
            }

            // Kiểm tra định dạng mật khẩu mới giống quy tắc lúc Đăng ký
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6 || newPassword.StartsWith(" ") || newPassword.EndsWith(" "))
            {
                ViewBag.Error = "Mật khẩu mới phải từ 6 ký tự trở lên và không được có khoảng trắng ở đầu hoặc cuối!";
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp với mật khẩu mới!";
                return View();
            }

            // Xác thực chính chủ: Tìm tài khoản khớp đồng thời cả Email và Số điện thoại
            var user = db.Users.FirstOrDefault(u => u.Email.ToLower() == email.Trim().ToLower() && u.Phone == phone.Trim());

            if (user == null)
            {
                ViewBag.Error = "Email hoặc Số điện thoại không chính xác, không tìm thấy tài khoản!";
                return View();
            }

            // Cập nhật mật khẩu mới xuống SQL Server
            user.PasswordHash = newPassword;
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Vui lòng đăng nhập bằng mật khẩu mới.";
            return RedirectToAction("Login");
        }
        [HttpGet]
        public ActionResult Profile()
        {
            if (Session["UserID"] == null)
            {
                return RedirectToAction("Login");
            }

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);
            var user = db.Users.Find(userId);

            if (user == null) return HttpNotFound();

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(string fullName, string phone, string address)
        {
            if (Session["UserID"] == null) return RedirectToAction("Login");

            ViewBag.Categories = db.Categories.ToList();
            int userId = Convert.ToInt32(Session["UserID"]);
            var user = db.Users.Find(userId);

            if (user == null) return HttpNotFound();

            // Kiểm tra dữ liệu hợp lệ (Giống quy tắc lúc Đăng ký)
            if (string.IsNullOrWhiteSpace(fullName) || fullName.StartsWith(" "))
            {
                ViewBag.Error = "Họ tên không được để trống hoặc bắt đầu bằng khoảng trắng!";
                return View(user);
            }

            if (string.IsNullOrEmpty(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^0[0-9]{9}$"))
            {
                ViewBag.Error = "Số điện thoại phải bắt đầu bằng số 0 và gồm đúng 10 chữ số!";
                return View(user);
            }

            if (string.IsNullOrWhiteSpace(address) || address.StartsWith(" "))
            {
                ViewBag.Error = "Địa chỉ giao hàng không được để trống hoặc bắt đầu bằng khoảng trắng!";
                return View(user);
            }

            // Lưu thông tin mới xuống SQL Server và cập nhật lại tên trên thanh menu
            user.FullName = fullName.Trim();
            user.Phone = phone.Trim();
            user.Address = address.Trim();
            db.SaveChanges();

            Session["UserFullName"] = user.FullName;
            TempData["ProfileSuccess"] = "Cập nhật thông tin hồ sơ cá nhân thành công!";

            return RedirectToAction("Profile");
        }
        // Giải phóng bộ nhớ kết nối SQL Server khi dùng xong
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
