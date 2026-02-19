namespace AuthServer.Identity.WebPanel.Models;

public sealed class UserWithRolesDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class RoleDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class CreateUserRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public List<string> Roles { get; set; } = new();
}

public sealed class UpdateUserRequest
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public sealed class AssignRolesRequest
{
    public Guid UserId { get; set; }
    public List<string> Roles { get; set; } = new();
}

public sealed class UpdateUserStatusRequest
{
    public Guid UserId { get; set; }
    public bool IsActive { get; set; }
}

public sealed class AdminChangePasswordRequest
{
    public Guid UserId { get; set; }
    public string NewPassword { get; set; } = "";
}

public sealed class RevokeAllTokensRequest
{
    // Not: API command’ını raw’dan bulamadım; büyük ihtimal UserId bekliyor.
    public Guid UserId { get; set; }
}

public sealed class CreateRoleRequest
{
    public string RoleName { get; set; } = "";
}

public sealed class UpdateRoleRequest
{
    public string RoleId { get; set; } = "";
    public string NewRoleName { get; set; } = "";
}

public sealed class UpdateRolePermissionsRequest
{
    public string RoleId { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
}
