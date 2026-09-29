Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports FakeItEasy.Core
Imports Nesto.Infrastructure.Contracts

''' <summary>
''' Nesto#490 (4C.2): configura un IServicioDialogos falso como hacían los tests con el ShowDialog de Prism
''' (al que acababan llamando todas las extensiones): toda confirmación (ShowConfirmation, ShowConfirmationAnswer,
''' ShowConfirmationAsync) se contesta con <paramref name="respuesta"/>, y cada diálogo se anota con el nombre
''' que tenía en Prism ("ConfirmationDialog" o "NotificationDialog" para ShowNotification/ShowError) y su mensaje.
''' </summary>
Friend Module DialogosDePrueba

    Friend Sub Configurar(dialogos As IServicioDialogos, respuesta As Func(Of Boolean),
                          Optional anotar As Action(Of String, String) = Nothing)
        A.CallTo(dialogos).WithReturnType(Of Boolean)() _
            .ReturnsLazily(Function(c As IFakeObjectCall)
                               AnotarLlamada(c, anotar)
                               Return EsConfirmacion(c) AndAlso respuesta()
                           End Function)
        A.CallTo(dialogos).WithReturnType(Of Task(Of Boolean))() _
            .ReturnsLazily(Function(c As IFakeObjectCall)
                               AnotarLlamada(c, anotar)
                               Return Task.FromResult(EsConfirmacion(c) AndAlso respuesta())
                           End Function)
        A.CallTo(dialogos).WithVoidReturnType() _
            .Invokes(Sub(c As IFakeObjectCall)
                         AnotarLlamada(c, anotar)
                         Dim callback = c.Arguments.OfType(Of Action(Of ResultadoDialogo)).FirstOrDefault()
                         If callback IsNot Nothing AndAlso EsConfirmacion(c) Then
                             callback(New ResultadoDialogo(If(respuesta(), ResultadoBoton.OK, ResultadoBoton.Cancel)))
                         End If
                     End Sub)
    End Sub

    Private Function EsConfirmacion(c As IFakeObjectCall) As Boolean
        Return c.Method.Name.StartsWith("ShowConfirmation", StringComparison.Ordinal)
    End Function

    Private Sub AnotarLlamada(c As IFakeObjectCall, anotar As Action(Of String, String))
        If anotar Is Nothing Then
            Return
        End If
        Dim nombre As String
        If EsConfirmacion(c) Then
            nombre = "ConfirmationDialog"
        ElseIf c.Method.Name = "ShowNotification" OrElse c.Method.Name = "ShowError" Then
            nombre = "NotificationDialog"
        ElseIf c.Method.Name = "ShowDialog" Then
            nombre = CStr(c.Arguments(0))
        Else
            nombre = c.Method.Name
        End If
        Dim parametros = c.Method.GetParameters()
        Dim mensaje As String = Nothing
        For i = 0 To parametros.Length - 1
            If parametros(i).Name = "message" Then
                mensaje = TryCast(c.Arguments(i), String)
            End If
        Next
        anotar(nombre, mensaje)
    End Sub
End Module
