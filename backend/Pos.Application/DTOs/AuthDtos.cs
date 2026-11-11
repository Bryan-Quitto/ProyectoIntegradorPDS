namespace Pos.Application.DTOs;

public record LoginCommand(string Username, string Password);

public record LoginResultDto(
    string Token,
    string Username,
    string FullName,
    string Role,
    int? PosSessionId,
    string? PosConfigName
);

public record OdooUserAuthDto(
    int Uid,
    string Name,
    string Username,
    string Role,
    int? PosSessionId,
    string? PosConfigName
);

public record OperatorDto(int Id, string Username, string FullName, string Role, bool IsActive);

