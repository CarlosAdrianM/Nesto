Imports System.IO
Imports System.Reflection
Imports System.Windows
Imports System.Windows.Input
Imports Microsoft.Reporting.NETCore
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Regions
Imports Unity

Public Class MenuBarViewModel
    Inherits BindableBase

    Private ReadOnly _container As IUnityContainer
    Private ReadOnly _regionManager As IRegionManager
    Private ReadOnly _configuracion As IConfiguracion
    Private ReadOnly _servicioComisiones As ComisionesService
    Private ReadOnly _servicioInformes As InformesService
    Private _listaVendedoresEquipo As List(Of VendedorDTO)
    Private ReadOnly _viewTypes As New Dictionary(Of String, Type)

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        _container = container
        _regionManager = regionManager
        _configuracion = configuracion
        _servicioComisiones = New ComisionesService(configuracion, servicioAutenticacion)
        _servicioInformes = New InformesService(configuracion, servicioAutenticacion)

        VentasEmpresasCommand = New DelegateCommand(AddressOf OnVentasEmpresas)
        RapportCommand = New DelegateCommand(AddressOf OnRapport)
        ClientesFichaCommand = New DelegateCommand(AddressOf OnClientesFicha)
        ControlPedidosCommand = New DelegateCommand(AddressOf OnControlPedidos)
        InventarioCommand = New DelegateCommand(AddressOf OnInventario)
        PickingCommand = New DelegateCommand(AddressOf OnPicking)
        PackingCommand = New DelegateCommand(AddressOf OnPacking)
        ClientesAlquileresCommand = New DelegateCommand(AddressOf OnClientesAlquileres)
        ClientesRemesasCommand = New DelegateCommand(AddressOf OnClientesRemesas)
        ClientesAgenciasCommand = New DelegateCommand(AddressOf OnClientesAgencias)
        RatioDeudaCommand = New DelegateCommand(AddressOf OnRatioDeuda)
        PrestashopCommand = New DelegateCommand(AddressOf OnPrestashop)
        VideosCommand = New DelegateCommand(AddressOf OnVideos)
        VendedoresComisionesCommand = New DelegateCommand(AddressOf OnVendedoresComisiones)
        VendedoresClientesCommand = New DelegateCommand(AddressOf OnVendedoresClientes)
        VendedoresPlanVentajasCommand = New DelegateCommand(AddressOf OnVendedoresPlanVentajas)
        ParametrosCommand = New DelegateCommand(AddressOf OnParametros)

        InicializarVisibilidad()
        ComprobarSiEsJefeDeVentas()
    End Sub

    Public Sub RegistrarTipoVista(nombre As String, tipo As Type)
        _viewTypes(nombre) = tipo
    End Sub

#Region "Propiedades de visibilidad"

    Private _ventasEmpresasVisible As Visibility = Visibility.Hidden
    Public Property VentasEmpresasVisible As Visibility
        Get
            Return _ventasEmpresasVisible
        End Get
        Set(value As Visibility)
            SetProperty(_ventasEmpresasVisible, value)
        End Set
    End Property

    Private _rapportVisible As Visibility = Visibility.Hidden
    Public Property RapportVisible As Visibility
        Get
            Return _rapportVisible
        End Get
        Set(value As Visibility)
            SetProperty(_rapportVisible, value)
        End Set
    End Property

    Private _almacenVisible As Visibility = Visibility.Hidden
    Public Property AlmacenVisible As Visibility
        Get
            Return _almacenVisible
        End Get
        Set(value As Visibility)
            SetProperty(_almacenVisible, value)
        End Set
    End Property

    Private _videosVisible As Visibility = Visibility.Collapsed
    Public Property VideosVisible As Visibility
        Get
            Return _videosVisible
        End Get
        Set(value As Visibility)
            SetProperty(_videosVisible, value)
        End Set
    End Property

#End Region

#Region "Propiedades de fechas"

    Private _opcionesFechas As String = "Actual"
    Public Property OpcionesFechas As String
        Get
            Return _opcionesFechas
        End Get
        Set(value As String)
            SetProperty(_opcionesFechas, value)
            RaisePropertyChanged(NameOf(MostrarFechas))
        End Set
    End Property

    Public ReadOnly Property MostrarFechas As Visibility
        Get
            If OpcionesFechas = "Personalizar" Then
                Return Visibility.Visible
            Else
                Return Visibility.Hidden
            End If
        End Get
    End Property

    Private _fechaInformeInicial As Date = Today
    Public Property FechaInformeInicial As Date
        Get
            Return _fechaInformeInicial
        End Get
        Set(value As Date)
            SetProperty(_fechaInformeInicial, value)
        End Set
    End Property

    Private _fechaInformeFinal As Date = Today
    Public Property FechaInformeFinal As Date
        Get
            Return _fechaInformeFinal
        End Get
        Set(value As Date)
            SetProperty(_fechaInformeFinal, value)
        End Set
    End Property

