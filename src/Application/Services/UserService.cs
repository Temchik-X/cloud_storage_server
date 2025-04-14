using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Threading.Tasks;
using Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Application.Models;

namespace Application.Services
{
    public class UserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<UserService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public int GetCurrentUserId()
        {
            var userName = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userName))
            {
                throw new InvalidOperationException("Пользователь не авторизован.");
            }
            var user = _context.Users.FirstOrDefaultAsync(u => u.Username == userName);
            if (user == null)
            {
                throw new InvalidOperationException("Пользователь не найден.");
            }
            return user.Result.Id;
        }
        public async Task<bool> UserIsAdmin()
        {
            var userName = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userName))
            {
                throw new InvalidOperationException("Пользователь не авторизован.");
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == userName);
            if (user == null)
            {
                throw new InvalidOperationException("Пользователь не найден.");
            }
            return user.IsAdmin;
        }
        public async Task<IEnumerable<UserModel>> GetUsersAsync()
        {
            _logger.LogInformation("Запрос на получение списка пользователей.");
            var users = await _context.Users.ToListAsync();
            return users;
        }
        public async Task<int> AddUserAsync(UserRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                _logger.LogWarning("Попытка добавить пользователя с пустым именем или паролем.");
                throw new ArgumentException("Имя пользователя и пароль обязательны.");
            }

            if (_context.Users.Any(u => u.Username == request.Username))
            {
                _logger.LogWarning($"Попытка добавить пользователя с уже существующим именем: {request.Username}");
                throw new InvalidOperationException("Пользователь с таким именем уже существует.");
            }

            var newUser = new UserModel
            {
                Username = request.Username,
                IsAdmin = request.IsAdmin,
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                Email = request.Email,
                FreeSpace = request.FreeSpace == null ? -1 : (int)request.FreeSpace,
                DateRegistration = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Добавлен новый пользователь: {newUser.Username} (ID: {newUser.Id}).");
            return newUser.Id;
        }
        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                _logger.LogWarning($"Попытка удалить несуществующего пользователя с ID: {userId}");
                return false;
            }

            _context.Users.Remove(user);
            _logger.LogInformation($"Удален пользователь с ID: {userId}.");
            await _context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> ChangePasswordAsync(int userId, string newPassword)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                _logger.LogWarning($"Попытка изменить пароль несуществующего пользователя с ID: {userId}");
                return false;
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                _logger.LogWarning($"Попытка изменить пароль на пустое значение для пользователя с ID: {userId}");
                throw new ArgumentException("Новый пароль обязателен.");
            }

            user.PasswordHash = PasswordHasher.HashPassword(newPassword);
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Пароль успешно изменен для пользователя с ID: {userId}.");
            return true;
        }
    }
}
