namespace Vault.Domain.Entities;

public enum UserRole { Admin, Engineer, User }

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = "";          // must be @sparkrobotic.com
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
