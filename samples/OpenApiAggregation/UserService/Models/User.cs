namespace AdaptArch.Extensions.Yarp.Samples.UserService.Models;

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    /// <summary>
    /// The unique identifier for the user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The user's username.
    /// </summary>
    public string Username { get; set; } = String.Empty;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; set; } = String.Empty;

    /// <summary>
    /// The user's full name.
    /// </summary>
    public string FullName { get; set; } = String.Empty;

    /// <summary>
    /// The date the user was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request model for creating a new user.
/// </summary>
public class CreateUserRequest
{
    /// <summary>
    /// The username for the new user.
    /// </summary>
    public string Username { get; set; } = String.Empty;

    /// <summary>
    /// The email address for the new user.
    /// </summary>
    public string Email { get; set; } = String.Empty;

    /// <summary>
    /// The full name for the new user.
    /// </summary>
    public string FullName { get; set; } = String.Empty;
}
