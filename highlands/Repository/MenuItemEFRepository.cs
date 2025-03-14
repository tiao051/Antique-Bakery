using highlands.Data;
using highlands.Models;
using Microsoft.EntityFrameworkCore;

namespace highlands.Repository
{
    public class MenuItemEFRepository
    {
        private readonly AppDbContext _context;

        public MenuItemEFRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<MenuItem>> GetAllMenuItemsAsync()
        {
            return await _context.MenuItems.AsNoTracking().Take(50).ToListAsync();
        }
    }
}
