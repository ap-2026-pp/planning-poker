using Microsoft.AspNetCore.Http;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для роботи з HTTP cookies.
/// </summary>
/// <param name="httpContextAccessor"></param>
internal class CookieService(IHttpContextAccessor httpContextAccessor) : ICookieService
{
    /// <summary>
    /// Зберігає токен у cookie з вказаними параметрами безпеки та часом життя.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="token"></param>
    /// <param name="expireTime"></param>
    public void SetTokenCookie(string name, string token, int expireTime)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddMinutes(expireTime)
        };
        httpContextAccessor.HttpContext?.Response.Cookies.Append(name, token, options);
    }

    /// <summary>
    /// Видаляє cookie за вказаною назвою.
    /// </summary>
    /// <param name="name"></param>
    public void DeleteCookie(string name)
    {
        httpContextAccessor.HttpContext?.Response.Cookies.Delete(name);
    }

    /// <summary>
    /// Отримує значення cookie за назвою.
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public string? GetCookie(string name)
    {
        return httpContextAccessor.HttpContext?.Request.Cookies[name];
    }
}