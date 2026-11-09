using bachhoaxanh.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace bachhoaxanh.Controllers
{
    public class ProductController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // ==========================================
        // 1. PRODUCT CATEGORY: Lọc danh mục, Tìm kiếm, Lọc giá & Phân trang
        // ==========================================
        public ActionResult Category(int? id, string keyword, string priceFilter, int page = 1)
        {
            // Truyền danh sách danh mục lên Menu Layout
            ViewBag.Categories = db.Categories.ToList();

            // Khởi tạo câu truy vấn LINQ: Bắt buộc chỉ lấy hàng đang bán và tồn kho > 0
            var query = db.Products.Where(p => p.IsActive == true && p.StockQuantity > 0);

            // Lọc theo Danh mục (nếu người dùng bấm vào menu danh mục)
            if (id != null)
            {
                query = query.Where(p => p.CategoryID == id);
                var cat = db.Categories.Find(id);
                ViewBag.CategoryName = cat != null ? cat.CategoryName : "Danh mục sản phẩm";
                ViewBag.CurrentCategoryId = id;
            }
            else
            {
                ViewBag.CategoryName = "Tất cả sản phẩm Bách Hóa";
            }

            // Lọc theo Từ khóa tìm kiếm
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p => p.ProductName.Contains(keyword));
                ViewBag.Keyword = keyword;
            }

            // Lọc theo tiêu chí Mức giá (Đáp ứng mục lọc theo các tiêu chí lọc)
            if (!string.IsNullOrEmpty(priceFilter))
            {
                ViewBag.PriceFilter = priceFilter;
                switch (priceFilter)
                {
                    case "duoi-50k":
                        query = query.Where(p => p.Price < 50000);
                        break;
                    case "50k-150k":
                        query = query.Where(p => p.Price >= 50000 && p.Price <= 150000);
                        break;
                    case "tren-150k":
                        query = query.Where(p => p.Price > 150000);
                        break;
                }
            }

            // Sắp xếp sản phẩm mới nhất lên đầu
            query = query.OrderByDescending(p => p.ProductID);

            // Xử lý Phân trang thủ công bằng LINQ (Mỗi trang hiện 8 sản phẩm)
            int pageSize = 8;
            int totalItems = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var products = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(products);
        }

        // Đường dẫn phụ hỗ trợ thanh tìm kiếm trên _MasterLayout (/Product/Search?keyword=...)
        public ActionResult Search(string keyword)
        {
            return RedirectToAction("Category", new { keyword = keyword });
        }

        // ==========================================
        // 2. PRODUCT DETAIL: Xem chi tiết sản phẩm
        // ==========================================
        public ActionResult Detail(int? id)
        {
            if (id == null) return RedirectToAction("Index", "Home");

            ViewBag.Categories = db.Categories.ToList();

            var product = db.Products.Find(id);
            if (product == null || product.IsActive == false)
            {
                return HttpNotFound();
            }

            return View(product);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}