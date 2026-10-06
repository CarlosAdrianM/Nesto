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
    ''' <summary>
    ''' NestoAPI#597: lo que ha pasado con la agencia (reenviado y qué etiqueta pegar, «viajará en el cierre
    ''' del día» o «pídeselo a la agencia»). Se enseña tal cual. Nothing si no aplica.
    ''' </summary>
    Public Property Aviso As String
    Public Property ReenviadoAAgencia As Boolean
    ''' <summary>Albarán con el que queda el envío tras reenviarlo (en CTT, uno nuevo).</summary>
    Public Property Albaran As String
    Public Property Bultos As Integer
    Public Property Reimpresion As Boolean
    ''' <summary>Etiqueta nueva en ZPL, como en TramitarEnvioResultadoDto.</summary>
    Public Property EtiquetaTipo As String
    Public Property EtiquetaCodificacion As String
    Public Property EtiquetaContenido As String
End Class
