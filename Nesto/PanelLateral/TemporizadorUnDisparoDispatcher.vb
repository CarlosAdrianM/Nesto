Imports System.Windows.Threading
Imports Nesto.Infrastructure.Menu

''' <summary>
''' Nesto#655 (26/11/26): el temporizador de un solo disparo del plegado por tiempo del menú lateral, con un DispatcherTimer (vence en
''' el hilo de la UI). Al vencer se para antes de llamar, así que dispara una vez. Nunca lanza.
''' </summary>
Public NotInheritable Class TemporizadorUnDisparoDispatcher
    Implements ITemporizadorUnDisparo

    Private ReadOnly _temporizador As DispatcherTimer
    Private _alVencer As Action

    Public Sub New(dispatcher As Dispatcher)
        _temporizador = New DispatcherTimer(DispatcherPriority.Background, If(dispatcher, Dispatcher.CurrentDispatcher))
        AddHandler _temporizador.Tick, AddressOf AlVencer
    End Sub

    Public ReadOnly Property Programado As Boolean Implements ITemporizadorUnDisparo.Programado
        Get
            Return _temporizador.IsEnabled
        End Get
    End Property

    Public Sub Programar(espera As TimeSpan, alVencer As Action) Implements ITemporizadorUnDisparo.Programar
        Try
            _temporizador.Stop()
            _alVencer = alVencer
            _temporizador.Interval = espera
            _temporizador.Start()
        Catch
            ' Sin temporizador, el panel se pliega igual al trabajar fuera
        End Try
    End Sub

    Public Sub Cancelar() Implements ITemporizadorUnDisparo.Cancelar
        Try
            _temporizador.Stop()
        Catch
        End Try
        _alVencer = Nothing
    End Sub

    Private Sub AlVencer(sender As Object, e As EventArgs)
        Dim accion As Action = _alVencer
        Cancelar()
        Try
            accion?.Invoke()
        Catch
            ' La acción ya registra sus errores
        End Try
    End Sub
End Class
