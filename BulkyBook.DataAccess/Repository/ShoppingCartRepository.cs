using BulkyBook.DataAccess.Repository.IRepository;
using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkyBook.DataAccess.Repository
{
    public class ShoppingCartRepository:Repository<ShoppingCart>,IShoppingCartRepository
    {
        private ApplicationDbContext _db;
        public ShoppingCartRepository(ApplicationDbContext db):base(db)
        {
            _db = db;
        }

        public int DecrementCount(ShoppingCart cart, int count)
        {
            cart.Count -= count;
            return cart.Count;
        }

        public int IncrementCount(ShoppingCart cart, int count)
        {
            cart.Count += count;
            return cart.Count;
        }

        /*public void update(ShoppingCart obj)
        {
            var editCart = _db.ShoppingCarts.FirstOrDefault(u => u.Id == obj.Id);
            if (editCart != null)
            {
                editCart.ProductId = obj.ProductId;
                editCart.ApplicationUserId = obj.ApplicationUserId;
                editCart.Count = obj.Count;

            }
        }*/
    }
}
