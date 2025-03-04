using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using PinDistance.Model;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PinDistance.Services
{
    public class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly string _jwtSecretKey;
        private readonly int _accessTokenExpiryMinutes = 30;
        private readonly int _refreshTokenExpiryDays = 7;
        private readonly ILogger<AuthService> _logger;

        public AuthService(IConfiguration configuration, ILogger<AuthService> logger)
        {
            _connectionString = configuration.GetConnectionString("appMySqlCon");
            _jwtSecretKey = configuration["JwtSecretKey"];
            _logger = logger;
        }

        public bool AuthenticateUser(string username, string password)
        {
            _logger.LogInformation($"Authenticating user: {username}");

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    var cmd = new MySqlCommand("SELECT UsmPassword FROM usermst WHERE UsmUserCode = @usercode", connection);
                    cmd.Parameters.AddWithValue("@usercode", username);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string storedHashedPassword = reader.GetString(0);
                            bool isAuthenticated = storedHashedPassword == password;
                            _logger.LogInformation($"User authentication {(isAuthenticated ? "successful" : "failed")} for {username}");
                            return isAuthenticated;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error during user authentication for {username}");
                }
                return false;
            }
        }

        public JwtTokenResult GenerateJwtToken(string username)
        {
            _logger.LogInformation($"Generating JWT token for {username}");

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSecretKey);
                var expiryTime = DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes);

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
                    Expires = expiryTime,
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
                };

                var accessToken = tokenHandler.CreateToken(tokenDescriptor);
                var refreshToken = GenerateSecureRefreshToken();

                SaveRefreshToken(username, refreshToken);

                _logger.LogInformation($"JWT token generated successfully for {username}");

                return new JwtTokenResult
                {
                    Token = tokenHandler.WriteToken(accessToken),
                    Expiry = expiryTime,
                    RefreshToken = refreshToken
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating JWT token for {username}");
                return null;
            }
        }

        public JwtTokenResult RefreshToken(string token, string refreshToken)
        {
            _logger.LogInformation("Attempting to refresh access token.");

            var principal = GetPrincipalFromExpiredToken(token);
            if (principal == null)
            {
                _logger.LogWarning("Invalid JWT token provided for refresh.");
                return null;
            }

            var username = principal.Identity.Name;
            _logger.LogInformation($"Refreshing token for user: {username}");

            using (var connection = new MySqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    string query = "SELECT token_hash, expiry_date, is_revoked FROM refresh_tokens WHERE username = @username ORDER BY id DESC LIMIT 1";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                _logger.LogWarning($"No refresh token found for user {username}");
                                return null;
                            }

                            string storedHash = reader.GetString("token_hash");
                            DateTime expiryDate = reader.GetDateTime("expiry_date");
                            bool isRevoked = reader.GetBoolean("is_revoked");

                            if (isRevoked || expiryDate < DateTime.UtcNow)
                            {
                                _logger.LogWarning($"Expired or revoked refresh token for user {username}");
                                return null;
                            }

                            using (var hmac = new HMACSHA256())
                            {
                                string computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));
                                if (storedHash != computedHash)
                                {
                                    _logger.LogWarning($"Invalid refresh token for user {username}");
                                    return null;
                                }
                            }
                        }
                    }

                    string revokeTokenQuery = "UPDATE refresh_tokens SET is_revoked = TRUE WHERE username = @username";
                    using (var cmd = new MySqlCommand(revokeTokenQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.ExecuteNonQuery();
                    }

                    var newAccessToken = GenerateJwtToken(username);
                    var newRefreshToken = GenerateSecureRefreshToken();
                    SaveRefreshToken(username, newRefreshToken);

                    _logger.LogInformation($"Token refreshed successfully for {username}");

                    return new JwtTokenResult
                    {
                        Token = newAccessToken.Token,
                        Expiry = newAccessToken.Expiry,
                        RefreshToken = newRefreshToken
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error refreshing token for user {username}");
                    return null;
                }
            }
        }

        private void SaveRefreshToken(string username, string refreshToken)
        {
            _logger.LogInformation($"Saving refresh token for {username}");

            using (var hmac = new HMACSHA256())
            {
                string tokenHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));

                using (var connection = new MySqlConnection(_connectionString))
                {
                    try
                    {
                        connection.Open();

                        string revokeOldTokensQuery = "UPDATE refresh_tokens SET is_revoked = TRUE WHERE username = @username AND is_revoked = FALSE";
                        using (var cmd = new MySqlCommand(revokeOldTokensQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@username", username);
                            cmd.ExecuteNonQuery();
                        }

                        string insertTokenQuery = "INSERT INTO refresh_tokens (username, token_hash, expiry_date) VALUES (@username, @tokenHash, @expiryDate)";
                        using (var cmd = new MySqlCommand(insertTokenQuery, connection))
                        {
                            cmd.Parameters.AddWithValue("@username", username);
                            cmd.Parameters.AddWithValue("@tokenHash", tokenHash);
                            cmd.Parameters.AddWithValue("@expiryDate", DateTime.UtcNow.AddDays(_refreshTokenExpiryDays));
                            cmd.ExecuteNonQuery();
                        }

                        _logger.LogInformation($"Refresh token saved successfully for {username}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error saving refresh token for {username}");
                    }
                }
            }
        }

        private string GenerateSecureRefreshToken()
        {
            var randomBytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }

        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_jwtSecretKey);

                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false
                }, out SecurityToken securityToken);

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting principal from expired token.");
                return null;
            }
        }
    }
}