#End Region

#Region "Commands"

    Public Property VentasEmpresasCommand As ICommand
    Public Property RapportCommand As ICommand
    Public Property ClientesFichaCommand As ICommand
    Public Property ControlPedidosCommand As ICommand
    Public Property InventarioCommand As ICommand
    Public Property PickingCommand As ICommand
    Public Property PackingCommand As ICommand
    Public Property ClientesAlquileresCommand As ICommand
    Public Property ClientesRemesasCommand As ICommand
    Public Property ClientesAgenciasCommand As ICommand
    Public Property RatioDeudaCommand As ICommand
    Public Property PrestashopCommand As ICommand
    Public Property VideosCommand As ICommand
    Public Property VendedoresComisionesCommand As ICommand
    Public Property VendedoresClientesCommand As ICommand
    Public Property VendedoresPlanVentajasCommand As ICommand
    Public Property ParametrosCommand As ICommand

#End Region

#Region "Inicialización"

    Private Sub InicializarVisibilidad()
        If (Environment.UserName.ToLower = "alfredo") OrElse _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
            VentasEmpresasVisible = Visibility.Visible
            RapportVisible = Visibility.Visible
        End If

        If _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) Then
            AlmacenVisible = Visibility.Visible
        End If

        If _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE) Then
            VideosVisible = Visibility.Visible
        End If
    End Sub

    Private Async Function ComprobarSiEsJefeDeVentas() As Task
        _listaVendedoresEquipo = Await _servicioComisiones.LeerVendedores()
        If _listaVendedoresEquipo.Any() Then
            RapportVisible = Visibility.Visible
        End If
    End Function

#End Region

#Region "Métodos de comando"

    Private Sub OnVentasEmpresas()
        Select Case OpcionesFechas
            Case "Actual"
                GenerarInformeVentasGrupo(DateSerial(Year(Now()), Month(Now()) + 0, 1), DateSerial(Year(Now()), Month(Now()) + 1, 0), False)
            Case "Anterior"
                GenerarInformeVentasGrupo(DateSerial(Year(Now()), Month(Now()) - 1, 1), DateSerial(Year(Now()), Month(Now()), 0), True)
            Case Else
                GenerarInformeVentasGrupo(FechaInformeInicial, FechaInformeFinal, True)
        End Select
    End Sub

    Private Sub OnRapport()
        Select Case OpcionesFechas
            Case "Actual"
                GenerarInformeRapports(Today, Today)
            Case "Anterior"
                GenerarInformeRapports(Today.AddDays(-1), Today.AddDays(-1))
            Case Else
                GenerarInformeRapports(FechaInformeInicial, FechaInformeFinal)
        End Select
    End Sub

    Private Sub OnClientesFicha()
        NavegarAVista("Clientes")
    End Sub

    Private Async Sub OnControlPedidos()
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.ControlPedidos.rdlc")
        Dim dataSource As List(Of Informes.ControlPedidosModel) = Await _servicioInformes.LeerControlPedidos()
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("ControlPedidosDataSet", dataSource))
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeControlPedidos.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub OnInventario()
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.UbicacionesInventario.rdlc")
        Dim dataSource As List(Of Informes.UbicacionesInventarioModel) = Await _servicioInformes.LeerUbicacionesInventario()
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("UbicacionesInventarioDataSet", dataSource))
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeUbicacionesInventario.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub OnPicking()
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.Picking.rdlc")
        Dim numeroPicking As Integer = Await Informes.PickingModel.UltimoPicking
        Dim dataSource As List(Of Informes.PickingModel) = Await Informes.PickingModel.CargarDatos(numeroPicking)
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("PickingDataSet", dataSource))
        Dim listaParametros As New List(Of ReportParameter) From {
            New ReportParameter("NumeroPicking", numeroPicking)
        }
        report.SetParameters(listaParametros)
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformePicking.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub OnPacking()
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.Packing.rdlc")
        Dim numeroPicking As Integer = Await Informes.PickingModel.UltimoPicking
        Dim dataSource As List(Of Informes.PackingModel) = Await Informes.PackingModel.CargarDatos(numeroPicking)
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("PackingDataSet", dataSource))
        Dim listaParametros As New List(Of ReportParameter) From {
            New ReportParameter("NumeroPicking", numeroPicking)
        }
        report.SetParameters(listaParametros)
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformePacking.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Sub OnClientesAlquileres()
        NavegarAVista("Alquileres")
    End Sub

    Private Sub OnClientesRemesas()
        NavegarAVista("Remesas")
    End Sub

    Private Sub OnClientesAgencias()
        NavegarAVista("Agencias")
    End Sub

    Private Sub OnRatioDeuda()
        NavegarAVista("Deuda")
    End Sub

    Private Sub OnPrestashop()
        NavegarAVista("Prestashop")
    End Sub

    Private Sub OnVideos()
        _regionManager.RequestNavigate("MainRegion", "VideosView")
    End Sub

    Private Sub OnVendedoresComisiones()
        NavegarAVista("Comisiones")
    End Sub

    Private Sub OnVendedoresClientes()
        NavegarAVista("ClienteComercial")
    End Sub

    Private Sub OnVendedoresPlanVentajas()
        NavegarAVista("PlanesVentajas")
    End Sub

    Private Sub OnParametros()
        Dim maquina As String = Environment.GetEnvironmentVariable("CLIENTNAME")
        Dim usuario As String = Environment.GetEnvironmentVariable("USERNAME")
        Dim delegacion As String = _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto)
        Dim almacenPedidoVta As String = _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta)
        Dim almacenRepo As String = _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenReposición)
        Dim almacenInventario As String = _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenInventario)
        Dim textoMensaje As String = $"{usuario} en {maquina}" + vbCr
        textoMensaje += $"Delegacion por defecto en {delegacion}" + vbCr
        textoMensaje += $"Almacen pedidos en {almacenPedidoVta}" + vbCr
        textoMensaje += $"Almacen reposicion en {almacenRepo}" + vbCr
        textoMensaje += $"Almacen inventario en {almacenInventario}" + vbCr
        MessageBox.Show(textoMensaje, "Parametros usuario")
    End Sub

