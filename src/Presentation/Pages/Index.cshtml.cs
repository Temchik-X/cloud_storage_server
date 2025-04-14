using Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Data;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Presentation.Pages
{
    public class IndexModel : PageModel
    {
        public readonly AuthService _authService;
        private readonly ILogger<IndexModel> _logger;
        public string? UserRole;
        public IndexModel(AuthService authService, ILogger<IndexModel> logger)
        {
            _authService = authService;
            _logger = logger;
            UserRole = null;
        }

        public IActionResult OnGet()
        {
            // Получаем токен из cookie
            var token = Request.Cookies["AuthToken"];
            if (!string.IsNullOrEmpty(token))
            {
                // Валидируем токен и получаем роль
                var role = _authService.ValidateTokenAndGetRole(token);

                if (role == "Admin")
                {
                    UserRole = role; // Разрешаем доступ администратору
                    return Page();
                }
                if (role == "User")
                {
                    UserRole = role;
                    return Page();
                }
                if (role == null)
                {
                    UserRole = role;
                    return Page();
                }
                return RedirectToPage("/AccessDenied"); // Запрещаем доступ
            }
            return Page();
        }
            
        public IActionResult OnPost()
        {
            Console.WriteLine("POST");
            return Page(); // Остаться на текущей странице
        }
    }
}
