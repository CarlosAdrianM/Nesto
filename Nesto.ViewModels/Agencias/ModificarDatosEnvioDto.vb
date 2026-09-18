''' <summary>
''' Cuerpo de POST api/EnviosAgencias/{id}/ModificarDatos (Nesto#340 slice A4.4). Los nombres son el
''' contrato con ModificarDatosEnvioDTO del servidor (ver AgenciaServiceModificarDatosTests).
''' No confundir con ModificarEnvioAgenciaDto, que corrige la DIRECCIÓN en la agencia (#317).
''' </summary>
Public Class ModificarDatosEnvioDto
    Public Property Reembolso As Decimal
    Public Property Retorno As Short
    ''' <summary>Descripción del tipo de retorno anterior (por agencia, solo la sabe el cliente), para la historia.</summary>
    Public Property RetornoAnteriorDescripcion As String
    Public Property Estado As Short
    Public Property FechaEntrega As Date?
    Public Property Rehusar As Boolean
    Public Property Observaciones As String
End Class

''' <summary>Respuesta del servidor a ModificarDatos.</summary>
Public Class ResultadoModificacionEnvioDto
    Public Property Numero As Integer
    Public Property CamposModificados As List(Of String)
    Public Property Asiento As Integer
    Public Property Rehusado As Boolean
    Public Property Mensaje As String
End Class
