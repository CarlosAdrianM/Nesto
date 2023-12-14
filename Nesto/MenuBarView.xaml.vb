Imports Nesto.ViewModels
Imports Prism.Regions
Imports Prism.Modularity
Imports Prism.RibbonRegionAdapter
Imports Prism.Ioc
Imports Prism.Unity
Imports Unity
Imports System.IO
Imports Microsoft.Reporting.NETCore
Imports System.Reflection
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models

<[Module](ModuleName:="MenuBarView")>
Public Class MenuBarView
    Implements IModule, IMenuBar
    Private container As IUnityContainer
    Private regionManager As IRegionManager
    Private configuracion As IConfiguracion
    Private servicioComisiones As ComisionesService
    Private listaVendedoresEquipo As List(Of VendedorDTO)

    Private Sub btnVentasEmpresas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVentasEmpresas.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeVentasGrupo(DateSerial(Year(Now()), Month(Now()) + 0, 1), DateSerial(Year(Now()), Month(Now()) + 1, 0), False)
            Case "Anterior"
                GenerarInformeVentasGrupo(DateSerial(Year(Now()), Month(Now()) - 1, 1), DateSerial(Year(Now()), Month(Now()), 0), True)
            Case Else
                'MsgBox("Parte del programa no implementada aún")
                GenerarInformeVentasGrupo(Me.DataContext.fechaInformeInicial, Me.DataContext.fechaInformeFinal, True)
        End Select
    End Sub
    Private Async Sub GenerarInformeVentasGrupo(FechaDesde As Date, FechaHasta As Date, SoloFacturas As Boolean)
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.ResumenVentas.rdlc")
        'Dim nada = Assembly.LoadFrom("Informes").GetManifestResourceNames()
        Dim dataSource As List(Of Informes.ResumenVentasModel) = Await Informes.ResumenVentasModel.CargarDatos(FechaDesde, FechaHasta, SoloFacturas)
        Dim report As LocalReport = New LocalReport()
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
    Private Sub btnVentasEmpresas_Loaded(sender As Object, e As RoutedEventArgs) Handles btnVentasEmpresas.Loaded
        If (Environment.UserName.ToLower = "alfredo") OrElse configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
            btnVentasEmpresas.Visibility = Visibility.Visible
            btnRapport.Visibility = Visibility.Visible
        End If

        If configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN) Then
            grpAlmacen.Visibility = Visibility.Visible
        End If
    End Sub

    Private Async Function ComprobarSiEsJefeDeVentas() As Task
        listaVendedoresEquipo = Await servicioComisiones.LeerVendedores()
        If listaVendedoresEquipo.Any() Then
            btnRapport.Visibility = Visibility.Visible
        End If
    End Function

    Private Sub btnRapport_Click(sender As Object, e As RoutedEventArgs) Handles btnRapport.Click
        Select Case cmbOpciones.Text
            Case "Actual"
                GenerarInformeRapports(Today, Today)
            Case "Anterior"
                GenerarInformeRapports(Today.AddDays(-1), Today.AddDays(-1))
            Case Else
                GenerarInformeRapports(Me.DataContext.fechaInformeInicial, Me.DataContext.fechaInformeFinal)
        End Select
    End Sub
    Private Async Sub GenerarInformeRapports(FechaDesde As Date, FechaHasta As Date)
        Dim cadenaVendedores As String
        If configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
            cadenaVendedores = String.Empty
        Else
            cadenaVendedores = String.Join(",", listaVendedoresEquipo.Select(Function(v) v.Vendedor))
        End If

        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.DetalleRapports.rdlc")
        'Dim nada = Assembly.LoadFrom("Informes").GetManifestResourceNames()
        Dim dataSource As List(Of Informes.DetalleRapportsModel) = Await Informes.DetalleRapportsModel.CargarDatos(FechaDesde, FechaHasta, cadenaVendedores)
        Dim report As LocalReport = New LocalReport()
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
    Private Sub btnClientesFicha_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnClientesFicha.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Clientes)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Async Sub btnControlPedidos_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnControlPedidos.Click
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.ControlPedidos.rdlc")
        Dim dataSource As List(Of Informes.ControlPedidosModel) = Await Informes.ControlPedidosModel.CargarDatos()
        Dim report As LocalReport = New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("ControlPedidosDataSet", dataSource))
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeControlPedidos.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub
    Private Async Sub btnInventario_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnInventario.Click
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.UbicacionesInventario.rdlc")
        'Dim nada = Assembly.LoadFrom("Informes").GetManifestResourceNames()
        Dim dataSource As List(Of Informes.UbicacionesInventarioModel) = Await Informes.UbicacionesInventarioModel.CargarDatos()
        Dim report As LocalReport = New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("UbicacionesInventarioDataSet", dataSource))
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeUbicacionesInventario.pdf"
        File.WriteAllBytes(fileName, pdf)
        Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
    End Sub
    Private Async Sub btnPicking_Click(sender As Object, e As RoutedEventArgs) Handles btnPicking.Click
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.Picking.rdlc")
        Dim numeroPicking As Integer = Await Informes.PickingModel.UltimoPicking
        Dim dataSource As List(Of Informes.PickingModel) = Await Informes.PickingModel.CargarDatos(numeroPicking)
        Dim report As LocalReport = New LocalReport()
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
    Private Async Sub btnPacking_Click(sender As Object, e As RoutedEventArgs) Handles btnPacking.Click
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.Packing.rdlc")
        Dim numeroPicking As Integer = Await Informes.PickingModel.UltimoPicking
        Dim dataSource As List(Of Informes.PackingModel) = Await Informes.PackingModel.CargarDatos(numeroPicking)
        Dim report As LocalReport = New LocalReport()
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
    Private Sub btnClientesAlquileres_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesAlquileres.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Alquileres)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnClientesRemesas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesRemesas.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Remesas)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnClientesAgencias_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnClientesAgencias.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Agencias)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnRatioDeuda_Click(sender As Object, e As System.Windows.RoutedEventArgs) Handles btnRatioDeuda.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Deuda)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnPrestashop_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnPrestashop.Click
        Dim frmPrestashop As New Prestashop
        frmPrestashop.Show()
    End Sub
    Private Sub btnVendedoresComisiones_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresComisiones.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Comisiones)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnVendedoresClientes_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresClientes.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of ClienteComercial)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub
    Private Sub btnVendedoresPlanVentajas_Click(sender As System.Object, e As System.Windows.RoutedEventArgs) Handles btnVendedoresPlanVentajas.Click
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of PlanesVentajas)()
        region.Add(vista, nombreVista(region, vista.ToString))
        region.Activate(vista)
    End Sub

    Private Function nombreVista(region As Region, nombre As String) As String
        Dim contador As Integer = 2
        Dim repetir As Boolean = True
        Dim nombreAmpliado As String = nombre
        While repetir
            repetir = False
            For Each view In region.Views
                If Not IsNothing(region.GetView(nombreAmpliado)) Then
                    nombreAmpliado = nombre + contador.ToString
                    contador = contador + 1
                    repetir = True
                    Exit For
                End If
            Next
        End While
        Return nombreAmpliado
    End Function

    Private Sub OnColeccionCambiada(sender As Object, e As EventArgs)

    End Sub

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes

    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Me.container = containerProvider.GetContainer()
        Me.regionManager = container.Resolve(Of IRegionManager)
        Me.configuracion = container.Resolve(Of Configuracion)
        servicioComisiones = New ComisionesService(configuracion)
        Me.DataContext = New MainViewModel(container, regionManager)
        DataContext.Titulo = "Sin Título"

        ComprobarSiEsJefeDeVentas()

        Dim view = Me
        If Not IsNothing(view) Then

            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = containerProvider.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = containerProvider.Resolve(Of IMainWindow)()
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "NewMainMenu")
            region.Add(view, "MenuBar")
        End If
    End Sub

    Private Sub RibbonApplicationMenuItem_Click(sender As Object, e As RoutedEventArgs)
        Dim maquina As String = Environment.GetEnvironmentVariable("CLIENTNAME")
        Dim usuario As String = Environment.GetEnvironmentVariable("USERNAME")
        Dim delegacion As String = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto)
        Dim almacenPedidoVta As String = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta)
        Dim almacenRepo As String = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenReposición)
        Dim almacenInventario As String = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenInventario)
        Dim textoMensaje As String = $"{usuario} en {maquina}" + vbCr
        textoMensaje += $"Delegación por defecto en {delegacion}" + vbCr
        textoMensaje += $"Almacén pedidos en {almacenPedidoVta}" + vbCr
        textoMensaje += $"Almacén reposición en {almacenRepo}" + vbCr
        textoMensaje += $"Almacén inventario en {almacenInventario}" + vbCr
        MessageBox.Show(textoMensaje, "Paramétros usuario")
    End Sub
End Class
