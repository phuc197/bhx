using bachhoaxanh.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace bachhoaxanh.Controllers
{
    public class CartController : Controller
    {
        private BachHoaXanhDBEntities db = new BachHoaXanhDBEntities();

        // Hàm lấy danh sách giỏ hàng hiện tại từ Session
        private List<CartItem> GetCart()
        {
            var cart = Session["Cart"] as List<CartItem>;
            if (cart == null)
            {
                cart = new List<CartItem>();
                Session["Cart"] = cart;
            }
            return cart;
        }

        // 1. HIỂN THỊ TRANG GIỎ HÀNG
        public ActionResult Index()
        {
            ViewBag.Categories = db.Categories.ToList();
            var cart = GetCart();

            // Cập nhật lại số lượng tồn kho mới nhất từ SQL phòng trường hợp kho vừa thay đổi
            foreach (var item in cart)
            {
                var dbProduct = db.Products.Find(item.ProductID);
                if (dbProduct != null)
                {
                    item.StockQuantity = dbProduct.StockQuantity;
                }
            }

            ViewBag.TotalAmount = cart.Sum(c => c.TotalPrice);
            Session["CartCount"] = cart.Sum(c => c.Quantity); // Lưu tổng số lượng để hiện trên Menu

            return View(cart);
        }

        // 2. THÊM SẢN PHẨM VÀO GIỎ (Nhận từ trang Home, Category hoặc Detail)
        public ActionResult AddToCart(int id, int quantity = 1)
        {
            var product = db.Products.Find(id);
            if (product == null || product.StockQuantity <= 0)
            {
                return RedirectToAction("Index", "Home");
            }

            var cart = GetCart();
            var existingItem = cart.FirstOrDefault(c => c.ProductID == id);

            if (existingItem != null)
            {
                // Kiểm tra cộng dồn không được vượt quá tồn kho
                int newQty = existingItem.Quantity + quantity;
                existingItem.Quantity = newQty > product.StockQuantity ? product.StockQuantity : newQty;
            }
            else
            {
                int validQty = quantity > product.StockQuantity ? product.StockQuantity : (quantity < 1 ? 1 : quantity);
                cart.Add(new CartItem
                {
                    ProductID = product.ProductID,
                    ProductName = product.ProductName,
                    ImageURL = product.ImageURL,
                    Unit = product.Unit,
                    Price = product.Price,
                    Quantity = validQty,
                    StockQuantity = product.StockQuantity
                });
            }

            Session["Cart"] = cart;
            Session["CartCount"] = cart.Sum(c => c.Quantity);

            // Checklist hỏi: Sau khi thêm hàng vào giỏ, chuyển tới trang giỏ hàng
            return RedirectToAction("Index");
        }

        // 3. CẬP NHẬT SỐ LƯỢNG TRONG GIỎ (Kiểm tra chặt chẽ < 0 và > tồn kho)
        [HttpPost]
        
        public ActionResult UpdateQuantity(int id, int quantity)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductID == id);

            if (item != null)
            {
                if (quantity < 1)
                {
                    // Ép về mức tối thiểu là 1 và gửi thông báo nhắc nhở
                    item.Quantity = 1;
                    TempData["CartWarning"] = "Số lượng mua tối thiểu là 1! Nếu muốn bỏ sản phẩm này, hãy bấm nút Xóa.";
                }
                else if (quantity > item.StockQuantity)
                {
                    item.Quantity = item.StockQuantity;
                    TempData["CartWarning"] = $"Sản phẩm '{item.ProductName}' chỉ còn tối đa {item.StockQuantity} {item.Unit} trong kho!";
                }
                else
                {
                    item.Quantity = quantity;
                }
            }

            Session["Cart"] = cart;
            Session["CartCount"] = cart.Sum(c => c.Quantity);

            return RedirectToAction("Index");
        }

        // 4. XÓA SẢN PHẨM KHỎI GIỎ HÀNG (Remove)
        public ActionResult RemoveFromCart(int id)
        {
            var cart = GetCart();
            var item = cart.FirstOrDefault(c => c.ProductID == id);

            if (item != null)
            {
                cart.Remove(item);
            }

            Session["Cart"] = cart;
            Session["CartCount"] = cart.Sum(c => c.Quantity);

            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}