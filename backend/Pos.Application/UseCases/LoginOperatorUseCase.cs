using Pos.Application.DTOs;
using Pos.Application.Exceptions;
using Pos.Application.Ports;

namespace Pos.Application.UseCases;

public class LoginOperatorUseCase
{
    private readonly IOperatorRepository _operatorRepository;
    private readonly IJwtProvider _jwtProvider;

    public LoginOperatorUseCase(IOperatorRepository operatorRepository, IJwtProvider jwtProvider)
    {
        _operatorRepository = operatorRepository;
        _jwtProvider = jwtProvider;
    }

    public async Task<LoginResultDto?> ExecuteAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            throw new ApplicationValidationException(nameof(command.Username), "El usuario y la contraseña son campos obligatorios.");
        }

        var hash = _operatorRepository.HashPassword(command.Password);
        var op = await _operatorRepository.GetActiveOperatorByCredentialsAsync(command.Username, hash, cancellationToken);

        if (op == null)
        {
            return null;
        }

        var token = _jwtProvider.GenerateToken(op.Username, op.FullName, op.Role);
        return new LoginResultDto(token, op.Username, op.FullName, op.Role);
    }
}
