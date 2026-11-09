using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using bachhoaxanh.Models; // Đảm bảo khớp namespace của dự án bạn

namespace bachhoaxanh.Controllers
{
    public class AdminProductController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // ==========================================
        // BẢO MẬT: Chặn khách hàng vãng lai gõ link lén vào trang quản trị
        // ==========================================
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
        // 1. DANH SÁCH HÀNG HÓA (Index)
        // ==========================================
        public ActionResult Index()
        {
            var products = db.Products.OrderByDescending(p => p.ProductID).ToList();
            return View(products);
        }

        // ==========================================
        // 2. THÊM SẢN PHẨM MỚI & UPLOAD ẢNH (GET & POST)
        // ==========================================
        [HttpGet]
        public ActionResult Create()
        {
            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Product product, HttpPostedFileBase ImageFile)
        {
            if (ModelState.IsValid)
            {
                if (ImageFile != null && ImageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(ImageFile.FileName)
                                    + "_" + DateTime.Now.ToString("yyyyMMddHHmmss")
                                    + Path.GetExtension(ImageFile.FileName);

                    string folderPath = Server.MapPath("~/Images/");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    string fullPath = Path.Combine(folderPath, fileName);
                    ImageFile.SaveAs(fullPath);
                    product.ImageURL = fileName;
                }

                product.SoldQuantity = 0; // Mặc định chưa bán sản phẩm nào
                product.IsActive = true;  // Mặc định đang mở bán

                db.Products.Add(product);
                db.SaveChanges();

                TempData["SuccessMessage"] = "Thêm sản phẩm mới và tải ảnh lên thành công!";
                return RedirectToAction("Index");
            }

            ViewBag.CategoryID = new SelectList(db.Categories, "CategoryID", "CategoryName", product.CategoryID);
            return View(product);
        }

        // ==========================================
        // 3. SỬA THÔNG TIN SẢN PHẨM & CẬP NHẬT ẢNH (GET & POST)
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

                dbProduct.ProductName = product.ProductName;
                dbProduct.CategoryID = product.CategoryID;
                dbProduct.Price = product.Price;
                dbProduct.StockQuantity = product.StockQuantity;
                dbProduct.Unit = product.Unit;
                dbProduct.Description = product.Description;
                dbProduct.IsActive = product.IsActive;

                // Nếu Admin chọn ảnh mới thì lưu ảnh mới, ngược lại giữ nguyên ảnh cũ
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
        // 4. XEM CHI TIẾT SẢN PHẨM (Dùng Partial View)
        // ==========================================
        public ActionResult Details(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var product = db.Products.Find(id);
            if (product == null) return HttpNotFound();

            return View(product);
        }

        // ==========================================
        // 5. XÓA SẢN PHẨM CÓ KIỂM TRA RÀNG BUỘC KHÓA NGOẠI
        // ==========================================
        public ActionResult Delete(int id)
        {
            var product = db.Products.Find(id);
            if (product != null)
            {
                bool isOrdered = db.OrderDetails.Any(od => od.ProductID == id);
                if (isOrdered)
                {
                    product.IsActive = false;
                    product.StockQuantity = 0;
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Sản phẩm đã có lịch sử mua hàng nên hệ thống chuyển sang Ngừng kinh doanh!";
                }
                else
                {
                    db.Products.Remove(product);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Đã xóa vĩnh viễn sản phẩm khỏi hệ thống!";
                }
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
