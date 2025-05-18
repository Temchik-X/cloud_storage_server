using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/stream")]
    public class StreamController : ControllerBase
    {
        private readonly StreamService _streamService;
        private readonly ILogger<StreamController> _logger;
        private readonly UserService _userService;
        private readonly FileService _fileService;
        private readonly FileExtensionContentTypeProvider _contentTypeProvider;

        public StreamController(
            StreamService streamService,
            ILogger<StreamController> logger,
            UserService userService,
            FileService fileService,
            FileExtensionContentTypeProvider contentTypeProvider)
        {
            _streamService = streamService;
            _logger = logger;
            _userService = userService;
            _fileService = fileService;
            _contentTypeProvider = contentTypeProvider;
        }

        /// <summary>
        /// Удаляет ранее сгенерированную ссылку доступа к файлу по токену.
        /// </summary>
        [HttpDelete("{url}")]
        public async Task<IActionResult> DeleteAccessLink(string url, CancellationToken cancellationToken)
        {

            try
            {
                await _streamService.DeleteFileAccessLinkAsync(url, cancellationToken);
                return NoContent(); // 204 — удалено успешно, контент отсутствует
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Попытка удалить несуществующую ссылку: {Token}", url);
                return NotFound(ex.Message); // 404, если не найдено
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении ссылки: {Token}", url);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpGet("url/{id:int}")]
        public async Task<IActionResult> GetStreamUrl(int id)
        {
            _logger.LogInformation("Получен запрос на получение URL для стриминга видео. FileId={FileId}", id);

            try
            {
                var userId = _userService.GetCurrentUserId();
                var result = await _streamService.GetStreamUrlAsync(userId, id);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Попытка доступа к несуществующему или удалённому файлу. FileId={FileId}", id);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении URL для стриминга видео. FileId={FileId}", id);
                return StatusCode(500, "Внутренняя ошибка сервера.");
            }
        }

        [HttpGet("video/{id}")]
        public async Task<IActionResult> GetStreamVideo(string id)
        {
            var fileId = await _streamService.GetFileIdByUrl(id);
            var userId = await _streamService.GetUserIdByUrl(id);
            _logger.LogInformation("Получен запрос на стриминг видео. FileId={FileId}", fileId);

            try
            {
                var result = await _fileService.GetStreamVideoAsync(userId, fileId);

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

    }
}
