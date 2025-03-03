using PinDistance.Services;
using System.IdentityModel.Tokens.Jwt;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PinDistance.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace PinDistance.Helpers
{
    public class RefreshTokenMiddleware : IMiddleware
    {
        private readonly IAuthService _authService;
        private readonly ILogger<RefreshTokenMiddleware> _logger;

        public RefreshTokenMiddleware(IAuthService authService, ILogger<RefreshTokenMiddleware> logger)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var authHeader) &&
                authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader.ToString().Split(" ")[1];

                if (IsTokenExpired(token))
                {
                    if (!context.Request.Headers.TryGetValue("Refresh-Token", out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
                    {
                        _logger.LogWarning("No refresh token provided.");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Unauthorized: No refresh token provided.");
                        return;
                    }

                    var newTokenResult = _authService.RefreshToken(token, refreshToken);
                    if (newTokenResult == null)
                    {
                        _logger.LogWarning("Invalid or expired refresh token.");
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsync("Unauthorized: Invalid or expired refresh token.");
                        return;
                    }

                    // Attach new tokens in response headers
                    context.Response.Headers["New-Access-Token"] = newTokenResult.Token;
                    context.Response.Headers["New-Refresh-Token"] = newTokenResult.RefreshToken;

                    _logger.LogInformation("New token issued via refresh token.");
                }
            }

            await next(context);
        }

        private bool IsTokenExpired(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                return jwtToken.ValidTo < DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Token validation failed: {ex.Message}");
                return true; // Treat as expired if validation fails
            }
        }
    }


}
