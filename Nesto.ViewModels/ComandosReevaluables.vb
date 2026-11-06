Imports System.Runtime.CompilerServices
Imports System.Windows.Input
Imports CommunityToolkit.Mvvm.Input

''' <summary>
''' Nesto#621: sustituye a la RelayCommandLegado (la RelayCommand propia de MainViewModel.vb). Devuelve un
''' <see cref="RelayCommand(Of Object)"/> del CommunityToolkit con las dos cosas que aquella hacía y el
''' Toolkit no hace por sí solo, para que Agencias, Alquileres, Clientes, PlanesVentajas y Remesas se
''' comporten exactamente igual:
''' <list type="bullet">
''' <item>Se reevalúa con <see cref="CommandManager.RequerySuggested"/> (foco, teclado, ratón,
''' <c>CommandManager.InvalidateRequerySuggested</c>), que es de lo que dependen esas pantallas para
''' activar y desactivar sus botones: en cada RequerySuggested se llama a <c>NotifyCanExecuteChanged</c>.</item>
''' <item>Si el predicado lanza, el comando queda desactivado y el error se registra UNA sola vez por
''' comando (incidente 13/10/26, Laura Magán: un CanExecute que lanza se reevalúa en CADA RequerySuggested
''' y cada error cerrado abría otro: 174 errores en ELMAH y Nesto sin poder cerrarse).</item>
''' </list>
''' </summary>
Public NotInheritable Class ComandosReevaluables

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Cómo se registra un error de CanExecute. Por defecto, IServicioRegistroErrores (POST api/Errores)
    ''' resuelto del contenedor; los tests lo sustituyen.
    ''' </summary>
    Public Shared Property RegistrarErrorCanExecute As Action(Of Exception, String) = AddressOf RegistrarErrorEnServidor

    ' RequerySuggested guarda sus manejadores con referencia débil: cada manejador vive lo que vive su comando.
    Private Shared ReadOnly _manejadores As New ConditionalWeakTable(Of RelayCommand(Of Object), ReevaluadorComando)

    Public Shared Function Crear(execute As Action(Of Object)) As RelayCommand(Of Object)
        Return Crear(execute, Nothing)
    End Function

    Public Shared Function Crear(execute As Action(Of Object), canExecute As Predicate(Of Object)) As RelayCommand(Of Object)
        If execute Is Nothing Then
            Throw New ArgumentNullException(NameOf(execute))
        End If
        If canExecute Is Nothing Then
            Return New RelayCommand(Of Object)(execute)
        End If

        Dim comando As New RelayCommand(Of Object)(execute, PredicadoProtegido(canExecute))
        Dim reevaluador As New ReevaluadorComando(comando)
        _manejadores.Add(comando, reevaluador)
        AddHandler CommandManager.RequerySuggested, AddressOf reevaluador.AlSugerirReevaluacion
        Return comando
    End Function

    Private Shared Function PredicadoProtegido(canExecute As Predicate(Of Object)) As Predicate(Of Object)
        Dim errorRegistrado As Boolean = False
        Return Function(parameter As Object) As Boolean
                   Try
                       Return canExecute(parameter)
                   Catch ex As Exception
                       If Not errorRegistrado Then
                           errorRegistrado = True
                           Try
                               RegistrarErrorCanExecute?.Invoke(ex, "ComandosReevaluables.CanExecute (" + canExecute.Method.DeclaringType?.Name + "." + canExecute.Method.Name + ")")
                           Catch
                               ' El registro de errores nunca debe empeorar lo que ya ha fallado.
                           End Try
                       End If
                       Return False
                   End Try
               End Function
    End Function

    Private Shared Sub RegistrarErrorEnServidor(ex As Exception, contexto As String)
        Debug.WriteLine(contexto + ": " + ex.ToString())
        Dim servicioErrores = TryCast(Nesto.Infrastructure.Contracts.Contenedor.Actual?.Resolver(GetType(Nesto.Infrastructure.Contracts.IServicioRegistroErrores)), Nesto.Infrastructure.Contracts.IServicioRegistroErrores)
        Dim unused = servicioErrores?.RegistrarErrorAsync(ex, contexto)
    End Sub

    Private NotInheritable Class ReevaluadorComando
        Private ReadOnly _comando As RelayCommand(Of Object)

        Public Sub New(comando As RelayCommand(Of Object))
            _comando = comando
        End Sub

        Public Sub AlSugerirReevaluacion(sender As Object, e As EventArgs)
            _comando.NotifyCanExecuteChanged()
        End Sub
    End Class

End Class
