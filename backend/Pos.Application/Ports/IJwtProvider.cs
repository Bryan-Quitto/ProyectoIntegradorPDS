namespace Pos.Application.Ports;

public interface IJwtProvider
{
    string GenerateToken(string username, string fullName, string role);
}
