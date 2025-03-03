using PinDistance.Model;

namespace PinDistance.Services
{
    public interface IAuthService
    {
        bool AuthenticateUser(string username, string password);
        JwtTokenResult GenerateJwtToken(string username);
        JwtTokenResult RefreshToken(string token, string refreshToken);

    }
}
