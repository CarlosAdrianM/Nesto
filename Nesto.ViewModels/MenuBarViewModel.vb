Imports System.IO
Imports System.Reflection
Imports System.Windows
Imports System.Windows.Input
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
        BalancePymesCommand = New DelegateCommand(Sub() GenerarInformeBalance("BPY", "BalancePymes"))
        PerdidasGananciasCommand = New DelegateCommand(Sub() GenerarInformeBalance("PGP", "PerdidasGananciasPymes"))
        InventarioCommand = New DelegateCommand(AddressOf OnInventario)
        PickingCommand = New DelegateCommand(AddressOf OnPicking)
        PackingCommand = New DelegateCommand(AddressOf OnPacking)
        ClientesAlquileresCommand = New DelegateCommand(AddressOf OnClientesAlquileres)
        ClientesRemesasCommand = New DelegateCommand(AddressOf OnClientesRemesas)
        ClientesAgenciasCommand = New DelegateCommand(AddressOf OnClientesAgencias)
        AgenciasMantenimientoCommand = New DelegateCommand(AddressOf OnAgenciasMantenimiento)
        FamiliasMantenimientoCommand = New DelegateCommand(AddressOf OnFamiliasMantenimiento)
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
    Public Property FamiliasMantenimientoCommand As ICommand
    Public Property RatioDeudaCommand As ICommand
    Public Property VideosCommand As ICommand
    Public Property NovedadesCommand As ICommand
    Public Property VendedoresComisionesCommand As ICommand
    Public Property VendedoresClientesCommand As ICommand
    Public Property VendedoresPlanVentajasCommand As ICommand
    Public Property ParametrosCommand As ICommand
    ' NestoAPI#350: balances y cuentas de resultados calculados por el servidor
    Public Property BalancePymesCommand As ICommand
    Public Property PerdidasGananciasCommand As ICommand

    ' NestoAPI#350: sumar la empresa Global (3) a los balances. OJO: es un AGREGADO de los
    ' mayores de ambas empresas, no un consolidado (las operaciones cruzadas no se eliminan).
    Private _incluirGlobalEnBalances As Boolean
    Public Property IncluirGlobalEnBalances As Boolean
        Get
            Return _incluirGlobalEnBalances
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_incluirGlobalEnBalances, value)
        End Set
    End Property

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

    ' Nesto#340 (retirada de flags 20/08/26): MotorPdfPicking llevaba semanas al 100% en
    ' QuestPDF (defecto incluido) — el RDLC local era código muerto. El PDF lo genera SIEMPRE
    ' NestoAPI. Extraídos para testear la interacción con IInformesService sin el Process.Start.
    Public Async Function ObtenerPdfPickingAsync() As Task(Of Byte())
        Dim numero As Integer = Await _servicioInformes.LeerUltimoPicking()
        Return Await _servicioInformes.DescargarPickingPdf(numero)
    End Function

    Public Async Function ObtenerPdfPackingAsync() As Task(Of Byte())
        Dim numero As Integer = Await _servicioInformes.LeerUltimoPicking()
        Return Await _servicioInformes.DescargarPackingPdf(numero)
    End Function

    Private Async Sub OnPicking()
        Dim pdf As Byte() = Await ObtenerPdfPickingAsync()
        Dim fileName As String = Path.GetTempPath + "InformePicking.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    Private Async Sub OnPacking()
        Dim pdf As Byte() = Await ObtenerPdfPackingAsync()
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

    ' NestoAPI#406: mantenimiento de familias (marcar "público igual que profesional")
    Private Sub OnFamiliasMantenimiento()
        NavegarAVista("FamiliasMantenimiento")
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
        ' Caso real 20/08/26: ya no es un MessageBox de solo lectura — la ventana muestra además
        ' los parámetros que el SERVIDOR declare editables para este usuario (p. ej. Tienda
        ' Online puede cambiarse el almacén de pedidos entre AMZ y ALG), con combo y validación.
        Dim dialogService = _container.Resolve(Of Prism.Services.Dialogs.IDialogService)()
        Dim parametrosDialogo As New Prism.Services.Dialogs.DialogParameters From {
            {"informacion", textoMensaje}
        }
        dialogService.ShowDialog("ParametrosUsuarioDialog", parametrosDialogo, Sub(r)
                                                                              End Sub)
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

    ' NestoAPI#350: balances y cuentas de resultados (BPY, PGP...) calculados por el servidor
    ' desde las tablas Balances/LinBalance (sustituye a los informes del Nesto viejo). Fechas:
    ' "Actual" = año en curso hasta hoy; "Anterior" = año pasado completo; "Personalizar" = las
    ' fechas elegidas. Siempre empresa 1: los balances solo están definidos para ella.
    Private Async Sub GenerarInformeBalance(numeroBalance As String, nombreFichero As String)
        Dim fechaDesde As Date
        Dim fechaHasta As Date
        If OpcionesFechas = "Personalizar" Then
            fechaDesde = FechaInformeInicial
            fechaHasta = FechaInformeFinal
        Else
            Dim fechas = CalcularFechasBalance(OpcionesFechas, Today)
            fechaDesde = fechas.Desde
            fechaHasta = fechas.Hasta
        End If

        Dim empresas As String = If(IncluirGlobalEnBalances,
            Constantes.Empresas.EMPRESA_DEFECTO & "," & Constantes.Empresas.EMPRESA_ESPEJO.Trim(),
            Constantes.Empresas.EMPRESA_DEFECTO)
        Dim pdf As Byte() = Await _servicioInformes.DescargarBalancePdf(
            empresas, numeroBalance, fechaDesde, fechaHasta)
        Dim fileName As String = Path.GetTempPath + nombreFichero + ".pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub

    ' Petición Carlos 19/08/26: los balances se piden a mes CERRADO. "Actual" = del 1 de enero
    ' al último día del mes anterior; "Anterior" = del 1 de enero al último día de dos meses
    ' atrás (hoy 19/08/26: Actual 01/01/26-31/07/26, Anterior 01/01/26-30/06/26). Si el cierre
    ' cae en el año pasado (enero/febrero), el 1 de enero se toma del AÑO del propio cierre
    ' (en enero: Actual = año pasado completo; Anterior = hasta el 30/11 del año pasado).
    Public Shared Function CalcularFechasBalance(opcion As String, hoy As Date) As (Desde As Date, Hasta As Date)
        Dim mesesCerrados As Integer = If(opcion = "Anterior", 2, 1)
        Dim primeroDeEsteMes As New Date(hoy.Year, hoy.Month, 1)
        Dim hasta As Date = primeroDeEsteMes.AddMonths(1 - mesesCerrados).AddDays(-1)
        Return (New Date(hasta.Year, 1, 1), hasta)
    End Function

    Private Async Sub GenerarInformeRapports(FechaDesde As Date, FechaHasta As Date)
        Dim cadenaVendedores As String
        If _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
            cadenaVendedores = String.Empty
        Else
            cadenaVendedores = String.Join(",", _listaVendedoresEquipo.Select(Function(v) v.Vendedor))
        End If

        ' El PDF lo genera NestoAPI con QuestPDF (api/Informes/DetalleRapports/Pdf);
        ' ya no se renderiza el RDLC en local (Nesto#340).
        Dim pdf As Byte() = Await _servicioInformes.DescargarDetalleRapportsPdf(FechaDesde, FechaHasta, cadenaVendedores)
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
