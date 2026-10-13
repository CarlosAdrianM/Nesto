Imports System.Collections.ObjectModel
Imports ControlesUsuario
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Prism
Imports CommunityToolkit.Mvvm.Input
Imports CommunityToolkit.Mvvm.Messaging
Imports Unity

Public Class ListaRapportsViewModel
    Inherits ViewModelBasico
    Implements IReceptorNavegacion, IActiveAware

    Private ReadOnly navegacion As IServicioNavegacion
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IRapportService
    Private ReadOnly container As IUnityContainer
    Private ReadOnly _dialogService As IServicioDialogos
    Private ReadOnly _messenger As IMessenger
    Private ReadOnly _empresaPorDefecto As String = Constantes.Empresas.EMPRESA_DEFECTO
    Public Property vendedor As String



    Public Sub New(navegacion As IServicioNavegacion, configuracion As IConfiguracion, servicio As IRapportService, container As IUnityContainer, dialogService As IServicioDialogos, messenger As IMessenger)
        Me.navegacion = navegacion
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.container = container
        _dialogService = dialogService
        _messenger = messenger

        cmdAbrirModulo = New RelayCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)
        CambiarModoComparativaCommand = New RelayCommand(Of String)(Sub(valor) ModoComparativa = valor)
        CambiarAgruparPorCommand = New RelayCommand(Of String)(Sub(valor) AgruparPor = valor)
        cmdCargarListaRapports = New RelayCommand(Of Object)(AddressOf OnCargarListaRapports, AddressOf CanCargarListaRapports)
        cmdCargarListaRapportsFiltrada = New RelayCommand(AddressOf OnCargarListaRapportsFiltrada, AddressOf CanCargarListaRapportsFiltrada)
        cmdCrearRapport = New RelayCommand(Of ClienteProbabilidadVenta)(AddressOf OnCrearRapport, AddressOf CanCrearRapport)
        GenerarResumenCommand = New RelayCommand(AddressOf OnGenerarResumen, AddressOf CanGenerarResumen)
        VerDetalleVentasCommand = New RelayCommand(Of VentaClienteResumenDTO)(AddressOf OnVerDetalleVentas, AddressOf CanVerDetalleVentas)
        VolverAResumenVentasCommand = New RelayCommand(AddressOf OnVolverAResumenVentas)
        AbrirFichaProductoCommand = New RelayCommand(Of VentaClienteResumenDTO)(AddressOf OnAbrirFichaProducto, AddressOf CanAbrirFichaProducto)

        CopiarSeguimientosCommand = New RelayCommand(AddressOf OnCopiarSeguimientos, AddressOf CanCopiarSeguimientos)

        AbrirFacturaChequeCommand = New RelayCommand(Of ChequeRegaloPendiente)(AddressOf OnAbrirFacturaCheque, Function(c) c IsNot Nothing AndAlso c.TieneFactura)
        LlamarChequeCommand = New RelayCommand(Of ChequeRegaloPendiente)(AddressOf OnLlamarCheque, Function(c) c IsNot Nothing)
        WhatsAppChequeCommand = New RelayCommand(Of ChequeRegaloPendiente)(AddressOf OnWhatsAppCheque, Function(c) c IsNot Nothing)

        listaTiposRapports = servicio.CargarListaTipos()
        listaEstadosRapport = servicio.CargarListaEstados()

        Titulo = "Lista de Rapports"
    End Sub


