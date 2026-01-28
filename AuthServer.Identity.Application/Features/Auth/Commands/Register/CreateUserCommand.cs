using MediatR;
using AuthServer.Identity.Application.Wrappers;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Register
{
  // Bu komut geriye ServiceResponse<string> (UserId) dönecek
  public class CreateUserCommand : IRequest<ServiceResponse<string>>
  {
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public string ConfirmPassword { get; set; }
  }
}