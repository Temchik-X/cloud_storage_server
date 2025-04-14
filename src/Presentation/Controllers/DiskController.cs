using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Application.Services;
using Microsoft.AspNetCore.Authorization;

namespace Presentation.Controllers
{
    [Authorize(Roles = "Admin")]
    [Route("api/disk")]
    [ApiController]
    public class DiskController : ControllerBase
    {
        private readonly DiskService _diskService;
        private readonly UserService _userService;
        private readonly ILogger<DiskController> _logger;

        public DiskController(DiskService diskService, UserService userService, ILogger<DiskController> logger)
        {
            _diskService = diskService;
            _userService = userService;
            _logger = logger;
        }

        [HttpGet("connectedDirectories")]
        public async Task<IActionResult> GetConnectedDirectories()
        {
            _logger.LogInformation("Получен запрос на получение подключенных директорий.");
            try
            {
                var result = await _diskService.GetConnectedDirectories();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса на получение подключенных директорий.");
                return StatusCode(500, $"Ошибка: {ex.Message}");
            }
        }
        [HttpGet("generalDirectoriesInfo")]
        public async Task<IActionResult> GetGeneralDirectoryInfo()
        {
            _logger.LogInformation("Получен запрос на получение общей информации системы.");
            try
            {
                var result = await _diskService.GetGeneralDirectoryInfoAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса на получение общей информации системы.");
                return StatusCode(500, $"Ошибка: {ex.Message}");
            }
        }

        [HttpPost("addDirectory")]
        public async Task<IActionResult> AddDirectory([FromBody] string newDirectoryName)
        {
            _logger.LogInformation($"Получен запрос на добавление директории: {newDirectoryName}");
            try
            {
                var result = await _diskService.AddDirectory(newDirectoryName);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, $"Неверные данные для добавления директории: {newDirectoryName}");
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, $"Конфликт при добавлении директории: {newDirectoryName}");
                return Conflict(ex.Message);
            }
        }

        [HttpGet("os")]
        public IActionResult GetOS()
        {
            _logger.LogInformation("Получен запрос на определение операционной системы.");
            try
            {
                var isWindows = _diskService.GetOS();
                if (isWindows) {
                    return Ok("Windows");
                }
                return Ok("NotWindows");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при определении операционной системы.");
                return StatusCode(500, $"Ошибка: {ex.Message}");
            }
        }

        [HttpDelete("deleteDirectory")]
        public async Task<IActionResult> DeleteDirectory([FromBody] int id)
        {
            _logger.LogInformation($"Получен запрос на удаление директории с ID: {id}");
            try
            {
                var result = await _diskService.DeleteDirectory(id);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex, $"Директория с ID {id} не найдена.");
                return NotFound(ex.Message);
            }
        }

        [HttpPost("index")]
        public async Task<IActionResult> IndexFiles()
        {
            if (!await _userService.UserIsAdmin())
            {
                return Forbid("Недостаточно прав!");
            }
            _logger.LogInformation("Получен запрос на индексацию файлов.");
            try
            {
                var result = await _diskService.IndexFiles();
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Нет доступных дисков для индексации.");
                return NotFound(ex.Message);
            }
        }
    }
}