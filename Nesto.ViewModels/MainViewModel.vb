Imports System.Windows.Input
Imports System.Windows.Controls
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media.Imaging
Imports Prism.Ioc
Imports CommunityToolkit.Mvvm.ComponentModel
Imports System.Net.Http
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Nesto.Contratos
Imports Unity
Imports System.ComponentModel
Imports System.Data
Imports Nesto.Infrastructure.Shared

' Nesto#340 (4A.5): la RelayCommand propia de siempre, renombrada para que no tape a la
' RelayCommand de CommunityToolkit.Mvvm (en VB el tipo del namespace gana al Imports).
' Sigue en Agencias, Alquileres, Clientes, Deuda, PlanesVentajas y Remesas hasta que esos
' usos migren; no se toca su comportamiento.
Public Class RelayCommandLegado
    Implements ICommand

    Private ReadOnly _execute As Action(Of Object)
    Private ReadOnly _canExecute As Predicate(Of Object)
    Public Sub New(ByVal execute As Action(Of Object))
        Me.New(execute, Nothing)
    End Sub
    Public Sub New(ByVal execute As Action(Of Object), ByVal canExecute As Predicate(Of Object))
        If execute Is Nothing Then
            Throw New ArgumentNullException("execute")
        End If
        _execute = execute
        _canExecute = canExecute
    End Sub
    ' Incidente 13/10/26 (Laura Magán): un CanExecute que lanza se reevalúa en CADA RequerySuggested
    ' (mover el ratón, el foco, el propio diálogo de error...), así que cada error cerrado abría otro:
    ' 174 errores en ELMAH y Nesto sin poder cerrarse. Si el predicado lanza, el comando queda
    ' desactivado y el error se registra UNA sola vez por comando (no en cada reevaluación).
    Private _errorCanExecuteRegistrado As Boolean

    ''' <summary>
    ''' Cómo se registra un error de CanExecute. Por defecto, IServicioRegistroErrores (POST api/Errores)
    ''' resuelto del contenedor de Prism; los tests lo sustituyen.
    ''' </summary>
    Public Shared Property RegistrarErrorCanExecute As Action(Of Exception, String) = AddressOf RegistrarErrorEnServidor

    '<DebuggerStepThrough()> _
    Public Function CanExecute(ByVal parameter As Object) As Boolean Implements ICommand.CanExecute
        If _canExecute Is Nothing Then
            Return True
        End If
        Try
            Return _canExecute(parameter)
        Catch ex As Exception
            If Not _errorCanExecuteRegistrado Then
                _errorCanExecuteRegistrado = True
                Try
                    RegistrarErrorCanExecute?.Invoke(ex, "RelayCommandLegado.CanExecute (" + _canExecute.Method.DeclaringType?.Name + "." + _canExecute.Method.Name + ")")
                Catch
                    ' El registro de errores nunca debe empeorar lo que ya ha fallado.
                End Try
            End If
            Return False
        End Try
    End Function

    Private Shared Sub RegistrarErrorEnServidor(ex As Exception, contexto As String)
        Debug.WriteLine(contexto + ": " + ex.ToString())
        Dim servicioErrores = TryCast(Nesto.Infrastructure.Contracts.Contenedor.Actual?.Resolver(GetType(Nesto.Infrastructure.Contracts.IServicioRegistroErrores)), Nesto.Infrastructure.Contracts.IServicioRegistroErrores)
        Dim unused = servicioErrores?.RegistrarErrorAsync(ex, contexto)
    End Sub

    Public Custom Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged

        AddHandler(ByVal value As EventHandler)
            AddHandler CommandManager.RequerySuggested, value
        End AddHandler

        RemoveHandler(ByVal value As EventHandler)
            RemoveHandler CommandManager.RequerySuggested, value
        End RemoveHandler
        RaiseEvent(ByVal sender As System.Object, ByVal e As System.EventArgs)
        End RaiseEvent
    End Event
    Public Sub Execute(ByVal parameter As Object) Implements ICommand.Execute
        _execute(parameter)
    End Sub

