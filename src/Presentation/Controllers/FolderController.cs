using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/folders")]
    [Authorize] // Защищаем маршруты
    [EnableCors("AllowAll")] // Применение политики CORS
    public class FolderController : ControllerBase
    {
        private readonly FolderService _folderService;
        private readonly UserService _userService;
        private readonly ILogger<FolderController> _logger;

        public FolderController(FolderService folderService, UserService userService, ILogger<FolderController> logger)
        {
            _folderService = folderService;
            _userService = userService;
            _logger = logger;
        }

        // Получение всех папок пользователя
        [HttpGet]
        public async Task<IActionResult> GetUserFolders()
        {
            try
            {
                var userId = _userService.GetCurrentUserId();
                var folders = await _folderService.GetUserFoldersAsync(userId);
                return Ok(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении папок пользователя.");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера." });
            }
        }
        [HttpGet("json")]
        public async Task<IActionResult> GetFoldersAndFiles()
        {
            try
            {
                var userId = _userService.GetCurrentUserId();
                var folders = await _folderService.DTOGetUserFoldersAsync(userId);
                return Ok(folders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении папок пользователя.");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера." });
            }
        }

        // Добавление новой папки
        [HttpPost]
        public async Task<IActionResult> AddFolder([FromBody] FolderRequest request)
        {
            var userId = _userService.GetCurrentUserId();
            var folder = await _folderService.AddFolderAsync(request.Name, userId, request.ParentFolderId);
            return Ok(folder);
        }
        
        // Удаление папки
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteFolder(int id)
        {
            await _folderService.DeleteFolderAsync(id);
            return Ok( new { message = "Папка успешно удалена." });
        }
    }


}
