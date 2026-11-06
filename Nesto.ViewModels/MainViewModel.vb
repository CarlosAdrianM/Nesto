Imports System.Windows.Input
Imports System.Windows.Controls
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media.Imaging
Imports CommunityToolkit.Mvvm.ComponentModel
Imports System.Net.Http
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Nesto.Contratos
Imports System.ComponentModel
Imports System.Data
Imports Nesto.Infrastructure.Shared

Public Class MainViewModel
    Inherits ObservableObject

    ' Nesto#490 (4C.5, tramo 3b): fuera el constructor que recibía el IUnityContainer y lo guardaba sin usarlo. Nadie pide
    ' este ViewModel al contenedor: todos lo crean con New.
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