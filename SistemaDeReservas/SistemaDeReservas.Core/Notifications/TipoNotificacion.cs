namespace SistemaDeReservas.Core.Notifications;

/// <summary>
/// D-NOT-02: catálogo de notificaciones internas de la Práctica 5. Se
/// serializa como texto en la API.
/// </summary>
public enum TipoNotificacion
{
    SolicitudPermisoCreada,
    SolicitudPermisoAprobada,
    SolicitudPermisoRechazada,
    ReservaConfirmada
}
