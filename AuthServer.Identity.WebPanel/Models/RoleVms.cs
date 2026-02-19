namespace AuthServer.Identity.WebPanel.Models.Management;

public sealed class CreateRoleVm
{
    public string RoleName { get; set; } = "";
}

public sealed class UpdateRoleVm
{
    public string RoleId { get; set; } = "";
    public string NewRoleName { get; set; } = "";
}

public sealed class RolePermissionsVm
{
    public string RoleId { get; set; } = "";
    public string RoleName { get; set; } = "";
    public List<PermissionItemVm> Permissions { get; set; } = new();
}

public sealed class PermissionItemVm
{
    public string Value { get; set; } = "";   // Permissions.Laboratories.View
    public string Module { get; set; } = "";  // Laboratories
    public string Action { get; set; } = "";  // View
    public bool Selected { get; set; }
}
