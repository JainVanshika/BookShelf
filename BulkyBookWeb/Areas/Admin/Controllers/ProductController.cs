using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using BulkyBook.Models.ViewModel;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
	[Authorize(Roles = SD.Role_Admin)]

	public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _hostEnvironment;
        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment hostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _hostEnvironment = hostEnvironment;
        }

        public IActionResult Index()
        {
            IEnumerable<Product> objProductList = _unitOfWork.product.GetAll(includeProperties:"Category,CoverType");

            return View(objProductList); /*objProductList*/
        }
        
        //upsert = create + edit action method (update+insert)
        public IActionResult Upsert(int? id)
        {
            //tightly binded view
            ProductVM productVM = new()
            {
                Product=new(),
                CategoryList=_unitOfWork.category.GetAll().Select(u=>new SelectListItem
                {
                    Text=u.Name,
                    Value=u.Id.ToString(),
                }),
                CoverTypeList=_unitOfWork.coverType.GetAll().Select(u=>new SelectListItem
                {
                    Text=u.Name,
                    Value=u.Id.ToString(),
                }),
            };
            if(id==null || id==0)
            {
                //create product
/*                ViewBag.CategoryList = CategoryList; //view bag
                ViewData["CoverTypeList"] = CoverTypeList;//view Data*/
                return View(productVM);
            }
            else 
            {
               //update product
               productVM.Product= _unitOfWork.product.GetFirstOrDefault(u=>u.Id==id);
                return View(productVM);

            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(ProductVM obj,IFormFile? file)
        {
            if(ModelState.IsValid)
            {
                string wwwRootPath = _hostEnvironment.WebRootPath;//storing www.rootpath
                if (file!=null)
                {
                    string fileName=Guid.NewGuid().ToString();  //generating new file name
                    var uploads=Path.Combine(wwwRootPath, @"Images\products"); //final location where file needs to be uploaded
                    var extension = Path.GetExtension(file.FileName);//rename the file but we want to keep the same extention
                    //finally we need to copy the file that was uploaded in the product folder
                    
                    if(obj.Product.ImageURL!=null)
                    {
                        var OldImagePath = Path.Combine(wwwRootPath, obj.Product.ImageURL.TrimStart('\\'));
                        if(System.IO.File.Exists(OldImagePath))
                        {
                            System.IO.File.Delete(OldImagePath);
                        }
                    }
                    using (var filestreams = new FileStream(Path.Combine(uploads, fileName + extension),FileMode.Create))
                    {
                        file.CopyTo(filestreams);
                    }
                    obj.Product.ImageURL = @"\Images\products\" + fileName + extension; //here we are modifying our obj
                }// hence new image is uploaded inside the folder

                if(obj.Product.Id==0)
                {
                    //insert
                    _unitOfWork.product.Add(obj.Product);
                    TempData["success"] = "Product created successfully";
                }
                else
                {
                    //update, if imageURL is null then we are not updating
                    _unitOfWork.product.update(obj.Product);
                    TempData["success"] = "Product updated successfully";
                }
                _unitOfWork.Save();
                //TempData["success"] = "Product created successfully";
                return RedirectToAction("Index");
            }
            return View(obj);
        }
        public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            var productFromDBDelete = _unitOfWork.product.GetFirstOrDefault(u => u.Id == id);
            if (productFromDBDelete == null)
            {
                return NotFound();
            }

            return View(productFromDBDelete);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int? id)
        {
            var obj = _unitOfWork.product.GetFirstOrDefault(u => u.Id == id);
            if (obj == null)
            {
                return NotFound();
                TempData["error"] = "Error while deleting!";
            }
            var OldImagePath = Path.Combine(_hostEnvironment.WebRootPath, obj.ImageURL.TrimStart('\\'));
            if (System.IO.File.Exists(OldImagePath))
            {
                System.IO.File.Delete(OldImagePath);
            }
            _unitOfWork.product.Remove(obj);
            _unitOfWork.Save();
            TempData["success"] = "Category deleted successfully!";

            return RedirectToAction("Index");

        }
/*        #region API CALLS
        [HttpGet]
        public IActionResult GetAll()
        {
            var productList=_unitOfWork.product.GetAll();
            return Json(new {data=productList}); 
        }
        #endregion*/

    }
}
