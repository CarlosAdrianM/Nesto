''' <summary>
''' Respuesta de NestoAPI al actualizar el seguimiento de un envío a demanda
''' (POST api/EnviosAgencias/{id}/ActualizarSeguimiento). <see cref="Estado"/> usa la numeración
''' canónica de EnviosAgencia.Estado (En curso 0, Tramitado 1, Entregado 2, Incidentado 3, Devuelto 4,
''' Cerrado a mano 5 de NestoAPI#676, que el seguimiento nunca devuelve: solo lo pone una persona).
''' </summary>
Public Class SeguimientoActualizadoDto
    Public Property Estado As Short
    Public Property FechaEntrega As Date?
    Public Property Detalle As String
End Class