#End Region

#Region "Métodos de informes"

    Private Async Sub GenerarInformeVentasGrupo(FechaDesde As Date, FechaHasta As Date, SoloFacturas As Boolean)
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.ResumenVentas.rdlc")
        Dim dataSource As List(Of Informes.ResumenVentasModel) = Await _servicioInformes.LeerResumenVentas(FechaDesde, FechaHasta, SoloFacturas)
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("ResumenVentasDataSet", dataSource))
        Dim listaParametros As New List(Of ReportParameter) From {
            New ReportParameter("FechaDesde", FechaDesde),
            New ReportParameter("FechaHasta", FechaHasta),
            New ReportParameter("SoloFacturas", SoloFacturas)
        }
        report.SetParameters(listaParametros)
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeVentas.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub GenerarInformeRapports(FechaDesde As Date, FechaHasta As Date)
        Dim cadenaVendedores As String
        If _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
            cadenaVendedores = String.Empty
        Else
            cadenaVendedores = String.Join(",", _listaVendedoresEquipo.Select(Function(v) v.Vendedor))
        End If

        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.DetalleRapports.rdlc")
        Dim dataSource As List(Of Informes.DetalleRapportsModel) = Await _servicioInformes.LeerDetalleRapports(FechaDesde, FechaHasta, cadenaVendedores)
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("DetalleRapportsDataSet", dataSource))
        Dim listaParametros As New List(Of ReportParameter) From {
            New ReportParameter("FechaDesde", FechaDesde),
            New ReportParameter("FechaHasta", FechaHasta)
        }
        report.SetParameters(listaParametros)
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeRapports.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

#End Region

#Region "Navegación"

    Private Sub NavegarAVista(nombreVista As String)
        Dim viewType As Type = Nothing
        If Not _viewTypes.TryGetValue(nombreVista, viewType) Then
            Return
        End If

        If nombreVista = "Prestashop" Then
            Dim ventana = CType(_container.Resolve(viewType), Window)
            ventana.Show()
            Return
        End If

        Dim region As IRegion = _regionManager.Regions("MainRegion")
        Dim vista = _container.Resolve(viewType)
        Dim nombre = ObtenerNombreVistaUnico(region, vista.ToString())
        region.Add(vista, nombre)
        region.Activate(vista)
    End Sub

    Private Function ObtenerNombreVistaUnico(region As IRegion, nombre As String) As String
        Dim contador As Integer = 2
        Dim repetir As Boolean = True
        Dim nombreAmpliado As String = nombre
        While repetir
            repetir = False
            For Each view In region.Views
                If region.GetView(nombreAmpliado) IsNot Nothing Then
                    nombreAmpliado = nombre + contador.ToString
                    contador = contador + 1
                    repetir = True
                    Exit For
                End If
            Next
        End While
        Return nombreAmpliado
    End Function

#End Region

End Class
