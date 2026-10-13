Imports Nesto.ViewModels

' Incidente 13/10/26 (Laura Magán): un CanExecute que lanza no puede tumbar la aplicación ni
' inundar ELMAH (se reevalúa en cada RequerySuggested). El comando queda desactivado y el error
' se registra una sola vez.
<TestClass()>
Public Class RelayCommandLegadoTests

    Private _registradorOriginal As Action(Of Exception, String)
    Private _registrados As List(Of String)

    <TestInitialize()>
    Public Sub Initialize()
        _registradorOriginal = RelayCommandLegado.RegistrarErrorCanExecute
        _registrados = New List(Of String)
        RelayCommandLegado.RegistrarErrorCanExecute = Sub(ex, contexto) _registrados.Add(contexto)
    End Sub

    <TestCleanup()>
    Public Sub Cleanup()
        RelayCommandLegado.RegistrarErrorCanExecute = _registradorOriginal
    End Sub

    Private Shared Function PredicadoQueLanza(param As Object) As Boolean
        Throw New NullReferenceException()
    End Function

    <TestMethod()>
    Public Sub CanExecute_SiElPredicadoLanza_DevuelveFalse()
        Dim comando As New RelayCommandLegado(Sub(p) Return, AddressOf PredicadoQueLanza)

        Assert.IsFalse(comando.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanExecute_SiElPredicadoLanzaVariasVeces_RegistraElErrorUnaSolaVez()
        Dim comando As New RelayCommandLegado(Sub(p) Return, AddressOf PredicadoQueLanza)

        For i = 1 To 10
            Dim unused = comando.CanExecute(Nothing)
        Next

        Assert.AreEqual(1, _registrados.Count)
        StringAssert.Contains(_registrados.Single(), "PredicadoQueLanza")
    End Sub

    <TestMethod()>
    Public Sub CanExecute_SiElRegistroFallaTambien_NoLanza()
        RelayCommandLegado.RegistrarErrorCanExecute = Sub(ex, contexto) Throw New InvalidOperationException()
        Dim comando As New RelayCommandLegado(Sub(p) Return, AddressOf PredicadoQueLanza)

        Assert.IsFalse(comando.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanExecute_SinPredicadoONormal_SigueIgual()
        Assert.IsTrue(New RelayCommandLegado(Sub(p) Return).CanExecute(Nothing))
        Assert.IsFalse(New RelayCommandLegado(Sub(p) Return, Function(p) False).CanExecute(Nothing))
        Assert.AreEqual(0, _registrados.Count)
    End Sub
End Class
