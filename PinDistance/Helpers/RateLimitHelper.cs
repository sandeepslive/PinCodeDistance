using AspNetCoreRateLimit;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PinDistance.Helpers
{
    public class RateLimitHelper : IMiddleware
    {

        private readonly IRateLimitCounterStore _rateLimitCounterStore;
        private readonly IIpPolicyStore _ipPolicyStore;
        public RateLimitHelper(IRateLimitCounterStore rateLimitCounterStore, IIpPolicyStore ipPolicyStore)
        {
            _rateLimitCounterStore = rateLimitCounterStore;
            _ipPolicyStore = ipPolicyStore;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {

            var originalBodyStream = context.Response.Body;
            using var memoryStream = new MemoryStream();
            context.Response.Body = memoryStream;

            await next(context); // Process request

            if (context.Response.Headers.TryGetValue("X-Rate-Limit-Limit", out var limit1) &&
            context.Response.Headers.TryGetValue("X-Rate-Limit-Remaining", out var remaining1) &&
            context.Response.Headers.TryGetValue("X-Rate-Limit-Reset", out var reset1))
            {
                Console.WriteLine($"🚀 Rate Limit Headers - Limit: {limit1}, Remaining: {remaining1}, Reset: {reset1}");
            }
            else
            {
                Console.WriteLine("⚠️ Rate limit headers not found in response.");
            }


            // Read rate limit headers added by AspNetCoreRateLimit
            var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                Console.WriteLine($"⚠️ Unauthorized login attempt from IP: {clientIp}, Path: {context.Request.Path}");
                await CopyResponse(memoryStream, originalBodyStream);
                return;
            }
            if (context.Request.Path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("🔹 Skipping rate limiting for login request.");
                await CopyResponse(memoryStream, originalBodyStream);
                return;
            }
            var policies = await _ipPolicyStore.GetAsync("IpRateLimit");
            var rule = policies?.IpRules.FirstOrDefault(r => r.Ip == clientIp) ?? new IpRateLimitPolicy
            {
                Ip = clientIp,
                Rules = new List<RateLimitRule> { new RateLimitRule { Period = "1m", Limit = 10 } }
            };

            var rulePeriod = rule.Rules.FirstOrDefault()?.Period ?? "1m";
            var limitKey = $"crlc_{clientIp}_{rulePeriod}";

            var bytes = Encoding.UTF8.GetBytes(limitKey);

            using var algorithm = SHA1.Create();
            var hash = algorithm.ComputeHash(bytes);
            var key  = Convert.ToBase64String(hash);
            var counter = await _rateLimitCounterStore.GetAsync(key);

            var rateLimit = rule.Rules.FirstOrDefault()?.Limit ?? 10;
            var requestCount = counter?.Count ?? 0;
            var remaining = Math.Max(rateLimit - requestCount, 0);
            var reset = DateTime.UtcNow.ToString("o");
            context.Response.Headers["X-Rate-Limit-Limit"] = rateLimit.ToString();
            context.Response.Headers["X-Rate-Limit-Remaining"] = remaining.ToString();
            context.Response.Headers["X-Rate-Limit-Reset"] = reset;

            // Modify JSON response
            if (context.Response.ContentType?.Contains("application/json") == true)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                var existingBody = await new StreamReader(memoryStream).ReadToEndAsync();
                var responseWithRateLimit = new
                {
                    data = JsonSerializer.Deserialize<object>(existingBody) ?? existingBody,
                    rateLimit = new { limit = rateLimit, remaining = remaining, reset = reset }
                };

                var jsonResponse = JsonSerializer.Serialize(responseWithRateLimit);
                var responseBytes = Encoding.UTF8.GetBytes(jsonResponse);

                context.Response.ContentLength = responseBytes.Length;
                context.Response.Body = originalBodyStream;
                await context.Response.Body.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
            else
            {
                await CopyResponse(memoryStream, originalBodyStream);
            }
        }

        private async Task CopyResponse(Stream source, Stream destination)
        {
            source.Seek(0, SeekOrigin.Begin);
            await source.CopyToAsync(destination);
        }
    }
    public static class RateLimitMiddlewareExtensions
    {
        public static IApplicationBuilder UseRateLimitMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RateLimitHelper>();
        }
    }
}