using AdaptArch.Extensions.Yarp.Samples.ProductService.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdaptArch.Extensions.Yarp.Samples.ProductService.Controllers;

/// <summary>
/// API for managing products in the catalog.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private static readonly List<Product> Products =
    [
        new Product { Id = 1, Name = "Laptop", Description = "High-performance laptop", Price = 1299.99m, Category = "Electronics", StockQuantity = 15, CreatedAt = DateTime.UtcNow.AddDays(-60) },
        new Product { Id = 2, Name = "Mouse", Description = "Wireless optical mouse", Price = 29.99m, Category = "Electronics", StockQuantity = 150, CreatedAt = DateTime.UtcNow.AddDays(-45) },
        new Product { Id = 3, Name = "Keyboard", Description = "Mechanical gaming keyboard", Price = 89.99m, Category = "Electronics", StockQuantity = 75, CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new Product { Id = 4, Name = "Monitor", Description = "27-inch 4K monitor", Price = 449.99m, Category = "Electronics", StockQuantity = 25, CreatedAt = DateTime.UtcNow.AddDays(-15) }
    ];

    /// <summary>
    /// Gets all products.
    /// </summary>
    /// <param name="category">Optional category filter.</param>
    /// <returns>A list of all products.</returns>
    /// <response code="200">Returns the list of products.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Product>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Product>> GetAll([FromQuery] string? category = null)
    {
        var products = String.IsNullOrWhiteSpace(category)
            ? Products
            : Products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

        return Ok(products);
    }

    /// <summary>
    /// Gets a specific product by ID.
    /// </summary>
    /// <param name="id">The product ID.</param>
    /// <returns>The requested product.</returns>
    /// <response code="200">Returns the product.</response>
    /// <response code="404">Product not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Product> GetById(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound();
        }
        return Ok(product);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    /// <param name="request">The product creation request.</param>
    /// <returns>The created product.</returns>
    /// <response code="201">Product created successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Product), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Product> Create([FromBody] CreateProductRequest request)
    {
        var product = new Product
        {
            Id = Products.Max(p => p.Id) + 1,
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            StockQuantity = request.StockQuantity,
            CreatedAt = DateTime.UtcNow
        };

        Products.Add(product);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>
    /// Updates an existing product.
    /// </summary>
    /// <param name="id">The product ID to update.</param>
    /// <param name="request">The product update request.</param>
    /// <returns>The updated product.</returns>
    /// <response code="200">Product updated successfully.</response>
    /// <response code="404">Product not found.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Product> Update(int id, [FromBody] UpdateProductRequest request)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound();
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        product.Category = request.Category;
        product.StockQuantity = request.StockQuantity;

        return Ok(product);
    }

    /// <summary>
    /// Deletes a product.
    /// </summary>
    /// <param name="id">The product ID to delete.</param>
    /// <returns>No content.</returns>
    /// <response code="204">Product deleted successfully.</response>
    /// <response code="404">Product not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product == null)
        {
            return NotFound();
        }

        Products.Remove(product);
        return NoContent();
    }
}
