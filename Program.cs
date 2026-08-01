using API.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(BuildConnectionString(builder.Configuration)));

var app = builder.Build();

// Apply pending migrations (creates the schema + seed data on first run, e.g. on Render)
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.MapOpenApi();

// NOTE: no UseHttpsRedirection() — Render terminates TLS at its proxy and
// forwards plain HTTP to the container, so redirecting would cause a loop.

// ---------- Users CRUD ----------

app.MapGet("/users", async (AppDbContext db) =>
    await db.Users.OrderBy(u => u.Id).ToListAsync());

app.MapGet("/users/{id:int}", async (int id, AppDbContext db) =>
    await db.Users.FindAsync(id) is User user
        ? Results.Ok(user)
        : Results.NotFound());

app.MapPost("/users", async (UserDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
        return Results.BadRequest("Name and Email are required.");

    if (await db.Users.AnyAsync(u => u.Email == dto.Email))
        return Results.Conflict($"A user with email '{dto.Email}' already exists.");

    var user = new User { Name = dto.Name, Email = dto.Email };
    db.Users.Add(user);
    await db.SaveChangesAsync();
    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id:int}", async (int id, UserDto dto, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Email))
        return Results.BadRequest("Name and Email are required.");

    if (await db.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id))
        return Results.Conflict($"A user with email '{dto.Email}' already exists.");

    user.Name = dto.Name;
    user.Email = dto.Email;
    await db.SaveChangesAsync();
    return Results.Ok(user);
});

app.MapDelete("/users/{id:int}", async (int id, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound();

    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// ---------- Products ----------

app.MapGet("/products", async (AppDbContext db) =>
    await db.Products.OrderBy(p => p.Id).ToListAsync());

app.MapGet("/products/{id:int}", async (int id, AppDbContext db) =>
    await db.Products.FindAsync(id) is Product product
        ? Results.Ok(product)
        : Results.NotFound());

app.MapPost("/products", async (ProductDto dto, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Name) || dto.Price < 0 || dto.Stock < 0)
        return Results.BadRequest("Name is required; Price and Stock must be non-negative.");

    var product = new Product { Name = dto.Name, Price = dto.Price, Stock = dto.Stock };
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});

// ---------- Orders ----------

app.MapGet("/orders", async (AppDbContext db) =>
    await db.Orders
        .OrderBy(o => o.Id)
        .Select(o => new
        {
            o.Id,
            o.Quantity,
            o.OrderDate,
            User = new { o.User!.Id, o.User.Name, o.User.Email },
            Product = new { o.Product!.Id, o.Product.Name, o.Product.Price }
        })
        .ToListAsync());

app.MapPost("/orders", async (OrderDto dto, AppDbContext db) =>
{
    if (dto.Quantity <= 0)
        return Results.BadRequest("Quantity must be greater than zero.");
    if (await db.Users.FindAsync(dto.UserId) is null)
        return Results.BadRequest($"User {dto.UserId} does not exist.");
    if (await db.Products.FindAsync(dto.ProductId) is null)
        return Results.BadRequest($"Product {dto.ProductId} does not exist.");

    var order = new Order { UserId = dto.UserId, ProductId = dto.ProductId, Quantity = dto.Quantity };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/", () => Results.Ok(new
{
    status = "ok",
    endpoints = new[] { "/users", "/products", "/orders", "/openapi/v1.json" }
}));

app.Run();

// Render provides the database as a DATABASE_URL (postgres://user:pass@host:port/db);
// convert it to a Npgsql connection string. Falls back to appsettings for local dev.
static string BuildConnectionString(IConfiguration config)
{
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(databaseUrl))
        return config.GetConnectionString("DefaultConnection")
               ?? throw new InvalidOperationException("No DATABASE_URL or DefaultConnection configured.");

    var uri = new Uri(databaseUrl);
    var userInfo = uri.UserInfo.Split(':', 2);

    return new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Username = Uri.UnescapeDataString(userInfo[0]),
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
        Database = uri.AbsolutePath.TrimStart('/'),
        SslMode = SslMode.Require
    }.ConnectionString;
}

internal record UserDto(string Name, string Email);
internal record ProductDto(string Name, decimal Price, int Stock);
internal record OrderDto(int UserId, int ProductId, int Quantity);
