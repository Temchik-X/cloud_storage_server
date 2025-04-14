using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Models;
using Application.Services;
using Microsoft.Extensions.Logging;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            _logger.LogInformation($"Получен запрос на вход пользователя: {model.Username}");

            if (!await _authService.ValidateUser(model))
            {
                _logger.LogWarning($"Неудачная попытка входа пользователя: {model.Username}");
                return Unauthorized(new {message = "Неверное имя пользователя или пароль." });
            }

            try
            {
                var token = _authService.GenerateToken(model);
                _logger.LogInformation($"Токен успешно сгенерирован для пользователя: {model.Username}");
                // Создаём cookie с токеном
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true, // Защита от доступа через JavaScript
                    Secure = true,   // Cookie будут отправляться только по HTTPS
                    SameSite = SameSiteMode.Strict, // Защита от CSRF
                    Expires = DateTime.UtcNow.AddHours(1) // Время жизни cookie
                };

                Response.Cookies.Append("AuthToken", token, cookieOptions);
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации токена.");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера."});
            }
        }
        [HttpGet("check")]
        public IActionResult Check()
        {
            return Ok(new { message = "Успешно" });
        }
        [Authorize]
        [HttpPost("access")]
        public IActionResult Access()
        {
            _logger.LogInformation("Получен запрос на доступ к защищенному ресурсу.");
            return Ok(new { message = "Успешно" });
        }

        [HttpGet("validate")]
        public IActionResult Validate()
        {
            // Читаем токен из cookie
            var token = Request.Cookies["AuthToken"];
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { message = "Токен отсутствует" });
            }

            // Валидируем токен и получаем роль
            var role = _authService.ValidateTokenAndGetRole(token);
            if (role == null)
            {
                return Unauthorized(new { message = "Недействительный токен" });
            }

            return Ok(new { role });
        }
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            // Удаляем cookie с токеном
            Response.Cookies.Delete("AuthToken");

            return Ok(new { message = "Вы успешно вышли из системы" });
        }
    }
}