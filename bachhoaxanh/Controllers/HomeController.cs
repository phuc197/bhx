using bachhoaxanh.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace bachhoaxanh.Controllers
{
    public class HomeController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        public ActionResult Index()
        {
            // 1. Dùng LINQ lấy danh sách danh mục để hiển thị lên Menu
            var categories = db.Categories.ToList();
            ViewBag.Categories = categories; // Dùng ViewBag để chuyển sang Layout

            // 2. Dùng LINQ truy vấn sản phẩm: Chỉ lấy sản phẩm Đang kinh doanh (IsActive = true) 
            // và Số lượng tồn kho lớn hơn 0 (StockQuantity > 0) theo đúng Checklist
            var products = db.Products
                             .Where(p => p.IsActive == true && p.StockQuantity > 0)
                             .ToList();

            // Trả danh sách sản phẩm về cho giao diện hiển thị
            return View(products);
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