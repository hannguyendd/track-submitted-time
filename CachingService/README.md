# CachingService - Product Management with RedisOM Caching

A .NET 10 ASP.NET Core minimal API service that demonstrates object caching using RedisOM together with an in-memory EF Core database. Products are cached as Redis documents, and the service logs whether data comes from the cache or database.

## Features

- **Dual-Storage Architecture**: In-memory database (primary) + RedisOM cache (secondary)
- **CRUD Operations**: Full Create, Read, Update, Delete endpoints for products
- **Smart Caching**: Automatically caches on retrieval, with TTL of 1 hour
- **Detailed Logging**: Tracks every operation source (CACHE vs DATABASE)
- **Batch Operations**: Get multiple products by ID list efficiently
- **Category Filtering**: Query products by category
- **Graceful Fallback**: Falls back to database if Redis is unavailable

## Architecture

```
Request → Repository
  ├─→ Try RedisOM Cache (document storage + indexes)
  │    └─→ If FOUND → Return + Log "CACHE"
  │    └─→ If MISS → Continue
  ├─→ Query In-Memory Database
  │    └─→ If FOUND → Cache it via RedisOM + Log "DATABASE"
  │    └─→ If NOT FOUND → Return null
  └─→ Log operation with source (CACHE or DATABASE)
```

## Setup & Prerequisites

### Requirements

- .NET 10 SDK
- Redis (optional - will fallback to database-only if not available)

### Installation

```bash
# Already installed, but if needed:
# Install dependencies
dotnet restore

# Build
dotnet build

# Run
dotnet run --project CachingService
```

The service runs on:

- HTTP: `http://localhost:5232`
- HTTPS: `https://localhost:7098`

## Configuration

### appsettings.Development.json

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "CachingService": "Information"
    }
  }
}
```

**Redis Connection**:

- Default: `localhost:6379`
- If Redis is unavailable, the app runs in database-only mode
- Set `Redis:ConnectionString` environment variable to override

## API Endpoints

### GET /api/products

Retrieve all products

- **Cache behavior**: Reads from the RedisOM collection first, then falls back to the database
- **Example response**:

```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Laptop",
    "price": 1299.99,
    "category": "Electronics",
    "stock": 15,
    "description": "High-performance laptop with 16GB RAM",
    "createdAt": "2026-04-23T10:00:00+00:00",
    "updatedAt": "2026-04-23T10:00:00+00:00"
  }
]
```

### GET /api/products/{id}

Retrieve a single product by ID

- **Cache behavior**: Uses RedisOM `FindByIdAsync` before falling back to the database
- **Example**: `GET /api/products/550e8400-e29b-41d4-a716-446655440000`

### GET /api/products/category/{category}

Retrieve all products in a category

- **Cache behavior**: Uses a RedisOM indexed query on `Category`
- **Example**: `GET /api/products/category/Electronics`

### POST /api/products/batch

Retrieve multiple products by ID list

- **Cache behavior**: Uses RedisOM lookups per ID, then queries the database for misses
- **Request body**:

```json
{
  "ids": [
    "550e8400-e29b-41d4-a716-446655440000",
    "550e8400-e29b-41d4-a716-446655440001"
  ]
}
```

### POST /api/products

Create a new product

- **Request body**:

```json
{
  "name": "Keyboard",
  "price": 79.99,
  "category": "Electronics",
  "stock": 20,
  "description": "Mechanical RGB keyboard"
}
```

- **Response**: `201 Created` with full product object

### PUT /api/products/{id}

Update an existing product

- **Request body**: Same as POST
- **Response**: `200 OK` with updated product

### DELETE /api/products/{id}

Delete a product

- **Response**: `204 No Content`

## Logging & Cache Tracking

All operations are logged with detailed information about their source:

### Example Logs

**First request (Cache Miss):**

```
Information: Attempting to retrieve product 550e8400... from cache
Information: Product 550e8400... not found in cache, checking DATABASE
Information: Product 550e8400... found in DATABASE, caching it
Information: Product 550e8400... cached in REDISOM
```

**Second request (Cache Hit):**

```
Information: Attempting to retrieve product 550e8400... from cache
Information: Product 550e8400... found in CACHE
```

**Batch Query (Mixed Results):**

```
Information: Attempting to retrieve 3 products by ID list from cache
Information: Retrieved 2 out of 3 products from CACHE by ID list
Information: Found 1 products missing from cache, checking DATABASE
Information: Retrieved 1 products from DATABASE, caching them
```

### Log Format

Each operation logs:

- **Attempt source**: CACHE or DATABASE
- **Result**: Found/Not Found/Error
- **Fallback information**: If primary source failed, shows fallback attempt
- **Action taken**: Cached/Updated/Deleted

### Viewing Logs

Logs appear in console output when running the service. For production, configure structured logging via:

- Serilog integration
- Application Insights
- Event logs
- Custom implementations

## Data Source Priority

```
Operation          Primary Source  Fallback        Cache Behavior
--------           ---------------  --------        ---------------
GET /api/products  REDISOM         In-Memory DB    Query RedisOM collection
GET by ID          REDISOM         In-Memory DB    Find document by ID
GET by Category    REDISOM         In-Memory DB    Indexed category query
GET by ID list     REDISOM         In-Memory DB    Mixed (partial hits)
POST               -               In-Memory DB    Cache new product
PUT                REDISOM + DB    In-Memory DB    Update cached document
DELETE             DB              -               Remove from cache
```

## Caching Strategy

### TTL (Time To Live)

- All cached items expire after **1 hour**
- Expired items automatically evicted from Redis

### RedisOM Documents

- Products are stored as RedisOM documents rather than JSON string values managed manually
- `Product.Name`, `Product.Price`, and `Product.Category` are indexed for query support
- The repository creates and updates documents through RedisOM APIs instead of explicit serialization

## Testing

### Using cURL

```bash
# Get all products
curl http://localhost:5232/api/products

