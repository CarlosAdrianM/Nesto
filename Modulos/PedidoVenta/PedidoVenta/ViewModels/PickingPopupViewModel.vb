Imports System.IO
Imports System.Reflection
Imports ControlesUsuario.Dialogs
Imports Microsoft.Reporting.NETCore
Imports Nesto.Contratos
Imports Nesto.Models.PedidoVenta
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

Public Class PickingPopupViewModel
    Inherits BindableBase
    Implements IDialogAware

    Private ReadOnly servicio As IPedidoVentaService
    Private ReadOnly eventAggregator As IEventAggregator
    Private ReadOnly dialogService As IDialogService
    Private ReadOnly configuracion As IConfiguracion


    Public Sub New(servicio As IPedidoVentaService, eventAggregator As IEventAggregator, dialogService As IDialogService, configuracion As IConfiguracion)
        Me.servicio = servicio
        Me.eventAggregator = eventAggregator
        Me.dialogService = dialogService
        Me.configuracion = configuracion

        cmdInformeKits = New DelegateCommand(AddressOf OnInformeKits)
        cmdInformePicking = New DelegateCommand(AddressOf OnInformePicking)
        cmdInformePacking = New DelegateCommand(AddressOf OnInformePacking)
        cmdSacarPicking = New DelegateCommand(Of PedidoVentaDTO)(AddressOf OnSacarPicking, AddressOf CanSacarPicking)
    End Sub

    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return "Picking"
        End Get
    End Property

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed

    End Sub

    Public Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        pedidoPicking = parameters.GetValue(Of PedidoVentaDTO)("pedidoPicking")
        If Not IsNothing(pedidoPicking) Then
            numeroPedidoPicking = pedidoPicking.numero
            numeroClientePicking = pedidoPicking.cliente
        Else
            numeroPedidoPicking = 0
        End If
    End Sub

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return True
    End Function




    Private _esPickingCliente As Boolean
    Public Property esPickingCliente As Boolean
        Get
            Return _esPickingCliente
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingCliente, value)
        End Set
    End Property

    Private _esPickingPedido As Boolean = True
    Public Property esPickingPedido As Boolean
        Get
            Return _esPickingPedido
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingPedido, value)
        End Set
    End Property

    Private _esPickingRutas As Boolean
    Public Property esPickingRutas As Boolean
        Get
            Return _esPickingRutas
        End Get
        Set(value As Boolean)
            SetProperty(_esPickingRutas, value)
        End Set
    End Property

    Private _estaSacandoPicking As Boolean
    Public Property estaSacandoPicking() As Boolean
        Get
            Return _estaSacandoPicking
        End Get
        Set(ByVal value As Boolean)
            SetProperty(_estaSacandoPicking, value)
        End Set
    End Property

    Private _numeroClientePicking As String
    Public Property numeroClientePicking() As String
        Get
            Return _numeroClientePicking
        End Get
        Set(ByVal value As String)
            SetProperty(_numeroClientePicking, value)
        End Set
    End Property

    Private _numeroPedidoPicking As Integer
    Public Property numeroPedidoPicking As Integer
        Get
            Return _numeroPedidoPicking
        End Get
        Set(value As Integer)
            SetProperty(_numeroPedidoPicking, value)
        End Set
    End Property

    Private _numeroPicking As Integer
    Public Property numeroPicking As Integer
        Get
            Return _numeroPicking
        End Get
        Set(value As Integer)
            SetProperty(_numeroPicking, value)
        End Set
    End Property

    Private _pedidoPicking As PedidoVentaDTO
    Public Property pedidoPicking As PedidoVentaDTO
        Get
            Return _pedidoPicking
        End Get
        Set(value As PedidoVentaDTO)
            SetProperty(_pedidoPicking, value)
        End Set
    End Property


    Private _cmdInformeKits As DelegateCommand
    Public Property cmdInformeKits As DelegateCommand
        Get
            Return _cmdInformeKits
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdInformeKits, value)
        End Set
    End Property
    Private Async Sub OnInformeKits()
        Try
            estaSacandoPicking = True
            Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.KitsQueSePuedenMontar.rdlc")
            Dim dataSource As List(Of Informes.KitsQueSePuedenMontarModel) = Await Informes.KitsQueSePuedenMontarModel.CargarDatos()
            Dim report As LocalReport = New LocalReport()
            report.LoadReportDefinition(reportDefinition)
            report.DataSources.Add(New ReportDataSource("KitsQueSePuedenMontarDataSet", dataSource))
            Dim pdf As Byte() = report.Render("PDF")
            Dim fileName As String = Path.GetTempPath + "InformeKitsQueSePuedenMontar.pdf"
            File.WriteAllBytes(fileName, pdf)
            Process.Start(New ProcessStartInfo(fileName) With {
                .UseShellExecute = True
            })
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaSacandoPicking = False
        End Try
    End Sub




    Private _cmdInformePacking As DelegateCommand
    Public Property cmdInformePacking As DelegateCommand
        Get
            Return _cmdInformePacking
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdInformePacking, value)
        End Set
    End Property
    Private Async Sub OnInformePacking()
        Try
            estaSacandoPicking = True
            If IsNothing(numeroPicking) OrElse numeroPicking <= 0 Then
                numeroPicking = Await Informes.PickingModel.UltimoPicking
            End If
            Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.Packing.rdlc")
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
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaSacandoPicking = False
        End Try
    End Sub



    Private _cmdInformePicking As DelegateCommand
    Public Property cmdInformePicking As DelegateCommand
        Get
            Return _cmdInformePicking
        End Get
        Private Set(value As DelegateCommand)
            SetProperty(_cmdInformePicking, value)
        End Set
    End Property
    Private Async Sub OnInformePicking()
        Try
            estaSacandoPicking = True
            Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.Picking.rdlc")
            If IsNothing(numeroPicking) OrElse numeroPicking <= 0 Then
                numeroPicking = Await Informes.PickingModel.UltimoPicking
            End If
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
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
        Finally
            estaSacandoPicking = False
        End Try

    End Sub



    Private _cmdSacarPicking As DelegateCommand(Of PedidoVentaDTO)
    Public Property cmdSacarPicking As DelegateCommand(Of PedidoVentaDTO)
        Get
            Return _cmdSacarPicking
        End Get
        Private Set(value As DelegateCommand(Of PedidoVentaDTO))
            SetProperty(_cmdSacarPicking, value)
        End Set
    End Property
    Private Function CanSacarPicking(arg As PedidoVentaDTO) As Boolean
        Return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)
    End Function
    Private Async Sub OnSacarPicking(pedidoPicking As PedidoVentaDTO)
        Try
            estaSacandoPicking = True
            Await Task.Run(Sub()
                               Try
                                   If esPickingPedido Then
                                       Dim empresaPicking As String
                                       If Not IsNothing(pedidoPicking) Then
                                           empresaPicking = pedidoPicking.empresa
                                       Else
                                           empresaPicking = Constantes.Empresas.EMPRESA_DEFECTO
                                       End If
                                       servicio.sacarPickingPedido(empresaPicking, numeroPedidoPicking)
                                   ElseIf esPickingCliente Then
                                       servicio.sacarPickingPedido(numeroClientePicking)
                                   ElseIf esPickingRutas Then
                                       servicio.sacarPickingPedido()
                                   Else
                                       Throw New Exception("No hay ningún tipo de picking seleccionado")
                                   End If
                               Catch ex As Exception
                                   Throw ex
                               End Try
                           End Sub)
            Dim textoMensaje As String
            If esPickingPedido Then
                textoMensaje = "Se ha asignado el picking correctamente al pedido " + numeroPedidoPicking.ToString
            ElseIf esPickingCliente Then
                textoMensaje = "Se ha asignado el picking correctamente al cliente " + numeroClientePicking
            ElseIf esPickingRutas Then
                textoMensaje = "Se ha asignado el picking correctamente a las rutas"
            Else
                Throw New Exception("Tiene que haber algún tipo de picking seleccionado")
            End If
            dialogService.ShowNotification("Picking", textoMensaje)
            numeroPicking = Await Informes.PickingModel.UltimoPicking
            eventAggregator.GetEvent(Of SacarPickingEvent).Publish(1)
        Catch ex As Exception
            Dim tituloError As String
            If esPickingPedido Then
                tituloError = "Error Picking pedido " + numeroPedidoPicking.ToString
            ElseIf esPickingCliente Then
                tituloError = "Error Picking cliente " + numeroClientePicking
            ElseIf esPickingRutas Then
                tituloError = "Error Picking Rutas"
            Else
                tituloError = "Error Picking sin tipo"
            End If
            Dim textoError As String
            If IsNothing(ex.InnerException) Then
                textoError = ex.Message
            Else
                textoError = ex.Message + vbCr + ex.InnerException.Message
            End If
            dialogService.ShowNotification(tituloError, textoError)
        Finally
            estaSacandoPicking = False
        End Try
    End Sub

End Class
