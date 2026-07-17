using System.Text.Json.Serialization;

namespace Tranquility.Wire;

/// <summary>Documented token issue request (POST /auth/token).</summary>
public sealed record TokenRequest(string? Username, string? Password);

/// <summary>OAuth2-style token response with documented snake_case fields.</summary>
public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
