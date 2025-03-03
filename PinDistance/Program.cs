using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PinDistance.Helpers;
using PinDistance.Services;

var builder = WebApplication.CreateBuilder(args);

// Load JWT secret key from environment variables (more secure than hardcoding)
var jwtSecret = builder.Configuration["JwtSecretKey"]; //
var key = Encoding.ASCII.GetBytes(jwtSecret);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<RateLimitHelper>();
builder.Services.AddTransient<IAuthService, AuthService>();
builder.Services.AddHttpClient<IDistanceService, DistanceService>();
builder.Services.AddHttpClient<IDistanceServiceV2, DistanceServiceV2>();

// JWT Authentication Configuration
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(builder.Configuration["JwtSecretKey"])),
        ValidateIssuer = false, // In production, set this to `true` and configure the issuer
        ValidateAudience = false, // In production, set this to `true` and configure the audience
        ValidateLifetime = true, // Ensures expired tokens are rejected
        ClockSkew = TimeSpan.Zero // Prevents default 5-minute token leeway
    };
});

// Rate Limiting Configuration
builder.Services.AddMemoryCache();
//builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
// Configure IpRateLimiting directly on the builder
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    // Enable rate limiting
    options.EnableEndpointRateLimiting = true;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP"; // Optional
    options.ClientIdHeader = "X-ClientId"; // Optional
    options.IpWhitelist = new List<string> { "127.0.0.1" }; // Optional

    // General rules
    options.GeneralRules = new List<RateLimitRule>
    {
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "60s",
            Limit = 5
        },
        new RateLimitRule
        {
            Endpoint = "*",
            Period = "5m",
            Limit = 20
        }
    };
    //options.QuotaExceededMessage = "{0}, {1}, {2}";
    options.QuotaExceededResponse = new QuotaExceededResponse
    {
        ContentType = "application/json", // Set content type to JSON
        StatusCode = 429, // Too Many Requests
        Content = "{{ \"statusCode\": 429, \"message\": \"Too Many Requests. You have exceeded your rate limit.\", \"details\": {{ \"limit\": \"{0}\", \"period\": \"{1}\", \"retryAfter\": \"{2}{3}\" }} }}"
    };

});


var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts(); // Enforce HTTPS in production
}


app.UseHttpsRedirection();

app.UseIpRateLimiting(); // ✅ Apply rate limiting BEFORE authentication

app.UseAuthentication(); // ✅ Authentication should come AFTER rate limiting
app.UseAuthorization();  // ✅ Authorization should come AFTER authentication

app.UseRateLimitMiddleware(); // ✅ Custom middleware should come AFTER authentication

app.MapControllers();

app.Run();
