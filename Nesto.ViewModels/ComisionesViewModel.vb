Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.ComponentModel
Imports System.Windows
Imports System.Globalization
Imports Microsoft.Practices.Prism.Commands
Imports Nesto.Modulos.PedidoVenta
Imports Microsoft.Practices.Unity
Imports Nesto.ViewModels
Imports System.Threading.Tasks
Imports System.Net.Http
Imports Nesto.Contratos
Imports Newtonsoft.Json

Public Class ComisionesViewModel
    Inherits Nesto.Contratos.ViewModelBase

    Private Shared DbContext As NestoEntities
    Private container As IUnityContainer
    Private configuracion As IConfiguracion

    Dim mainModel As New Nesto.Models.MainModel
    Private vendedor As String = mainModel.leerParametro("1", "Vendedor")

    Public Sub New(container As IUnityContainer, configuracion As IConfiguracion)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Me.container = container
        Me.configuracion = configuracion
        DbContext = New NestoEntities
        If vendedor = "" Then
            listaVendedores = New ObservableCollection(Of Vendedores)(From c In DbContext.Vendedores Where c.Empresa = "1" And (c.Estado = 0 OrElse c.Estado = 4 OrElse c.Estado = 2))
        Else
            listaVendedores = New ObservableCollection(Of Vendedores)(From c In DbContext.Vendedores Where c.Empresa = "1" And (c.Estado = 0 OrElse c.Estado = 4 OrElse c.Estado = 2) And c.Número.Trim = vendedor)
        End If

        colMeses = New Collection(Of String)
        For i = 12 To 1 Step -1
            _colMeses.Add(Now.AddMonths(i).ToString("MMMM"))
        Next

        mesActual = colMeses(0)
        vendedorActual = listaVendedores.FirstOrDefault

        Titulo = "Comisiones"

        cmdAbrirPedido = New DelegateCommand(Of Object)(AddressOf OnAbrirPedido, AddressOf CanAbrirPedido)


    End Sub

    Private _listaVendedores As ObservableCollection(Of Vendedores)
    Public Property listaVendedores As ObservableCollection(Of Vendedores)
        Get
            Return _listaVendedores
        End Get
        Set(value As ObservableCollection(Of Vendedores))
            _listaVendedores = value
            OnPropertyChanged("listaVendedores")
        End Set
    End Property

    Private _vendedorActual As Vendedores
    Public Property vendedorActual As Vendedores
        Get
            Return _vendedorActual
        End Get
        Set(value As Vendedores)
            _vendedorActual = value
            OnPropertyChanged("vendedorActual")

            MostrarPanelAntiguo = vendedorActual.Número = "IF " OrElse vendedorActual.Número = "AH " OrElse vendedorActual.Número = "SC " OrElse vendedorActual.Número = "PI "

            If MostrarPanelAntiguo Then
                comisionesActual = DbContext.Comisiones("1", fechaDesde, fechaHasta, vendedorActual.Número, 0).FirstOrDefault
            Else
                CalcularComisionAsync()
            End If

            listaPedidos = New ObservableCollection(Of vstLinPedidoVtaConVendedor)(From l In DbContext.vstLinPedidoVtaConVendedor
                                                                                   Where (l.Empresa = "1" Or l.Empresa = "3") And l.Estado >= -1 And l.Estado <= 1 And l.Vendedor = vendedorActual.Número
                                                                                   Order By l.Número, l.Nº_Orden
                                                                                   Select l)
            If fechaDesde >= New Date(2017, 3, 1) Then
                listaVentasComision = New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In DbContext.vstLinPedidoVtaComisiones
                                                                                             Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                                 (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                                 l.Vendedor = vendedorActual.Número)
                                                                                             Order By l.Grupo, l.Dirección
                                                                                             Select l)
                listaVentasFamilia = New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In DbContext.vstLinPedidoVtaComisiones
                                                                                            Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                                 (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                                 l.Vendedor = vendedorActual.Número)
                                                                                            Order By l.Familia
                                                                                            Select l)
            Else
                listaVentasComision = Nothing
                listaVentasFamilia = Nothing
            End If
        End Set
    End Property

    Private _comisionesActual As Comisiones_Result
    Public Property comisionesActual As Comisiones_Result
        Get
            Return _comisionesActual
        End Get
        Set(value As Comisiones_Result)
            _comisionesActual = value
            OnPropertyChanged("comisionesActual")
        End Set
    End Property

    Private _comisionAnualResumenActual As ComisionAnualResumen
    Public Property ComisionAnualResumenActual() As ComisionAnualResumen
        Get
            Return _comisionAnualResumenActual
        End Get
        Set(ByVal value As ComisionAnualResumen)
            SetProperty(_comisionAnualResumenActual, value)
        End Set
    End Property

    Private _colMeses
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
            fechaDesde = DateSerial(Year(Now), DateTime.ParseExact(value, "MMMM", CultureInfo.CurrentCulture).Month, 1)
            If fechaDesde > Now Then
                fechaDesde = fechaDesde.AddYears(-1)
            End If
            fechaHasta = (fechaDesde.AddMonths(1)).AddDays(-1)

            IncluirAlbaranes = EsMesEnCurso

            If vendedorActual IsNot Nothing Then
                If MostrarPanelAntiguo Then
                    comisionesActual = DbContext.Comisiones("1", fechaDesde, fechaHasta, vendedorActual.Número, 0).FirstOrDefault
                Else
                    CalcularComisionAsync()
                End If
                If fechaDesde >= New Date(2017, 3, 1) Then
                    listaVentasComision = New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In DbContext.vstLinPedidoVtaComisiones
                                                                                                 Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                                 (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                                 l.Vendedor = vendedorActual.Número)
                                                                                                 Order By l.Grupo, l.Dirección
                                                                                                 Select l)
                    listaVentasFamilia = New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In DbContext.vstLinPedidoVtaComisiones
                                                                                                Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                                 (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                                 l.Vendedor = vendedorActual.Número)
                                                                                                Order By l.Familia
                                                                                                Select l)
                Else
                    listaVentasComision = Nothing
                    listaVentasFamilia = Nothing
                End If
            End If
        End Set
    End Property

    Private _fechaDesde As Date
    Public Property fechaDesde As Date
        Get
            Return _fechaDesde
        End Get
        Set(value As Date)
            _fechaDesde = value
            OnPropertyChanged("EsMesEnCurso")
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
            OnPropertyChanged("listaPedidos")
        End Set
    End Property

    Private _listaVentasComision As ObservableCollection(Of vstLinPedidoVtaComisiones)
    Public Property listaVentasComision As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Get
            Return _listaVentasComision
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaComisiones))
            _listaVentasComision = value
            OnPropertyChanged("listaVentasComision")
        End Set
    End Property

    Private _listaVentasFamilia As ObservableCollection(Of vstLinPedidoVtaComisiones)
    Public Property listaVentasFamilia As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Get
            Return _listaVentasFamilia
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaComisiones))
            _listaVentasFamilia = value
            OnPropertyChanged("listaVentasFamilia")
        End Set
    End Property

    Private _mostrarPanelAntiguo As Boolean
    Public Property MostrarPanelAntiguo() As Boolean
        Get
            Return _mostrarPanelAntiguo
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_mostrarPanelAntiguo, value)
            OnPropertyChanged("MostrarPanelComisionAnual")
        End Set
    End Property

    Private _incluirAlbaranes As Boolean = True
    Public Property IncluirAlbaranes() As Boolean
        Get
            Return _incluirAlbaranes
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_incluirAlbaranes, value)
            CalcularComisionAsync()
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
        PedidoVentaViewModel.cargarPedido(arg.Empresa, arg.Número, container)
    End Sub
