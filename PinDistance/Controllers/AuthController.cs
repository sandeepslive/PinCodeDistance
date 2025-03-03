using System.Linq;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PinDistance.Model;
using PinDistance.Services;
using Microsoft.Extensions.Logging;

namespace PinDistance.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
        {
            // Validate that the username and password are provided.
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                _logger.LogWarning("Login attempt failed: Username or password missing.");
                return UnprocessableEntity(new BadResponseModel
                 {
                     message = "Username and password are required.",
                     status = new StatusDTO() { Code = 422, text = " Missing parameters" }
                 });
            }


            if (_authService.AuthenticateUser(request.Username, request.Password))
            {
                var tokenResult = _authService.GenerateJwtToken(request.Username);

                if (tokenResult == null)
                {
                    _logger.LogError("Token generation failed for user: {Username}", request.Username);
                    return StatusCode(500, new BadResponseModel
                    {
                        message = "Failed to generate token.",
                        status = new StatusDTO() { Code = 500, text = "Server Error" }
                    });
                }
                _logger.LogInformation("User {Username} successfully authenticated.", request.Username);
                return Ok(new
                {
                    Token = tokenResult.Token,
                    Expiry = tokenResult.Expiry,
                    RefreshToken = tokenResult.RefreshToken
                });
            }
            _logger.LogWarning("Invalid login attempt for user: {Username}", request.Username);
            return Unauthorized(new BadResponseModel
            {
                message = "Invalid credentials.",
                status = new StatusDTO() { Code = 401, text = "Unauthorized" }


            });
        }

        [Authorize]
        [HttpGet("protected")]
        public IActionResult Protected()
        {
            return Ok("You have accessed a protected endpoint.");
        }

        [Authorize]
        [HttpPost("refresh")]
        public IActionResult RefreshToken([FromBody] TokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Token and refresh token are required.");
            }

            var tokenResult = _authService.RefreshToken(request.Token, request.RefreshToken);
            if (tokenResult == null)
            {
                return Unauthorized("Invalid or expired token/refresh token.");
            }

            return Ok(new
            {
                Token = tokenResult.Token,
                Expiry = tokenResult.Expiry
            });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class TokenRequest
    {
        public string Token { get; set; }
        public string? RefreshToken { get; set; }
    }
}
