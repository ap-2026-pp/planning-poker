using Microsoft.AspNetCore.Http;
using PlanningPoker.Domain.Interfaces.Services;

namespace PlanningPoker.BLL.Services;

internal class CookieService(IHttpContextAccessor httpContextAccessor) : ICookieService
{
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

    public void DeleteCookie(string name)
    {
        httpContextAccessor.HttpContext?.Response.Cookies.Delete(name);
    }

    public string? GetCookie(string name)
    {
        return httpContextAccessor.HttpContext?.Request.Cookies[name];
    }
}