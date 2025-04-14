using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [Authorize(Roles = "Admin")] // Только администраторы
    [Route("api/users")]
    [ApiController]

    public class UserController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly ILogger<FileController> _logger;

        public UserController(UserService userService, ILogger<FileController> logger)
        {
            _userService = userService;
            _logger = logger;
        }
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            _logger.LogInformation("Получен запрос на список пользователей.");
            var users = await _userService.GetUsersAsync();
            return Ok(users);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddUser([FromBody] UserRequest request)
        {
            try
            {
                _logger.LogInformation($"Получен запрос на добавление пользователя: {request.Username}");
                var userId = await _userService.AddUserAsync(request);
                return Ok(new { Message = "Пользователь успешно добавлен.", UserId = userId });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Ошибка при добавлении пользователя: {ex.Message}");
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"Ошибка при добавлении пользователя: {ex.Message}");
                return Conflict(ex.Message);
            }
        }

        [HttpDelete("delete/{userId}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            _logger.LogInformation($"Получен запрос на удаление пользователя с ID: {userId}");
            var isDeleted = await _userService.DeleteUserAsync(userId);

            if (!isDeleted)
            {
                _logger.LogWarning($"Пользователь с ID: {userId} не найден.");
                return NotFound("Пользователь не найден.");
            }

            return Ok(new { Message = "Пользователь успешно удален." });
        }

        [HttpPut("changePassword/{userId}")]
        public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordRequest request)
        {
            try
            {
                _logger.LogInformation($"Получен запрос на изменение пароля для пользователя с ID: {userId}");
                var isChanged = await _userService.ChangePasswordAsync(userId, request.NewPassword);

                if (!isChanged)
                {
                    _logger.LogWarning($"Пользователь с ID: {userId} не найден.");
                    return NotFound("Пользователь не найден.");
                }

                return Ok(new { Message = "Пароль успешно изменен." });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Ошибка при изменении пароля: {ex.Message}");
                return BadRequest(ex.Message);
            }
        }
    }
}
