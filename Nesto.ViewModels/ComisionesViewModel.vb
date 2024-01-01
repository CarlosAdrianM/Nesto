Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Globalization
Imports Prism.Commands
Imports Nesto.Modulos.PedidoVenta
Imports System.Net.Http
Imports Newtonsoft.Json
Imports Nesto.Models.Nesto.Models
Imports Unity
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models

Public Class ComisionesViewModel
    Inherits BindableBase

    'Private Shared DbContext As NestoEntities = New NestoEntities
    Private container As IUnityContainer
    Private configuracion As IConfiguracion
    Public ReadOnly Property DialogService As IDialogService

    Private vendedor As String
    Private datosCargados As Boolean = False

    Private mismoMesAnnoPasado As String
    Private mesAnteriorAnnoPasado As String
    Private ReadOnly Property _servicio As ComisionesService
    Public Sub New(container As IUnityContainer, configuracion As IConfiguracion, dialogService As IDialogService)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Me.container = container
        Me.configuracion = configuracion
        dialogService = dialogService
        _servicio = New ComisionesService(configuracion)

        colMeses = New Collection(Of String)
        For i = 12 To 1 Step -1
            Dim nombreMes = Now.AddMonths(i).ToString("MMMM")
            _colMeses.Add(nombreMes)
        Next

        mismoMesAnnoPasado = $"{Now.AddMonths(12).ToString("MMMM")} de {DateTime.Today.AddYears(-1).Year}"
        mesAnteriorAnnoPasado = $"{Now.AddMonths(11).ToString("MMMM")} de {DateTime.Today.AddYears(-1).Year}"
        _colMeses.Add(mismoMesAnnoPasado)
        _colMeses.Add(mesAnteriorAnnoPasado)

        mesActual = colMeses(0)

        Titulo = "Comisiones"

        cmdAbrirPedido = New DelegateCommand(Of Object)(AddressOf OnAbrirPedido, AddressOf CanAbrirPedido)
    End Sub

    Async Function CargarDatos() As Task
        If datosCargados Then
            Return
        End If
        datosCargados = True

        Try
            'If DbContext.Database.Connection.State = System.Data.ConnectionState.Open Then
            '    DbContext.Database.Connection.Close()
            'End If
            listaVendedores = New ObservableCollection(Of VendedorDTO)((Await _servicio.LeerVendedores()).Where(Function(c) c.Estado = 0 OrElse c.Estado = 4 OrElse c.Estado = 2 OrElse c.Estado = 9))
        Catch ex As Exception
            DialogService.ShowError(ex.Message)
        End Try

        vendedorActual = listaVendedores.FirstOrDefault
    End Function

    Private _listaVendedores As ObservableCollection(Of VendedorDTO)
    Public Property listaVendedores As ObservableCollection(Of VendedorDTO)
        Get
            Return _listaVendedores
        End Get
        Set(value As ObservableCollection(Of VendedorDTO))
            _listaVendedores = value
            RaisePropertyChanged(NameOf(listaVendedores))
        End Set
    End Property

    Private _vendedorActual As VendedorDTO
    Public Property vendedorActual As VendedorDTO
        Get
            Return _vendedorActual
        End Get
        Set(value As VendedorDTO)
            If _vendedorActual?.Vendedor = value?.Vendedor Then
                Return
            End If
            _vendedorActual = value
            RaisePropertyChanged("vendedorActual")
            RaisePropertyChanged("MostrarPanelAntiguo")
            RaisePropertyChanged("MostrarPanelComisionAnual")
            If IsNothing(value) Then
                Return
            End If

            If MostrarPanelAntiguo Then
                'Task.Run(Sub()

                Try
                    EstaOcupado = True
                    comisionesActual = _servicio.LeerComisionesAntiguas(fechaDesde, fechaHasta, vendedorActual.Vendedor, 0)
                Catch ex As Exception
                    DialogService.ShowError(ex.Message)
                Finally
                    EstaOcupado = False
                End Try


                '         End Sub)
            Else
                Try
                    EstaOcupado = True
                    CalcularComisionAsync()
                Catch ex As Exception
                    DialogService.ShowError(ex.Message)
                Finally
                    EstaOcupado = False
                End Try
            End If

            listaPedidos = _servicio.LeerListaPedidosVendedor(vendedorActual.Vendedor)
            ActualizarListadosAgrupados()
        End Set
    End Property

    Private _comisionesActual As Comisiones_Result
    Public Property comisionesActual As Comisiones_Result
        Get
            Return _comisionesActual
        End Get
        Set(value As Comisiones_Result)
            _comisionesActual = value
            RaisePropertyChanged("comisionesActual")
        End Set
    End Property

    Private _comisionAnualResumenActual As ComisionAnualResumen
    Public Property ComisionAnualResumenActual() As ComisionAnualResumen
        Get
            Return _comisionAnualResumenActual
        End Get
        Set(ByVal value As ComisionAnualResumen)
            SetProperty(_comisionAnualResumenActual, value)
            RaisePropertyChanged("MostrarColumnaTres")
            RaisePropertyChanged("MostrarColumnaCuatro")
        End Set
    End Property

    Private _colMeses As Collection(Of String)
    Public Property colMeses As Collection(Of String)
        Get
            Return _colMeses
        End Get
        Set(value As Collection(Of String))
            _colMeses = value
        End Set
    End Property

    Private _mesActual
    Public Property mesActual As String
        Get
            Return _mesActual
        End Get
        Set(value As String)
            SetProperty(_mesActual, value)
            RaisePropertyChanged("MostrarPanelAntiguo")
            RaisePropertyChanged("MostrarPanelComisionAnual")
            If mesActual = mismoMesAnnoPasado Then
                fechaDesde = New Date(DateTime.Today.AddYears(-1).Year, DateTime.Today.Month, 1)
            ElseIf mesActual = mesAnteriorAnnoPasado Then
                fechaDesde = New Date(DateTime.Today.AddYears(-1).Year, DateTime.Today.AddMonths(-1).Month, 1)
            Else
                fechaDesde = DateSerial(Year(Now), Date.ParseExact(value, "MMMM", CultureInfo.CurrentCulture).Month, 1)
            End If
            If fechaDesde > Now Then
                fechaDesde = fechaDesde.AddYears(-1)
            End If
            fechaHasta = (fechaDesde.AddMonths(1)).AddDays(-1)

            IncluirAlbaranes = EsMesEnCurso
            IncluirPicking = IncluirPicking AndAlso EsMesEnCurso

            If vendedorActual IsNot Nothing Then
                If MostrarPanelAntiguo Then
                    comisionesActual = _servicio.LeerComisionesAntiguas(fechaDesde, fechaHasta, vendedorActual.Vendedor, 0)
                Else
                    CalcularComisionAsync()
                End If

                ActualizarListadosAgrupados()
            End If
        End Set
    End Property

    Private Sub ActualizarListadosAgrupados()
        If fechaDesde >= New Date(2017, 3, 1) Then
            Dim consulta = _servicio.LeerVentasVendedor(fechaDesde, fechaHasta, vendedorActual.Vendedor)
            listaVentasComision = New ObservableCollection(Of vstLinPedidoVtaComisiones)(consulta.OrderBy(Function(g) g.Grupo).ThenBy(Function(d) d.Dirección))
            listaVentasFamilia = New ObservableCollection(Of vstLinPedidoVtaComisiones)(consulta.OrderBy(Function(g) g.Familia))
            Dim preListaVentasFecha = New ObservableCollection(Of vstLinPedidoVtaComisiones)(consulta.OrderBy(Function(g) g.Fecha_Albarán))

            For Each l In preListaVentasFecha
                Dim fecha As Date = l.Fecha_Albarán
                l.Fecha_Albarán = fecha.Date
            Next
            listaVentasFecha = preListaVentasFecha
        Else
            listaVentasComision = Nothing
            listaVentasFamilia = Nothing
            listaVentasFecha = Nothing
        End If
    End Sub

    Public Property Titulo As String
    Private _fechaDesde As Date
    Public Property fechaDesde As Date
        Get
            Return _fechaDesde
        End Get
        Set(value As Date)
            _fechaDesde = value
            RaisePropertyChanged("EsMesEnCurso")
        End Set
    End Property

    Private _fechaHasta As Date
    Public Property fechaHasta As Date
        Get
            Return _fechaHasta
        End Get
        Set(value As Date)
            _fechaHasta = value
        End Set
    End Property

    Private _listaPedidos As ObservableCollection(Of vstLinPedidoVtaConVendedor)
    Public Property listaPedidos As ObservableCollection(Of vstLinPedidoVtaConVendedor)
        Get
            Return _listaPedidos
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaConVendedor))
            _listaPedidos = value
            RaisePropertyChanged("listaPedidos")
        End Set
    End Property

    Private _listaVentasComision As ObservableCollection(Of vstLinPedidoVtaComisiones)
    Public Property listaVentasComision As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Get
            Return _listaVentasComision
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaComisiones))
            _listaVentasComision = value
            RaisePropertyChanged("listaVentasComision")
        End Set
    End Property

    Private _listaVentasFamilia As ObservableCollection(Of vstLinPedidoVtaComisiones)
    Public Property listaVentasFamilia As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Get
            Return _listaVentasFamilia
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaComisiones))
            _listaVentasFamilia = value
            RaisePropertyChanged(NameOf(listaVentasFamilia))
        End Set
    End Property

    Private _listaVentasFecha As ObservableCollection(Of vstLinPedidoVtaComisiones)
    Public Property listaVentasFecha As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Get
            Return _listaVentasFecha
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaComisiones))
            _listaVentasFecha = value
            RaisePropertyChanged(NameOf(listaVentasFecha))
        End Set
    End Property

    Public ReadOnly Property MostrarPanelAntiguo() As Boolean
        Get
            Return Not IsNothing(vendedorActual) AndAlso (((vendedorActual.Vendedor.Trim() = "JE" OrElse vendedorActual.Vendedor.Trim() = "DV") AndAlso fechaDesde >= New Date(2019, 1, 1)))
        End Get
    End Property


    'Public ReadOnly Property MostrarColumnaTres() As Boolean
    '    Get
    '        Return Not IsNothing(ComisionAnualResumenActual) AndAlso
    '            Not IsNothing(ComisionAnualResumenActual.Etiquetas) AndAlso
    '            ComisionAnualResumenActual.Etiquetas.Count >= 3 AndAlso
    '            Not IsNothing(ComisionAnualResumenActual.Etiquetas(2)) AndAlso
    '            Not IsNothing(ComisionAnualResumenActual.Etiquetas(2).Nombre) AndAlso
    '            ComisionAnualResumenActual.Etiquetas(2).Nombre.Trim <> ""
    '    End Get
    'End Property
    'Public ReadOnly Property MostrarColumnaCuatro() As Boolean
    '    Get
    '        Return Not IsNothing(ComisionAnualResumenActual) AndAlso
    '            Not IsNothing(ComisionAnualResumenActual.Etiquetas) AndAlso
    '            ComisionAnualResumenActual.Etiquetas.Count >= 4 AndAlso
    '            Not IsNothing(ComisionAnualResumenActual.Etiquetas(3)) AndAlso
    '            Not IsNothing(ComisionAnualResumenActual.Etiquetas(3).Nombre) AndAlso
    '            ComisionAnualResumenActual.Etiquetas(3).Nombre.Trim <> ""
    '    End Get
    'End Property

    Private _incluirAlbaranes As Boolean = True
    Public Property IncluirAlbaranes() As Boolean
        Get
            Return _incluirAlbaranes
        End Get
        Set(ByVal value As Boolean)
            If _incluirAlbaranes <> value Then
                SetProperty(_incluirAlbaranes, value)
                RaisePropertyChanged("MostrarPanelAntiguo")
                RaisePropertyChanged("MostrarPanelComisionAnual")
                CalcularComisionAsync()
            End If
        End Set
    End Property

    Private _incluirPicking As Boolean
    Public Property IncluirPicking As Boolean
        Get
            Return _incluirPicking
        End Get
        Set(value As Boolean)
            If _incluirPicking <> value Then
                SetProperty(_incluirPicking, value)
                RaisePropertyChanged("MostrarPanelAntiguo")
                RaisePropertyChanged("MostrarPanelComisionAnual")
                CalcularComisionAsync()
            End If
        End Set
    End Property

    Public ReadOnly Property MostrarPanelComisionAnual As Boolean
        Get
            Return Not MostrarPanelAntiguo
        End Get
    End Property

    Public ReadOnly Property EsMesEnCurso As Boolean
        Get
            Return fechaDesde.Month = Today.Month
        End Get
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(ByVal value As Boolean) ' el ByVal es necesario para que actualice
            SetProperty(_estaOcupado, value)
        End Set
    End Property

