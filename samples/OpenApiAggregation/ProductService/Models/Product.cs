namespace AdaptArch.Extensions.Yarp.Samples.ProductService.Models;

/// <summary>
/// Represents a product in the catalog.
/// </summary>
public class Product
{
    /// <summary>
    /// The unique identifier for the product.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The product name.
    /// </summary>
    public string Name { get; set; } = String.Empty;

    /// <summary>
    /// The product description.
    /// </summary>
    public string Description { get; set; } = String.Empty;

    /// <summary>
    /// The product price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The product category.
    /// </summary>
    public string Category { get; set; } = String.Empty;

    /// <summary>
    /// Stock quantity available.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// The date the product was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request model for creating a new product.
/// </summary>
public class CreateProductRequest
{
    /// <summary>
    /// The product name.
    /// </summary>
    public string Name { get; set; } = String.Empty;

    /// <summary>
    /// The product description.
    /// </summary>
    public string Description { get; set; } = String.Empty;

    /// <summary>
    /// The product price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The product category.
    /// </summary>
    public string Category { get; set; } = String.Empty;

    /// <summary>
    /// Initial stock quantity.
    /// </summary>
    public int StockQuantity { get; set; }
}

/// <summary>
/// Request model for updating a product.
/// </summary>
public class UpdateProductRequest
{
    /// <summary>
    /// The updated product name.
    /// </summary>
    public string Name { get; set; } = String.Empty;

    /// <summary>
    /// The updated product description.
    /// </summary>
    public string Description { get; set; } = String.Empty;

    /// <summary>
    /// The updated product price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// The updated product category.
    /// </summary>
    public string Category { get; set; } = String.Empty;

    /// <summary>
    /// The updated stock quantity.
    /// </summary>
    public int StockQuantity { get; set; }
}
