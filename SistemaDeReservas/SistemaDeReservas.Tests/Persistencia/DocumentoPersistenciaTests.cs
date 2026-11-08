using Microsoft.EntityFrameworkCore;
using SistemaDeReservas.Business.Documentos;
using SistemaDeReservas.Core.Documents;
using SistemaDeReservas.Core.Users;
using SistemaDeReservas.Infrastructure.Repositories;
using SistemaDeReservas.Infrastructure.Storage;

namespace SistemaDeReservas.Tests.Persistencia;

/// <summary>
/// RF-DOC-01 a RF-DOC-04 y RD-09: metadatos, listados y eliminación lógica con
/// repositorios EF reales sobre SQL Server LocalDB. Cada verificación lee con
/// un contexto nuevo e independiente del que escribió.
/// </summary>
[Collection(ColeccionPersistencia.NombreColeccionPersistencia)]
public class DocumentoPersistenciaTests : BasePruebaPersistencia
{
    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-DOC-01")]
    [Trait("Requerimiento", "RD-09")]
    public async Task Guardar_PersistsCompleteMetadataReadableFromNewContext()
    {
        var usuario = await CrearUsuarioAsync();
        var fecha = new DateTime(2026, 11, 8, 12, 30, 0, DateTimeKind.Utc);
        var documento = new Documento
        {
            Id = Guid.NewGuid(),
            NombreOriginal = "acta.pdf",
            Tipo = "application/pdf",
            TamanoBytes = 1234,
            FechaSubidaUtc = fecha,
            UsuarioId = usuario.Id,
            ContenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant(),
            Eliminado = false
        };

        await using (var contexto = NuevoContexto())
        {
            await new DocumentoRepository(contexto).GuardarAsync(documento);
        }

        await using var verificacion = NuevoContexto();
        var guardado = await verificacion.Documentos
            .AsNoTracking()
            .SingleAsync(fila => fila.Id == documento.Id);

        Assert.Equal("acta.pdf", guardado.NombreOriginal);
        Assert.Equal("application/pdf", guardado.Tipo);
        Assert.Equal(1234, guardado.TamanoBytes);
        Assert.Equal(fecha, guardado.FechaSubidaUtc);
        Assert.Equal(usuario.Id, guardado.UsuarioId);
        Assert.Equal(documento.ContenidoId, guardado.ContenidoId);
        Assert.False(guardado.Eliminado);
        Assert.Null(guardado.EliminadoEnUtc);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-DOC-02")]
    [Trait("Requerimiento", "RF-DOC-04")]
    public async Task Listados_ExcludeDeletedDocumentsAndFilterByOwner()
    {
        var propietario = await CrearUsuarioAsync();
        var otro = await CrearUsuarioAsync();
        var propio = Documento(propietario.Id, "propio.txt", new DateTime(2026, 11, 1));
        var propioEliminado = Documento(
            propietario.Id, "eliminado.txt", new DateTime(2026, 11, 2));
        propioEliminado.Eliminar(DateTime.UtcNow);
        var ajeno = Documento(otro.Id, "ajeno.txt", new DateTime(2026, 11, 3));

        await using (var contexto = NuevoContexto())
        {
            var repositorio = new DocumentoRepository(contexto);
            await repositorio.GuardarAsync(propio);
            await repositorio.GuardarAsync(propioEliminado);
            await repositorio.GuardarAsync(ajeno);
        }

        await using var verificacion = NuevoContexto();
        var repositorioVerificacion = new DocumentoRepository(verificacion);
        var propios = await repositorioVerificacion.ObtenerPorUsuarioAsync(propietario.Id);
        var todos = await repositorioVerificacion.ObtenerTodosAsync();

        Assert.Equal(new[] { propio.Id }, propios.Select(documento => documento.Id));
        Assert.Equal(
            new[] { ajeno.Id, propio.Id },
            todos.Select(documento => documento.Id));

        await using var contextoFila = NuevoContexto();
        var filaEliminada = await contextoFila.Documentos
            .AsNoTracking()
            .SingleAsync(documento => documento.Id == propioEliminado.Id);
        Assert.True(filaEliminada.Eliminado);
        Assert.NotNull(filaEliminada.EliminadoEnUtc);
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-DOC-04")]
    [Trait("Requerimiento", "RD-04")]
    [Trait("Requerimiento", "RD-09")]
    public async Task EliminarLogico_ConServicioReal_ConservaFilaYContenidoFisico()
    {
        var usuario = await CrearUsuarioAsync();
        var directorio = Path.Combine(
            Path.GetTempPath(),
            "documentos-persistencia-" + Guid.NewGuid().ToString("N"));
        var contenido = "contenido persistente"u8.ToArray();

        try
        {
            Documento subido;
            await using (var contexto = NuevoContexto())
            {
                var servicio = new SubirDocumentoService(
                    new DocumentoRepository(contexto),
                    new AlmacenArchivosDocumentos(directorio),
                    1024);
                var resultado = await servicio.SubirAsync(
                    new ActorDocumento(usuario.Id, Rol.Estandar),
                    "acta.txt",
                    "text/plain",
                    new MemoryStream(contenido));

                Assert.Equal(EstadoSubidaDocumento.Subido, resultado.Estado);
                subido = Assert.IsType<Documento>(resultado.Documento);
            }

            await using (var contexto = NuevoContexto())
            {
                var servicio = new EliminarDocumentoService(
                    new DocumentoRepository(contexto));
                var resultado = await servicio.EliminarAsync(
                    new ActorDocumento(usuario.Id, Rol.Estandar),
                    subido.Id);

                Assert.Equal(EstadoEliminacionDocumento.Eliminado, resultado.Estado);
            }

            await using var verificacion = NuevoContexto();
            var fila = await verificacion.Documentos
                .AsNoTracking()
                .SingleAsync(documento => documento.Id == subido.Id);
            Assert.True(fila.Eliminado);
            Assert.NotNull(fila.EliminadoEnUtc);

            var archivo = Assert.Single(Directory.GetFiles(directorio));
            Assert.Equal(contenido, await File.ReadAllBytesAsync(archivo));
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [LocalDbFact]
    [Trait("Categoria", "Persistencia")]
    [Trait("Requerimiento", "RF-DOC-01")]
    public async Task Esquema_CreaIndiceUnicoDeContenido()
    {
        var usuario = await CrearUsuarioAsync();
        var contenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var primero = Documento(usuario.Id, "primero.txt", DateTime.UtcNow);
        primero.ContenidoId = contenidoId;
        var segundo = Documento(usuario.Id, "segundo.txt", DateTime.UtcNow);
        segundo.ContenidoId = contenidoId;

        await using (var contexto = NuevoContexto())
        {
            await new DocumentoRepository(contexto).GuardarAsync(primero);
        }

        await using var contextoDuplicado = NuevoContexto();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
            await new DocumentoRepository(contextoDuplicado).GuardarAsync(segundo));

        await using var verificacion = NuevoContexto();
        Assert.Equal(1, await verificacion.Documentos.CountAsync());
    }

    private static Documento Documento(Guid usuarioId, string nombre, DateTime fecha) => new()
    {
        Id = Guid.NewGuid(),
        NombreOriginal = nombre,
        Tipo = "text/plain",
        TamanoBytes = 1,
        FechaSubidaUtc = fecha,
        UsuarioId = usuarioId,
        ContenidoId = Guid.NewGuid().ToString("N").ToUpperInvariant(),
        Eliminado = false
    };
}