#Region "Comandos"
    Private _cmdAbrirPedido As DelegateCommand(Of Object)
    Public Property cmdAbrirPedido As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirPedido
        End Get
        Private Set(value As DelegateCommand(Of Object))
            SetProperty(_cmdAbrirPedido, value)
        End Set
    End Property
    Private Function CanAbrirPedido(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirPedido(arg As Object)
        If IsNothing(arg) Then
            Return
        End If
        PedidoVentaViewModel.CargarPedido(arg.Empresa, arg.Número, container)
    End Sub

#End Region

    Private Async Function CalcularComisionAsync() As Task
        ComisionAnualResumenActual = Await CalcularComisionAnual(vendedorActual.Vendedor, fechaDesde.Year, fechaDesde.Month, IncluirAlbaranes, IncluirPicking)
    End Function


    Private Async Function CalcularComisionAnual(vendedor As String, anno As Integer, mes As Integer, incluirAlbaranes As Boolean, incluirPicking As Boolean) As Task(Of ComisionAnualResumen)
        EstaOcupado = True

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""


            Try
                Dim urlConsulta As String = "Comisiones"
                urlConsulta += "?vendedor=" + vendedor.Trim
                urlConsulta += "&anno=" + anno.ToString
                urlConsulta += "&mes=" + mes.ToString
                urlConsulta += "&incluirAlbaranes=" + incluirAlbaranes.ToString
                urlConsulta += "&incluirPicking=" + incluirPicking.ToString

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If
                Dim comision As ComisionAnualResumen = JsonConvert.DeserializeObject(Of ComisionAnualResumen)(respuesta)
                Return comision
            Catch ex As Exception
                Throw New Exception("No se han podido cargar las comisiones del vendedor " + vendedor)
            Finally
                EstaOcupado = False
            End Try



        End Using
    End Function
End Class

