using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IUnitOfWork _unitOfWork;
        public HomeController(ILogger<HomeController> logger,IUnitOfWork unitOfWork)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> productList = _unitOfWork.product.GetAll(includeProperties:"Category,CoverType");
            return View(productList);
        }
        //this will display the details of book+number of item he/she want to add in cart
        public IActionResult Details(int productid)
        {
            ShoppingCart cartobj = new()
            {
                Count = 1,
                ProductId= productid,
                Product = _unitOfWork.product.GetFirstOrDefault(u => u.Id == productid, includeProperties: "Category,CoverType"),
            };
            return View(cartobj);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public IActionResult Details(ShoppingCart shoppingCart)
        {
            var claimIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimIdentity.FindFirst(ClaimTypes.NameIdentifier);
            shoppingCart.ApplicationUserId = claim.Value;

            ShoppingCart cartFromDb = _unitOfWork.shoppingCart.GetFirstOrDefault(u => u.ApplicationUserId==claim.Value && u.ProductId == shoppingCart.ProductId);
            if(cartFromDb == null)
            {
                _unitOfWork.shoppingCart.Add(shoppingCart);
            }
            else
            {
                /*if(cartFromDb.Count>shoppingCart.Count)
                {
                    _unitOfWork.shoppingCart.DecrementCount(cartFromDb, shoppingCart.Count);
                }
                else
                {
                    _unitOfWork.shoppingCart.IncrementCount(cartFromDb, shoppingCart.Count);

                }*/
                _unitOfWork.shoppingCart.IncrementCount(cartFromDb, shoppingCart.Count);
            }

            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}