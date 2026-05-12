namespace PlanningPoker.Domain.Interfaces.Services;

public interface ICookieService
{
    void SetTokenCookie(string name, string token, int expireTime);
    void DeleteCookie(string name);
    string? GetCookie(string name);
}
