using Microsoft.EntityFrameworkCore;
using Pos.Application.DTOs;
using Pos.Application.Ports;

namespace Pos.Infrastructure.Persistence;

public class SqliteOperatorRepository : IOperatorRepository
{
    private readonly PosDbContext _dbContext;

    public SqliteOperatorRepository(PosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<OperatorDto?> GetActiveOperatorByCredentialsAsync(
        string username,
        string passwordHash,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.LocalOperators
            .AsNoTracking()
            .FirstOrDefaultAsync(
                u => u.Username == username && u.PasswordHash == passwordHash && u.IsActive,
                cancellationToken);

        if (entity == null)
        {
            return null;
        }

        return new OperatorDto(
            entity.Id,
            entity.Username,
            entity.FullName,
            entity.Role,
            entity.IsActive);
    }

    public string HashPassword(string plainPassword)
    {
        return PosDbContext.HashPassword(plainPassword);
    }
}
