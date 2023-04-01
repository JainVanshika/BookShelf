using BulkyBook.DataAccess.Repository;
using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModel;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Diagnostics;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        public  IUnitOfWork _unitOfWork { get; set; }
        [BindProperty]
        public OrderVM orderVM { get; set; }

        public OrderController(IUnitOfWork unitOfWork)
        {
            _unitOfWork= unitOfWork;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Details(int orderId)
        {
            orderVM = new OrderVM()
            {
                orderHeader = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == orderId, includeProperties: "ApplicationUser"),
                orderDetails = _unitOfWork.orderDetail.GetAll(u => u.OrderId == orderId, includeProperties: "Product"),
            };
            return View(orderVM);
        }
        [ActionName("Details")]
        [HttpPost]
        [ValidateAntiForgeryToken]
		public IActionResult Details_PAY_NOW(int orderId)
		{
            orderVM.orderHeader = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == orderVM.orderHeader.Id, includeProperties: "ApplicationUser");
            orderVM.orderDetails = _unitOfWork.orderDetail.GetAll(u => u.OrderId == orderVM.orderHeader.Id, includeProperties: "Product");

			//stripe setting
			var domain = "https://localhost:44309/";
			var options = new SessionCreateOptions
			{
				PaymentMethodTypes = new List<string>
				{
					"card",
				},
				LineItems = new List<SessionLineItemOptions>()
				,
				Mode = "payment",
				SuccessUrl = domain + $"admin/Order/PaymentConfirmation?orderHeaderId={orderVM.orderHeader.Id}",
				CancelUrl = domain + $"admin/Order/Index",
			};
			foreach (var item in orderVM.orderDetails)
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
			_unitOfWork.orderHeader.UpdateStripePaymentId(orderVM.orderHeader.Id, session.Id, session.PaymentIntentId);
			_unitOfWork.Save();

			Response.Headers.Add("Location", session.Url);
			return new StatusCodeResult(303);
			
		}

		public IActionResult PaymentConfirmation(int orderHeaderId)
		{
			OrderHeader orderHeader = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == orderHeaderId);
			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				var service = new SessionService();
				Session session = service.Get(orderHeader.SessionId);
				//check stripe status
				if (session.PaymentStatus.ToLower() == "paid")
				{
					_unitOfWork.orderHeader.UpdateStripePaymentId(orderHeaderId, session.Id, session.PaymentIntentId);
					_unitOfWork.orderHeader.UpdateStatus(orderHeaderId, orderHeader.OrderStatus, SD.PaymentStatusApproved);
					_unitOfWork.Save();
				}
			}
			return View(orderHeaderId);
		}
		[HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles =SD.Role_Admin+","+SD.Role_Employee)]
        public IActionResult UpdateOrderDetail()
        {
            var orderHeaderFromDb = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == orderVM.orderHeader.Id,tracked:false);
            orderHeaderFromDb.Name= orderVM.orderHeader.Name;
            orderHeaderFromDb.PhoneNumber= orderVM.orderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress= orderVM.orderHeader.StreetAddress;
            orderHeaderFromDb.City= orderVM.orderHeader.City;
            orderHeaderFromDb.State= orderVM.orderHeader.State;
            orderHeaderFromDb.PostalCode= orderVM.orderHeader.PostalCode;
            if(orderVM.orderHeader.Carrier!=null)
            {
                orderHeaderFromDb.Carrier= orderVM.orderHeader.Carrier;
            }
            if(orderVM.orderHeader.TrackingNumber!=null)
            {
                orderHeaderFromDb.TrackingNumber= orderVM.orderHeader.TrackingNumber;
            }
            _unitOfWork.orderHeader.Update(orderHeaderFromDb);
            _unitOfWork.Save();
            TempData["success"] = "order Details updated successfully.";
            return RedirectToAction("Details","Order",new {orderId=orderHeaderFromDb.Id});
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult StartProcessing()
        {
            _unitOfWork.orderHeader.UpdateStatus(orderVM.orderHeader.Id,SD.StatusInProcess);
            _unitOfWork.Save();
			TempData["success"] = "order status updated successfully.";
			return RedirectToAction("Details", "Order", new { orderId = orderVM.orderHeader.Id });

		}
        [HttpPost]
        [ValidateAntiForgeryToken]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult ShipOrder()
        {
            var orderHeader = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == orderVM.orderHeader.Id, tracked: false);
            orderHeader.TrackingNumber= orderVM.orderHeader.TrackingNumber;
            orderHeader.Carrier= orderVM.orderHeader.Carrier;
            orderHeader.ShippingDate = DateTime.Now;
            orderHeader.OrderStatus = SD.StatusShipped;
            if (orderHeader.OrderStatus == SD.PaymentStatusDelayedPayment)
            {
                orderHeader.PaymentDueDate=DateTime.Now.AddDays(30);
            }
            _unitOfWork.orderHeader.Update(orderHeader);
            _unitOfWork.Save();
			TempData["success"] = "order shipped successfully.";
			return RedirectToAction("Details", "Order", new { orderId = orderVM.orderHeader.Id });
		}
		[HttpPost]
		[ValidateAntiForgeryToken]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		public IActionResult CancelOrder()
		{
			var orderHeader = _unitOfWork.orderHeader.GetFirstOrDefault(u => u.Id == orderVM.orderHeader.Id, tracked: false);
            
            if (orderHeader.PaymentStatus == SD.PaymentStatusApproved) //if payment is already done
            {
                var option = new RefundCreateOptions
                {
                    Reason=RefundReasons.RequestedByCustomer,
                    PaymentIntent=orderHeader.PaymentIntentId
                };
                var service = new RefundService();
                Refund refund = service.Create(option);
				_unitOfWork.orderHeader.UpdateStatus(orderHeader.Id,SD.StatusCancelled,SD.StatusRefunded);
			}
            else //if payment is not done
            {
				_unitOfWork.orderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);

			}
			_unitOfWork.Save();
			TempData["success"] = "order cancelled successfully.";
			return RedirectToAction("Details", "Order", new { orderId = orderVM.orderHeader.Id });
		}
		#region API CALLS
		[HttpGet]
        public IActionResult GetAll(string status)
        {
            IEnumerable<OrderHeader> orderHeaders;
            if (User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee))
            {
                orderHeaders = _unitOfWork.orderHeader.GetAll(includeProperties: "ApplicationUser");
            }
            else
            {
                var claimsIdentity=(ClaimsIdentity)User.Identity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                orderHeaders = _unitOfWork.orderHeader.GetAll(u => u.ApplicationUserId == claim.Value, includeProperties: "ApplicationUser");
            }
            switch(status)
            {
                case "pending":
                    orderHeaders = orderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
                case "inprocess":
                    orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusInProcess);
                    break;
                case "completed":
                    orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusShipped);
                    break;
                case "approved":
                    orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusApproved);
                    break;
                default:
                    break;
            }
            
            return Json(new { data = orderHeaders });
        }
        #endregion
    }
}
