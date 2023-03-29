using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.DataAccess.Repository
{
    public class ProductRepository : Repository<Product>, IProductRepository
    {
        private ApplicationDbContext _db;
        public ProductRepository(ApplicationDbContext db):base(db)
        {
            _db = db;
        }
        public void update(Product obj)
        {
            var editProduct = _db.Products.FirstOrDefault(u=>u.Id==obj.Id);
            if(editProduct != null)
            {
                editProduct.Title=obj.Title;
                editProduct.Description = obj.Description;
                editProduct.ISBN = obj.ISBN;
                editProduct.Author = obj.Author;
                editProduct.ListPrice = obj.ListPrice;
                editProduct.Price = obj.Price;
                editProduct.Price50 = obj.Price50;
                editProduct.Price100 = obj.Price100;
                editProduct.CategoryId = obj.CategoryId;
                editProduct.CoverTypeId = obj.CoverTypeId;
                if(obj.ImageURL!= null)
                {
                    editProduct.ImageURL = obj.ImageURL;
                }
            }
        
        }
    }
}
