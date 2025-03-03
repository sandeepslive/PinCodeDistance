using Microsoft.Extensions.Configuration; // For IConfiguration
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System; // For DateTime
using MySqlConnector;
using PinDistance.Model;
using System.Security.Cryptography;

namespace PinDistance.Services
{
    // AuthService.cs

    public class AuthService : IAuthService
    {
        private readonly string _connectionString;
        private readonly string _jwtSecretKey;
        private readonly int _accessTokenExpiryMinutes = 30; // Access token expiry
        private readonly int _refreshTokenExpiryDays = 7; // Refresh token expiry
        public AuthService(IConfiguration configuration) // Inject IConfiguration
        {
            _connectionString = configuration.GetConnectionString("appMySqlCon"); // Get from configuration
            _jwtSecretKey = configuration["JwtSecretKey"]; // Get from configuration
        }

        public bool AuthenticateUser(string username, string password)
        {
            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();
                var cmd = new MySqlCommand("SELECT UsmPassword  FROM usermst WHERE UsmUserCode  = @usercode", connection);
                cmd.Parameters.AddWithValue("@usercode", username);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        string storedHashedPassword = reader.GetString(0);
                        return storedHashedPassword == password; // Compare hash
                    }

                }
                return false; // User not found or password incorrect
            }
        }

        //public JwtTokenResult RefreshToken(string token)
        //{
        //    var tokenHandler = new JwtSecurityTokenHandler();
        //    var key = Encoding.ASCII.GetBytes(_jwtSecretKey);

        //    var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
        //    {
        //        ValidateIssuerSigningKey = true,
        //        IssuerSigningKey = new SymmetricSecurityKey(key),
        //        ValidateIssuer = false,
        //        ValidateAudience = false,
        //        ValidateLifetime = false // We handle expiration manually
        //    }, out SecurityToken validatedToken);

        //    var jwtToken = (JwtSecurityToken)validatedToken;
        //    var expiryDate = jwtToken.ValidTo;

        //    if (expiryDate < DateTime.UtcNow.AddMinutes(5)) // Sliding expiration: Refresh if close to expiry
        //    {
        //        var username = principal.Identity.Name;
        //        return GenerateJwtToken(username);
        //    }

        //    return new JwtTokenResult
        //    {
        //        Token = token,
        //        Expiry = expiryDate
        //    };
        //}

        public JwtTokenResult GenerateJwtToken(string username)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecretKey); // From config
            var expiryTime = DateTime.UtcNow.AddMinutes(30); // Token expiry (adjust as needed)

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
                Expires = expiryTime, // Token expiration time
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var accessToken = tokenHandler.CreateToken(tokenDescriptor);

            var refreshToken = GenerateSecureRefreshToken(); // Generate separate refresh token

            // Store refresh token in DB (optional)
            SaveRefreshToken(username, refreshToken);

            return new JwtTokenResult
            {
                Token = tokenHandler.WriteToken(accessToken),
                Expiry = expiryTime,
                RefreshToken = refreshToken
            };
        }
        public JwtTokenResult RefreshToken(string token, string refreshToken)
        {
            var principal = GetPrincipalFromExpiredToken(token);
            if (principal == null) return null; // Invalid token

            var username = principal.Identity.Name;

            using (var connection = new MySqlConnection(_connectionString))
            {
                connection.Open();

                // Get the latest refresh token for the user
                string query = "SELECT token_hash, expiry_date, is_revoked FROM refresh_tokens WHERE username = @username ORDER BY id DESC LIMIT 1";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return null; // No token found

                        string storedHash = reader.GetString("token_hash");
                        DateTime expiryDate = reader.GetDateTime("expiry_date");
                        bool isRevoked = reader.GetBoolean("is_revoked");

                        if (isRevoked || expiryDate < DateTime.UtcNow) return null; // Token expired or revoked

                        // Validate refresh token
                        using (var hmac = new HMACSHA256())
                        {
                            string computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));
                            if (storedHash != computedHash) return null; // Invalid token
                        }
                    }
                }

                // Revoke the old token
                string revokeTokenQuery = "UPDATE refresh_tokens SET is_revoked = TRUE WHERE username = @username";
                using (var cmd = new MySqlCommand(revokeTokenQuery, connection))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.ExecuteNonQuery();
                }

                // Generate new tokens
                var newAccessToken = GenerateJwtToken(username);
                var newRefreshToken = GenerateSecureRefreshToken();
                SaveRefreshToken(username, newRefreshToken);

                return new JwtTokenResult
                {
                    Token = newAccessToken.Token,
                    Expiry = newAccessToken.Expiry,
                    RefreshToken = newRefreshToken
                };
            }
        }
        private ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecretKey);

            try
            {
                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,      // Adjust if you have a valid issuer
                    ValidateAudience = false,    // Adjust if you have a valid audience
                    ValidateLifetime = false     // Ignore token expiration here
                };

                SecurityToken securityToken;
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out securityToken);

                if (!(securityToken is JwtSecurityToken jwtSecurityToken) ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch (Exception)
            {
                // Optionally log the exception
                return null;
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

        private void SaveRefreshToken(string username, string refreshToken)
        {
            using (var hmac = new HMACSHA256())
            {
                string tokenHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(refreshToken)));

                using (var connection = new MySqlConnection(_connectionString))
                {
                    connection.Open();

                    // Revoke old refresh tokens before saving the new one
                    string revokeOldTokensQuery = "UPDATE refresh_tokens SET isrevoked = TRUE WHERE username = @username AND isrevoked = FALSE";
                    using (var cmd = new MySqlCommand(revokeOldTokensQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.ExecuteNonQuery();
                    }

                    // Insert the new refresh token
                    string insertTokenQuery = "INSERT INTO refresh_tokens (username, tokenhash, expirydate) VALUES (@username, @tokenHash, @expiryDate)";
                    using (var cmd = new MySqlCommand(insertTokenQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@username", username);
                        cmd.Parameters.AddWithValue("@tokenHash", tokenHash);
                        cmd.Parameters.AddWithValue("@expiryDate", DateTime.UtcNow.AddDays(7)); // 7-day validity
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}