#Region "Propiedades"
    ' Agrupar por: "grupo", "familia" o "subgrupo"
    Private _agruparPor As String = "grupo"
    Public Property AgruparPor As String
        Get
            Return _agruparPor
        End Get
        Set(value As String)
            If SetProperty(_agruparPor, value) Then
                OnCargarResumenVentas()
                RaisePropertyChanged(NameOf(IsAgruparPorGrupo))
                RaisePropertyChanged(NameOf(IsAgruparPorFamilia))
                RaisePropertyChanged(NameOf(IsAgruparPorSubgrupo))
            End If
        End Set
    End Property

    Private _clienteSeleccionado As String
    Public Property clienteSeleccionado As String
        Get
            Return _clienteSeleccionado
        End Get
        Set(value As String)
            If SetProperty(_clienteSeleccionado, value) Then
                MostrandoDetalleVentas = False
            End If
            cmdCargarListaRapports.NotifyCanExecuteChanged()
            CopiarSeguimientosCommand.NotifyCanExecuteChanged()
            If _clienteSeleccionado = String.Empty Then
                ClienteCompleto = Nothing
                cmdCargarListaRapports.Execute(Nothing) ' Por fecha
            End If
        End Set
    End Property

    Private _clienteCompleto As Object
    ''' <summary>Nesto#444: ficha completa del cliente del selector de la izquierda, para
    ''' mostrar sus teléfonos directamente, sin cargar ni crear ningún rapport.</summary>
    Public Property ClienteCompleto As Object
        Get
            Return _clienteCompleto
        End Get
        Set(value As Object)
            Dim unused = SetProperty(_clienteCompleto, value)
        End Set
    End Property

    Private _clienteProbabilidadSeleccionado As ClienteProbabilidadVenta
    Public Property ClienteProbabilidadSeleccionado As ClienteProbabilidadVenta
        Get
            Return _clienteProbabilidadSeleccionado
        End Get
        Set(value As ClienteProbabilidadVenta)
            Dim unused = SetProperty(_clienteProbabilidadSeleccionado, value)
            If Not IsNothing(value) Then
                cmdCrearRapport.Execute(value)
            End If
        End Set
    End Property

    Private _contactoSeleccionado As String
    Public Property contactoSeleccionado As String
        Get
            Return _contactoSeleccionado
        End Get
        Set(value As String)
            Dim unused = SetProperty(_contactoSeleccionado, value)
        End Set
    End Property

    Private _estaGenerandoResumen As Boolean
    Public Property EstaGenerandoResumen As Boolean
        Get
            Return _estaGenerandoResumen
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_estaGenerandoResumen, value)
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_estaOcupado, value)
        End Set
    End Property

    Private _esUsuarioElVendedor As Boolean = True
    Public Property esUsuarioElVendedor As Boolean
        Get
            Return _esUsuarioElVendedor
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_esUsuarioElVendedor, value)
        End Set
    End Property

    Private _fechaSeleccionada As Date = Date.Today
    Public Property fechaSeleccionada As Date
        Get
            Return _fechaSeleccionada
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaSeleccionada, value)
        End Set
    End Property

    Private _filtro As String
    Public Property Filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            Dim unused = SetProperty(_filtro, value)
            cmdCargarListaRapportsFiltrada.Execute(Nothing)
        End Set
    End Property


    Private _grupoSubgrupoSeleccionado As String
    Public Property GrupoSubgrupoSeleccionado As String
        Get
            Return _grupoSubgrupoSeleccionado
        End Get
        Set(value As String)
            ' Al abrir Rapports, el selector de subgrupos pone «(Todos los subgrupos)» (cadena vacía) donde había
            ' Nothing: es el mismo filtro y no se vuelve a pedir la lista (antes salían dos llamadas seguidas a
            ' api/Clientes/SugerenciasContacto, 07/10/26).
            Dim antes As String = If(_grupoSubgrupoSeleccionado, String.Empty)
            If SetProperty(_grupoSubgrupoSeleccionado, value) AndAlso If(value, String.Empty) <> antes Then
                ActualizarClientesProbabilidad(value)
            End If
        End Set
    End Property

    ' Implementación de IActiveAware
    Private _isActive As Boolean
    Public Property IsActive As Boolean Implements IActiveAware.IsActive
        Get
            Return _isActive
        End Get
        Set(value As Boolean)
            If _isActive <> value Then
                _isActive = value
                RaiseEvent IsActiveChanged(Me, EventArgs.Empty)

                If _isActive Then
                    OnLoaded()
                Else
                    OnUnloaded()
                End If
            End If
        End Set
    End Property
    Public Property IsAgruparPorFamilia As Boolean
        Get
            Return AgruparPor = "familia"
        End Get
        Set(value As Boolean)
            If value Then
                If AgruparPor <> "familia" Then
                    AgruparPor = "familia"
                    RaisePropertyChanged(NameOf(IsAgruparPorFamilia))
                    RaisePropertyChanged(NameOf(IsAgruparPorGrupo))
                    RaisePropertyChanged(NameOf(IsAgruparPorSubgrupo))
                    RaisePropertyChanged(NameOf(AgruparPor))
                End If
            End If
        End Set
    End Property

    Public Property IsAgruparPorGrupo As Boolean
        Get
            Return AgruparPor = "grupo"
        End Get
        Set(value As Boolean)
            If value Then
                If AgruparPor <> "grupo" Then
                    AgruparPor = "grupo"
                    RaisePropertyChanged(NameOf(IsAgruparPorFamilia))
                    RaisePropertyChanged(NameOf(IsAgruparPorGrupo))
                    RaisePropertyChanged(NameOf(IsAgruparPorSubgrupo))
                    RaisePropertyChanged(NameOf(AgruparPor))
                End If
            End If
        End Set
    End Property

    Public Property IsAgruparPorSubgrupo As Boolean
        Get
            Return AgruparPor = "subgrupo"
        End Get
        Set(value As Boolean)
            If value Then
                If AgruparPor <> "subgrupo" Then
                    AgruparPor = "subgrupo"
                    RaisePropertyChanged(NameOf(IsAgruparPorFamilia))
                    RaisePropertyChanged(NameOf(IsAgruparPorGrupo))
                    RaisePropertyChanged(NameOf(IsAgruparPorSubgrupo))
                    RaisePropertyChanged(NameOf(AgruparPor))
                End If
            End If
        End Set
    End Property
    Public Property IsComparativaAnual As Boolean
        Get
            Return ModoComparativa = "anual"
        End Get
        Set(value As Boolean)
            If value Then
                If ModoComparativa <> "anual" Then
                    ModoComparativa = "anual"
                    RaisePropertyChanged(NameOf(IsComparativaAnual))
                    ' También deberías notificar el cambio de ModoComparativa si lo usas en UI
                    RaisePropertyChanged(NameOf(ModoComparativa))
                End If
            Else
                If ModoComparativa = "anual" Then
                    ' Cambia a otro valor si el toggle se desmarca, por ejemplo "ultimos12meses"
                    ModoComparativa = "ultimos12meses"
                    RaisePropertyChanged(NameOf(IsComparativaAnual))
                    RaisePropertyChanged(NameOf(ModoComparativa))
                End If
            End If
        End Set
    End Property
    Public Property IsComparativaUltimos12Meses As Boolean
        Get
            Return ModoComparativa = "ultimos12meses"
        End Get
        Set(value As Boolean)
            If value Then
                If ModoComparativa <> "ultimos12meses" Then
                    ModoComparativa = "ultimos12meses"
                    RaisePropertyChanged(NameOf(IsComparativaUltimos12Meses))
                    RaisePropertyChanged(NameOf(IsComparativaAnual))
                    RaisePropertyChanged(NameOf(ModoComparativa))
                End If
            End If
        End Set
    End Property

    Private _isLoadingClientesProbabilidad As Boolean
    Public Property IsLoadingClientesProbabilidad As Boolean
        Get
            Return _isLoadingClientesProbabilidad
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_isLoadingClientesProbabilidad, value)
        End Set
    End Property

    Private _listaClientesProbabilidad As ObservableCollection(Of ClienteProbabilidadVenta)
    Public Property ListaClientesProbabilidad As ObservableCollection(Of ClienteProbabilidadVenta)
        Get
            Return _listaClientesProbabilidad
        End Get
        Set(value As ObservableCollection(Of ClienteProbabilidadVenta))
            Dim unused = SetProperty(_listaClientesProbabilidad, value)
        End Set
    End Property

    Private _ritmoContacto As RitmoContactoDTO
    ''' <summary>NestoAPI#603: panel de ritmo encima de la lista; Nothing con la API antigua.</summary>
    Public Property RitmoContacto As RitmoContactoDTO
        Get
            Return _ritmoContacto
        End Get
        Set(value As RitmoContactoDTO)
            If SetProperty(_ritmoContacto, value) Then
                RaisePropertyChanged(NameOf(HayRitmoContacto))
            End If
        End Set
    End Property

    Public ReadOnly Property HayRitmoContacto As Boolean
        Get
            Return _ritmoContacto IsNot Nothing
        End Get
    End Property

    Private _listaVendedoresSugerencias As New ObservableCollection(Of VendedorSugerencias)
    ''' <summary>Nesto#521: los vendedores cuyas sugerencias puede ver el usuario (el suyo, su equipo o todos).</summary>
    Public Property ListaVendedoresSugerencias As ObservableCollection(Of VendedorSugerencias)
        Get
            Return _listaVendedoresSugerencias
        End Get
        Set(value As ObservableCollection(Of VendedorSugerencias))
            If SetProperty(_listaVendedoresSugerencias, value) Then
                RaisePropertyChanged(NameOf(HayVariosVendedoresSugerencias))
            End If
        End Set
    End Property

    ''' <summary>Nesto#521: el desplegable solo sale si hay de quién elegir (jefes de ventas y Dirección).</summary>
    Public ReadOnly Property HayVariosVendedoresSugerencias As Boolean
        Get
            Return _listaVendedoresSugerencias IsNot Nothing AndAlso _listaVendedoresSugerencias.Count > 1
        End Get
    End Property

    Private _vendedorSugerencias As String
    ''' <summary>
    ''' Nesto#521: de qué vendedor se ven los «clientes para contactar». Por defecto, el del usuario. Al cambiarlo se recargan
    ''' la lista, el objetivo del día, las prioridades y la frase de ese vendedor. NO cambia el vendedor de los rapports
    ''' que se crean (siguen con <see cref="vendedor"/>).
    ''' </summary>
    Public Property VendedorSugerencias As String
        Get
            Return If(String.IsNullOrWhiteSpace(_vendedorSugerencias), vendedor, _vendedorSugerencias)
        End Get
        Set(value As String)
            ' El ComboBox puede empujar Nothing al cambiar la lista: no es una elección del usuario.
            If String.IsNullOrWhiteSpace(value) Then
                Return
            End If
            Dim antes As String = VendedorSugerencias
            If SetProperty(_vendedorSugerencias, value.Trim()) AndAlso Not String.Equals(antes?.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase) Then
                ActualizarClientesProbabilidad(If(GrupoSubgrupoSeleccionado, String.Empty))
                ' NestoAPI#593: los cheques pendientes también son del vendedor elegido (si ya se han mirado).
                If _chequesPendientesCargados Then
                    ActualizarChequesPendientes()
                End If
            End If
        End Set
    End Property

