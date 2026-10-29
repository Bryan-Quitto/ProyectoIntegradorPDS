namespace Pos.Application.DTOs;

public record LoginCommand(string Username, string Password);

public record LoginResultDto(string Token, string Username, string FullName, string Role);

public record OperatorDto(int Id, string Username, string FullName, string Role, bool IsActive);
