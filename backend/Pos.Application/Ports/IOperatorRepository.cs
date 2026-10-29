using Pos.Application.DTOs;

namespace Pos.Application.Ports;

public interface IOperatorRepository
{
    Task<OperatorDto?> GetActiveOperatorByCredentialsAsync(string username, string passwordHash, CancellationToken cancellationToken = default);
    string HashPassword(string plainPassword);
}
