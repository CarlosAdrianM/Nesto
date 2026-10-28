Imports System.Collections.ObjectModel
Imports System.Globalization
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services

''' <summary>
''' NestoAPI#727: facturar el fin de mes por la API (sustituye al paso del «Nesto viejo»). Reúne en un pedido del contacto
''' principal los albaranes hasta la fecha de corte de los pedidos de fin de mes SIN P.O. de cada cliente y los factura; los
''' pedidos con P.O. no entran (se facturan por P.O. cuando están completos). Solo Administración, Dirección e Informática.
''' </summary>
Public Class FacturarFinDeMesDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "FacturarFinDeMesDialog"
    Public Const TITULO As String = "Facturar fin de mes"

    Private ReadOnly dialogService As IServicioDialogos
    Private ReadOnly servicioFacturacion As IServicioFacturacionRutas

    Public Sub New(configuracion As IConfiguracion, dialogService As IServicioDialogos, servicioFacturacion As IServicioFacturacionRutas)
        Me.dialogService = dialogService
        Me.servicioFacturacion = servicioFacturacion

        Title = TITULO
        PuedeFacturarFinDeMes = TienePermiso(configuracion)
        _fechaCorte = UltimoDiaDelMesAnterior(Date.Today)

        Facturas = New ObservableCollection(Of FacturaFinDeMesDTO)()
        Saltados = New ObservableCollection(Of ClienteFinDeMesSaltadoDTO)()
        Errores = New ObservableCollection(Of ErrorFinDeMesDTO)()

        FacturarFinDeMesCommand = New AsyncRelayCommand(AddressOf FacturarFinDeMesAsync, AddressOf CanFacturarFinDeMes)
        CerrarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.Cancel))
    End Sub

    ''' <summary>Los mismos grupos que exige la API (si no, 403).</summary>
    Public Shared Function TienePermiso(configuracion As IConfiguracion) As Boolean
        If configuracion Is Nothing Then
            Return False
        End If
        Return configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) OrElse
            configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) OrElse
            configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)
    End Function

    ''' <summary>La fecha de corte por defecto (la misma que pone la API si no se le manda ninguna).</summary>
    Public Shared Function UltimoDiaDelMesAnterior(hoy As Date) As Date
        Return New Date(hoy.Year, hoy.Month, 1).AddDays(-1)
    End Function

    Public Overrides Function CanCloseDialog() As Boolean
        Return Not EstaProcesando
    End Function

#Region "Propiedades"

    Public ReadOnly Property PuedeFacturarFinDeMes As Boolean

    Private _fechaCorte As Date?
    Public Property FechaCorte As Date?
        Get
            Return _fechaCorte
        End Get
        Set(value As Date?)
            If SetProperty(_fechaCorte, value) Then
                FacturarFinDeMesCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    ''' <summary>Hasta hoy: la API no admite una fecha de corte posterior.</summary>
    Public ReadOnly Property FechaMaxima As Date
        Get
            Return Date.Today
        End Get
    End Property

    Private _cliente As String
    ''' <summary>Opcional: en blanco, todos los clientes.</summary>
    Public Property Cliente As String
        Get
            Return _cliente
        End Get
        Set(value As String)
            If SetProperty(_cliente, value) Then
                OnPropertyChanged(NameOf(HayCliente))
                If Not HayCliente Then
                    Contacto = Nothing
                End If
            End If
        End Set
    End Property

    ''' <summary>El contacto solo se puede poner con un cliente (la API lo exige).</summary>
    Public ReadOnly Property HayCliente As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(Cliente)
        End Get
    End Property

    Private _contacto As String
    Public Property Contacto As String
        Get
            Return _contacto
        End Get
        Set(value As String)
            Dim unused = SetProperty(_contacto, value)
        End Set
    End Property

    Private _estaProcesando As Boolean
    Public Property EstaProcesando As Boolean
        Get
            Return _estaProcesando
        End Get
        Set(value As Boolean)
            If SetProperty(_estaProcesando, value) Then
                FacturarFinDeMesCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _resumen As String
    ''' <summary>«N facturas, M clientes saltados, K errores» tras facturar.</summary>
    Public Property Resumen As String
        Get
            Return _resumen
        End Get
        Set(value As String)
            Dim unused = SetProperty(_resumen, value)
        End Set
    End Property

    Private _mensajeError As String
    ''' <summary>El texto de la API cuando no factura (400/403…), tal cual.</summary>
    Public Property MensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            If SetProperty(_mensajeError, value) Then
                OnPropertyChanged(NameOf(HayMensajeError))
            End If
        End Set
    End Property

    Public ReadOnly Property HayMensajeError As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(MensajeError)
        End Get
    End Property

    Public ReadOnly Property Facturas As ObservableCollection(Of FacturaFinDeMesDTO)
    Public ReadOnly Property Saltados As ObservableCollection(Of ClienteFinDeMesSaltadoDTO)
    Public ReadOnly Property Errores As ObservableCollection(Of ErrorFinDeMesDTO)

