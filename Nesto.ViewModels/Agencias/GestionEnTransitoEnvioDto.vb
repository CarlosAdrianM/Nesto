''' <summary>
''' Nesto#625 (NestoAPI#726): respuesta de GET api/EnviosAgencias/{id}/Gestiones: qué se le puede pedir a la agencia sobre un
''' envío que ya viaja (hoy solo CTT). Si no se puede nada, Motivo dice por qué. Mismos nombres que el servidor (PascalCase).
''' </summary>
Public Class GestionesEnvioDto
    Public Property PararEnVuelo As Boolean
    Public Property Retorno As Boolean
    Public Property CambiarFecha As Boolean
    Public Property CambiarDireccion As Boolean
    Public Property Motivo As String

    ''' <summary>Ninguna gestión posible (también cuando la API no contesta).</summary>
    Public Shared ReadOnly Property Ninguna As GestionesEnvioDto
        Get
            Return New GestionesEnvioDto()
        End Get
    End Property

    Public ReadOnly Property AlgunaPosible As Boolean
        Get
            Return PararEnVuelo OrElse Retorno OrElse CambiarFecha OrElse CambiarDireccion
        End Get
    End Property
End Class

''' <summary>Nesto#625: respuesta { Mensaje } de PararEnVuelo, FechaEntrega y DireccionEntrega.</summary>
Public Class MensajeGestionEnvioDto
    Public Property Mensaje As String
End Class

''' <summary>Nesto#625: respuesta de POST api/EnviosAgencias/{id}/Retorno. EnvioRetornoId puede venir vacío.</summary>
Public Class RetornoEnvioDto
    Public Property CodigoRetorno As String
    Public Property EnvioRetornoId As Integer?
End Class

''' <summary>Nesto#625: cuerpo de POST api/EnviosAgencias/{id}/FechaEntrega. La fecha va como "yyyy-MM-dd".</summary>
Public Class FechaEntregaEnvioPeticion
    Public Property Fecha As String
End Class

''' <summary>
''' Nesto#625: cuerpo de POST api/EnviosAgencias/{id}/DireccionEntrega. Pais en ISO2 (ES por defecto). Teléfono y correo son
''' obligatorios para CTT.
''' </summary>
Public Class DireccionEntregaEnvioPeticion
    Public Property Nombre As String
    Public Property Direccion As String
    Public Property CodigoPostal As String
    Public Property Poblacion As String
    Public Property Pais As String
    Public Property Telefono As String
    Public Property Correo As String
End Class
