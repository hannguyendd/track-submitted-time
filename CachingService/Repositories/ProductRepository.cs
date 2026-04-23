using CachingService.Models;
using CachingService.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Redis.OM;
using Redis.OM.Contracts;
using Redis.OM.Searching;

namespace CachingService.Repositories;

public class ProductRepository(
    ProductDbContext dbContext,
    IRedisConnectionProvider? redisProvider,
    ILogger<ProductRepository> logger) : IProductRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private IRedisCollection<Product>? GetCollection() => redisProvider?.RedisCollection<Product>();

    public async Task<Product?> GetByIdAsync(string id)
    {
        logger.LogInformation("Attempting to retrieve product {ProductId} from RedisOM cache", id);

        var collection = GetCollection();
        if (collection is not null)
        {
            try
            {
                var cachedProduct = await collection.FindByIdAsync(id);
                if (cachedProduct is not null)
                {
                    logger.LogInformation("Product {ProductId} found in CACHE", id);
                    return cachedProduct;
                }

                logger.LogInformation("Product {ProductId} not found in cache, checking DATABASE", id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RedisOM cache lookup failed for product {ProductId}, falling back to database", id);
            }
        }

        var dbProduct = await dbContext.Products.FindAsync(id);
        if (dbProduct != null)
        {
            logger.LogInformation("Product {ProductId} found in DATABASE, caching it", id);
            if (collection is not null)
            {
                try
                {
                    await redisProvider!.Connection.SetAsync(dbProduct, CacheTtl);
                    logger.LogInformation("Product {ProductId} cached in REDISOM", id);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to cache product {ProductId} in RedisOM", id);
                }
            }
        }

        return dbProduct;
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        logger.LogInformation("Attempting to retrieve all products from RedisOM cache");

        var collection = GetCollection();
        if (collection is not null)
        {
            try
            {
                var cachedProducts = await collection.ToListAsync();
                if (cachedProducts.Count > 0)
                {
                    logger.LogInformation("Retrieved {ProductCount} products from CACHE", cachedProducts.Count);
                    return cachedProducts;
                }

                logger.LogInformation("No products in cache, checking DATABASE");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RedisOM cache lookup failed for all products, falling back to database");
            }
        }

        var dbProducts = await dbContext.Products.ToListAsync();
        logger.LogInformation("Retrieved {ProductCount} products from DATABASE, caching them", dbProducts.Count);

        if (collection is not null)
        {
            try
            {
                await collection.InsertAsync(dbProducts, CacheTtl);
                logger.LogInformation("Cached {ProductCount} products in REDISOM", dbProducts.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cache products in RedisOM");
            }
        }

        return dbProducts;
    }

    public async Task<IEnumerable<Product>> GetByCategoryAsync(string category)
    {
        logger.LogInformation("Attempting to retrieve products in category '{Category}' from RedisOM cache", category);

        var collection = GetCollection();
        if (collection is not null)
        {
            try
            {
                var cachedProducts = await collection
                    .Where(p => p.Category == category)
                    .ToListAsync();

                if (cachedProducts.Count > 0)
                {
                    logger.LogInformation("Retrieved {ProductCount} products from CACHE for category '{Category}'", cachedProducts.Count, category);
                    return cachedProducts;
                }

                logger.LogInformation("No products in cache for category '{Category}', checking DATABASE", category);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RedisOM cache lookup failed for category '{Category}', falling back to database", category);
            }
        }

        var dbProducts = await dbContext.Products.Where(p => p.Category == category).ToListAsync();
        logger.LogInformation("Retrieved {ProductCount} products from DATABASE for category '{Category}', caching them", dbProducts.Count, category);

        if (collection is not null)
        {
            try
            {
                await collection.InsertAsync(dbProducts, CacheTtl);
                logger.LogInformation("Cached {ProductCount} products in REDISOM for category '{Category}'", dbProducts.Count, category);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cache products in RedisOM for category '{Category}'", category);
            }
        }

        return dbProducts;
    }

    public async Task<IEnumerable<Product>> GetByIdListAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        logger.LogInformation("Attempting to retrieve {IdCount} products by ID list from RedisOM cache", idList.Count);

        var collection = GetCollection();
        if (collection is not null)
        {
            try
            {
                var validIds = idList.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
                var cacheTasks = validIds.Select(id => collection.FindByIdAsync(id));
                var cachedResults = await Task.WhenAll(cacheTasks);
                var cachedProducts = cachedResults.Where(product => product is not null).Cast<Product>().ToList();
                var cachedIds = cachedProducts.Where(product => product.Id is not null).Select(product => product.Id!).ToHashSet();
                var missingIds = validIds.Where(id => !cachedIds.Contains(id)).ToList();

                logger.LogInformation("Retrieved {ProductCount} out of {RequestedCount} products from CACHE by ID list", cachedProducts.Count, idList.Count);

                if (missingIds.Count > 0)
                {
                    logger.LogInformation("Found {MissingCount} products missing from cache, checking DATABASE", missingIds.Count);

                    var dbProducts = await dbContext.Products
                        .Where(p => p.Id != null && missingIds.Contains(p.Id))
                        .ToListAsync();
                    logger.LogInformation("Retrieved {DbProductCount} products from DATABASE, caching them", dbProducts.Count);

                    try
                    {
                        await collection.InsertAsync(dbProducts, CacheTtl);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to cache missing products in RedisOM");
                    }

                    return cachedProducts.Concat(dbProducts);
                }

                return cachedProducts;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "RedisOM cache lookup failed for ID list, falling back to database");
            }
        }

        var allDbProducts = await dbContext.Products
            .Where(p => p.Id != null && idList.Contains(p.Id))
            .ToListAsync();
        logger.LogInformation("Retrieved {ProductCount} products from DATABASE by ID list, caching them", allDbProducts.Count);

        if (collection is not null)
        {
            try
            {
                await collection.InsertAsync(allDbProducts, CacheTtl);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cache products in RedisOM for ID list");
            }
        }

        return allDbProducts;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        logger.LogInformation("Creating new product: {ProductName}", product.Name);

        product.Id ??= Guid.NewGuid().ToString();
        product.CreatedAt = DateTimeOffset.UtcNow;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Product {ProductId} saved to DATABASE", product.Id);

        var collection = GetCollection();
        if (collection is not null)
        {
            try
            {
                await collection.InsertAsync(product, CacheTtl);
                logger.LogInformation("Product {ProductId} cached in REDISOM", product.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to cache new product {ProductId} in RedisOM", product.Id);
            }
        }

        return product;
    }

    public async Task<Product?> UpdateAsync(string id, Product product)
    {
        logger.LogInformation("Updating product {ProductId}", id);

        var existingProduct = await dbContext.Products.FindAsync(id);
        if (existingProduct == null)
        {
            logger.LogWarning("Product {ProductId} not found for update", id);
            return null;
        }

        // Update properties
        existingProduct.Name = product.Name;
        existingProduct.Price = product.Price;
        existingProduct.Category = product.Category;
        existingProduct.Stock = product.Stock;
        existingProduct.Description = product.Description;
        existingProduct.UpdatedAt = DateTimeOffset.UtcNow;

        dbContext.Products.Update(existingProduct);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Product {ProductId} updated in DATABASE", id);

        if (redisProvider is not null)
        {
            try
            {
                await redisProvider.Connection.SetAsync(existingProduct, CacheTtl);
                logger.LogInformation("Product {ProductId} updated in REDISOM cache", id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update product {ProductId} in RedisOM cache", id);
            }
        }

        return existingProduct;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        logger.LogInformation("Deleting product {ProductId}", id);

        var product = await dbContext.Products.FindAsync(id);
        if (product == null)
        {
            logger.LogWarning("Product {ProductId} not found for deletion", id);
            return false;
        }

        dbContext.Products.Remove(product);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Product {ProductId} deleted from DATABASE", id);

        var collection = GetCollection();
        if (collection is not null)
        {
            try
            {
                await collection.DeleteAsync(product);
                logger.LogInformation("Product {ProductId} deleted from REDISOM cache", id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete product {ProductId} from RedisOM cache", id);
            }
        }

        return true;
    }
}
