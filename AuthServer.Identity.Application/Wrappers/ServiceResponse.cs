using System.Collections.Generic;

namespace AuthServer.Identity.Application.Wrappers
{
  public class ServiceResponse<T>
  {
    public T Data { get; set; }
    public bool Succeeded { get; set; }
    public string Message { get; set; }
    public List<string> Errors { get; set; }

    public ServiceResponse()
    {
    }

    public ServiceResponse(T data, string message = null)
    {
      Succeeded = true;
      Message = message;
      Data = data;
    }

    public ServiceResponse(string message)
    {
      Succeeded = false;
      Message = message;
    }
  }
}