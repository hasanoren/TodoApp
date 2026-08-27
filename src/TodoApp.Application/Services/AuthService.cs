using TodoApp.Application.DTOs;
using TodoApp.Application.Interfaces;
using TodoApp.Domain.Entities;
using TodoApp.Domain.Exceptions;

namespace TodoApp.Application.Services;

public class AuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public AuthService(IUserRepository userRepository, IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser is not null)
        {
            throw new ConflictException("Bu e-posta adresi zaten kayıtlı."); // BR-001
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = UserRole.User, // BR-005: varsayılan rol
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
        await _userRepository.SaveChangesAsync();

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Token = string.Empty // JWT üretimini bir sonraki task'ta (Login) ekleyeceğiz
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationException("E-posta veya şifre hatalı.");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        return new AuthResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Token = token
        };
    }
}