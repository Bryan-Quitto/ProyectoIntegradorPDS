using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;

namespace Pos.Application.UseCases;

public class LoginOperatorUseCase
{
    private readonly IOdooPosGateway _odooGateway;
    private readonly IJwtProvider _jwtProvider;

    public LoginOperatorUseCase(IOdooPosGateway odooGateway, IJwtProvider jwtProvider)
    {
        _odooGateway = odooGateway;
        _jwtProvider = jwtProvider;
    }

    public async Task<LoginResultDto?> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            throw new ApplicationValidationException(nameof(command.Username), "El usuario y la contraseña son campos obligatorios.");
        }

        var authUser = await _odooGateway.AuthenticateUserAsync(command.Username, command.Password, cancellationToken);
        if (authUser == null)
        {
            return null;
        }

        var token = _jwtProvider.GenerateToken(
            authUser.Username,
            authUser.Name,
            authUser.Role,
            authUser.PosSessionId,
            authUser.PosConfigName,
            authUser.Uid
        );

        return new LoginResultDto(
            token,
            authUser.Username,
            authUser.Name,
            authUser.Role,
            authUser.PosSessionId,
            authUser.PosConfigName
        );
    }
}
