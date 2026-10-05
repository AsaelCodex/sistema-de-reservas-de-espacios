namespace SistemaDeReservas.Core.Users;

public class Sesion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime EmitidaEn { get; set; }
    public DateTime VencimientoEn { get; set; }
}
