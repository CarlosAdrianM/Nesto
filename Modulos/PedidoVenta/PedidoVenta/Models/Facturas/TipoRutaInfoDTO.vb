Imports System.Collections.Generic

Namespace Models.Facturas
    ''' <summary>
    ''' DTO con información de un tipo de ruta para mostrar en UI.
    ''' Corresponde al modelo del backend TipoRutaInfoDTO.
    ''' </summary>
    Public Class TipoRutaInfoDTO
        ''' <summary>
        ''' Identificador único del tipo de ruta (ej: "PROPIA", "AGENCIA")
        ''' </summary>
        Public Property Id As String

        ''' <summary>
        ''' Nombre amigable para mostrar en la interfaz (ej: "Ruta propia")
        ''' </summary>
        Public Property NombreParaMostrar As String

        ''' <summary>
        ''' Descripción del tipo de ruta
        ''' </summary>
        Public Property Descripcion As String

        ''' <summary>
        ''' Lista de códigos de ruta que pertenecen a este tipo (ej: ["16", "AT"])
        ''' </summary>
        Public Property RutasContenidas As List(Of String)

        ''' <summary>
        ''' Texto para mostrar en UI: "NombreParaMostrar (rutasContenidas separadas por comas)"
        ''' Ej: "Ruta propia (16, AT)"
        ''' </summary>
        Public ReadOnly Property DisplayText As String
            Get
                If RutasContenidas IsNot Nothing AndAlso RutasContenidas.Count > 0 Then
                    Dim rutasText = String.Join(", ", RutasContenidas)
                    Return $"{NombreParaMostrar} ({rutasText})"
                Else
                    Return NombreParaMostrar
                End If
            End Get
        End Property
    End Class
End Namespace