End Class
Public Class MainViewModel
    Inherits ObservableObject

    Public Sub New()

    End Sub

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(ByVal value As String)
            SetProperty(_titulo, value)
        End Set
    End Property


    Public ReadOnly Property mostrarFechas As Visibility
        Get
            If opcionesFechas = "Personalizar" Then
                Return Visibility.Visible
            Else
                Return Visibility.Hidden
            End If
        End Get
    End Property

    Private _opcionesFechas As String = "Actual"
    Public Property opcionesFechas As String
        Get
            Return _opcionesFechas
        End Get
        Set(value As String)
            _opcionesFechas = value
            OnPropertyChanged("mostrarFechas")
        End Set
    End Property

    Private ReadOnly container As IUnityContainer

    Public Sub New(container As IUnityContainer)
        Dim parametros = New Parametros
        CargarVendedor()
        Me.container = container
        'cmdCerrarVentana = New RelayCommand(Of Object)(AddressOf OnCerrarVentana, AddressOf CanCerrarVentana)
        Titulo = "Sin Título"
    End Sub

    Public Async Function CargarVendedor() As Task
        Vendedor = Await leerParametro("1", Parametros.Claves.Vendedor)
    End Function

    Private _Vendedor As String
    Public Property Vendedor As String
        Get
            Return _Vendedor
        End Get

        Set(ByVal value As String)
            Me._Vendedor = value
            OnPropertyChanged("Vendedor")
        End Set
    End Property

    Private _fechaInformeInicial As Date = Today
    Public Property fechaInformeInicial As Date
        Get
            Return _fechaInformeInicial
        End Get
        Set(value As Date)
            _fechaInformeInicial = value
        End Set
    End Property

    Private _fechaInformeFinal As Date = Today
    Public Property fechaInformeFinal As Date
        Get
            Return _fechaInformeFinal
        End Get
        Set(value As Date)
            _fechaInformeFinal = value
        End Set
    End Property

    Private Function CrearClienteApi() As HttpClient
        Try
            ' Nesto#369: cliente con JWT y URL de configuración (funciona también en desarrollo/localhost)
            Return Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(Of Nesto.Infrastructure.Contracts.IClienteApiFactory)().Crear()
        Catch
            ' Fallback si el contenedor no está disponible (tests, diseño)
            Return New HttpClient With {.BaseAddress = New Uri("http://api.nuevavision.es/api/")}
        End Try
    End Function

    Public Async Function leerParametro(empresa As String, clave As String) As Task(Of String)

        Using client As HttpClient = CrearClienteApi()
            Dim response As HttpResponseMessage

            Try
                response = Await client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=" + System.Environment.UserName + "&clave=" + clave)

                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    respuesta = JsonConvert.DeserializeObject(Of String)(respuesta)
                    Return respuesta.Trim
                Else
                    Throw New Exception("No se puede leer el parámetro")
                End If
            Catch ex As Exception
                Throw New Exception("No se puede leer el parámetro")
            End Try

        End Using

    End Function

End Class
Public Class RatioDataTemplateSelector
    Inherits DataTemplateSelector
    Public Overrides Function SelectTemplate(ByVal item As Object, ByVal container As DependencyObject) As DataTemplate

        Dim element As FrameworkElement
        element = TryCast(container, FrameworkElement)

        If element IsNot Nothing AndAlso item IsNot Nothing Then

            Dim RatioItem As Double = TryCast(item.Item("Ratio"), Object)

            If CDbl(RatioItem.ToString) >= 0 Then
                Return TryCast(element.FindResource("RatiosTemplateVerde"), DataTemplate)
            Else
                Return TryCast(element.FindResource("RatiosTemplateRojo"), DataTemplate)
            End If
        End If

        Return Nothing
    End Function
End Class

Public Class IconoConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements System.Windows.Data.IValueConverter.Convert
        Dim uri
        If value >= 0 Then
            uri = New Uri("pack://application:,,,/images/FlechaVerde.png")

        Else
            uri = New Uri("pack://application:,,,/images/FlechaRoja.png")
        End If
        Return New BitmapImage(uri)
    End Function
    Public Function ConvertBack(value As Object, targetType As System.Type, parameter As Object, culture As System.Globalization.CultureInfo) As Object Implements System.Windows.Data.IValueConverter.ConvertBack
        Return 0
    End Function
End Class