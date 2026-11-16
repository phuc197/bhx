using bachhoaxanh.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace bachhoaxanh.Controllers
{
    public class AdminController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // 1. DANH SÁCH SẢN PHẨM (Index: Chỉ hiện 4-5 cột chính theo checklist)
        public ActionResult Index()
        {
            var products = db.Products.OrderByDescending(p => p.ProductID).ToList();
            return View(products);
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Admin")
            {
                filterContext.Result = new RedirectResult("/Account/Login");
                return;
            }
            base.OnActionExecuting(filterContext);
        }
        // 2. FORM THÊM SẢN PHẨM MỚI (GET)
        [HttpGet]
        public ActionResult Create()
        {
            // Truyền danh sách Danh mục để làm ô chọn Dropdown
            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName");
            return View();
        }
             public ActionResult Dashboard()
        {
            ViewBag.TotalProducts = db.Products.Count();
            ViewBag.TotalOrders = db.Orders.Count();
            ViewBag.TotalCustomers = db.Users.Count(u => u.Role == "Customer");
            ViewBag.TotalRevenue = db.Orders.Any() ? db.Orders.Sum(o => o.TotalAmount) : 0;

            return View();
        }
        // 3. XỬ LÝ LƯU SẢN PHẨM & UPLOAD ẢNH (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Product product, HttpPostedFileBase ImageFile)
        {
            if (ModelState.IsValid)
            {
                // Xử lý Upload ảnh từ máy tính vào thư mục ~/Images/
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    // Tạo tên file duy nhất để tránh bị trùng tên ảnh cũ
                    string fileName = Path.GetFileNameWithoutExtension(ImageFile.FileName)
                                    + "_" + DateTime.Now.ToString("yyyyMMddHHmmss")
                                    + Path.GetExtension(ImageFile.FileName);

                    string path = Path.Combine(Server.MapPath("~/Images/"), fileName);

                    // Tạo thư mục Images nếu chưa tồn tại
                    Directory.CreateDirectory(Server.MapPath("~/Images/"));

                    // Lưu file ảnh vật lý vào thư mục
                    ImageFile.SaveAs(path);

                    // Lưu tên file vào cột ImageURL trong SQL
                    product.ImageURL = fileName;
                }

                product.SoldQuantity = 0; // Sản phẩm mới chưa bán cái nào
                product.IsActive = true;  // Mặc định đang kinh doanh

                db.Products.Add(product);
                db.SaveChanges();

                TempData["AdminMessage"] = "Thêm sản phẩm bách hóa mới thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }
        // ==========================================
        // 4. SỬA THÔNG TIN SẢN PHẨM & CẬP NHẬT ẢNH (GET & POST)
        // ==========================================
        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();

            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Product product, HttpPostedFileBase ImageFile)
        {
            if (ModelState.IsValid)
            {
                var dbProduct = db.Products.Find(product.ProductID);
                if (dbProduct == null) return HttpNotFound();

                // Cập nhật các thông tin cơ bản
                dbProduct.ProductName = product.ProductName;
                dbProduct.CategoryID = product.CategoryID;
                dbProduct.Price = product.Price;
                dbProduct.StockQuantity = product.StockQuantity;
                dbProduct.Unit = product.Unit;
                dbProduct.Description = product.Description;
                dbProduct.IsActive = product.IsActive;

                // Nếu Admin có chọn tải lên ảnh mới thì lưu ảnh mới, ngược lại giữ nguyên ảnh cũ
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(ImageFile.FileName)
                                    + "_" + DateTime.Now.ToString("yyyyMMddHHmmss")
                                    + Path.GetExtension(ImageFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Images/"), fileName);
                    ImageFile.SaveAs(path);
                    dbProduct.ImageURL = fileName;
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật thông tin sản phẩm thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }

        // ==========================================
        // 5. XEM CHI TIẾT SẢN PHẨM (Dùng Partial View ăn điểm nâng cao)
        // ==========================================
        public ActionResult Details(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();

            return View(product);
        }

        // ==========================================
        // 6. XÓA SẢN PHẨM CÓ KIỂM TRA RÀNG BUỘC KHÓA NGOẠI
        // ==========================================
        public ActionResult Delete(int id)
        {
            var product = db.Products.Find(id);
            if (product != null)
            {
                // 1. Xóa tất cả các dòng chi tiết hóa đơn cũ có dính tới sản phẩm này để không bị lỗi khóa ngoại
                var oldOrderDetails = db.OrderDetails.Where(od => od.ProductID == id).ToList();
                if (oldOrderDetails.Any())
                {
                    db.OrderDetails.RemoveRange(oldOrderDetails);
                }

                // 2. Xóa vĩnh viễn sản phẩm khỏi bảng Products
                db.Products.Remove(product);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Đã xóa vĩnh viễn sản phẩm khỏi hệ thống!";
            }
            return RedirectToAction("Index");
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}