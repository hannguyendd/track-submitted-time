using Redis.OM.Modeling;

namespace CachingService.Models;

[Document]
public class Product
{
    [RedisIdField]
    public string? Id { get; set; }

    [Indexed]
    public string Name { get; set; } = null!;

    [Indexed]
    public decimal Price { get; set; }

    [Indexed]
    public string Category { get; set; } = null!;

    public int Stock { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
