Imports Nesto.ViewModels

' Incidente 13/10/26 (Laura Magán): un CanExecute que lanza no puede tumbar la aplicación ni
' inundar ELMAH (se reevalúa en cada RequerySuggested). El comando queda desactivado y el error
' se registra una sola vez.
<TestClass()>
Public Class ComandosReevaluablesTests

    Private _registradorOriginal As Action(Of Exception, String)
    Private _registrados As List(Of String)

    <TestInitialize()>
    Public Sub Initialize()
        _registradorOriginal = ComandosReevaluables.RegistrarErrorCanExecute
        _registrados = New List(Of String)
        ComandosReevaluables.RegistrarErrorCanExecute = Sub(ex, contexto) _registrados.Add(contexto)
    End Sub

    <TestCleanup()>
    Public Sub Cleanup()
        ComandosReevaluables.RegistrarErrorCanExecute = _registradorOriginal
    End Sub

    Private Shared Function PredicadoQueLanza(param As Object) As Boolean
        Throw New NullReferenceException()
    End Function

    <TestMethod()>
    Public Sub CanExecute_SiElPredicadoLanza_DevuelveFalse()
        Dim comando = ComandosReevaluables.Crear(Sub(p) Return, AddressOf PredicadoQueLanza)

        Assert.IsFalse(comando.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanExecute_SiElPredicadoLanzaVariasVeces_RegistraElErrorUnaSolaVez()
        Dim comando = ComandosReevaluables.Crear(Sub(p) Return, AddressOf PredicadoQueLanza)

        For i = 1 To 10
            Dim unused = comando.CanExecute(Nothing)
        Next

        Assert.AreEqual(1, _registrados.Count)
        StringAssert.Contains(_registrados.Single(), "PredicadoQueLanza")
    End Sub

    <TestMethod()>
    Public Sub CanExecute_SiElRegistroFallaTambien_NoLanza()
        ComandosReevaluables.RegistrarErrorCanExecute = Sub(ex, contexto) Throw New InvalidOperationException()
        Dim comando = ComandosReevaluables.Crear(Sub(p) Return, AddressOf PredicadoQueLanza)

        Assert.IsFalse(comando.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Sub CanExecute_SinPredicadoONormal_SigueIgual()
        Assert.IsTrue(ComandosReevaluables.Crear(Sub(p) Return).CanExecute(Nothing))
        Assert.IsFalse(ComandosReevaluables.Crear(Sub(p) Return, Function(p) False).CanExecute(Nothing))
        Assert.AreEqual(0, _registrados.Count)
    End Sub

    <TestMethod()>
    Public Sub Crear_DevuelveLaRelayCommandDelToolkit()
        Assert.IsInstanceOfType(ComandosReevaluables.Crear(Sub(p) Return, Function(p) True), GetType(CommunityToolkit.Mvvm.Input.RelayCommand(Of Object)))
    End Sub

    ' Como la RelayCommandLegado: las pantallas que la usaban activan y desactivan sus botones con
    ' CommandManager.RequerySuggested (foco, teclado, InvalidateRequerySuggested), no con NotifyCanExecuteChanged.
    <TestMethod()>
    Public Sub CanExecuteChanged_SeLevantaConRequerySuggested()
        Dim comando = ComandosReevaluables.Crear(Sub(p) Return, Function(p) True)
        Dim veces = 0
        AddHandler comando.CanExecuteChanged, Sub(s, e) veces += 1

        System.Windows.Input.CommandManager.InvalidateRequerySuggested()
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(Sub()
                                                                     End Sub, System.Windows.Threading.DispatcherPriority.SystemIdle)

        Assert.IsTrue(veces >= 1)
        GC.KeepAlive(comando)
    End Sub
End Class