# Get single product (after noting an ID from above)
curl http://localhost:5232/api/products/{id}

# Get by category
curl http://localhost:5232/api/products/category/Electronics

# Create product
curl -X POST http://localhost:5232/api/products \
  -H "Content-Type: application/json" \
  -d '{"name":"USB Hub","price":49.99,"category":"Electronics","stock":30,"description":"7-port USB 3.0 hub"}'

# Update product
curl -X PUT http://localhost:5232/api/products/{id} \
  -H "Content-Type: application/json" \
  -d '{"name":"USB Hub Pro","price":59.99,"category":"Electronics","stock":25,"description":"7-port USB 3.0 hub"}'

# Get batch
curl -X POST http://localhost:5232/api/products/batch \
  -H "Content-Type: application/json" \
  -d '{"ids":["{id1}","{id2}"]}'

# Delete product
curl -X DELETE http://localhost:5232/api/products/{id}
```

### Using VS Code REST Client

See `CachingService.http` for pre-configured requests.

## Performance Notes

- **Cache hit**: ~1-2ms response time (no database query)
- **Cache miss**: ~5-10ms response time (database query + RedisOM write)
- **Batch with partial cache**: O(n) where n = number of cache misses and uncached IDs
- **No Redis**: Falls back to database queries only (~5-10ms per query)

## Project Structure

```
CachingService/
├── Program.cs                   — Service setup, endpoints, seed data
├── CachingService.csproj        — Project file with dependencies
├── appsettings.Development.json — Redis connection config
├── Models/
│   └── Product.cs              — Product entity
├── Data/
│   └── ProductDbContext.cs      — EF Core in-memory DbContext
└── Repositories/
    ├── IProductRepository.cs    — Repository interface
    └── ProductRepository.cs     — Implementation with RedisOM caching
```

## Dependencies

- `Redis.OM` (1.1.0) - RedisOM library
- `StackExchange.Redis` (2.7.17) - Redis client (included by RedisOM)
- `Microsoft.EntityFrameworkCore.InMemory` (10.0.7) - In-memory database
- Microsoft.Extensions.Logging - Built-in ASP.NET logging

## Troubleshooting

### Redis Connection Error

```
⚠ Warning: Redis connection failed
   Make sure Redis is running at localhost:6379
```

**Solution**: Either start Redis or the service will run in database-only mode

### Getting 404 on endpoints

- Make sure you're using the correct base URL: `http://localhost:5000`
- Check that the service is running: `dotnet run`

### Cache not working

- Verify Redis is running: `redis-cli ping` (should return `PONG`)
- Check logs for Redis errors
- Restart the service

## Future Enhancements

- [ ] Add distributed cache invalidation
- [ ] Implement cache warming on startup
- [ ] Add metrics/telemetry
- [ ] Implement rate limiting
- [ ] Add authentication/authorization
