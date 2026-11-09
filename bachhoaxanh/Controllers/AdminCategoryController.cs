using System;
using System.Linq;
using System.Web.Mvc;
using bachhoaxanh.Models; 

namespace bachhoaxanh.Controllers
{
    public class AdminCategoryController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // Bảo mật: Chỉ cho phép tài khoản Admin truy cập
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Session["UserRole"] == null || Session["UserRole"].ToString() != "Admin")
            {
                filterContext.Result = new RedirectResult("/Account/Login");
                return;
            }
            base.OnActionExecuting(filterContext);
        }

        // 1. DANH SÁCH DANH MỤC (Kèm đếm tổng số sản phẩm trong từng loại)
        public ActionResult Index()
        {
            var categories = db.Categories.ToList();
            return View(categories);
        }

        // 2. THÊM DANH MỤC MỚI (GET & POST)
        [HttpGet]
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Tên danh mục không được để trống!");
                return View(category);
            }

            if (ModelState.IsValid)
            {
                db.Categories.Add(category);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Thêm danh mục mới thành công!";
                return RedirectToAction("Index");
            }
            return View(category);
        }

        // 3. SỬA DANH MỤC (GET & POST)
        [HttpGet]
        public ActionResult Edit(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var category = db.Categories.Find(id);
            if (category == null) return HttpNotFound();
            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                ModelState.AddModelError("CategoryName", "Tên danh mục không được để trống!");
                return View(category);
            }

            if (ModelState.IsValid)
            {
                var dbCat = db.Categories.Find(category.CategoryID);
                if (dbCat != null)
                {
                    dbCat.CategoryName = category.CategoryName;
                    dbCat.Description = category.Description;
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
                }
                return RedirectToAction("Index");
            }
            return View(category);
        }

        // 4. CHI TIẾT DANH MỤC (Hiển thị danh sách sản phẩm thuộc danh mục đó - Nâng cao)
        public ActionResult Details(int? id)
        {
            if (id == null) return RedirectToAction("Index");
            var category = db.Categories.Find(id);
            if (category == null) return HttpNotFound();

            ViewBag.ProductsInCategory = db.Products.Where(p => p.CategoryID == id).ToList();
            return View(category);
        }

        // 5. XÓA DANH MỤC (Kiểm tra ràng buộc khóa ngoại chống lỗi xóa nhầm)
        public ActionResult Delete(int id)
        {
            var category = db.Categories.Find(id);
            if (category != null)
            {
                // Kiểm tra xem danh mục này có đang chứa sản phẩm nào không
                bool hasProducts = db.Products.Any(p => p.CategoryID == id);
                if (hasProducts)
                {
                    TempData["ErrorMessage"] = "Không thể xóa danh mục '" + category.CategoryName + "' vì đang có sản phẩm thuộc danh mục này!";
                }
                else
                {
                    db.Categories.Remove(category);
                    db.SaveChanges();
                    TempData["SuccessMessage"] = "Đã xóa danh mục thành công!";
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
