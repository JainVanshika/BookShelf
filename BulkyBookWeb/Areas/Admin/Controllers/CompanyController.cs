using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IActionResult Index()
        {
            IEnumerable<Company> objCompanyList = _unitOfWork.company.GetAll();
            return View(objCompanyList);
        }
        public IActionResult Upsert(int? id)
        {
            Company company = new();
            if (id == null || id == 0)
            {
                //create 
                return View(company);
            }
            else
            {
                //update
                company = _unitOfWork.company.GetFirstOrDefault(u => u.Id == id);
                return View(company);
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(Company obj)
        {
            if (ModelState.IsValid)
            {
                if (obj.Id == 0)
                {
                    _unitOfWork.company.Add(obj);
                    TempData["success"] = "company created successfully";
                }
                else
                {
                    _unitOfWork.company.Update(obj);
                    TempData["success"] = "company updated successfully";
                }
                _unitOfWork.Save();
                return RedirectToAction("Index");
            }
            return View(obj);
        }
        /*public IActionResult Delete(int? id)
        {
            if (id == null || id == 0)
            {
                return NotFound();
            }
            var CompanyFromDB = _unitOfWork.company.GetFirstOrDefault(u => u.Id == id);
            if (CompanyFromDB == null)
            {
                return NotFound();
            }
            return View(CompanyFromDB);
        }*/
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int? id)
        {
            var obj = _unitOfWork.company.GetFirstOrDefault(u => u.Id == id);
            if (obj == null)
            {
                return NotFound();
                //return Json(new { success = false, message = "Error while deleting" });
                TempData["error"] = "Error while deleting!";
            }
            _unitOfWork.company.Remove(obj);
            _unitOfWork.Save();
            TempData["success"] = "company deleted successfully";
            return RedirectToAction("Index");
            //return Json(new { success = true, message = "Delete Successful" });
        }
    }
}
