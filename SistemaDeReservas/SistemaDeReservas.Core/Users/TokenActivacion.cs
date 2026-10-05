namespace SistemaDeReservas.Core.Users;

public class TokenActivacion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime EmitidoEn { get; set; }
    public DateTime VencimientoEn { get; set; }
    public DateTime? UsadoEn { get; set; }
}
