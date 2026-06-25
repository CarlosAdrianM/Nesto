''' <summary>
''' Respuesta de NestoAPI al actualizar el seguimiento de un envío a demanda
''' (POST api/EnviosAgencias/{id}/ActualizarSeguimiento). <see cref="Estado"/> usa la numeración
''' canónica de EnviosAgencia.Estado (En curso 0, Tramitado 1, Entregado 2, Incidentado 3, Devuelto 4).
''' </summary>
Public Class SeguimientoActualizadoDto
    Public Property Estado As Short
    Public Property FechaEntrega As Date?
    Public Property Detalle As String
End Class
