using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Serilog.Context;
using System.Security.Claims;

namespace Presentation.Middleware;

public class JwtMiddleware(
    RequestDelegate next,
    ILogger<JwtMiddleware> logger,
    IConfiguration configuration
)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<JwtMiddleware> _logger = logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
    private readonly string _expectedIssuer =
        configuration["Jwt:Issuer"];

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Capturar X-Request-ID do Kong
        var requestId =
            context.Request.Headers["X-Request-ID"].FirstOrDefault() ?? Guid.NewGuid()
                .ToString("N")[..8];

        // 2. Tentar extrair informações do JWT (se presente)
        var userInfo = TryExtractUserInfo(context.Request);

        // 3. Configurar contexto de log com todas as informações
        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("SessionId", userInfo?.SessionId ?? ""))
        using (LogContext.PushProperty("UserId", userInfo?.UserId ?? ""))
        using (LogContext.PushProperty("Username", userInfo?.Username ?? ""))
        {
            // 4. Log da requisição
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? "";

            _logger.LogInformation("Request received: {Method} {Path}", method, path);

            // 5. Adicionar informações do usuário ao contexto HTTP
            if (userInfo != null)
            {
                context.Items["UserInfo"] = userInfo;

                // Criar ClaimsPrincipal básico para compatibilidade com [Authorize]
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

                // Adicionar roles se existirem
                foreach (var role in userInfo.Roles)
                {
                    claims.Add(new(ClaimTypes.Role, role));
                }

                var identity = new ClaimsIdentity(claims, "jwt");
                context.User = new ClaimsPrincipal(identity);
            }

            // 6. Continuar pipeline
            await _next(context);

            // 7. Log de resposta
            _logger.LogInformation(
                "Request completed: {Method} {Path} -> {StatusCode}",
                method,
                path,
                context.Response.StatusCode
            );
        }
    }

    /// <summary>
    /// Extrai informações básicas do JWT sem validação cryptográfica completa.
    /// Valida apenas estrutura, issuer e expiração.
    /// </summary>
    private SimpleUserInfo? TryExtractUserInfo(HttpRequest request)
    {
        try
        {
            // Extrair token do header Authorization
            var authHeader = request.Headers["Authorization"].FirstOrDefault();
            if (
                string.IsNullOrEmpty(authHeader)
                || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            )
            {
                return null; // Sem token = endpoint público
            }

            var token = authHeader["Bearer ".Length..].Trim();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            // ✅ Verificar se o token tem estrutura válida de JWT
            if (!_tokenHandler.CanReadToken(token))
            {
                _logger.LogWarning("Token JWT com formato inválido");
                return null;
            }

            // ✅ Ler token sem validação de assinatura (apenas estrutura)
            var jwtToken = _tokenHandler.ReadJwtToken(token);

            // ✅ Verificar issuer
            var issuer = jwtToken.Claims.FirstOrDefault(x => x.Type == "iss")?.Value;
            if (string.IsNullOrEmpty(issuer) || issuer != _expectedIssuer)
            {
                _logger.LogWarning(
                    "JWT com issuer inválido. Esperado: {Expected}, Recebido: {Received}",
                    _expectedIssuer,
                    issuer
                );
                return null;
            }

            // ✅ Verificar se não expirou
            var exp = jwtToken.Claims.FirstOrDefault(x => x.Type == "exp")?.Value;
            if (!string.IsNullOrEmpty(exp) && long.TryParse(exp, out var expTimestamp))
            {
                var expirationTime = DateTimeOffset.FromUnixTimeSeconds(expTimestamp);
                if (expirationTime <= DateTimeOffset.UtcNow)
                {
                    _logger.LogWarning("JWT expirado em {ExpirationTime}", expirationTime);
                    return null;
                }
            }

            // ✅ Extrair informações do usuário
            var userId = jwtToken.Claims.FirstOrDefault(x => x.Type == "sub")?.Value ?? "";
            var username =
                jwtToken.Claims.FirstOrDefault(x => x.Type == "preferred_username")?.Value ?? "";
            var email = jwtToken.Claims.FirstOrDefault(x => x.Type == "email")?.Value;
            var sessionId =
                jwtToken.Claims.FirstOrDefault(x => x.Type == "session_state")?.Value ?? "";

            // Extrair roles (podem estar em diferentes claims dependendo do Keycloak)
            var roles = ExtractRoles(jwtToken);

            _logger.LogDebug("JWT válido para usuário: {UserId} ({Username})", userId, username);

            return new SimpleUserInfo
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
            _logger.LogWarning(ex, "Erro ao processar JWT");
            return null;
        }
    }

    /// <summary>
    /// Extrai roles do JWT (Keycloak pode ter estruturas diferentes)
    /// </summary>
    private List<string> ExtractRoles(JwtSecurityToken jwtToken)
    {
        var roles = new List<string>();

        try
        {
            // Tentar extrair de realm_access.roles
            var realmAccess = jwtToken.Claims.FirstOrDefault(x => x.Type == "realm_access")?.Value;
            if (!string.IsNullOrEmpty(realmAccess))
            {
                // Parse JSON simples para extrair roles
                if (realmAccess.Contains("\"roles\""))
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
            }

            // Tentar extrair roles diretas
            var directRoles = jwtToken
                .Claims.Where(x => x.Type == "roles" || x.Type == "role")
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrEmpty(x));
            roles.AddRange(directRoles);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao extrair roles do JWT (não é crítico)");
        }

        return roles.Distinct().ToList();
    }
}

/// <summary>
/// Informações básicas do usuário extraídas do JWT
/// </summary>
public class SimpleUserInfo
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
/// Extensions para facilitar uso nos controllers
/// </summary>
public static class SimpleJwtExtensions
{
    /// <summary>
    /// Obtém informações do usuário do contexto
    /// </summary>
    public static SimpleUserInfo? GetUserInfo(this HttpContext context)
    {
        return context.Items["UserInfo"] as SimpleUserInfo;
    }

    /// <summary>
    /// Verifica se o usuário está autenticado via JWT válido
    /// </summary>
    public static bool IsAuthenticated(this HttpContext context)
    {
        return context.GetUserInfo()?.IsAuthenticated == true;
    }

    /// <summary>
    /// Obtém User ID ou null
    /// </summary>
    public static string? GetUserId(this HttpContext context)
    {
        return context.GetUserInfo()?.UserId;
    }

    /// <summary>
    /// Obtém Username ou null
    /// </summary>
    public static string? GetUsername(this HttpContext context)
    {
        return context.GetUserInfo()?.Username;
    }

    /// <summary>
    /// Obtém Session ID ou null
    /// </summary>
    public static string? GetSessionId(this HttpContext context)
    {
        return context.GetUserInfo()?.SessionId;
    }

    /// <summary>
    /// Verifica se o usuário tem uma role específica
    /// </summary>
    public static bool HasRole(this HttpContext context, string role)
    {
        var userInfo = context.GetUserInfo();
        return userInfo?.Roles?.Contains(role, StringComparer.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Obtém token JWT original
    /// </summary>
    public static string? GetToken(this HttpContext context)
    {
        return context.GetUserInfo()?.Token;
    }
}
