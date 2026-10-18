namespace ResortManagementSystem.Domain.Entities;

public class UserAccount
{
    public int UserAccountId { get; set; }
    public string Email { get; set; } = "";          // stored lowercase, unique
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Staff";      // Staff | Manager | Admin | SuperAdmin
    public int? CompanyId { get; set; }              // null = platform-level (Super Admin)
    public bool IsActive { get; set; } = true;
}