#Region "Cheques regalo (NestoAPI#593)"
    Private _resumenChequesRegalo As ResumenChequesRegalo
    ''' <summary>
    ''' NestoAPI#593: generados, usados, pendientes y caducados por vendedor (los que puede ver el usuario: el suyo, su equipo
    ''' o todos). Nothing si no hay campaña en curso: entonces la pestaña «Cheques regalo» no sale.
    ''' </summary>
    Public Property ResumenChequesRegalo As ResumenChequesRegalo
        Get
            Return _resumenChequesRegalo
        End Get
        Set(value As ResumenChequesRegalo)
            If SetProperty(_resumenChequesRegalo, value) Then
                RaisePropertyChanged(NameOf(HayCampanaChequesRegalo))
            End If
        End Set
    End Property

    Public ReadOnly Property HayCampanaChequesRegalo As Boolean
        Get
            Return _resumenChequesRegalo IsNot Nothing AndAlso _resumenChequesRegalo.Campana IsNot Nothing
        End Get
    End Property

    Private _resumenChequesVendedorSeleccionado As ResumenChequesVendedor
    ''' <summary>Al elegir una fila del resumen, la lista de pendientes (y la de clientes para contactar) pasa a ese vendedor.</summary>
    Public Property ResumenChequesVendedorSeleccionado As ResumenChequesVendedor
        Get
            Return _resumenChequesVendedorSeleccionado
        End Get
        Set(value As ResumenChequesVendedor)
            If SetProperty(_resumenChequesVendedorSeleccionado, value) AndAlso value IsNot Nothing AndAlso Not value.EsTotal AndAlso
                    Not String.IsNullOrWhiteSpace(value.Vendedor) Then
                VendedorSugerencias = value.Vendedor
            End If
        End Set
    End Property

    Private _listaChequesPendientes As New ObservableCollection(Of ChequeRegaloPendiente)
    Public Property ListaChequesPendientes As ObservableCollection(Of ChequeRegaloPendiente)
        Get
            Return _listaChequesPendientes
        End Get
        Set(value As ObservableCollection(Of ChequeRegaloPendiente))
            If SetProperty(_listaChequesPendientes, value) Then
                RaisePropertyChanged(NameOf(TextoChequesPendientes))
            End If
        End Set
    End Property

    Public ReadOnly Property TextoChequesPendientes As String
        Get
            Dim n As Integer = If(_listaChequesPendientes?.Count, 0)
            Dim deQuien As String = If(String.IsNullOrWhiteSpace(VendedorSugerencias), String.Empty, $" de {VendedorSugerencias.Trim()}")
            Return If(n = 0, $"Ningún cheque sin usar{deQuien}", If(n = 1, $"1 cheque sin usar{deQuien}", $"{n} cheques sin usar{deQuien}"))
        End Get
    End Property

    Private _isLoadingChequesRegalo As Boolean
    Public Property IsLoadingChequesRegalo As Boolean
        Get
            Return _isLoadingChequesRegalo
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_isLoadingChequesRegalo, value)
        End Set
    End Property

    Private _mostrandoChequesRegalo As Boolean
    ''' <summary>La pestaña «Cheques regalo» está elegida: la primera vez se cargan los pendientes del vendedor.</summary>
    Public Property MostrandoChequesRegalo As Boolean
        Get
            Return _mostrandoChequesRegalo
        End Get
        Set(value As Boolean)
            If SetProperty(_mostrandoChequesRegalo, value) AndAlso value AndAlso Not _chequesPendientesCargados Then
                ActualizarChequesPendientes()
            End If
        End Set
    End Property

    Private _chequesPendientesCargados As Boolean
    Private _cargaChequesPendientes As Integer

    ''' <summary>Abre un fichero con el programa del sistema (el PDF de la factura) o un enlace (WhatsApp). Sustituible en pruebas.</summary>
    Public Property AbrirConElSistema As Action(Of String) = Sub(ruta) Process.Start(New ProcessStartInfo(ruta) With {.UseShellExecute = True})

    Public Property AbrirFacturaChequeCommand As RelayCommand(Of ChequeRegaloPendiente)
    Public Property LlamarChequeCommand As RelayCommand(Of ChequeRegaloPendiente)
    Public Property WhatsAppChequeCommand As RelayCommand(Of ChequeRegaloPendiente)

    Public Async Function CargarResumenChequesRegaloAsync() As Task
        Try
            ResumenChequesRegalo = Await servicio.CargarResumenChequesRegalo()
        Catch
            ResumenChequesRegalo = Nothing
        End Try
    End Function

    Private Async Sub ActualizarChequesPendientes()
        Try
            Await CargarChequesPendientesAsync()
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Los cheques sin usar del vendedor elegido en el desplegable (como los clientes para contactar), los que caducan antes
    ''' primero. Si se cambia de vendedor con una carga en marcha, solo se pinta la última.
    ''' </summary>
    Public Async Function CargarChequesPendientesAsync() As Task
        _cargaChequesPendientes += 1
        Dim estaCarga As Integer = _cargaChequesPendientes
        _chequesPendientesCargados = True
        IsLoadingChequesRegalo = True
        Try
            Dim lista = Await servicio.CargarChequesRegaloPendientes(VendedorSugerencias)
            If estaCarga <> _cargaChequesPendientes Then
                Return
            End If
            ListaChequesPendientes = New ObservableCollection(Of ChequeRegaloPendiente)(If(lista, New List(Of ChequeRegaloPendiente)))
        Finally
            If estaCarga = _cargaChequesPendientes Then
                IsLoadingChequesRegalo = False
            End If
        End Try
    End Function

    Private Async Sub OnAbrirFacturaCheque(cheque As ChequeRegaloPendiente)
        Await AbrirFacturaChequeAsync(cheque)
    End Sub

    Public Async Function AbrirFacturaChequeAsync(cheque As ChequeRegaloPendiente) As Task
        If cheque Is Nothing OrElse Not cheque.TieneFactura Then
            Return
        End If
        Try
            IsLoadingChequesRegalo = True
            Dim pdf As Byte() = Await servicio.DescargarFacturaPdf(cheque.EmpresaFactura, cheque.FacturaOrigen)
            Dim limpio As String = New String(cheque.FacturaOrigen.Trim().Select(Function(c) If(IO.Path.GetInvalidFileNameChars().Contains(c), "_"c, c)).ToArray())
            Dim ruta As String = IO.Path.Combine(IO.Path.GetTempPath(), $"Factura_{limpio}.pdf")
            IO.File.WriteAllBytes(ruta, pdf)
            AbrirConElSistema.Invoke(ruta)
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        Finally
            IsLoadingChequesRegalo = False
        End Try
    End Function

    Private Async Sub OnLlamarCheque(cheque As ChequeRegaloPendiente)
        Await LlamarChequeAsync(cheque)
    End Sub

    ''' <summary>Como al pinchar en «clientes para contactar»: abre un rapport nuevo de tipo Teléfono de ese cliente.</summary>
    Public Async Function LlamarChequeAsync(cheque As ChequeRegaloPendiente) As Task
        If cheque Is Nothing Then
            Return
        End If
        Await CrearRapportAsync(cheque.Cliente?.Trim(), cheque.Contacto?.Trim(), TIPO_RAPPORT_TELEFONO)
    End Function

    Private Async Sub OnWhatsAppCheque(cheque As ChequeRegaloPendiente)
        Await WhatsAppChequeAsync(cheque)
    End Sub

    ''' <summary>Abre el chat de WhatsApp con el móvil de la ficha (si tiene) y un rapport nuevo de tipo WhatsApp.</summary>
    Public Async Function WhatsAppChequeAsync(cheque As ChequeRegaloPendiente) As Task
        If cheque Is Nothing Then
            Return
        End If
        Dim enlace As String = TelefonoHelper.EnlaceWhatsApp(cheque.Movil)
        If enlace IsNot Nothing Then
            Try
                AbrirConElSistema.Invoke(enlace)
            Catch ex As Exception
                _dialogService.ShowError($"No se ha podido abrir WhatsApp: {ex.Message}")
            End Try
        End If
        Await CrearRapportAsync(cheque.Cliente?.Trim(), cheque.Contacto?.Trim(), TIPO_RAPPORT_WHATSAPP)
    End Function

    Public Const TIPO_RAPPORT_TELEFONO As String = "T"
    Public Const TIPO_RAPPORT_WHATSAPP As String = "W"
#End Region

    Private _listaEstadosRapport As List(Of idShortDescripcion)
    Public Property listaEstadosRapport As List(Of idShortDescripcion)
        Get
            Return _listaEstadosRapport
        End Get
        Set(value As List(Of idShortDescripcion))
            Dim unused = SetProperty(_listaEstadosRapport, value)
        End Set
    End Property

    Private _listaRapports As ObservableCollection(Of SeguimientoClienteDTO)
    Public Property listaRapports As ObservableCollection(Of SeguimientoClienteDTO)
        Get
            Return _listaRapports
        End Get
        Set(value As ObservableCollection(Of SeguimientoClienteDTO))
            Dim unused = SetProperty(_listaRapports, value)
        End Set
    End Property

    Private _listaTiposRapports As List(Of idDescripcion)
    Public Property listaTiposRapports As List(Of idDescripcion)
        Get
            Return _listaTiposRapports
        End Get
        Set(value As List(Of idDescripcion))
            Dim unused = SetProperty(_listaTiposRapports, value)
        End Set
    End Property


    ' Modo de comparativa: "anual" o "ultimos12meses"
    Private _modoComparativa As String = "anual"
    Public Property ModoComparativa As String
        Get
            Return _modoComparativa
        End Get
        Set(value As String)
            If SetProperty(_modoComparativa, value) Then
                OnCargarResumenVentas()
                RaisePropertyChanged(NameOf(IsComparativaAnual))
                RaisePropertyChanged(NameOf(IsComparativaUltimos12Meses))
            End If
        End Set
    End Property

    Private _rapportSeleccionado As SeguimientoClienteDTO
    Public Property rapportSeleccionado As SeguimientoClienteDTO
        Get
            Return _rapportSeleccionado
        End Get
        Set(value As SeguimientoClienteDTO)
            Try
                SyncLock _syncLock
                    ' Nesto#445: al desmontar/remontar la pestaña o recargar la lista, el
                    ' DataGrid empuja Nothing (o reasigna el mismo rapport) por el binding;
                    ' si eso vaciara la región de detalle se perdería el rapport a medio
                    ' escribir. Solo se navega cuando se selecciona de verdad OTRO rapport.
                    If Not SetProperty(_rapportSeleccionado, value) OrElse value Is Nothing Then
                        Exit Property
                    End If
                    Application.Current.Dispatcher.Invoke(Sub()
                                                              Dim parameters As New ParametrosNavegacion From {
                                                                  {"rapportParameter", rapportSeleccionado}
                                                              }
                                                              ' SingleActiveRegion + IsNavigationTarget=>False acumulaba la vista de rapport
                                                              ' anterior en region.Views al cambiar de rapport seleccionado (solo
                                                              ' desactivada, no eliminada). Quitamos las previas antes de navegar.
                                                              navegacion.QuitarVistas("RapportDetailRegion")
                                                              navegacion.RequestNavigate("RapportDetailRegion", "RapportView", parameters)
                                                          End Sub)
                End SyncLock
            Catch
                _dialogService.ShowError("No se ha podido actualizar el rapport seleccionado")
            End Try
        End Set
    End Property

    Private _resumenListaRapports As String
    Public Property ResumenListaRapports As String
        Get
            Return _resumenListaRapports
        End Get
        Set(value As String)
            Dim unused = SetProperty(_resumenListaRapports, value)
        End Set
    End Property

    Private _resumenVentasCliente As ResumenVentasClienteResponse
    Public Property ResumenVentasCliente As ResumenVentasClienteResponse
        Get
            Return _resumenVentasCliente
        End Get
        Set(value As ResumenVentasClienteResponse)
            Dim unused = SetProperty(_resumenVentasCliente, value)
        End Set
    End Property

    Private _detalleVentasProductos As ResumenVentasClienteResponse
    Public Property DetalleVentasProductos As ResumenVentasClienteResponse
        Get
            Return _detalleVentasProductos
        End Get
        Set(value As ResumenVentasClienteResponse)
            Dim unused = SetProperty(_detalleVentasProductos, value)
        End Set
    End Property

    Private _mostrandoDetalleVentas As Boolean = False
    Public Property MostrandoDetalleVentas As Boolean
        Get
            Return _mostrandoDetalleVentas
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_mostrandoDetalleVentas, value)
            RaisePropertyChanged(NameOf(MostrandoResumenVentas))
        End Set
    End Property

    Public ReadOnly Property MostrandoResumenVentas As Boolean
        Get
            Return Not MostrandoDetalleVentas
        End Get
    End Property

    Private _filtroDetalleVentas As String
    Public Property FiltroDetalleVentas As String
        Get
            Return _filtroDetalleVentas
        End Get
        Set(value As String)
            Dim unused = SetProperty(_filtroDetalleVentas, value)
        End Set
    End Property

    Public ReadOnly Property SubtituloResumenVentas As String
        Get
            Return If(ResumenVentasCliente IsNot Nothing,
                $"Comparado con las ventas del {ResumenVentasCliente.FechaDesdeAnterior:dd/MM/yy} al {ResumenVentasCliente.FechaHastaAnterior:dd/MM/yy}",
                String.Empty)
        End Get
    End Property

    Private _puedeCopiarSeguimientos As Boolean = False
    Public Property PuedeCopiarSeguimientos As Boolean
        Get
            Return _puedeCopiarSeguimientos
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_puedeCopiarSeguimientos, value)
        End Set
    End Property

    Private ReadOnly _syncLock As New Object()

    Private _tipoRapportSeleccionado As idDescripcion
    Public Property TipoRapportSeleccionado As idDescripcion
        Get
            Return _tipoRapportSeleccionado
        End Get
        Set(ByVal value As idDescripcion)
            If SetProperty(_tipoRapportSeleccionado, value) Then
                ActualizarClientesProbabilidad(GrupoSubgrupoSeleccionado)
                Dim unused = configuracion.GuardarParametro(_empresaPorDefecto, Parametros.Claves.UltTipoSeguimientoCliente, _tipoRapportSeleccionado.id)
            End If
        End Set
    End Property

    Public ReadOnly Property TituloResumenVentas As String
        Get
            Return If(ResumenVentasCliente IsNot Nothing,
                $"Resumen de ventas del cliente del {ResumenVentasCliente.FechaDesdeActual:dd/MM/yy} al {ResumenVentasCliente.FechaHastaActual:dd/MM/yy}",
                "Resumen de ventas del cliente")
        End Get
    End Property

#End Region

#Region "Comandos"
    Private _cmdAbrirModulo As RelayCommand(Of Object)
    Public Property cmdAbrirModulo As RelayCommand(Of Object)
        Get
            Return _cmdAbrirModulo
        End Get
        Private Set(value As RelayCommand(Of Object))
            Dim unused = SetProperty(_cmdAbrirModulo, value)
        End Set
    End Property
    Private Function CanAbrirModulo(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirModulo(arg As Object)
        navegacion.RequestNavigate("MainRegion", "ListaRapportsView")
    End Sub


    Public Property CambiarModoComparativaCommand As RelayCommand(Of String)
    Public Property CambiarAgruparPorCommand As RelayCommand(Of String)
    Private Async Sub OnCargarResumenVentas()
        MostrandoDetalleVentas = False
        Await LlamarApiResumenVentasAsync()
    End Sub


    Private _cmdCargarListaRapports As RelayCommand(Of Object)
    Public Property cmdCargarListaRapports As RelayCommand(Of Object)
        Get
            Return _cmdCargarListaRapports
        End Get
        Private Set(value As RelayCommand(Of Object))
            Dim unused = SetProperty(_cmdCargarListaRapports, value)
        End Set
    End Property
    Private Function CanCargarListaRapports(arg As Object) As Boolean
        Return Not IsNothing(clienteSeleccionado) Or Not IsNothing(fechaSeleccionada)
    End Function
    Private Async Sub OnCargarListaRapports(arg As Object)
        Await CargarListaRapportsAsync()
    End Sub

    ''' <summary>
    ''' Nesto#488: la carga es una Function que se espera (no un Async Sub lanzado con Task.Run): así
    ''' quien la llama sabe cuándo ha terminado y todo sigue en el hilo de la UI. Con el RelayCommand de
    ''' CommunityToolkit, un NotifyCanExecuteChanged desde un hilo del pool toca los botones fuera de su hilo.
    ''' </summary>
    Public Async Function CargarListaRapportsAsync() As Task
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(_empresaPorDefecto, "Vendedor")
        End If
        ResumenListaRapports = String.Empty
        If Not IsNothing(clienteSeleccionado) Then
            listaRapports = Await servicio.cargarListaRapports(_empresaPorDefecto, clienteSeleccionado, contactoSeleccionado)
            Await LlamarApiResumenVentasAsync()
        Else
            Dim parametroVendedor As String
            parametroVendedor = IIf(esUsuarioElVendedor, configuracion.usuario, vendedor)
            listaRapports = Await servicio.cargarListaRapports(parametroVendedor, fechaSeleccionada)
            rapportSeleccionado = listaRapports.FirstOrDefault
        End If
        GenerarResumenCommand.NotifyCanExecuteChangedEnUi()
    End Function

    Private _cmdCargarListaRapportsFiltrada As RelayCommand
    Public Property cmdCargarListaRapportsFiltrada As RelayCommand
        Get
            Return _cmdCargarListaRapportsFiltrada
        End Get
        Private Set(value As RelayCommand)
            Dim unused = SetProperty(_cmdCargarListaRapportsFiltrada, value)
        End Set
    End Property
    Private Function CanCargarListaRapportsFiltrada() As Boolean
        Return Not IsNothing(Filtro)
    End Function
    Private Async Sub OnCargarListaRapportsFiltrada()
        Dim todosLosClientes As String = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.PermitirVerClientesTodosLosVendedores)
        Try
            EstaOcupado = True
            listaRapports = If(todosLosClientes = "1",
                Await servicio.cargarListaRapportsFiltrada(String.Empty, Filtro),
                Await servicio.cargarListaRapportsFiltrada(vendedor, Filtro))

            rapportSeleccionado = listaRapports.FirstOrDefault
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        Finally
            EstaOcupado = False
        End Try

    End Sub


    Public Event IsActiveChanged As EventHandler Implements IActiveAware.IsActiveChanged

    Private _cmdCrearRapport As RelayCommand(Of ClienteProbabilidadVenta)
    Public Property cmdCrearRapport As RelayCommand(Of ClienteProbabilidadVenta)
        Get
            Return _cmdCrearRapport
        End Get
        Private Set(value As RelayCommand(Of ClienteProbabilidadVenta))
            Dim unused = SetProperty(_cmdCrearRapport, value)
        End Set
    End Property

    Private Function CanCrearRapport(cliente As ClienteProbabilidadVenta) As Boolean
        Return True
    End Function
    Private Async Sub OnCrearRapport(cliente As ClienteProbabilidadVenta)
        Await CrearRapportAsync(cliente?.cliente, cliente?.contacto, TipoRapportSeleccionado.id)
    End Sub

    ''' <summary>
    ''' Un rapport nuevo (sin guardar) del cliente, con el vendedor del usuario y el tipo indicado; antes carga sus rapports.
    ''' Sin cliente, un rapport en blanco. NestoAPI#593: lo usan también los botones de llamar/WhatsApp de los cheques.
    ''' </summary>
    Public Async Function CrearRapportAsync(cliente As String, contacto As String, tipo As String) As Task

        If Not IsNothing(cliente) Then
            clienteSeleccionado = cliente
            contactoSeleccionado = contacto
            Try
                ' Nesto#488: esperar la carga DE VERDAD y en el hilo de la UI. Con Task.Run(Execute) el Async Sub
                ' seguía en el pool: la lista cargada podía llegar después y pisar el rapport nuevo.
                Await CargarListaRapportsAsync()
            Catch
                _dialogService.ShowError("No se ha podido cargar la lista de rapports")
            End Try
        End If


        Dim rapportNuevo As New SeguimientoClienteDTO With {
            .Empresa = _empresaPorDefecto,
            .Estado = SeguimientoClienteDTO.EstadoSeguimientoDTO.Vigente,
            .Fecha = IIf(fechaSeleccionada >= Today, Now, fechaSeleccionada),
            .Tipo = tipo,
            .TipoCentro = SeguimientoClienteDTO.TiposCentro.NoSeSabe,
            .Vendedor = vendedor,
            .Usuario = configuracion.usuario
        }

        If Not IsNothing(cliente) Then
            rapportNuevo.Cliente = cliente
            rapportNuevo.Contacto = contacto
        End If

        'no entiendo por qué es necesaria esta línea
        'pero si la quitamos solo crea bien un rapport de cada dos
        rapportSeleccionado = Nothing

        rapportSeleccionado = rapportNuevo
        If IsNothing(listaRapports) Then
            listaRapports = New ObservableCollection(Of SeguimientoClienteDTO)
        End If
        listaRapports.Add(rapportNuevo)
    End Function

    Public Property GenerarResumenCommand As RelayCommand
    Private Function CanGenerarResumen() As Boolean
        Return Not IsNothing(clienteSeleccionado) AndAlso listaRapports IsNot Nothing AndAlso listaRapports.Count > 10 AndAlso String.IsNullOrEmpty(ResumenListaRapports)
    End Function
    Private Async Sub OnGenerarResumen()
        If Not CanGenerarResumen() Then Exit Sub

        Try
            EstaGenerandoResumen = True
            ResumenListaRapports = Await servicio.CargarResumenRapports(_empresaPorDefecto, clienteSeleccionado, contactoSeleccionado)
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        Finally
            EstaGenerandoResumen = False
            GenerarResumenCommand.NotifyCanExecuteChanged() ' Deshabilitar el botón tras generar el resumen
        End Try
    End Sub


    Public Property VerDetalleVentasCommand As RelayCommand(Of VentaClienteResumenDTO)
    Private Function CanVerDetalleVentas(venta As VentaClienteResumenDTO) As Boolean
        Return venta IsNot Nothing AndAlso venta.Nombre <> "TOTAL"
    End Function
    Private Async Sub OnVerDetalleVentas(venta As VentaClienteResumenDTO)
        If Not CanVerDetalleVentas(venta) Then Exit Sub

        Try
            EstaOcupado = True
            FiltroDetalleVentas = venta.Nombre
            Dim detalle = Await servicio.CargarDetalleVentasProducto(clienteSeleccionado, venta.Nombre, ModoComparativa, AgruparPor)
            detalle.Datos = detalle.Datos.OrderBy(Function(x) x.Diferencia).ToList()
            AgregarLineaTotal(detalle.Datos)
            DetalleVentasProductos = detalle
            MostrandoDetalleVentas = True
        Catch ex As Exception
            _dialogService.ShowError("No se ha podido cargar el detalle de ventas: " & ex.Message)
        Finally
            EstaOcupado = False
        End Try
    End Sub

    Public Property VolverAResumenVentasCommand As RelayCommand
    Private Sub OnVolverAResumenVentas()
        MostrandoDetalleVentas = False
    End Sub

    Public Property AbrirFichaProductoCommand As RelayCommand(Of VentaClienteResumenDTO)
    Private Function CanAbrirFichaProducto(venta As VentaClienteResumenDTO) As Boolean
        Return venta IsNot Nothing AndAlso venta.Nombre <> "TOTAL" AndAlso venta.Nombre.Contains(" - ")
    End Function
    Private Sub OnAbrirFichaProducto(venta As VentaClienteResumenDTO)
        If Not CanAbrirFichaProducto(venta) Then Exit Sub
        Dim productoId = venta.Nombre.Split({" - "}, 2, StringSplitOptions.None)(0).Trim()
        Dim parameters As New ParametrosNavegacion From {
            {"numeroProductoParameter", productoId}
        }
        navegacion.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    ' Comando para actualizar SelectedAction usando RelayCommand
    Private _tipoRapportCambiaCommand As RelayCommand(Of String)
    Public ReadOnly Property TipoRapportCambiaCommand As RelayCommand(Of String)
        Get
            If _tipoRapportCambiaCommand Is Nothing Then
                _tipoRapportCambiaCommand = New RelayCommand(Of String)(AddressOf OnTipoRapportCambia)
            End If
            Return _tipoRapportCambiaCommand
        End Get
    End Property

    ' Método que se ejecuta cuando se selecciona una opción
    Private Sub OnTipoRapportCambia(selectedId As String)
        If selectedId IsNot Nothing Then
            ' Encuentra el elemento de listaTiposRapports con el id correspondiente y lo asigna a SelectedAction
            TipoRapportSeleccionado = listaTiposRapports.Single(Function(item) item.id = selectedId)
        End If
    End Sub

    ' Propiedad para verificar si un item es el seleccionado
    Public Function IsSelectedAction(itemId As String) As Boolean
        Return (Not IsNothing(TipoRapportSeleccionado)) AndAlso TipoRapportSeleccionado.id = itemId
    End Function

    Public Property CopiarSeguimientosCommand As RelayCommand
    Private Function CanCopiarSeguimientos() As Boolean
        Return Not String.IsNullOrWhiteSpace(clienteSeleccionado)
    End Function
    Private Sub OnCopiarSeguimientos()
        Dim parameters As New ParametrosDialogo From {
            {"empresa", _empresaPorDefecto},
            {"cliente", clienteSeleccionado},
            {"contacto", contactoSeleccionado}
        }
        _dialogService.ShowDialog("CopiarSeguimientosView", parameters, Sub(result)
                                                                            If result.Result = ResultadoBoton.OK Then
                                                                                cmdCargarListaRapports.Execute(Nothing)
                                                                            End If
                                                                        End Sub)
    End Sub




    Private Async Sub ActualizarClientesProbabilidad(grupoSubgrupo As String)
        ' Sin tipo todavía (el grupo ha llegado antes de que OnNavigatedTo lo lea) no se pide nada: se pide al poner el
        ' tipo. Antes salía una llamada con el tipo vacío.
        If IsNothing(TipoRapportSeleccionado.id) Then
            Exit Sub
        End If
        Await ActualizarClientesProbabilidadAsync(grupoSubgrupo)
    End Sub

    ''' <summary>
    ''' NestoAPI#603: la lista sale de GET api/Clientes/SugerenciasContacto (prioridad, cadencia, motivo y
    ''' ritmo). Las atendidas van al final, marcadas. Con la API antigua, sin prioridad ni ritmo.
    ''' </summary>
    Public Async Function ActualizarClientesProbabilidadAsync(grupoSubgrupo As String) As Task
        ' Nesto#521: al cambiar de vendedor (o de tipo o de grupo) con una carga en marcha, solo se pinta la última: la
        ' respuesta de la anterior podría llegar después y dejar en pantalla la lista de otro vendedor.
        _cargaSugerencias += 1
        Dim estaCarga As Integer = _cargaSugerencias
        IsLoadingClientesProbabilidad = True
        Try
            Dim respuesta = Await servicio.CargarSugerenciasContacto(VendedorSugerencias, TipoRapportSeleccionado.descripcion, grupoSubgrupo)
            If estaCarga <> _cargaSugerencias Then
                Return
            End If
            ' Nesto#381: defensivo ante null (el ctor de ObservableCollection peta con Nothing).
            ListaClientesProbabilidad = New ObservableCollection(Of ClienteProbabilidadVenta)(OrdenSugerenciasContacto.Ordenar(respuesta?.Sugerencias))
            RitmoContacto = respuesta?.Ritmo
        Finally
            If estaCarga = _cargaSugerencias Then
                IsLoadingClientesProbabilidad = False
            End If
        End Try
    End Function

    Private _cargaSugerencias As Integer

    ''' <summary>
    ''' Nesto#521: los vendedores del desplegable. Se piden una vez (la pestaña se reutiliza) y en paralelo a la primera
    ''' lista, que ya sale con el vendedor del usuario; cargar el desplegable no vuelve a pedir la lista.
    ''' </summary>
    Public Async Function CargarVendedoresSugerenciasAsync() As Task
        If _vendedoresSugerenciasCargados Then
            Return
        End If
        _vendedoresSugerenciasCargados = True
        Dim vendedores As List(Of VendedorSugerencias) = Nothing
        Try
            vendedores = Await servicio.CargarVendedoresSugerencias()
        Catch
            vendedores = Nothing
        End Try
        ListaVendedoresSugerencias = New ObservableCollection(Of VendedorSugerencias)(If(vendedores, New List(Of VendedorSugerencias)))
    End Function

    Private _vendedoresSugerenciasCargados As Boolean

    Private Sub OnLoaded()
        ' Suscríbete solo si no hay una suscripción activa (Nesto#490 4C.1: Messenger en vez de IEventAggregator;
        ' registrar dos veces al mismo receptor lanza, de ahí el IsRegistered).
        If Not _messenger.IsRegistered(Of RapportGuardadoMensaje)(Me) Then
            ' NestoAPI#603 c2: al guardar un rapport la lista se recarga con el grupo seleccionado (antes el 0 del
            ' aviso llegaba como grupoSubgrupo "0" por la conversión implícita de VB y se perdía el filtro).
            _messenger.Register(Of RapportGuardadoMensaje)(Me, Sub(r, m)
                                                                  Dim lista = DirectCast(r, ListaRapportsViewModel)
                                                                  lista.ActualizarClientesProbabilidad(If(lista.GrupoSubgrupoSeleccionado, String.Empty))
                                                              End Sub)
        End If
        If Not _messenger.IsRegistered(Of RapportNoGuardadoMensaje)(Me) Then
            _messenger.Register(Of RapportNoGuardadoMensaje)(Me, Sub(r, m) DirectCast(r, ListaRapportsViewModel).QuitarRapportNoGuardado(m.Value))
        End If
    End Sub

    ''' <summary>
    ''' Nesto#206: OnCrearRapport mete el rapport nuevo en la lista ANTES de guardarlo (la pantalla
    ''' de rapport se abre sobre la fila seleccionada). Si el guardado falla, la fila se quedaba
    ''' como si hubiera ido bien. Un rapport nuevo (Id = 0) que no se ha podido guardar se quita;
    ''' uno ya existente que falla al modificarse se queda, porque en la base de datos sigue.
    ''' </summary>
    Public Sub QuitarRapportNoGuardado(rapport As Object)
        Dim dto As SeguimientoClienteDTO = TryCast(rapport, SeguimientoClienteDTO)
        If dto Is Nothing OrElse dto.Id <> 0 OrElse listaRapports Is Nothing OrElse Not listaRapports.Contains(dto) Then
            Return
        End If
        listaRapports.Remove(dto)
        If Object.ReferenceEquals(rapportSeleccionado, dto) Then
            rapportSeleccionado = Nothing
        End If
    End Sub

    Private Sub OnUnloaded()
        ' Desuscribirse de los mensajes (no hace nada si no estaba suscrita)
        _messenger.Unregister(Of RapportGuardadoMensaje)(Me)
        _messenger.Unregister(Of RapportNoGuardadoMensaje)(Me)
    End Sub


    ' Nesto#490 (4C.4): la lista reutiliza su pestaña (antes IsNavigationTarget = True), así que hereda de ViewModelBasico
    ' y no de ViewModelBase (que abre una pestaña nueva cada vez). Sin INavigationAware, Prism ya reutiliza la vista abierta.
    Public Async Sub AlLlegar(parametrosNavegacion As ParametrosNavegacion) Implements IReceptorNavegacion.AlLlegar
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.Vendedor)
        End If
        ' Nesto#521: el desplegable de vendedor se pide a la vez que la lista (que sale con el vendedor del usuario).
        Dim cargaVendedores As Task = CargarVendedoresSugerenciasAsync()
        ' NestoAPI#593: el resumen de los cheques regalo (si hay campaña sale la pestaña), también en paralelo.
        Dim cargaCheques As Task = CargarResumenChequesRegaloAsync()
        If IsNothing(TipoRapportSeleccionado) OrElse IsNothing(TipoRapportSeleccionado.id) Then
            Dim tipo = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.UltTipoSeguimientoCliente)
            TipoRapportCambiaCommand.Execute(tipo)
        Else
            ActualizarClientesProbabilidad(GrupoSubgrupoSeleccionado)
        End If

        Dim permitirCopiar = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.PermitirCopiarSeguimientos)
        PuedeCopiarSeguimientos = permitirCopiar = "1"
        Await cargaVendedores
        Await cargaCheques
        If _chequesPendientesCargados Then
            ActualizarChequesPendientes()
        End If
    End Sub

