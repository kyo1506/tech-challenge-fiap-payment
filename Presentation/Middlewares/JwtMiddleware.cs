using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Serilog.Context;

namespace Presentation.Middlewares;

/// <summary>
/// Middleware híbrido que combina funcionalidades:
/// 1. Enriquecimento de logs com contexto de requisição (como Identity)
/// 2. Validação e autenticação JWT (como Games)
/// 3. Suporte completo ao Kong Ingress Controller
/// </summary>
public class JwtMiddleware(
    RequestDelegate next,
    ILogger<JwtMiddleware> logger,
    IConfiguration configuration
)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<JwtMiddleware> _logger = logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly string? _expectedIssuer = configuration["Jwt:Issuer"];

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId =
            context.Request.Headers["X-Kong-Request-ID"].FirstOrDefault() ?? Guid.NewGuid()
                .ToString("N")[..8];

        var correlationId =
            context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context
                .Request.Headers["X-Request-ID"]
                .FirstOrDefault()
            ?? Guid.NewGuid().ToString("N")[..12];

        var userInfo = TryExtractAndValidateUserInfo(context.Request);

        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("SessionId", userInfo?.SessionId ?? ""))
        using (LogContext.PushProperty("UserId", userInfo?.UserId ?? ""))
        using (LogContext.PushProperty("Username", userInfo?.Username ?? ""))
        {
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "";

            _logger.LogInformation("Request started: {Method} {Path}", method, path);

            try
            {
                if (userInfo != null)
                {
                    context.Items["UserInfo"] = userInfo;
                    context.User = CreateClaimsPrincipal(userInfo);
                }

                await _next(context);

                _logger.LogInformation(
                    "Request completed: {Method} {Path} -> {StatusCode}",
                    method,
                    path,
                    context.Response.StatusCode
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Request failed: {Method} {Path}", method, path);
                throw;
            }
        }
    }

    /// <summary>
    /// Extrai e VALIDA completamente o JWT (mais robusto que Identity)
    /// </summary>
    private UserInfo? TryExtractAndValidateUserInfo(HttpRequest request)
    {
        try
        {
            var authHeader = request.Headers.Authorization.FirstOrDefault();
            if (
                string.IsNullOrEmpty(authHeader)
                || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            )
            {
                return null;
            }

            var token = authHeader["Bearer ".Length..].Trim();
            if (string.IsNullOrEmpty(token) || !_tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("JWT com formato inválido");
                return null;
            }

            var jwtToken = _tokenHandler.ReadJwtToken(token);

            var issuer = jwtToken.Claims.FirstOrDefault(x => x.Type == "iss")?.Value;
            if (!string.IsNullOrEmpty(_expectedIssuer) && issuer != _expectedIssuer)
            {
                _logger.LogDebug(
                    "JWT issuer mismatch. Expected: {Expected}, Got: {Received}",
                    _expectedIssuer,
                    issuer
                );
                return null;
            }

            var exp = jwtToken.Claims.FirstOrDefault(x => x.Type == "exp")?.Value;
            if (!string.IsNullOrEmpty(exp) && long.TryParse(exp, out var expTimestamp))
            {
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expTimestamp);
                if (expirationTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogDebug("JWT expired at {ExpirationTime}", expirationTime);
                    return null;
                }
            }

            var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? "";
            var username =
                jwtToken.Claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value ?? "";
            var email = jwtToken.Claims.FirstOrDefault(x => x.Type == "email")?.Value;
            var sessionId =
                jwtToken.Claims.FirstOrDefault(x => x.Type == "session_state")?.Value ?? "";
            var roles = ExtractRoles(jwtToken);

            return new UserInfo
            {
                UserId = userId,
                Username = username,
                Email = email,
                SessionId = sessionId,
                Roles = roles,
                IsAuthenticated = true,
                Token = token,
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Não foi possível extrair/validar JWT");
            return null;
        }
    }

    /// <summary>
    /// Extrai roles do Keycloak JWT
    /// </summary>
    private List<string> ExtractRoles(JwtSecurityToken jwtToken)
    {
        var roles = new List<string>();

        try
        {
            var realmAccess = jwtToken.Claims.FirstOrDefault(x => x.Type == "realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccess) && realmAccess.Contains("\"roles\""))
            {
                var rolesStart = realmAccess.IndexOf("[");
                var rolesEnd = realmAccess.IndexOf("]");
                if (rolesStart > 0 && rolesEnd > rolesStart)
                {
                    var rolesJson = realmAccess.Substring(
                        rolesStart + 1,
                        rolesEnd - rolesStart - 1
                    );
                    var roleItems = rolesJson.Split(',');
                    foreach (var role in roleItems)
                    {
                        var cleanRole = role.Trim().Replace("\"", "");
                        if (!string.IsNullOrEmpty(cleanRole))
                            roles.Add(cleanRole);
                    }
                }
            }

            var directRoles = jwtToken
                .Claims.Where(x => x.Type == "roles" || x.Type == "role")
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrEmpty(x));
            roles.AddRange(directRoles);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao extrair roles (não crítico)");
        }

        return [.. roles.Distinct()];
    }

    /// <summary>
    /// Cria ClaimsPrincipal para compatibilidade com [Authorize]
    /// </summary>
    private static ClaimsPrincipal CreateClaimsPrincipal(UserInfo userInfo)
    {
        var claims = new List<Claim>
        {
            new("sub", userInfo.UserId),
            new("preferred_username", userInfo.Username),
            new("session_state", userInfo.SessionId),
            new(ClaimTypes.NameIdentifier, userInfo.UserId),
            new(ClaimTypes.Name, userInfo.Username),
        };

        if (!string.IsNullOrEmpty(userInfo.Email))
        {
            claims.Add(new("email", userInfo.Email));
            claims.Add(new(ClaimTypes.Email, userInfo.Email));
        }

        // Adicionar roles
        foreach (var role in userInfo.Roles)
        {
            claims.Add(new(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, "jwt");
        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Informações do usuário
/// </summary>
public class UserInfo
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string? Email { get; set; }
    public string SessionId { get; set; } = "";
    public List<string> Roles { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public string Token { get; set; } = "";
}

/// <summary>
/// Extensions para o middleware JWT
/// </summary>
public static class EnhancedJwtExtensions
{
    public static UserInfo? GetUserInfo(this HttpContext context) =>
        context.Items["UserInfo"] as UserInfo;

    public static bool IsAuthenticated(this HttpContext context) =>
        context.GetUserInfo()?.IsAuthenticated == true;

    public static string? GetUserId(this HttpContext context) => context.GetUserInfo()?.UserId;

    public static string? GetUsername(this HttpContext context) => context.GetUserInfo()?.Username;

    public static string? GetSessionId(this HttpContext context) =>
        context.GetUserInfo()?.SessionId;

    public static bool HasRole(this HttpContext context, string role) =>
        context.GetUserInfo()?.Roles?.Contains(role, StringComparer.OrdinalIgnoreCase) == true;

    public static string? GetToken(this HttpContext context) => context.GetUserInfo()?.Token;

    public static string? GetCorrelationId(this HttpContext context) =>
        context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? context
            .Request.Headers["X-Request-ID"]
            .FirstOrDefault();

    public static string? GetRequestId(this HttpContext context) =>
        context.Request.Headers["X-Kong-Request-ID"].FirstOrDefault();
}

/// <summary>
/// Extension para registro do middleware
/// </summary>
public static class JwtMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtMiddleware(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<JwtMiddleware>();
    }
}
