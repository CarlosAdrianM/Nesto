Imports System.Text.RegularExpressions

''' <summary>
''' Incidencia 1191: al borrar un envío que nació de «Volver a enviar» un devuelto (EnviosAgenciaReenvios.EnvioNuevo,
''' NestoAPI#699) la BD lo impide (FK_EnviosAgenciaReenvios_Nuevo) y Nesto enseñaba el genérico de EF en inglés
''' («An error occurred while updating the entries...»). Aquí se desenrolla la excepción y se dice la causa de verdad.
''' OJO: en RELEASE la API solo devuelve el mensaje de fuera (sin InnerException), así que muchas veces solo llega el
''' genérico; para ese caso se da un mensaje que apunta a lo más probable.
''' </summary>
Public NotInheritable Class ErroresBorradoEnvio
    Private Sub New()
    End Sub

    Public Const MENSAJE_GENERICO_EF As String = "An error occurred while updating the entries"
    Public Const CLAVE_AJENA_REENVIOS As String = "FK_EnviosAgenciaReenvios"

    Public Const MENSAJE_REENVIO As String = "Este envío no se puede borrar porque nació de la decisión de volver a enviar un envío devuelto. Si no se va a reenviar, avise a Informática."
    Public Const MENSAJE_GENERICO As String = "La base de datos no deja borrar este envío, probablemente porque está relacionado con otros datos (por ejemplo, porque nació de la decisión de volver a enviar un envío devuelto). Si no se va a reenviar, avise a Informática."

    Private Shared ReadOnly _restriccion As New Regex("REFERENCE\s+(?:constraint\s+)?""(?<fk>[^""]+)""", RegexOptions.IgnoreCase)
    Private Shared ReadOnly _tabla As New Regex("(?:table|tabla)\s+""(?:[^"".]+\.)?(?<tabla>[^""]+)""", RegexOptions.IgnoreCase)

    ''' <summary>La excepción más interna de la cadena (EF anida DbUpdateException → UpdateException → SqlException).</summary>
    Public Shared Function ExcepcionMasInterna(ex As Exception) As Exception
        Dim actual As Exception = ex
        While actual?.InnerException IsNot Nothing
            actual = actual.InnerException
        End While
        Return actual
    End Function

    ''' <summary>Los mensajes de toda la cadena, de fuera a dentro, uno por línea.</summary>
    Public Shared Function TextoDeLaCadena(ex As Exception) As String
        Dim mensajes As New List(Of String)
        Dim actual As Exception = ex
        While actual IsNot Nothing
            mensajes.Add(actual.Message)
            actual = actual.InnerException
        End While
        Return String.Join(Environment.NewLine, mensajes)
    End Function

    ''' <summary>El mensaje para el usuario cuando falla «Borrar Envío».</summary>
    Public Shared Function MensajeParaUsuario(ex As Exception) As String
        If ex Is Nothing Then
            Return "No se ha podido borrar el envío."
        End If
        Dim texto = TextoDeLaCadena(ex)

        If texto.IndexOf(CLAVE_AJENA_REENVIOS, StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return MENSAJE_REENVIO
        End If

        Dim restriccion = _restriccion.Match(texto)
        If restriccion.Success Then
            Dim tabla = _tabla.Match(texto, restriccion.Index)
            Dim conQue = If(tabla.Success, $"«{tabla.Groups("tabla").Value}»", "otros datos")
            Return $"Este envío no se puede borrar porque está relacionado con {conQue} (restricción {restriccion.Groups("fk").Value}). Avise a Informática."
        End If

        If texto.IndexOf(MENSAJE_GENERICO_EF, StringComparison.OrdinalIgnoreCase) >= 0 Then
            Return MENSAJE_GENERICO
        End If

        Return ExcepcionMasInterna(ex).Message
    End Function
End Class