#End Region

#Region "Funciones Auxiliares"
    Private Sub AgregarLineaTotal(datos As List(Of VentaClienteResumenDTO))
        If datos.Count > 0 Then
            Dim total As New VentaClienteResumenDTO With {
            .Nombre = "TOTAL",
            .VentaAnnoActual = datos.Sum(Function(d) d.VentaAnnoActual),
            .VentaAnnoAnterior = datos.Sum(Function(d) d.VentaAnnoAnterior),
            .UnidadesAnnoActual = datos.Sum(Function(d) d.UnidadesAnnoActual),
            .UnidadesAnnoAnterior = datos.Sum(Function(d) d.UnidadesAnnoAnterior)
        }
            datos.Add(total)
        End If
    End Sub

    Private Async Function LlamarApiResumenVentasAsync() As Task
        If String.IsNullOrEmpty(clienteSeleccionado) Then Exit Function

        Try
            EstaOcupado = True
            Dim resumen = Await servicio.CargarResumenVentasCliente(clienteSeleccionado, ModoComparativa, AgruparPor)
            resumen.Datos = resumen.Datos.OrderBy(Function(x) x.Diferencia).ToList()
            AgregarLineaTotal(resumen.Datos)
            ResumenVentasCliente = resumen

            RaisePropertyChanged(NameOf(TituloResumenVentas))
            RaisePropertyChanged(NameOf(SubtituloResumenVentas))
        Catch ex As Exception
            _dialogService.ShowError("No se ha podido cargar el resumen de ventas: " & ex.Message)
        Finally
            EstaOcupado = False
        End Try
    End Function

#End Region
End Class
