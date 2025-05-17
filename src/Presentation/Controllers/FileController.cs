using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.StaticFiles;

namespace Presentation.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/files")]
    [EnableCors("AllowAll")] // Применение политики CORS
    public class FileController : ControllerBase
    {
        private readonly FileService _fileService;
        private readonly UserService _userService;
        private readonly ILogger<FileController> _logger;
        private readonly IconService _iconService;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public FileController(FileService fileService, UserService userService, ILogger<FileController> logger, IconService iconService, FileExtensionContentTypeProvider contentTypeProvider)
        {
            _fileService = fileService;
            _userService = userService;
            _logger = logger;
            _iconService = iconService;
            _contentTypeProvider = contentTypeProvider;
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetFilesList(bool? isDeleted = false)
        {
            _logger.LogInformation("Получен запрос на получение списка файлов.");
            try
            {
                var userId = _userService.GetCurrentUserId();
                var result = await _fileService.GetFilesList(userId, isDeleted);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса на получение списка файлов.");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }
        [HttpGet("video/{id:int}")]
        public async Task<IActionResult> GetStreamVideo(int id)
        {
            _logger.LogInformation("Получен запрос на стриминг видео. FileId={FileId}", id);

            try
            {
                var userId = _userService.GetCurrentUserId();
                var result = await _fileService.GetStreamVideoAsync(userId, id);

                if (!System.IO.File.Exists(result.FilePath))
                    return NotFound("Файл физически не найден.");
                // Попробуем получить корректный MIME по расширению
                if (!_contentTypeProvider.TryGetContentType(result.FilePath, out var contentType))
                {
                    // fallback на переданный или на binary
                    contentType = !string.IsNullOrEmpty(result.ContentType)
                        ? result.ContentType
                        : "application/octet-stream";
                }
                // enableRangeProcessing = true включает поддержку Range-запросов
                return PhysicalFile(
                    result.FilePath,
                    contentType,
                    result.FileName,
                    enableRangeProcessing: true);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Попытка доступа к несуществующему или удалённому файлу. FileId={FileId}", id);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при стриминге видео. FileId={FileId}", id);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }
        [HttpPost("restore-many-files")]
        public async Task<IActionResult> RestoreManyAsync(List<int> ids)
        {
            _logger.LogInformation("Получен запрос на восстановление файлов.");
            try
            {
                var userId = _userService.GetCurrentUserId();
                await _fileService.RestoreManyAsync(userId, ids);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса на восстановление файлов.");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }
        [HttpPost("permanently-delete-files")]
        public async Task<IActionResult> PermanentlyDeleteManyAsync(List<int> ids)
        {
            _logger.LogInformation("Получен запрос на восстановление файлов.");
            try
            {
                var userId = _userService.GetCurrentUserId();
                await _fileService.PermanentlyDeleteManyAsync(userId, ids);
                return Ok();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке запроса на восстановление файлов.");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }
        [HttpPost("json")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> GetJsonRecord(int? id)
        {
            _logger.LogInformation($"Получен запрос на получение директории с ID: {id}");
            try
            {
                var userId = _userService.GetCurrentUserId();
                var json = await _fileService.DTOGetJsonRecordAsync(userId, id);
                return Ok(json);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при скачивании файла с ID: {id}");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpGet("download/{id:int}")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> DownloadFile(int id)
        {
            _logger.LogInformation($"Получен запрос на скачивание файла с ID: {id}");
            try
            {
                var userId = _userService.GetCurrentUserId();
                var (fileStream, fileName) = await _fileService.DownloadFile(id, userId);
                return File(fileStream, "application/octet-stream", fileName);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при скачивании файла с ID: {id}");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpPost("upload")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile file, int? folderId = null)
        {
            _logger.LogInformation("Получен запрос на загрузку файла.");
            try
            {
                var userId = _userService.GetCurrentUserId();
                var result = await _fileService.UploadFile(file, userId, folderId);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex.Message);
                return BadRequest(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файла.");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }
        [HttpPost("upload-many-files")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
        public async Task<IActionResult> UploadManyFiles(List<IFormFile> files, int? folderId = null)
        {
            try
            {
                _logger.LogInformation("Получен запрос на загрузку папки.");
                var userId = _userService.GetCurrentUserId();
                var result = await _fileService.UploadManyFilesAsync(files, userId, folderId);
                return Ok(new { message = result });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex.Message);
                return BadRequest(ex.Message);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке массива файлов.");
                return StatusCode(500, new { message = "Внутренняя ошибка сервера." });
            }
        }
        // Перевести на загрузки по id файла
        [HttpPost("download/icon")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> DownloadIconByFileId(int iconId, int fileId)
        {
            _logger.LogInformation($"Получен запрос на получени иконки с ID: {iconId}");
            try
            {
                var userId = _userService.GetCurrentUserId();
                var (fileStream, fileName) = await _iconService.DownloadIcon(fileId, iconId, userId);
                return File(fileStream, "application/octet-stream", fileName);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при скачивании файла с ID: {iconId}");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteFile(int id)
        {
            _logger.LogInformation($"Получен запрос на удаление файла с ID: {id}");
            try
            {
                var userId = _userService.GetCurrentUserId();
                var result = await _fileService.DeleteFile(id, userId);
                return Ok(result);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении файла с ID: {id}");
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

    }
}