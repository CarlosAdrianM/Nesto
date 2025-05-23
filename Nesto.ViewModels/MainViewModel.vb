﻿Imports Nesto.Models.MainModel
Imports System.Windows.Input
Imports System.Windows.Controls
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media.Imaging
Imports Prism.Mvvm
Imports Prism.Regions
Imports System.Net.Http
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports Nesto.Contratos
Imports Unity
Imports System.ComponentModel
Imports System.Data
Imports Nesto.Infrastructure.Shared

Public Class RelayCommand
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
    '<DebuggerStepThrough()> _
    Public Function CanExecute(ByVal parameter As Object) As Boolean Implements ICommand.CanExecute

        Return If(_canExecute Is Nothing, True, _canExecute(parameter))

    End Function
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
    Inherits BindableBase

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

    Private _RatiosVenta As RatioVenta
    Public Property RatiosVenta As RatioVenta
        Get
            Return _RatiosVenta
        End Get

        Set(ByVal value As RatioVenta)
            Me._RatiosVenta = value
            RaisePropertyChanged("RatiosVenta")
        End Set
    End Property
    Public Property MediaRatioVenta() As Decimal
        Get
            Dim decSuma As Decimal
            For Each item In Me.RatiosVenta
                decSuma = decSuma + item.Ratio
            Next
            If Me.RatiosVenta.Count <> 0 Then
                decSuma = decSuma / Me.RatiosVenta.Count
            Else
                decSuma = 0
            End If
            Return decSuma
        End Get
        Set(value As Decimal)

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
            RaisePropertyChanged("mostrarFechas")
        End Set
    End Property

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager)
        'Me._RatiosVenta = RatioVenta.CargarRatiosVenta
        Dim parametros = New Parametros
        CargarVendedor()
        Me.container = container
        Me.regionManager = regionManager
        'cmdCerrarVentana = New DelegateCommand(Of Object)(AddressOf OnCerrarVentana, AddressOf CanCerrarVentana)
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
            RaisePropertyChanged("Vendedor")
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

    Public Async Function leerParametro(empresa As String, clave As String) As Task(Of String)

        Using client As New HttpClient
            client.BaseAddress = New Uri("http://api.nuevavision.es/api/")
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