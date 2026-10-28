namespace Pos.Infrastructure.Odoo;

public class OdooOptions
{
    public const string SectionKey = "Odoo";

    public string BaseUrl { get; set; } = "http://localhost:8069";
    public string Database { get; set; } = "pos_db";
    public string Username { get; set; } = "bryanleninqn@gmail.com";
    public string ApiKey { get; set; } = "PENDIENTE_GENERAR_EN_ODOO";
    public string Password { get; set; } = string.Empty;

    public string EffectiveSecret =>
        !string.IsNullOrWhiteSpace(ApiKey) && ApiKey != "PENDIENTE_GENERAR_EN_ODOO"
            ? ApiKey
            : Password;
}
