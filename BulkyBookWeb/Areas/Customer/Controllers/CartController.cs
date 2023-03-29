using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModel;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe.Checkout;
using System.Drawing.Text;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public int OrderTotal { get; set; }
        public CartController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public IActionResult Index()
        {
            var claimsIdentiy = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentiy.FindFirst(ClaimTypes.NameIdentifier);
            ShoppingCartVM = new ShoppingCartVM()
            {
                ListCart=_unitOfWork.shoppingCart.GetAll(u=>u.ApplicationUserId==claim.Value,includeProperties: "Product"),
                OrderHeader=new()
            };
            foreach(var cart in ShoppingCartVM.ListCart)
            {
                cart.Price = GetPricedBasedQuantity(cart.Count, cart.Product.Price, cart.Product.Price50, cart.Product.Price100);
                ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
            }

            return View(ShoppingCartVM);
        }
        public IActionResult Summary()
        {
			var claimsIdentiy = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentiy.FindFirst(ClaimTypes.NameIdentifier);
			ShoppingCartVM = new ShoppingCartVM()
			{
				ListCart = _unitOfWork.shoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product"),
				OrderHeader = new()
			};
            ShoppingCartVM.OrderHeader.ApplicationUser = _unitOfWork.applicationUser.GetFirstOrDefault(u => u.Id == claim.Value);

            ShoppingCartVM.OrderHeader.Name = ShoppingCartVM.OrderHeader.ApplicationUser.Name;
            ShoppingCartVM.OrderHeader.PhoneNumber = ShoppingCartVM.OrderHeader.ApplicationUser.PhoneNumber;
			ShoppingCartVM.OrderHeader.StreetAddress = ShoppingCartVM.OrderHeader.ApplicationUser.streetAddress;
			ShoppingCartVM.OrderHeader.City = ShoppingCartVM.OrderHeader.ApplicationUser.City;
			ShoppingCartVM.OrderHeader.State = ShoppingCartVM.OrderHeader.ApplicationUser.State;
			ShoppingCartVM.OrderHeader.PostalCode = ShoppingCartVM.OrderHeader.ApplicationUser.PostalCode;


			foreach (var cart in ShoppingCartVM.ListCart)
			{
				cart.Price = GetPricedBasedQuantity(cart.Count, cart.Product.Price, cart.Product.Price50, cart.Product.Price100);
				ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}
			return View(ShoppingCartVM);
        }
        [HttpPost]
        [ActionName("Summary")]
        [ValidateAntiForgeryToken]
		public IActionResult SummaryPOST(ShoppingCartVM ShoppingCartVM)
		{
			var claimsIdentiy = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentiy.FindFirst(ClaimTypes.NameIdentifier);
            ShoppingCartVM.ListCart = _unitOfWork.shoppingCart.GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "Product");

            ShoppingCartVM.OrderHeader.PaymentStatus = SD.PaymentStatusPending;
            ShoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            ShoppingCartVM.OrderHeader.OrderDate=System.DateTime.Now;
            ShoppingCartVM.OrderHeader.ApplicationUserId = claim.Value;

			foreach (var cart in ShoppingCartVM.ListCart)
			{
				cart.Price = GetPricedBasedQuantity(cart.Count, cart.Product.Price, cart.Product.Price50, cart.Product.Price100);
				ShoppingCartVM.OrderHeader.OrderTotal += (cart.Price * cart.Count);
			}
            _unitOfWork.orderHeader.Add(ShoppingCartVM.OrderHeader);
            _unitOfWork.Save();
            foreach(var cart in ShoppingCartVM.ListCart)
            {
                OrderDetail orderDetail = new()
                {
                    ProductId=cart.ProductId,
                    OrderId=ShoppingCartVM.OrderHeader.Id,
                    Price=cart.Price,
                    Count=cart.Count,
                };
                _unitOfWork.orderDetail.Add(orderDetail);
                _unitOfWork.Save();
            }

            //stripe setting
            var domain = "https://localhost:44309/";
			var options = new SessionCreateOptions
			{
                PaymentMethodTypes=new List<string>
                {
                    "card",
                },
				LineItems = new List<SessionLineItemOptions>()
		        ,
				Mode = "payment",
				SuccessUrl = domain+$"customer/cart/OrderConfirmation?id={ShoppingCartVM.OrderHeader.Id}",
				CancelUrl = domain+$"customer/cart/Index",
			};
            foreach(var item in ShoppingCartVM.ListCart)
            {

                var sessionLineItem = new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Price * 100),
                        Currency = "inr",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title,
                        },
                    },
                    Quantity = item.Count,
                };
                options.LineItems.Add(sessionLineItem);
		
			}
			var service = new SessionService();
			Session session = service.Create(options);
            _unitOfWork.orderHeader.UpdateStripePaymentId(ShoppingCartVM.OrderHeader.Id,session.Id,session.PaymentIntentId);
            _unitOfWork.Save();

			Response.Headers.Add("Location", session.Url);
			return new StatusCodeResult(303);


/*			_unitOfWork.shoppingCart.RemoveRange(ShoppingCartVM.ListCart);
            _unitOfWork.Save();
            return RedirectToAction("Index", "Home");*/
		}
        public IActionResult OrderConfirmation(int id) 
        {
            OrderHeader orderHeader = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == id);
			var service = new SessionService();
			Session session = service.Get(orderHeader.SessionId);
            //check stripe status
            if (session.PaymentStatus.ToLower()=="paid") 
            {
                _unitOfWork.orderHeader.UpdateStatus(id,SD.StatusApproved,SD.PaymentStatusApproved);
                _unitOfWork.Save();
            }
            List<ShoppingCart> shoppingCarts=_unitOfWork.shoppingCart.GetAll(u=>u.ApplicationUserId==orderHeader.ApplicationUserId).ToList();
			_unitOfWork.shoppingCart.RemoveRange(shoppingCarts);
			_unitOfWork.Save();
            return View(id);
		}


		public IActionResult plus(int cartId) 
        {
            var cart = _unitOfWork.shoppingCart.GetFirstOrDefault(u => u.Id == cartId);
            _unitOfWork.shoppingCart.IncrementCount(cart, 1);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult minus(int cartId)
        {
            
            var cart=_unitOfWork.shoppingCart.GetFirstOrDefault(u=>u.Id== cartId);
            if(cartId!=null)
            {
				_unitOfWork.shoppingCart.DecrementCount(cart, 1);
				if (cart.Count <= 0)
				{
					return remove(cartId);
				}
			}

            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult remove(int cartId)
        {
            var cart=_unitOfWork.shoppingCart.GetFirstOrDefault(u=>u.Id==cartId);
            _unitOfWork.shoppingCart.Remove(cart);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));

        }


        private double GetPricedBasedQuantity(double quantity,double price, double price50, double price100)
        {
            if(quantity<=50)
            {
                return price;
            }
            else
            {
                if(quantity<=100)
                {
                    return price50;
                }
                return price100;
            }
        }
    }
}