#End Region

    Private Async Function CalcularComisionAsync() As Task
        ComisionAnualResumenActual = Await CalcularComisionAnual(vendedorActual.Número, fechaDesde.Year, fechaDesde.Month, IncluirAlbaranes)
    End Function


    Private Async Function CalcularComisionAnual(vendedor As String, anno As Integer, mes As Integer, incluirAlbaranes As Boolean) As Task(Of ComisionAnualResumen)

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

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se han podido cargar las comisiones del vendedor " + vendedor)
            Finally

            End Try

            Dim comision As ComisionAnualResumen = JsonConvert.DeserializeObject(Of ComisionAnualResumen)(respuesta)

            Return comision

        End Using
    End Function


    Public Class ComisionAnualResumen
        Public Property Id As Integer
        Public Property Vendedor As String
        Public Property Anno As Integer
        Public Property Mes As Integer
        Public Property GeneralVenta As Decimal
        Public Property GeneralProyeccion As Decimal
        Public Property GeneralTipo As Decimal
        Public Property GeneralComision As Decimal
        Public Property GeneralFaltaParaSalto As Decimal
        Public Property UnionLaserVenta As Decimal
        Public Property UnionLaserTipo As Decimal
        Public Property UnionLaserComision As Decimal
        Public Property EvaVisnuVenta As Decimal
        Public Property EvaVisnuTipo As Decimal
        Public Property EvaVisnuComision As Decimal
        Public Property OtrosAparatosVenta As Decimal
        Public Property OtrosAparatosTipo As Decimal
        Public Property OtrosAparatosComision As Decimal
        Public Property TotalComisiones As Decimal
    End Class
End Class

