using CachingService.Data;
using CachingService.Models;
using CachingService.Repositories;
using Microsoft.EntityFrameworkCore;
using Redis.OM;
using Redis.OM.Contracts;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();

// Configure in-memory database
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseInMemoryDatabase("ProductDb"));

// Configure Redis connection
var redisConnectionString = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";

try
{
    var redisConnection = ConnectionMultiplexer.Connect(redisConnectionString);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisConnection);
    builder.Services.AddSingleton<IRedisConnectionProvider>(_ => new RedisConnectionProvider(redisConnection));
    Console.WriteLine("✓ Successfully connected to Redis");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠ Warning: Redis connection failed ({ex.Message})");
    Console.WriteLine($"   Make sure Redis is running at {redisConnectionString}");
    Console.WriteLine($"   Running in cache-disabled mode (will use only database)");
}

// Register repository
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// Configure logging
builder.Services.AddLogging(config =>
{
    config.ClearProviders();
    config.AddConsole();
    config.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

var redisProvider = app.Services.GetService<IRedisConnectionProvider>();
if (redisProvider is not null)
{
    try
    {
        if (!redisProvider.Connection.IsIndexCurrent(typeof(Product)))
        {
            try
            {
                redisProvider.Connection.DropIndex(typeof(Product));
            }
            catch
            {
            }

            await redisProvider.Connection.CreateIndexAsync(typeof(Product));
            Console.WriteLine("✓ RedisOM index created for Product");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠ Warning: RedisOM index setup failed ({ex.Message})");
    }
}

// Seed sample data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    dbContext.Database.EnsureCreated();

    if (!dbContext.Products.Any())
    {
        var sampleProducts = new[]
        {
            new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Laptop",
                Price = 1299.99m,
                Category = "Electronics",
                Stock = 15,
                Description = "High-performance laptop with 16GB RAM",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Mouse",
                Price = 29.99m,
                Category = "Electronics",
                Stock = 50,
                Description = "Wireless mouse with USB receiver",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Desk Chair",
                Price = 249.99m,
                Category = "Furniture",
                Stock = 8,
                Description = "Ergonomic office chair",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new Product
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Monitor",
                Price = 399.99m,
                Category = "Electronics",
                Stock = 12,
                Description = "27-inch 4K display",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        dbContext.Products.AddRange(sampleProducts);
        await dbContext.SaveChangesAsync();
        Console.WriteLine($"✓ Seeded {sampleProducts.Length} sample products to in-memory database");
    }
}

// Configure HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// CRUD Endpoints

// GET all products
app.MapGet("/api/products", async (IProductRepository repo) =>
{
    var products = await repo.GetAllAsync();
    return Results.Ok(products);
})
.WithName("GetAllProducts")
.WithOpenApi();

// GET product by ID
app.MapGet("/api/products/{id}", async (string id, IProductRepository repo) =>
{
    var product = await repo.GetByIdAsync(id);
    return product is null ? Results.NotFound() : Results.Ok(product);
})
.WithName("GetProductById")
.WithOpenApi();

// GET products by category
app.MapGet("/api/products/category/{category}", async (string category, IProductRepository repo) =>
{
    var products = await repo.GetByCategoryAsync(category);
    return Results.Ok(products);
})
.WithName("GetProductsByCategory")
.WithOpenApi();

// GET products by ID list
app.MapPost("/api/products/batch", async (GetProductsByIdsRequest request, IProductRepository repo) =>
{
    if (request.Ids == null || request.Ids.Count == 0)
        return Results.BadRequest("Ids list cannot be empty");

    var products = await repo.GetByIdListAsync(request.Ids);
    return Results.Ok(products);
})
.WithName("GetProductsByIdList")
.WithOpenApi();

// POST create product
app.MapPost("/api/products", async (CreateProductRequest request, IProductRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category))
        return Results.BadRequest("Name and Category are required");

    var product = new Product
    {
        Name = request.Name,
        Price = request.Price,
        Category = request.Category,
        Stock = request.Stock,
        Description = request.Description
    };

    var createdProduct = await repo.CreateAsync(product);
    return Results.Created($"/api/products/{createdProduct.Id}", createdProduct);
})
.WithName("CreateProduct")
.WithOpenApi();

// PUT update product
app.MapPut("/api/products/{id}", async (string id, UpdateProductRequest request, IProductRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Category))
        return Results.BadRequest("Name and Category are required");

    var product = new Product
    {
        Name = request.Name,
        Price = request.Price,
        Category = request.Category,
        Stock = request.Stock,
        Description = request.Description
    };

    var updatedProduct = await repo.UpdateAsync(id, product);
    return updatedProduct is null ? Results.NotFound() : Results.Ok(updatedProduct);
})
.WithName("UpdateProduct")
.WithOpenApi();

// DELETE product
app.MapDelete("/api/products/{id}", async (string id, IProductRepository repo) =>
{
    var deleted = await repo.DeleteAsync(id);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteProduct")
.WithOpenApi();

app.Run();

// Request/Response DTOs
record CreateProductRequest(string Name, decimal Price, string Category, int Stock, string Description);
record UpdateProductRequest(string Name, decimal Price, string Category, int Stock, string Description);
record GetProductsByIdsRequest(List<string> Ids);
