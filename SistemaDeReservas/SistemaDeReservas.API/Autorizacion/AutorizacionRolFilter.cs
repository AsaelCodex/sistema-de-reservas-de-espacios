using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SistemaDeReservas.Business.ControlAcceso;
using SistemaDeReservas.Core.Users;

namespace SistemaDeReservas.API.Autorizacion;

public sealed class AutorizacionRolFilter : IAsyncAuthorizationFilter
{
    private readonly SesionService _sesion;

    public AutorizacionRolFilter(SesionService sesion)
    {
        _sesion = sesion;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor operacion)
        {
            return;
        }

        var nivel = DeclaracionOperaciones.ObtenerNivel(operacion.MethodInfo);
        if (nivel != NivelOperacion.Administrador)
        {
            return;
        }

        var autorizacion = context.HttpContext.Request.Headers["Authorization"].ToString();
        var consulta = await _sesion.ConsultarUsuarioAsync(
            autorizacion, context.HttpContext.RequestAborted);

        if (consulta.Estado == EstadoConsulta.Rechazado || consulta.Usuario is null)
        {
            context.Result = new ObjectResult(new { mensaje = consulta.Mensaje })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        if (consulta.Usuario.Rol != Rol.Administrador)
        {
            context.Result = new ObjectResult(new
            {
                mensaje = "Se requiere el rol Administrador para ejecutar esta operación."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
