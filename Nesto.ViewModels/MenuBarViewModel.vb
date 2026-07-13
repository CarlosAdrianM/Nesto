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
    Private ReadOnly _servicioInformes As IInformesService
    Private _listaVendedoresEquipo As List(Of VendedorDTO)
    Private ReadOnly _viewTypes As New Dictionary(Of String, Type)

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.New(container, regionManager, configuracion, servicioAutenticacion, New InformesService(configuracion, servicioAutenticacion))
    End Sub

    ' Constructor para tests: permite inyectar un IInformesService mockeado.
    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion, servicioInformes As IInformesService)
        _container = container
        _regionManager = regionManager
        _configuracion = configuracion
        _servicioComisiones = New ComisionesService(configuracion, servicioAutenticacion)
        _servicioInformes = servicioInformes

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
        AgenciasMantenimientoCommand = New DelegateCommand(AddressOf OnAgenciasMantenimiento)
        RatioDeudaCommand = New DelegateCommand(AddressOf OnRatioDeuda)
        VideosCommand = New DelegateCommand(AddressOf OnVideos)
        NovedadesCommand = New DelegateCommand(AddressOf OnNovedades)
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
    Public Property AgenciasMantenimientoCommand As ICommand
    Public Property RatioDeudaCommand As ICommand
    Public Property VideosCommand As ICommand
    Public Property NovedadesCommand As ICommand
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
        ' El PDF lo genera NestoAPI con QuestPDF (api/Informes/ControlPedidos/Pdf); ya no se
        ' renderiza el RDLC en local (roadmap: mover el render de informes al backend).
        Dim pdf As Byte() = Await _servicioInformes.DescargarControlPedidosPdf()
        Dim fileName As String = Path.GetTempPath + "InformeControlPedidos.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub OnInventario()
        ' El PDF lo genera NestoAPI con QuestPDF (api/Informes/UbicacionesInventario/Pdf); ya no se
        ' renderiza el RDLC en local (roadmap: mover el render de informes al backend).
        Dim pdf As Byte() = Await _servicioInformes.DescargarUbicacionesInventarioPdf()
        Dim fileName As String = Path.GetTempPath + "InformeUbicacionesInventario.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    ' Nesto#340: picking y packing son informes delicados (el packing viaja dentro de la caja del
    ' cliente), así que el motor QuestPDF (descarga de NestoAPI) se activa POR USUARIO con el
    ' parámetro MotorPdfPicking = "QuestPDF"; por defecto sigue el RDLC local de siempre.
    Private Async Function UsarQuestPdfPicking() As Task(Of Boolean)
        Dim motor As String = Await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.MotorPdfPicking)
        Return motor?.Trim() = "QuestPDF"
    End Function

    ' Extraídos para testear la interacción con IInformesService sin tocar el Process.Start.
    Public Async Function ObtenerPdfPickingAsync() As Task(Of Byte())
        Dim numero As Integer = Await _servicioInformes.LeerUltimoPicking()
        Return Await _servicioInformes.DescargarPickingPdf(numero)
    End Function

    Public Async Function ObtenerPdfPackingAsync() As Task(Of Byte())
        Dim numero As Integer = Await _servicioInformes.LeerUltimoPicking()
        Return Await _servicioInformes.DescargarPackingPdf(numero)
    End Function

    Private Async Sub OnPicking()
        Dim pdf As Byte()
        If Await UsarQuestPdfPicking() Then
            pdf = Await ObtenerPdfPickingAsync()
        Else
            Dim numeroPicking As Integer = Await _servicioInformes.LeerUltimoPicking()
            Dim dataSource As List(Of Informes.PickingModel) = Await _servicioInformes.LeerPicking(numeroPicking)
            Dim listaParametros As New List(Of ReportParameter) From {
                New ReportParameter("NumeroPicking", numeroPicking)
            }
            pdf = Nesto.Infrastructure.Services.RenderizadorInformes.RenderizarPdf(
                "Nesto.Informes.Picking.rdlc", "PickingDataSet", dataSource, listaParametros)
        End If
        Dim fileName As String = Path.GetTempPath + "InformePicking.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub OnPacking()
        Dim pdf As Byte()
        If Await UsarQuestPdfPicking() Then
            pdf = Await ObtenerPdfPackingAsync()
        Else
            Dim numeroPicking As Integer = Await _servicioInformes.LeerUltimoPicking()
            Dim dataSource As List(Of Informes.PackingModel) = Await _servicioInformes.LeerPacking(numeroPicking)
            Dim listaParametros As New List(Of ReportParameter) From {
                New ReportParameter("NumeroPicking", numeroPicking)
            }
            pdf = Nesto.Infrastructure.Services.RenderizadorInformes.RenderizarPdf(
                "Nesto.Informes.Packing.rdlc", "PackingDataSet", dataSource, listaParametros)
        End If
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

    ' Nesto#340: mantenimiento de agencias (alta de Innovatrans, editar campos, fuel, cuarentena)
    Private Sub OnAgenciasMantenimiento()
        NavegarAVista("AgenciasMantenimiento")
    End Sub

    Private Sub OnRatioDeuda()
        NavegarAVista("Deuda")
    End Sub

    Private Sub OnVideos()
        _regionManager.RequestNavigate("MainRegion", "VideosView")
    End Sub

    ' Nesto#372: consulta del changelog completo desde Herramientas → Ayuda → Novedades
    Private Async Sub OnNovedades()
        Try
            Dim novedadesService = _container.Resolve(Of INovedadesService)()
            Dim dialogService = _container.Resolve(Of Prism.Services.Dialogs.IDialogService)()
            Dim novedades = Await novedadesService.ObtenerNovedades()
            Dim parametros As New Prism.Services.Dialogs.DialogParameters From {
                {"novedades", novedades}
            }
            dialogService.ShowDialog("NovedadesDialog", parametros, Sub(r)
                                                                    End Sub)
        Catch ex As Exception
            ' Consultar las novedades nunca debe tirar la aplicación
        End Try
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
        ' El PDF lo genera NestoAPI con QuestPDF (api/Informes/ResumenVentas/Pdf) en vista comparativa
        ' Año Actual vs. Año Anterior; ya no se renderiza el RDLC en local.
        Dim pdf As Byte() = Await _servicioInformes.DescargarResumenVentasPdf(FechaDesde, FechaHasta, SoloFacturas)
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

        Dim dataSource As List(Of Informes.DetalleRapportsModel) = Await _servicioInformes.LeerDetalleRapports(FechaDesde, FechaHasta, cadenaVendedores)
        Dim listaParametros As New List(Of ReportParameter) From {
            New ReportParameter("FechaDesde", FechaDesde),
            New ReportParameter("FechaHasta", FechaHasta)
        }
        Dim pdf As Byte() = Nesto.Infrastructure.Services.RenderizadorInformes.RenderizarPdf(
            "Nesto.Informes.DetalleRapports.rdlc", "DetalleRapportsDataSet", dataSource, listaParametros)
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
