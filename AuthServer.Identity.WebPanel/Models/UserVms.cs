namespace AuthServer.Identity.WebPanel.Models.Management;

public sealed class CreateUserByAdminVm
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public List<string> Roles { get; set; } = new();

    // UI için
    public List<RoleDto> AllRoles { get; set; } = new();
}

public sealed class UpdateUserVm
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class AssignRolesVm
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = new();

    // UI için
    public List<RoleDto> AllRoles { get; set; } = new();
}

public sealed class AdminChangePasswordVm
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