#End Region

#Region "Comandos"

    Public ReadOnly Property FacturarFinDeMesCommand As AsyncRelayCommand
    Public ReadOnly Property CerrarCommand As RelayCommand

    Private Function CanFacturarFinDeMes() As Boolean
        Return PuedeFacturarFinDeMes AndAlso Not EstaProcesando AndAlso FechaCorte.HasValue
    End Function

    ''' <summary>El texto de la confirmación previa.</summary>
    Public Function TextoConfirmacion() As String
        Dim quien As String = If(HayCliente,
            $"el cliente {Cliente.Trim()}" & If(String.IsNullOrWhiteSpace(Contacto), String.Empty, $" (contacto {Contacto.Trim()})"),
            "todos los clientes")
        Return $"Se van a reunir y facturar los albaranes de fin de mes hasta el {FechaCorte.Value:dd/MM/yyyy} de {quien}. " &
            "Los pedidos con P.O. no entran: se facturan por P.O. cuando están completos. ¿Continúa?"
    End Function

    Private Async Function FacturarFinDeMesAsync() As Task
        If Not CanFacturarFinDeMes() Then
            Return
        End If
        If Not dialogService.ShowConfirmationAnswer(TITULO, TextoConfirmacion(), True) Then
            Return
        End If

        EstaProcesando = True
        MensajeError = Nothing
        Resumen = Nothing
        Facturas.Clear()
        Saltados.Clear()
        Errores.Clear()
        Try
            Dim request As New FacturarFinDeMesRequestDTO With {
                .Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                .FechaCorte = FechaCorte.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                .Cliente = If(HayCliente, Cliente.Trim(), Nothing),
                .Contacto = If(HayCliente AndAlso Not String.IsNullOrWhiteSpace(Contacto), Contacto.Trim(), Nothing)
            }
            Dim resultado As ResultadoFacturacionFinDeMesDTO = Await servicioFacturacion.FacturarFinDeMes(request)
            VolcarResultado(resultado)
            dialogService.ShowNotification(TITULO, Resumen)
        Catch ex As Exception
            MensajeError = ex.Message
            dialogService.ShowError(ex.Message)
        Finally
            EstaProcesando = False
        End Try
    End Function

    Private Sub VolcarResultado(resultado As ResultadoFacturacionFinDeMesDTO)
        If resultado IsNot Nothing Then
            For Each factura In If(resultado.Facturas, New List(Of FacturaFinDeMesDTO))
                Facturas.Add(factura)
            Next
            For Each saltado In If(resultado.Saltados, New List(Of ClienteFinDeMesSaltadoDTO))
                Saltados.Add(saltado)
            Next
            For Each errorFinDeMes In If(resultado.Errores, New List(Of ErrorFinDeMesDTO))
                Errores.Add(errorFinDeMes)
            Next
        End If
        Resumen = TextoResumen(Facturas.Count, Saltados.Count, Errores.Count)
    End Sub

    Public Shared Function TextoResumen(facturas As Integer, saltados As Integer, errores As Integer) As String
        Return $"{facturas} {If(facturas = 1, "factura", "facturas")}, " &
            $"{saltados} {If(saltados = 1, "cliente saltado", "clientes saltados")}, " &
            $"{errores} {If(errores = 1, "error", "errores")}"
    End Function

#End Region

End Class
