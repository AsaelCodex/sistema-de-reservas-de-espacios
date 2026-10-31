using SistemaDeReservas.Business.GestionPermisos;
using SistemaDeReservas.Business.Reservas;
using SistemaDeReservas.Core.Permissions;
using SistemaDeReservas.Core.Reservations;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Tests.Fakes;

namespace SistemaDeReservas.Tests.Reservas;

public class CrearReservaServiceTests
{
    private static readonly DateTime Fecha = new(2026, 11, 10);

    private readonly FakeReservaRepository _reservas = new();
    private readonly FakeUsuarioRepository _usuarios = new();
    private readonly FakePermisoOtorgadoRepository _permisosOtorgados = new();
    private readonly CrearReservaService _servicio;

    public CrearReservaServiceTests()
    {
        _servicio = new CrearReservaService(
            _reservas,
            new AutorizacionPermisosService(_usuarios, _permisosOtorgados));
    }

    // RF-NEG-02 / RF-NEG-06: con permiso otorgado, el propietario es el actor,
    // el servidor genera Id y CreadaEn, y la reserva nace en Solicitada.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Crear_WithGrantedPermission_CreatesSolicitadaOwnedByActor()
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "  salón a  ",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        var reserva = Assert.IsType<Reserva>(resultado.Reserva);
        Assert.NotEqual(Guid.Empty, reserva.Id);
        Assert.Equal(usuario.Id, reserva.UsuarioId);
        Assert.Equal("SALÓN A", reserva.Espacio);
        Assert.Equal(Fecha.Date, reserva.Fecha);
        Assert.Equal(new TimeSpan(10, 0, 0), reserva.HoraInicio);
        Assert.Equal(new TimeSpan(12, 0, 0), reserva.HoraFin);
        Assert.Equal(EstadoReserva.Solicitada, reserva.Estado);
        Assert.NotEqual(default, reserva.CreadaEn);
        Assert.Equal(DateTimeKind.Utc, reserva.CreadaEn.Kind);
        Assert.Same(reserva, Assert.Single(_reservas.Reservas));
    }

    // RF-NEG-06 / RF-GP-05: el Administrador conserva el comportamiento
    // implícito del catálogo y no necesita un permiso otorgado.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Crear_AsAdministratorWithoutGrant_IsCreated()
    {
        // Preparar
        var administrador = await CrearUsuarioAsync(Rol.Administrador);

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(administrador),
            "SALON A",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Empty(_permisosOtorgados.Permisos);
        Assert.Single(_reservas.Reservas);
    }

    // RF-NEG-06: sin permiso no se guarda nada.
    [Fact]
    [Trait("Requerimiento", "RF-NEG-06")]
    public async Task Crear_WithoutPermission_IsRejectedWithoutWrite()
    {
        // Preparar
        var usuario = await CrearUsuarioAsync();

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.PermisoDenegado, resultado.Estado);
        Assert.Contains("permiso", resultado.Mensaje);
        Assert.Null(resultado.Reserva);
        Assert.Empty(_reservas.Reservas);
    }

    // RD-07: un actor ausente, vacío o con rol fuera del enum se rechaza sin
    // consultar disponibilidad ni escribir.
    [Theory]
    [InlineData("nulo")]
    [InlineData("vacio")]
    [InlineData("rol")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithInvalidActor_IsRejectedWithoutWrite(string caso)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        ActorReserva? actor = caso switch
        {
            "nulo" => null,
            "vacio" => new ActorReserva(Guid.Empty, Rol.Administrador),
            _ => new ActorReserva(usuario.Id, (Rol)99)
        };

        // Actuar
        var resultado = await _servicio.CrearAsync(
            actor,
            "SALON A",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.ActorInvalido, resultado.Estado);
        Assert.Contains("no es válido", resultado.Mensaje);
        Assert.Null(resultado.Reserva);
        Assert.Empty(_reservas.Reservas);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithoutSpace_IsRejectedWithoutWrite(string? espacio)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            espacio,
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.DatosInvalidos, resultado.Estado);
        Assert.Contains("espacio", resultado.Mensaje);
        Assert.Empty(_reservas.Reservas);
    }

    // RD-07: la longitud se comprueba contra la columna real (200).
    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithSpaceLongerThanPersistence_IsRejectedWithoutWrite()
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        var espacio = new string('A', EspacioReserva.LongitudMaxima + 1);

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            espacio,
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.DatosInvalidos, resultado.Estado);
        Assert.Contains("200", resultado.Mensaje);
        Assert.Empty(_reservas.Reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithSpaceAtMaximumLength_IsCreated()
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        var espacio = new string('A', EspacioReserva.LongitudMaxima);

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            espacio,
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Equal(espacio, resultado.Reserva!.Espacio);
        Assert.Single(_reservas.Reservas);
    }

    [Fact]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithDefaultDate_IsRejectedWithoutWrite()
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            default,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.DatosInvalidos, resultado.Estado);
        Assert.Contains("fecha", resultado.Mensaje);
        Assert.Empty(_reservas.Reservas);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(10, 10)]
    [InlineData(12, 10)]
    [InlineData(10, 24)]
    [InlineData(24, 25)]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithInvalidTimeRange_IsRejectedWithoutWrite(
        int inicioHoras,
        int finHoras)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(inicioHoras, 0, 0),
            new TimeSpan(finHoras, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.DatosInvalidos, resultado.Estado);
        Assert.Contains("00:00", resultado.Mensaje);
        Assert.Empty(_reservas.Reservas);
    }

    // RD-07: 00:00 y el último instante anterior a 24:00 son válidos.
    [Theory]
    [InlineData(0, 0, 0, 1)]
    [InlineData(0, 0, 23, 59)]
    [InlineData(23, 58, 23, 59)]
    [Trait("Requerimiento", "RD-07")]
    public async Task Crear_WithBoundaryTimes_IsCreated(
        int inicioHoras,
        int inicioMinutos,
        int finHoras,
        int finMinutos)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(inicioHoras, inicioMinutos, 0),
            new TimeSpan(finHoras, finMinutos, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Single(_reservas.Reservas);
    }

    // RF-NEG-02: solapamiento exacto, parcial y contenido.
    [Theory]
    [InlineData(10, 12, 10, 12)]
    [InlineData(10, 12, 11, 13)]
    [InlineData(10, 12, 9, 11)]
    [InlineData(10, 12, 10, 11)]
    [InlineData(10, 12, 9, 13)]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WithOverlappingTime_IsRejectedWithoutWrite(
        int inicioExistente,
        int finExistente,
        int inicioNuevo,
        int finNuevo)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        var existente = Existente(
            EstadoReserva.Solicitada,
            new TimeSpan(inicioExistente, 0, 0),
            new TimeSpan(finExistente, 0, 0));
        await _reservas.GuardarAsync(existente);

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(inicioNuevo, 0, 0),
            new TimeSpan(finNuevo, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.ConflictoHorario, resultado.Estado);
        Assert.Contains("horario", resultado.Mensaje);
        Assert.Null(resultado.Reserva);
        Assert.Same(existente, Assert.Single(_reservas.Reservas));
    }

    // RF-NEG-02: los horarios consecutivos no se solapan.
    [Theory]
    [InlineData(12, 14)]
    [InlineData(8, 10)]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WithConsecutiveTime_IsCreated(int inicioNuevo, int finNuevo)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        await _reservas.GuardarAsync(Existente(
            EstadoReserva.Solicitada,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0)));

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(inicioNuevo, 0, 0),
            new TimeSpan(finNuevo, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Equal(2, _reservas.Reservas.Count);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WithSameTimeInAnotherSpace_IsCreated()
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        await _reservas.GuardarAsync(Existente(
            EstadoReserva.Solicitada,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0),
            espacio: "SALON A"));

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON B",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Equal(2, _reservas.Reservas.Count);
    }

    [Fact]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WithSameTimeOnAnotherDate_IsCreated()
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        await _reservas.GuardarAsync(Existente(
            EstadoReserva.Solicitada,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0)));

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha.AddDays(1),
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Equal(2, _reservas.Reservas.Count);
    }

    // RF-NEG-02: Solicitada y Confirmada bloquean el espacio.
    [Theory]
    [InlineData(EstadoReserva.Solicitada)]
    [InlineData(EstadoReserva.Confirmada)]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WhenExistingStateBlocks_IsRejected(EstadoReserva estado)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        await _reservas.GuardarAsync(Existente(
            estado,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0)));

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.ConflictoHorario, resultado.Estado);
        Assert.Single(_reservas.Reservas);
    }

    // RF-NEG-02: Rechazada, Cancelada y Finalizada no bloquean.
    [Theory]
    [InlineData(EstadoReserva.Rechazada)]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Finalizada)]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WhenExistingStateIsTerminal_IsCreated(EstadoReserva estado)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        await _reservas.GuardarAsync(Existente(
            estado,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0)));

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            "SALON A",
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.Creada, resultado.Estado);
        Assert.Equal(2, _reservas.Reservas.Count);
    }

    // RF-NEG-02: el nombre se normaliza antes de comparar; mayúsculas y
    // espacios externos no permiten evadir la comprobación.
    [Theory]
    [InlineData("SALON A", "salon a")]
    [InlineData("SALON A", "  SALON A  ")]
    [InlineData("SALON A", "Salon A")]
    [Trait("Requerimiento", "RF-NEG-02")]
    public async Task Crear_WithEquivalentSpaceName_IsRejected(
        string espacioExistente,
        string espacioNuevo)
    {
        // Preparar
        var usuario = await CrearUsuarioAutorizadoAsync();
        await _reservas.GuardarAsync(Existente(
            EstadoReserva.Solicitada,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0),
            espacio: espacioExistente));

        // Actuar
        var resultado = await _servicio.CrearAsync(
            Actor(usuario),
            espacioNuevo,
            Fecha,
            new TimeSpan(10, 0, 0),
            new TimeSpan(12, 0, 0));

        // Verificar
        Assert.Equal(EstadoCreacionReserva.ConflictoHorario, resultado.Estado);
        Assert.Single(_reservas.Reservas);
    }

    private async Task<Usuario> CrearUsuarioAsync(Rol rol = Rol.Estandar)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario de prueba",
            Correo = $"{Guid.NewGuid():N}@prueba.local",
            ContraseñaHash = "hash-de-prueba",
            Rol = rol,
            Activo = true
        };
        await _usuarios.AgregarAsync(usuario);
        return usuario;
    }

    private async Task<Usuario> CrearUsuarioAutorizadoAsync()
    {
        var usuario = await CrearUsuarioAsync();
        _permisosOtorgados.AgregarValidado(new PermisoOtorgado
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            Permiso = Permiso.CrearReserva,
            SolicitudPermisoId = Guid.NewGuid(),
            OtorgadoEn = DateTime.UtcNow
        });
        return usuario;
    }

    private static ActorReserva Actor(Usuario usuario)
    {
        return new ActorReserva(usuario.Id, usuario.Rol);
    }

    private static Reserva Existente(
        EstadoReserva estado,
        TimeSpan horaInicio,
        TimeSpan horaFin,
        string espacio = "SALON A")
    {
        return new Reserva
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Espacio = EspacioReserva.Normalizar(espacio),
            Fecha = Fecha.Date,
            HoraInicio = horaInicio,
            HoraFin = horaFin,
            Estado = estado,
            CreadaEn = DateTime.UtcNow
        };
    }
}
