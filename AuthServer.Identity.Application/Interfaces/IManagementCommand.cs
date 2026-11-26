namespace AuthServer.Identity.Application.Interfaces;

// Management mutations and their audit records must commit in the same transaction.
public interface IManagementCommand { }
