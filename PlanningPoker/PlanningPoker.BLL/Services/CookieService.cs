using Microsoft.AspNetCore.Http;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL.Services;

/// <summary>
/// Сервіс для роботи з HTTP cookies.
/// </summary>
/// <param name="httpContextAccessor">Доступ до поточного HTTP контексту.</param>
internal class CookieService(IHttpContextAccessor httpContextAccessor) : ICookieService
{
    /// <summary>
    /// Зберігає токен у cookie з вказаними параметрами безпеки та часом життя.
    /// </summary>
    /// <param name="name">Назва cookie.</param>
    /// <param name="token">Значення токена, яке буде збережене в cookie.</param>
    /// <param name="expireTime">Час життя cookie у хвилинах.</param>
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
    /// <param name="name">Назва cookie, яку потрібно видалити.</param>
    public void DeleteCookie(string name)
    {
        httpContextAccessor.HttpContext?.Response.Cookies.Delete(name);
    }

    /// <summary>
    /// Отримує значення cookie за назвою.
    /// </summary>
    /// <param name="name">Назва cookie, яку потрібно отримати.</param>
    /// <returns>Значення cookie, якщо воно існує, інакше null.</returns>
    public string? GetCookie(string name)
    {
        return httpContextAccessor.HttpContext?.Request.Cookies[name];
    }
}