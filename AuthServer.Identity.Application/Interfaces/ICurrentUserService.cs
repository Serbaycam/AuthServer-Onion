namespace AuthServer.Identity.Application.Interfaces
{
    public interface ICurrentUserService
    {
        string? UserId { get; }
        string? IpAddress { get; }
    }
}
