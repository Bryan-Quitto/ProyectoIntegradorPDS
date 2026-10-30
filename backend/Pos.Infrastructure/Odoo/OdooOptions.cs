namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Configuración de conexión a Odoo 20 (API JSON-2). Los valores reales se inyectan
/// exclusivamente por variables de entorno / .env (RNF-09); no hay secretos por defecto.
/// </summary>
public class OdooOptions
{
    public const string SectionKey = "Odoo";
    public const string ApiKeyPlaceholder = "PENDIENTE_GENERAR_EN_ODOO";

    public string BaseUrl { get; set; } = "http://localhost:8069";
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;

    public bool HasUsableApiKey =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.Equals(ApiKey, ApiKeyPlaceholder, StringComparison.Ordinal);
}
