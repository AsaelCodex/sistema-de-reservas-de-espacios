namespace SistemaDeReservas.Core.Users;

public class CodigoRecuperacion
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateTime EmitidoEn { get; set; }
    public DateTime VencimientoEn { get; set; }
    public DateTime? UsadoEn { get; set; }
}
