using CachingService.Models;

namespace CachingService.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task<IEnumerable<Product>> GetByCategoryAsync(string category);
    Task<IEnumerable<Product>> GetByIdListAsync(IEnumerable<string> ids);
    Task<Product> CreateAsync(Product product);
    Task<Product?> UpdateAsync(string id, Product product);
    Task<bool> DeleteAsync(string id);
}
