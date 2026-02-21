using AdaptArch.Extensions.Yarp.Samples.UserService.Models;
using Microsoft.AspNetCore.Mvc;

namespace AdaptArch.Extensions.Yarp.Samples.UserService.Controllers;

/// <summary>
/// API for managing users.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private static readonly List<User> Users =
    [
        new User { Id = 1, Username = "alice", Email = "alice@example.com", FullName = "Alice Smith", CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new User { Id = 2, Username = "bob", Email = "bob@example.com", FullName = "Bob Johnson", CreatedAt = DateTime.UtcNow.AddDays(-15) },
        new User { Id = 3, Username = "charlie", Email = "charlie@example.com", FullName = "Charlie Brown", CreatedAt = DateTime.UtcNow.AddDays(-7) }
    ];

    /// <summary>
    /// Gets all users.
    /// </summary>
    /// <remarks>Returns all registered users in the system.</remarks>
    /// <returns>A list of all users.</returns>
    /// <response code="200">Returns the list of users.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<User>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<User>> GetAll()
    {
        return Ok(Users);
    }

    /// <summary>
    /// Gets a specific user by ID.
    /// </summary>
    /// <remarks>Looks up a user by their unique numeric identifier.</remarks>
    /// <param name="id">The user ID.</param>
    /// <returns>The requested user.</returns>
    /// <response code="200">Returns the user.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<User> GetById(int id)
    {
        var user = Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    /// <remarks>Validates the request and creates a new user with a generated ID.</remarks>
    /// <param name="request">The user creation request.</param>
    /// <returns>The created user.</returns>
    /// <response code="201">User created successfully.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost]
    [ProducesResponseType(typeof(User), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<User> Create([FromBody] CreateUserRequest request)
    {
        var user = new User
        {
            Id = Users.Max(u => u.Id) + 1,
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow
        };

        Users.Add(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    /// <summary>
    /// Deletes a user.
    /// </summary>
    /// <remarks>Permanently removes the user with the given ID.</remarks>
    /// <param name="id">The user ID to delete.</param>
    /// <returns>No content.</returns>
    /// <response code="204">User deleted successfully.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(int id)
    {
        var user = Users.FirstOrDefault(u => u.Id == id);
        if (user == null)
        {
            return NotFound();
        }

        Users.Remove(user);
        return NoContent();
    }
}
