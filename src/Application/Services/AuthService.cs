using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Data;
using Application.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services
{
    public class AuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly ApplicationDbContext _context;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger, ApplicationDbContext context)
        {
            _configuration = configuration;
            _logger = logger;
            _context = context;
        }

        public string GenerateToken(LoginModel model)
        {
            try
            {
                var userData = _context.Users.FirstOrDefault(u => u.Username == model.Username);
                // Создаём claims для токена
                var claims = new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, model.Username),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.NameIdentifier, model.Username),
                    new Claim(ClaimTypes.Role, userData.IsAdmin ? "Admin" : "User")
                };

                // Генерация ключа и credentials
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

                // Создание токена
                var token = new JwtSecurityToken(
                    issuer: _configuration["Jwt:Issuer"],
                    audience: _configuration["Jwt:Audience"],
                    claims: claims,
                    expires: DateTime.UtcNow.AddHours(1),
                    signingCredentials: creds);

                _logger.LogInformation($"JWT-токен успешно сгенерирован для пользователя: {model.Username}");

                return new JwtSecurityTokenHandler().WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации JWT-токена.");
                throw;
            }
        }

        public async Task<bool> ValidateUser(LoginModel model)
        {
            try
            {
                var userData = await _context.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
                if (userData == null)
                {
                    _logger.LogError("Пользователь не найден.");
                    return false;
                }
                if (PasswordHasher.VerifyPassword(model.Password, userData.PasswordHash))
                {
                    _logger.LogInformation($"Успешная аутентификация пользователя: {model.Username}");
                    return true;
                }
                _logger.LogWarning($"Неудачная попытка аутентификации пользователя: {model.Username}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неудачная попытка аутентификации пользователя.");
                return false;
            }
        }

        
        public string ValidateTokenAndGetRole(string token)
        {
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var principal = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true
                }, out SecurityToken validatedToken);

                _logger.LogInformation("Токен успешно валидирован.");
                // Извлекаем роль из claims
                var roleClaim = principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role);
                if (roleClaim == null)
                {
                    _logger.LogWarning("Роль не найдена в токене.");
                    return null; // Возвращаем null, если роль отсутствует
                }

                return roleClaim.Value; // Возвращаем роль
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при валидации токена.");
            }
            return null; // Возвращаем null в случае ошибки
        }
    }
}