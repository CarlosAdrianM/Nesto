Imports System.Windows.Input
Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.ComponentModel
Imports System.Windows
Imports System.Net
Imports System.IO
Imports System.Windows.Controls
Imports System.Text.RegularExpressions
Imports System.Data.Objects
Imports Microsoft.Practices.Prism.Mvvm
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Practices.Prism.PubSubEvents
Imports System.Text
Imports Microsoft.Win32
Imports Microsoft.Practices.Prism
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports System.Transactions
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Net.Http.Formatting
Imports System.Threading.Tasks


Public Class AgenciasViewModel
    Inherits BindableBase
    '    Implements IActiveAware

    ' El modo cuadre sirve para cuadrar los saldos iniciales de cada agencia con la contabilidad
    ' En este modo al contabilizar los reembolsos no se toca la contabilidad, pero sí se pone la 
    ' fecha de cobro a 01/01/15
    Const MODO_CUADRE = False

    Const CARGO_AGENCIA = 26
    Public Const COD_PAIS As String = "34"
    Public Const ESTADO_INICIAL_ENVIO = 0
    Public Const ESTADO_TRAMITADO_ENVIO = 1
    Private Const ESTADO_SIN_FACTURAR = 1
    Private Const ESTADO_LINEA_PENDIENTE = -1
    Private Const EMPRESA_ESPEJO As String = "3  "

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Private Shared DbContext As NestoEntities
    Dim mainModel As New Nesto.Models.MainModel
    Dim empresaDefecto As String = String.Format("{0,-3}", mainModel.leerParametro("1", "EmpresaPorDefecto"))

    Dim factory As New Dictionary(Of String, Func(Of IAgencia))


    Public Sub New()

    End Sub

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If

        Me.container = container
        Me.regionManager = regionManager

        DbContext = New NestoEntities

        ' Prism
        cmdCargarEstado = New DelegateCommand(Of Object)(AddressOf OnCargarEstado, AddressOf CanCargarEstado)
        cmdAgregarReembolsoContabilizar = New DelegateCommand(Of Object)(AddressOf OnAgregarReembolsoContabilizar, AddressOf CanAgregarReembolsoContabilizar)
        cmdRecibirRetorno = New DelegateCommand(Of Object)(AddressOf OnRecibirRetorno, AddressOf CanRecibirRetorno)
        cmdQuitarReembolsoContabilizar = New DelegateCommand(Of Object)(AddressOf OnQuitarReembolsoContabilizar, AddressOf CanQuitarReembolsoContabilizar)
        cmdContabilizarReembolso = New DelegateCommand(Of Object)(AddressOf OnContabilizarReembolso, AddressOf CanContabilizarReembolso)
        cmdDescargarImagen = New DelegateCommand(Of Object)(AddressOf OnDescargarImagen, AddressOf CanDescargarImagen)
        cmdModificarEnvio = New DelegateCommand(Of Object)(AddressOf OnModificarEnvio, AddressOf CanModificarEnvio)
        cmdImprimirManifiesto = New DelegateCommand(Of Object)(AddressOf OnImprimirManifiesto, AddressOf CanImprimirManifiesto)
        cmdRehusarEnvio = New DelegateCommand(Of Object)(AddressOf OnRehusarEnvio, AddressOf CanRehusarEnvio)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        factory.Add("ASM", Function() New AgenciaASM(Me))
        factory.Add("OnTime", Function() New AgenciaOnTime(Me))

        listaEmpresas = New ObservableCollection(Of Empresas)(From c In DbContext.Empresas)
        empresaSeleccionada = (From e In DbContext.Empresas Where e.Número = empresaDefecto).FirstOrDefault
        listaAgencias = New ObservableCollection(Of AgenciasTransporte)(From c In DbContext.AgenciasTransporte Where c.Empresa = empresaSeleccionada.Número)
        agenciaSeleccionada = listaAgencias.FirstOrDefault

        bultos = 1
        If Not IsNothing(agenciaSeleccionada) Then
            listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado = ESTADO_INICIAL_ENVIO Order By e.Numero)
        End If
        envioActual = listaEnvios.LastOrDefault
        numeroPedido = mainModel.leerParametro(empresaDefecto, "UltNumPedidoVta")
        fechaFiltro = Today
        listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
        'listaReembolsos = New ObservableCollection(Of EnviosAgencia)
        listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia)
    End Sub

#Region "Propiedades"
    ' Carlos 03/09/14
    ' Las propiedades que terminan en "envio" son las que se usan de manera temporal para que el usuario
    ' pueda modificar los datos. Por ejemplo, nombreEnvio se actualiza con el nombre del cliente cada vez que
    ' cambiamos de pedido, pero el usuario puede modificarlas. En el momento de hacer la inserción en la tabla
    ' EnviosAgencia coge el valor que tenga esta propiedad. Así permitimos hacer excepciones y no hay que 
    ' mandarlo siempre con el valor que tiene el campo en la tabla.

    '*** Propiedades de Prism 
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    Private _ConfirmationRequest As InteractionRequest(Of IConfirmation)
    Public Property ConfirmationRequest As InteractionRequest(Of IConfirmation)
        Get
            Return _ConfirmationRequest
        End Get
        Private Set(value As InteractionRequest(Of IConfirmation))
            _ConfirmationRequest = value
        End Set
    End Property

    Private resultMessage As String
    Public Property InteractionResultMessage As String
        Get
            Return Me.resultMessage
        End Get
        Set(value As String)
            Me.resultMessage = value
            Me.OnPropertyChanged("InteractionResultMessage")
        End Set
    End Property

    '*** Propiedades de Nesto
    Private _resultadoWebservice As String
    Public Property resultadoWebservice As String
        Get
            Return _resultadoWebservice
        End Get
        Set(value As String)
            SetProperty(_resultadoWebservice, value)
        End Set
    End Property

    Private _listaAgencias As ObservableCollection(Of AgenciasTransporte)
    Public Property listaAgencias As ObservableCollection(Of AgenciasTransporte)
        Get
            Return _listaAgencias
        End Get
        Set(value As ObservableCollection(Of AgenciasTransporte))
            SetProperty(_listaAgencias, value)
        End Set
    End Property

    Private agenciaEspecifica As IAgencia

    Private _agenciaSeleccionada As AgenciasTransporte
    Public Property agenciaSeleccionada As AgenciasTransporte
        Get
            Return _agenciaSeleccionada
        End Get
        Set(value As AgenciasTransporte)
            SetProperty(_agenciaSeleccionada, value)
            Try
                If Not IsNothing(agenciaSeleccionada) Then
                    agenciaEspecifica = factory(agenciaSeleccionada.Nombre).Invoke
                    OnPropertyChanged("servicioActual")
                    OnPropertyChanged("horarioActual")
                    OnPropertyChanged("retornoActual")
                    'listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Estado = ESTADO_INICIAL_ENVIO)
                    listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado = ESTADO_INICIAL_ENVIO Order By e.Numero)
                    listaReembolsos = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado >= ESTADO_TRAMITADO_ENVIO And e.Reembolso > 0 And e.FechaPagoReembolso Is Nothing)
                    listaRetornos = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado >= ESTADO_TRAMITADO_ENVIO And e.Retorno <> agenciaEspecifica.retornoSinRetorno And e.FechaRetornoRecibido Is Nothing Order By e.Fecha)
                    OnPropertyChanged("sumaContabilidad")
                    OnPropertyChanged("descuadreContabilidad")
                    OnPropertyChanged("visibilidadSoloImprimir")
                End If
            Catch
                'mensajeError = "No se encuentra la implementación de la agencia " + agenciaSeleccionada.Nombre
                NotificationRequest.Raise(New Notification() With { _
                    .Title = "Error", _
                    .Content = "No se encuentra la implementación de la agencia " + agenciaSeleccionada.Nombre _
                })
            End Try

        End Set
    End Property

    Private _XMLdeSalida As XDocument
    Public Property XMLdeSalida As XDocument
        Get
            Return _XMLdeSalida
        End Get
        Set(value As XDocument)
            SetProperty(_XMLdeSalida, value)
        End Set
    End Property

    Private _XMLdeEntrada As XDocument
    Public Property XMLdeEntrada As XDocument
        Get
            Return _XMLdeEntrada
        End Get
        Set(value As XDocument)
            SetProperty(_XMLdeEntrada, value)
        End Set
    End Property

    Private _empresaSeleccionada As Empresas
    Public Property empresaSeleccionada As Empresas
        Get
            Return _empresaSeleccionada
        End Get
        Set(value As Empresas)
            SetProperty(_empresaSeleccionada, value)
            listaAgencias = New ObservableCollection(Of AgenciasTransporte)(From c In DbContext.AgenciasTransporte Where c.Empresa = empresaSeleccionada.Número)
            If numeroPedido = "" Then
                agenciaSeleccionada = listaAgencias.FirstOrDefault
            End If
            If Not IsNothing(agenciaSeleccionada) Then
                listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado = ESTADO_INICIAL_ENVIO Order By e.Numero)
                listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
                listaReembolsos = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado = ESTADO_TRAMITADO_ENVIO And e.Reembolso > 0 And e.FechaPagoReembolso Is Nothing)
                listaRetornos = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado >= ESTADO_TRAMITADO_ENVIO And e.Retorno <> agenciaEspecifica.retornoSinRetorno And e.FechaRetornoRecibido Is Nothing Order By e.Fecha)
            Else
                listaEnvios = New ObservableCollection(Of EnviosAgencia)
                listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)
                listaReembolsos = New ObservableCollection(Of EnviosAgencia)
                listaRetornos = New ObservableCollection(Of EnviosAgencia)
            End If
            listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia)

            OnPropertyChanged("sumaContabilidad")
            OnPropertyChanged("descuadreContabilidad")
            'actualizar lista de pedidos o de envíos, dependiendo de la pestaña que esté seleccionada
            'una vez actualizadas, seleccionar el pedido o el envío actual también
        End Set
    End Property

    Private _pedidoSeleccionado As CabPedidoVta
    Public Property pedidoSeleccionado As CabPedidoVta
        Get
            Return _pedidoSeleccionado
        End Get
        Set(value As CabPedidoVta)
            SetProperty(_pedidoSeleccionado, value)
            configurarAgenciaPedido(agenciaSeleccionada)
            If Not IsNothing(pedidoSeleccionado) AndAlso Not IsNothing(pedidoSeleccionado.Clientes) Then
                Try
                    reembolso = importeReembolso()
                    bultos = 1
                    nombreEnvio = pedidoSeleccionado.Clientes.Nombre.Trim
                    direccionEnvio = pedidoSeleccionado.Clientes.Dirección.Trim
                    poblacionEnvio = pedidoSeleccionado.Clientes.Población.Trim
                    provinciaEnvio = pedidoSeleccionado.Clientes.Provincia.Trim
                    codPostalEnvio = pedidoSeleccionado.Clientes.CodPostal.Trim
                    telefonoEnvio = telefonoUnico(pedidoSeleccionado.Clientes.Teléfono.Trim, "F")
                    movilEnvio = telefonoUnico(pedidoSeleccionado.Clientes.Teléfono.Trim, "M")
                    correoEnvio = correoUnico()
                    observacionesEnvio = pedidoSeleccionado.Comentarios
                    attEnvio = nombreEnvio
                    If IsNothing(empresaSeleccionada.FechaPicking) Then
                        fechaEnvio = Today
                    Else
                        fechaEnvio = empresaSeleccionada.FechaPicking
                    End If
                    listaEnviosPedido = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Pedido = pedidoSeleccionado.Número)
                    envioActual = listaEnviosPedido.LastOrDefault
                Catch ex As Exception
                    If Not IsNothing(NotificationRequest) Then
                        NotificationRequest.Raise(New Notification() With { _
                         .Title = "Error", _
                        .Content = ex.Message _
                        })
                    End If
                End Try

            Else
                NotificationRequest.Raise(New Notification() With { _
                 .Title = "Error", _
                .Content = "El pedido seleccionado no existe" _
                })
            End If
        End Set
    End Property

    Private _listaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTiposRetorno
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            SetProperty(_listaTiposRetorno, value)
            OnPropertyChanged("retornoModificar")
        End Set
    End Property

    Private _retornoActual As tipoIdDescripcion
    Public Property retornoActual As tipoIdDescripcion
        Get
            Return _retornoActual
        End Get
        Set(value As tipoIdDescripcion)
            SetProperty(_retornoActual, value)
        End Set
    End Property

    Private _reembolso As Decimal
    Public Property reembolso As Decimal
        Get
            Return _reembolso
        End Get
        Set(value As Decimal)
            SetProperty(_reembolso, value)
        End Set
    End Property

    Private _bultos As Integer
    Public Property bultos As Integer
        Get
            Return _bultos
        End Get
        Set(value As Integer)
            SetProperty(_bultos, value)
        End Set
    End Property

    Private _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            SetProperty(_mensajeError, value)
        End Set
    End Property

    Private _listaServicios As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaServicios As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaServicios
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            SetProperty(_listaServicios, value)
        End Set
    End Property

    Private _servicioActual As tipoIdDescripcion
    Public Property servicioActual As tipoIdDescripcion
        Get
            Return _servicioActual
        End Get
        Set(value As tipoIdDescripcion)
            SetProperty(_servicioActual, value)
        End Set
    End Property

    Private _listaHorarios As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaHorarios As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaHorarios
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            SetProperty(_listaHorarios, value)
        End Set
    End Property

    Private _horarioActual As tipoIdDescripcion
    Public Property horarioActual As tipoIdDescripcion
        Get
            Return _horarioActual
        End Get
        Set(value As tipoIdDescripcion)
            SetProperty(_horarioActual, value)
        End Set
    End Property

    Private _nombreEnvio As String
    Public Property nombreEnvio As String
        Get
            Return _nombreEnvio
        End Get
        Set(value As String)
            SetProperty(_nombreEnvio, value)
        End Set
    End Property

    Private _direccionEnvio As String
    Public Property direccionEnvio As String
        Get
            Return _direccionEnvio
        End Get
        Set(value As String)
            SetProperty(_direccionEnvio, value)
        End Set
    End Property

    Private _poblacionEnvio As String
    Public Property poblacionEnvio As String
        Get
            Return _poblacionEnvio
        End Get
        Set(value As String)
            SetProperty(_poblacionEnvio, value)
        End Set
    End Property

    Private _provinciaEnvio As String
    Public Property provinciaEnvio As String
        Get
            Return _provinciaEnvio
        End Get
        Set(value As String)
            SetProperty(_provinciaEnvio, value)
        End Set
    End Property

    Private _codPostalEnvio As String
    Public Property codPostalEnvio As String
        Get
            Return _codPostalEnvio
        End Get
        Set(value As String)
            SetProperty(_codPostalEnvio, value)
        End Set
    End Property

    Private _telefonoEnvio As String
    Public Property telefonoEnvio As String
        Get
            Return _telefonoEnvio
        End Get
        Set(value As String)
            SetProperty(_telefonoEnvio, value)
        End Set
    End Property

    Private _movilEnvio As String
    Public Property movilEnvio As String
        Get
            Return _movilEnvio
        End Get
        Set(value As String)
            SetProperty(_movilEnvio, value)
        End Set
    End Property

    Private _correoEnvio As String
    Public Property correoEnvio As String
        Get
            Return _correoEnvio
        End Get
        Set(value As String)
            SetProperty(_correoEnvio, value)
        End Set
    End Property

    Private _observacionesEnvio As String
    Public Property observacionesEnvio As String
        Get
            Return _observacionesEnvio
        End Get
        Set(value As String)
            SetProperty(_observacionesEnvio, value)
        End Set
    End Property

    Private _attEnvio As String
    Public Property attEnvio As String
        Get
            Return _attEnvio
        End Get
        Set(value As String)
            SetProperty(_attEnvio, value)
        End Set
    End Property

    Private _fechaEnvio As Date
    Public Property fechaEnvio As Date
        Get
            Return _fechaEnvio
        End Get
        Set(value As Date)
            SetProperty(_fechaEnvio, value)
        End Set
    End Property

    Private _envioActual As EnviosAgencia
    Public Property envioActual As EnviosAgencia
        Get
            Return _envioActual
        End Get
        Set(value As EnviosAgencia)
            SetProperty(_envioActual, value)
            estadoEnvioCargado = Nothing
            mensajeError = ""
            If Not IsNothing(envioActual) AndAlso Not IsNothing(envioActual.AgenciasTransporte) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso agenciaSeleccionada.Numero <> envioActual.AgenciasTransporte.Numero Then
                agenciaSeleccionada = envioActual.AgenciasTransporte
            End If

            OnPropertyChanged("agenciaSeleccionada") 'Una vez haya datos (un pedido con dos envios de agencias diferentes), comprobar si se puede quitar esta línea
            If Not IsNothing(cmdCargarEstado) Then
                cmdCargarEstado.RaiseCanExecuteChanged()
            End If

            If Not IsNothing(envioActual) Then
                reembolsoModificar = envioActual.Reembolso
                retornoModificar = (From l In listaTiposRetorno Where l.id = envioActual.Retorno).FirstOrDefault
                estadoModificar = envioActual.Estado
                listaHistoriaEnvio = New ObservableCollection(Of EnviosHistoria)(From h In DbContext.EnviosHistoria Where h.NumeroEnvio = envioActual.Numero)
            Else
                listaHistoriaEnvio = Nothing
            End If

            If Not IsNothing(cmdModificarEnvio) Then
                cmdModificarEnvio.RaiseCanExecuteChanged()
            End If

            If Not IsNothing(cmdRehusarEnvio) Then
                cmdRehusarEnvio.RaiseCanExecuteChanged()
            End If

            OnPropertyChanged("sePuedeModificarReembolso")
        End Set
    End Property

    Private _listaEnvios As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnvios As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnvios
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            SetProperty(_listaEnvios, value)
        End Set
    End Property

    Private _fechaFiltro As Date
    Public Property fechaFiltro As Date
        Get
            Return _fechaFiltro
        End Get
        Set(value As Date)
            SetProperty(_fechaFiltro, value)
            'actualizamos listaPedidos
            listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
            envioActual = listaEnviosTramitados.FirstOrDefault
        End Set
    End Property

    Private _clienteFiltro As String
    Public Property clienteFiltro As String
        Get
            Return _clienteFiltro
        End Get
        Set(value As String)
            SetProperty(_clienteFiltro, value)
            listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Cliente = clienteFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
            envioActual = listaEnviosTramitados.FirstOrDefault
        End Set
    End Property

    Private _nombreFiltro As String
    Public Property nombreFiltro As String
        Get
            Return _nombreFiltro
        End Get
        Set(value As String)
            SetProperty(_nombreFiltro, value)
            listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Clientes.Nombre.Contains(nombreFiltro) And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
            envioActual = listaEnviosTramitados.FirstOrDefault
        End Set
    End Property



    'Private Property _listaPedidos As ObservableCollection(Of CabPedidoVta)
    'Public Property listaPedidos As ObservableCollection(Of CabPedidoVta)
    '    Get
    '        Return _listaPedidos
    '    End Get
    '    Set(value As ObservableCollection(Of CabPedidoVta))
    '        _listaPedidos = value
    '        OnPropertyChanged("listaPedidos")
    '    End Set
    'End Property

    Private _numeroPedido As String
    Public Property numeroPedido As String
        Get
            Return _numeroPedido
        End Get
        Set(value As String)
            SetProperty(_numeroPedido, value)
            Dim pedidoAnterior As CabPedidoVta
            pedidoAnterior = pedidoSeleccionado
            Dim pedidoNumerico As Integer
            If Integer.TryParse(numeroPedido, pedidoNumerico) Then ' si el pedido es numérico
                'pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = pedidoNumerico And c.Empresa <> EMPRESA_ESPEJO).FirstOrDefault
                pedidoSeleccionado = (From c In DbContext.CabPedidoVta Where c.Número = pedidoNumerico).FirstOrDefault
            Else ' si no es numérico (es una factura, lo tratamos como un cobro)
                pedidoSeleccionado = (From c In DbContext.CabPedidoVta Join l In DbContext.LinPedidoVta On c.Empresa Equals l.Empresa And c.Número Equals l.Número Where l.Nº_Factura = numeroPedido Select c).FirstOrDefault
                If Not IsNothing(pedidoSeleccionado) Then
                    retornoActual = (From s In listaTiposRetorno Where s.id = agenciaEspecifica.retornoSoloCobros).FirstOrDefault
                    servicioActual = (From s In listaServicios Where s.id = agenciaEspecifica.servicioSoloCobros).FirstOrDefault
                    horarioActual = (From s In listaHorarios Where s.id = agenciaEspecifica.horarioSoloCobros).FirstOrDefault
                    bultos = 0
                    SetProperty(_numeroPedido, pedidoSeleccionado.Número.ToString)
                End If
            End If
            If IsNothing(pedidoSeleccionado) OrElse IsNothing(pedidoSeleccionado.Clientes) Then
                If IsNothing(pedidoAnterior) Then
                    SetProperty(_numeroPedido, "36") 'ñapa a arreglar cuando esté inspirado
                Else
                    pedidoSeleccionado = pedidoAnterior
                    SetProperty(_numeroPedido, pedidoAnterior.Número.ToString)
                End If
            End If
            'Dim agenciaNueva As AgenciasTransporte = (From a In DbContext.AgenciasTransporte Where a.Ruta = pedidoSeleccionado.Ruta).FirstOrDefault
            OnPropertyChanged("empresaSeleccionada")
            OnPropertyChanged("agenciaSeleccionada")
        End Set
    End Property

    Private _numeroMultiusuario As Integer
    Public Property numeroMultiusuario As Integer
        Get
            Return _numeroMultiusuario
        End Get
        Set(value As Integer)
            SetProperty(_numeroMultiusuario, value)
            multiusuario = (From m In DbContext.MultiUsuarios Where m.Empresa = empresaSeleccionada.Número And m.Número = numeroMultiusuario).FirstOrDefault
        End Set
    End Property

    Private _multiusuario As MultiUsuarios
    Public Property multiusuario As MultiUsuarios
        Get
            Return _multiusuario
        End Get
        Set(value As MultiUsuarios)
            SetProperty(_multiusuario, value)
        End Set
    End Property

    Private _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            SetProperty(_PestañaSeleccionada, value)
            If PestañaSeleccionada.Name = "tabReembolsos" And IsNothing(listaReembolsos) Then
                listaReembolsos = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Estado = ESTADO_TRAMITADO_ENVIO And e.Reembolso > 0 And e.FechaPagoReembolso Is Nothing)
            End If

            If PestañaSeleccionada.Name = "tabRetornos" And IsNothing(listaRetornos) Then
                listaRetornos = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Agencia = agenciaSeleccionada.Numero And e.Estado >= ESTADO_TRAMITADO_ENVIO And e.Retorno <> agenciaEspecifica.retornoSinRetorno And e.FechaRetornoRecibido Is Nothing Order By e.Fecha)
            End If
        End Set
    End Property

    Private _listaEmpresas As ObservableCollection(Of Empresas)
    Public Property listaEmpresas As ObservableCollection(Of Empresas)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of Empresas))
            SetProperty(_listaEmpresas, value)
        End Set
    End Property

    Private _listaEnviosPedido As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnviosPedido As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnviosPedido
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            SetProperty(_listaEnviosPedido, value)
            If Not IsNothing(cmdCargarEstado) Then
                cmdCargarEstado.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _listaEnviosTramitados As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnviosTramitados As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnviosTramitados
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            SetProperty(_listaEnviosTramitados, value)
            OnPropertyChanged("sePuedeModificarReembolso")
        End Set
    End Property

    Private _XMLdeEstado As XDocument
    Public Property XMLdeEstado As XDocument
        Get
            Return _XMLdeEstado
        End Get
        Set(value As XDocument)
            SetProperty(_XMLdeEstado, value)
        End Set
    End Property

    Private Property _barraProgresoFinal As Integer
    Public Property barraProgresoFinal As Integer
        Get
            Return _barraProgresoFinal
        End Get
        Set(value As Integer)
            SetProperty(_barraProgresoFinal, value)
        End Set
    End Property

    Private Property _barraProgresoActual As Integer
    Public Property barraProgresoActual As Integer
        Get
            Return _barraProgresoActual
        End Get
        Set(value As Integer)
            SetProperty(_barraProgresoActual, value)
        End Set
    End Property

    Private _estadoEnvioCargado As estadoEnvio
    Public Property estadoEnvioCargado As estadoEnvio
        Get
            Return _estadoEnvioCargado
        End Get
        Set(value As estadoEnvio)
            SetProperty(_estadoEnvioCargado, value)
        End Set
    End Property

    Private _listaReembolsos As ObservableCollection(Of EnviosAgencia)
    Public Property listaReembolsos As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaReembolsos
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            SetProperty(_listaReembolsos, value)
            OnPropertyChanged("sumaReembolsos")
        End Set
    End Property

    Private _listaReembolsosSeleccionados As ObservableCollection(Of EnviosAgencia)
    Public Property listaReembolsosSeleccionados As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaReembolsosSeleccionados
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            SetProperty(_listaReembolsosSeleccionados, value)
            OnPropertyChanged("sumaSeleccionadas")
            cmdContabilizarReembolso.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _lineaReembolsoSeleccionado As EnviosAgencia
    Public Property lineaReembolsoSeleccionado As EnviosAgencia
        Get
            Return _lineaReembolsoSeleccionado
        End Get
        Set(value As EnviosAgencia)
            SetProperty(_lineaReembolsoSeleccionado, value)
        End Set
    End Property

    Private _lineaReembolsoContabilizar As EnviosAgencia
    Public Property lineaReembolsoContabilizar As EnviosAgencia
        Get
            Return _lineaReembolsoContabilizar
        End Get
        Set(value As EnviosAgencia)
            SetProperty(_lineaReembolsoContabilizar, value)
        End Set
    End Property

    Private _numClienteContabilizar As String
    Public Property numClienteContabilizar As String
        Get
            Return _numClienteContabilizar
        End Get
        Set(value As String)
            If MODO_CUADRE Then ' No permitimos poner cliente en modo cuadre
                Return
            End If
            SetProperty(_numClienteContabilizar, value)
            cmdContabilizarReembolso.RaiseCanExecuteChanged()
        End Set
    End Property


    Public ReadOnly Property sumaSeleccionadas As Double
        Get
            If Not IsNothing(listaReembolsosSeleccionados) Then
                Return Aggregate r In listaReembolsosSeleccionados Into Sum(r.Reembolso)
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property sumaReembolsos As Double
        Get
            If Not IsNothing(listaReembolsos) Then
                Return Aggregate r In listaReembolsos Into Sum(r.Reembolso)
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property sumaContabilidad As Double
        Get
            If Not IsNothing(agenciaSeleccionada) AndAlso agenciaSeleccionada.Empresa = empresaSeleccionada.Número Then
                Return Aggregate c In DbContext.Contabilidad Where c.Empresa = empresaSeleccionada.Número And c.Nº_Cuenta = agenciaSeleccionada.CuentaReembolsos Into Sum(c.Debe - c.Haber)
            Else
                Return 0
            End If
        End Get
    End Property

    Public ReadOnly Property descuadreContabilidad As Double
        Get
            Return sumaContabilidad - sumaReembolsos
        End Get
    End Property

    Private _digitalizacionActual As digitalizacion
    Public Property digitalizacionActual As digitalizacion
        Get
            Return _digitalizacionActual
        End Get
        Set(value As digitalizacion)
            SetProperty(_digitalizacionActual, value)
            OnPropertyChanged("cmdDescargarImagen")
            If Not IsNothing(cmdDescargarImagen) Then
                cmdDescargarImagen.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _reembolsoModificar As Double
    Public Property reembolsoModificar As Double
        Get
            Return _reembolsoModificar
        End Get
        Set(value As Double)
            SetProperty(_reembolsoModificar, value)
            OnPropertyChanged("envioActual")
        End Set
    End Property

    Private _retornoModificar As tipoIdDescripcion
    Public Property retornoModificar As tipoIdDescripcion
        Get
            Return _retornoModificar
        End Get
        Set(value As tipoIdDescripcion)
            SetProperty(_retornoModificar, value)
            OnPropertyChanged("envioActual")
        End Set
    End Property

    Private _estadoModificar As Integer
    Public Property estadoModificar As Integer
        Get
            Return _estadoModificar
        End Get
        Set(value As Integer)
            SetProperty(_estadoModificar, value)
            OnPropertyChanged("envioActual")
        End Set
    End Property

    Private _observacionesModificacion As String
    Public Property observacionesModificacion As String
        Get
            Return _observacionesModificacion
        End Get
        Set(value As String)
            SetProperty(_observacionesModificacion, value)
        End Set
    End Property

    Public ReadOnly Property sePuedeModificarReembolso As Boolean
        Get
            Return Not IsNothing(envioActual) AndAlso Not IsNothing(listaEnviosTramitados) AndAlso listaEnviosTramitados.Count > 0
        End Get
    End Property

    Private _listaHistoriaEnvio As ObservableCollection(Of EnviosHistoria)
    Public Property listaHistoriaEnvio As ObservableCollection(Of EnviosHistoria)
        Get
            Return _listaHistoriaEnvio
        End Get
        Set(value As ObservableCollection(Of EnviosHistoria))
            SetProperty(_listaHistoriaEnvio, value)
            OnPropertyChanged("mostrarHistoria")
        End Set
    End Property

    Private _mostrarHistoria As Visibility
    Public ReadOnly Property mostrarHistoria As Visibility
        Get
            If (Not IsNothing(listaHistoriaEnvio)) AndAlso listaHistoriaEnvio.Count > 0 Then
                Return Visibility.Visible
            Else
                Return Visibility.Collapsed
            End If
        End Get
    End Property

    Public ReadOnly Property visibilidadSoloImprimir
        Get
            If Not IsNothing(agenciaEspecifica) Then
                Return agenciaEspecifica.visibilidadSoloImprimir
            Else
                Return Visibility.Hidden
            End If
        End Get
    End Property

    'Private _IsActive As Boolean
    'Public Property IsActive As Boolean Implements IActiveAware.IsActive
    '    Get
    '        Return _IsActive
    '    End Get
    '    Set(value As Boolean)
    '        SetProperty(_IsActive, value)
    '    End Set
    'End Property
    'Public Event IsActiveChanged(sender As Object, e As System.EventArgs) Implements IActiveAware.IsActiveChanged

    Private _listaRetornos As ObservableCollection(Of EnviosAgencia)
    Public Property listaRetornos As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaRetornos
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            SetProperty(_listaRetornos, value)
        End Set
    End Property

    Private _lineaRetornoSeleccionado As EnviosAgencia
    Public Property lineaRetornoSeleccionado As EnviosAgencia
        Get
            Return _lineaRetornoSeleccionado
        End Get
        Set(value As EnviosAgencia)
            SetProperty(_lineaRetornoSeleccionado, value)
        End Set
    End Property



#End Region

#Region "Comandos"
    Private _cmdTramitar As ICommand
    Public ReadOnly Property cmdTramitar() As ICommand
        Get
            If _cmdTramitar Is Nothing Then
                _cmdTramitar = New RelayCommand(AddressOf Tramitar, AddressOf CanTramitar)
            End If
            Return _cmdTramitar
        End Get
    End Property
    Private Function CanTramitar(ByVal param As Object) As Boolean
        Return Not IsNothing(envioActual)
    End Function
    Private Sub Tramitar(ByVal param As Object)
        agenciaEspecifica.llamadaWebService(DbContext)
        'mensajeError = "Pedido " + envioActual.Pedido.ToString.Trim + " tramitado correctamente"
    End Sub

    Private _cmdTramitarTodos As ICommand
    Public ReadOnly Property cmdTramitarTodos() As ICommand
        Get
            If _cmdTramitarTodos Is Nothing Then
                _cmdTramitarTodos = New RelayCommand(AddressOf TramitarTodos, AddressOf CanTramitarTodos)
            End If
            Return _cmdTramitarTodos
        End Get
    End Property
    Private Function CanTramitarTodos(ByVal param As Object) As Boolean
        Return Not IsNothing(listaEnvios) AndAlso listaEnvios.Count > 0
    End Function
    Private Sub TramitarTodos(ByVal param As Object)
        barraProgresoActual = 0
        barraProgresoFinal = listaEnvios.Count
        For Each envio In listaEnvios
            barraProgresoActual = barraProgresoActual + 1
            envioActual = envio
            mensajeError = "Tramitando pedido " + envio.Pedido.ToString
            Debug.Print("Tramitando pedido " + envio.Pedido.ToString)
            cmdTramitar.Execute(Nothing)
        Next
    End Sub

    Private _cmdImprimirEtiquetaPedido As ICommand
    Public ReadOnly Property cmdImprimirEtiquetaPedido() As ICommand
        Get
            If _cmdImprimirEtiquetaPedido Is Nothing Then
                _cmdImprimirEtiquetaPedido = New RelayCommand(AddressOf ImprimirEtiquetaPedido, AddressOf canImprimirEtiquetaPedido)
            End If
            Return _cmdImprimirEtiquetaPedido
        End Get
    End Property
    Private Function canImprimirEtiquetaPedido(ByVal param As Object) As Boolean
        Return Not IsNothing(envioActual)
    End Function
    Private Sub ImprimirEtiquetaPedido(ByVal param As Object)
        agenciaEspecifica.imprimirEtiqueta()
    End Sub

    Private _cmdGuardar As ICommand
    Public ReadOnly Property cmdGuardar() As ICommand
        Get
            If _cmdGuardar Is Nothing Then
                _cmdGuardar = New RelayCommand(AddressOf Guardar, AddressOf CanGuardar)
            End If
            Return _cmdGuardar
        End Get
    End Property
    Private Function CanGuardar(ByVal param As Object) As Boolean
        Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        Return changes.Any
    End Function
    Private Sub Guardar(ByVal param As Object)
        Try
            DbContext.SaveChanges()
            mensajeError = ""
        Catch ex As Exception
            mensajeError = ex.InnerException.Message
        End Try
    End Sub

    Private _cmdBorrar As ICommand
    Public ReadOnly Property cmdBorrar() As ICommand
        Get
            If _cmdBorrar Is Nothing Then
                _cmdBorrar = New RelayCommand(AddressOf Borrar, AddressOf canBorrar)
            End If
            Return _cmdBorrar
        End Get
    End Property
    Private Function canBorrar(ByVal param As Object) As Boolean
        Return envioActual IsNot Nothing
    End Function
    Private Sub Borrar(ByVal param As Object)
        DbContext.EnviosAgencia.DeleteObject(envioActual)
        listaEnvios.Remove(envioActual)
        envioActual = listaEnvios.LastOrDefault
    End Sub

    Private _cmdInsertar As ICommand
    Public ReadOnly Property cmdInsertar() As ICommand
        Get
            If _cmdInsertar Is Nothing Then
                _cmdInsertar = New RelayCommand(AddressOf Insertar, AddressOf CanInsertar)
            End If
            Return _cmdInsertar
        End Get
    End Property
    Private Function CanInsertar(ByVal param As Object) As Boolean
        Return Not IsNothing(pedidoSeleccionado) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso pedidoSeleccionado.Empresa <> EMPRESA_ESPEJO
    End Function
    Private Sub Insertar(ByVal param As Object)
        insertarRegistro()
    End Sub

    Private _cmdImprimirEInsertar As ICommand
    Public ReadOnly Property cmdImprimirEInsertar() As ICommand
        Get
            If _cmdImprimirEInsertar Is Nothing Then
                _cmdImprimirEInsertar = New RelayCommand(AddressOf ImprimirEInsertar, AddressOf CanImprimirEInsertar)
            End If
            Return _cmdImprimirEInsertar
        End Get
    End Property
    Private Function CanImprimirEInsertar(ByVal param As Object) As Boolean
        Return CanInsertar(Nothing)
    End Function
    Private Sub ImprimirEInsertar(ByVal param As Object)
        Try
            cmdInsertar.Execute(Nothing)
            cmdImprimirEtiquetaPedido.Execute(Nothing)
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With { _
             .Title = "Error", _
            .Content = ex.Message _
            })
            Return
        End Try
        NotificationRequest.Raise(New Notification() With { _
             .Title = "Envío", _
            .Content = "Envío insertado correctamente e impresa la etiqueta" _
        })
    End Sub


    'Private _cmdImprimirManifiesto As ICommand
    'Public ReadOnly Property cmdImprimirManifiesto() As ICommand
    '    Get
    '        If _cmdImprimirManifiesto Is Nothing Then
    '            _cmdImprimirManifiesto = New RelayCommand(AddressOf ImprimirManifiesto, AddressOf CanImprimirManifiesto)
    '        End If
    '        Return _cmdImprimirManifiesto
    '    End Get
    'End Property
    'Private Function CanImprimirManifiesto(ByVal param As Object) As Boolean
    '    Return True
    'End Function
    'Private Sub ImprimirManifiesto(ByVal param As Object)

    'End Sub


    Private _cmdCargarEstado As DelegateCommand(Of Object)
    Public Property cmdCargarEstado As DelegateCommand(Of Object)
        Get
            Return _cmdCargarEstado
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdCargarEstado = value
        End Set
    End Property
    Private Function CanCargarEstado(arg As Object) As Boolean
        Return Not IsNothing(envioActual) AndAlso Not IsNothing(listaEnviosPedido) AndAlso listaEnviosPedido.Count > 0 'AndAlso Not IsNothing(agenciaEspecifica)
        'Return True
    End Function
    Private Sub OnCargarEstado(arg As Object)
        If IsNothing(envioActual) Then
            mensajeError = "No se puede cargar el estado porque no hay ningún envío seleccionado"
            Return
        End If
        'XMLdeEstado = cargarEstado(envioActual)
        XMLdeEstado = agenciaEspecifica.cargarEstado(envioActual)
        estadoEnvioCargado = agenciaEspecifica.transformarXMLdeEstado(XMLdeEstado)
        mensajeError = "Estado del envío " + envioActual.Numero.ToString + " cargado correctamente"
    End Sub

    Private _cmdAgregarReembolsoContabilizar As DelegateCommand(Of Object)
    Public Property cmdAgregarReembolsoContabilizar As DelegateCommand(Of Object)
        Get
            Return _cmdAgregarReembolsoContabilizar
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdAgregarReembolsoContabilizar = value
        End Set
    End Property
    Private Function CanAgregarReembolsoContabilizar(arg As Object) As Boolean
        Return Not IsNothing(lineaReembolsoSeleccionado)
    End Function
    Private Sub OnAgregarReembolsoContabilizar(arg As Object)
        If Not IsNothing(lineaReembolsoSeleccionado) Then
            listaReembolsosSeleccionados.Add(lineaReembolsoSeleccionado)
            listaReembolsos.Remove(lineaReembolsoSeleccionado)
            OnPropertyChanged("sumaSeleccionadas")
            OnPropertyChanged("sumaReembolsos")
            cmdContabilizarReembolso.RaiseCanExecuteChanged()
        Else
            mensajeError = "No hay ninguna línea seleccionada"
        End If
    End Sub

    Private _cmdQuitarReembolsoContabilizar As DelegateCommand(Of Object)
    Public Property cmdQuitarReembolsoContabilizar As DelegateCommand(Of Object)
        Get
            Return _cmdQuitarReembolsoContabilizar
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdQuitarReembolsoContabilizar = value
        End Set
    End Property
    Private Function CanQuitarReembolsoContabilizar(arg As Object) As Boolean
        Return Not IsNothing(lineaReembolsoContabilizar)
    End Function
    Private Sub OnQuitarReembolsoContabilizar(arg As Object)
        If Not IsNothing(lineaReembolsoContabilizar) Then
            listaReembolsos.Add(lineaReembolsoContabilizar)
            listaReembolsosSeleccionados.Remove(lineaReembolsoContabilizar)
            OnPropertyChanged("sumaSeleccionadas")
            OnPropertyChanged("sumaReembolsos")
            cmdContabilizarReembolso.RaiseCanExecuteChanged()
        Else
            mensajeError = "No hay ninguna línea seleccionada"
        End If
    End Sub

    Private _cmdContabilizarReembolso As DelegateCommand(Of Object)
    Public Property cmdContabilizarReembolso As DelegateCommand(Of Object)
        Get
            Return _cmdContabilizarReembolso
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdContabilizarReembolso = value
        End Set
    End Property
    Private Function CanContabilizarReembolso(arg As Object) As Boolean
        Return (Not IsNothing(numClienteContabilizar) AndAlso numClienteContabilizar.Length > 0 AndAlso Not IsNothing(listaReembolsosSeleccionados) AndAlso listaReembolsosSeleccionados.Count > 0) Or MODO_CUADRE
    End Function
    Private Sub OnContabilizarReembolso(arg As Object)
        Me.ConfirmationRequest.Raise(
            New Confirmation() With {
                .Content = "¿Desea contabilizar?", .Title = "Contabilizar"
            },
            Sub(c)
                InteractionResultMessage = If(c.Confirmed, "OK", "KO")
            End Sub
        )

        If InteractionResultMessage = "KO" Or IsNothing(listaReembolsosSeleccionados) Then
            Return
        End If

        ' Comprobamos si existe el cliente
        Dim cliente As Clientes = (From c In DbContext.Clientes Where c.Empresa = empresaSeleccionada.Número And c.Nº_Cliente = numClienteContabilizar And c.ClientePrincipal = True And c.Estado >= 0).FirstOrDefault
        If IsNothing(cliente) Then
            NotificationRequest.Raise(New Notification() With { _
                .Title = "Error al contabilizar", _
                .Content = "El cliente " + numClienteContabilizar + " no existe en " + empresaSeleccionada.Nombre _
            })
            Return
        End If

        Dim asiento As Integer = 0 'para guardar el asiento que devuelve prdContabilizar

        ' Empezamos una transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()

            If Not MODO_CUADRE Then
                For Each linea In listaReembolsosSeleccionados
                    DbContext.PreContabilidad.AddObject(New PreContabilidad With { _
                        .Empresa = empresaSeleccionada.Número,
                        .Diario = "_PagoReemb",
                        .Asiento = 1,
                        .Fecha = Today,
                        .TipoApunte = "3",
                        .TipoCuenta = "1",
                        .Nº_Cuenta = linea.AgenciasTransporte.CuentaReembolsos,
                        .Concepto = "Pago reembolso " + linea.Cliente,
                        .Haber = linea.Reembolso,
                        .Nº_Documento = linea.AgenciasTransporte.Nombre,
                        .Delegación = "ALG",
                        .FormaVenta = "VAR" _
                    })
                Next
                DbContext.PreContabilidad.AddObject(New PreContabilidad With { _
                        .Empresa = empresaSeleccionada.Número,
                        .Diario = "_PagoReemb",
                        .Asiento = 1,
                        .Fecha = Today,
                        .TipoApunte = "3",
                        .TipoCuenta = "2",
                        .Nº_Cuenta = numClienteContabilizar,
                        .Contacto = "0",
                        .Concepto = "Pago reembolso " + agenciaSeleccionada.Nombre,
                        .Debe = sumaSeleccionadas,
                        .Nº_Documento = agenciaSeleccionada.Nombre,
                        .Delegación = "ALG",
                        .FormaVenta = "VAR",
                        .FormaPago = "CHQ",
                        .Vendedor = "NV" _
                    })
                DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)


                asiento = DbContext.prdContabilizar(empresaSeleccionada.Número, "_PagoReemb")
            End If
            If (asiento > 0 Or MODO_CUADRE) Then
                Dim fechaAFijar As Date = Today
                If MODO_CUADRE Then
                    fechaAFijar = "01/01/2015"
                End If
                For Each linea In listaReembolsosSeleccionados
                    linea.FechaPagoReembolso = fechaAFijar
                Next
                DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)

                OnPropertyChanged("sumaContabilidad")
                OnPropertyChanged("descuadreContabilidad")
                OnPropertyChanged("sumaReembolsos")

                transaction.Complete()
                success = True ' Marcamos correctas las transacciones

                listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia)
            Else
                transaction.Dispose()
                success = False
            End If

            ' Comprobamos que las transacciones sean correctas
            If success Then
                ' Reset the context since the operation succeeded. 

                DbContext.AcceptAllChanges()
                NotificationRequest.Raise(New Notification() With { _
                     .Title = "Contabilizado Correctamente", _
                    .Content = "Nº Asiento: " + asiento.ToString _
                })
            Else
                NotificationRequest.Raise(New Notification() With { _
                     .Title = "¡Error!", _
                    .Content = "Se ha producido un error y no se han grabado los datos" _
                })
            End If
        End Using ' finaliza la transacción
    End Sub

    Private _cmdDescargarImagen As DelegateCommand(Of Object)
    Public Property cmdDescargarImagen As DelegateCommand(Of Object)
        Get
            Return _cmdDescargarImagen
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdDescargarImagen = value
        End Set
    End Property
    Private Function CanDescargarImagen(arg As Object) As Boolean
        Return Not IsNothing(digitalizacionActual)
    End Function
    Private Sub OnDescargarImagen(arg As Object)
        Dim saveDialog As New SaveFileDialog

        saveDialog.Title = "Guardar justificante de entrega"
        saveDialog.Filter = "Imagen (*.jpg)|*.jpg"
        saveDialog.ShowDialog()

        'exit if no file selected
        If saveDialog.FileName = "" Then
            Exit Sub
        End If

        Dim cln As System.Net.WebClient = New System.Net.WebClient
        cln.DownloadFile(digitalizacionActual.urlDigitalizacion, saveDialog.FileName)

        mensajeError = "Guardada en " + saveDialog.FileName
    End Sub

    Private _cmdModificarEnvio As DelegateCommand(Of Object)
    Public Property cmdModificarEnvio As DelegateCommand(Of Object)
        Get
            Return _cmdModificarEnvio
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdModificarEnvio = value
        End Set
    End Property
    Private Function CanModificarEnvio(arg As Object) As Boolean
        Return sePuedeModificarReembolso
    End Function
    Private Sub OnModificarEnvio(arg As Object)
        modificarEnvio(envioActual, reembolsoModificar, retornoModificar, estadoModificar)
    End Sub

    Private _cmdImprimirManifiesto As DelegateCommand(Of Object)
    Public Property cmdImprimirManifiesto As DelegateCommand(Of Object)
        Get
            Return _cmdImprimirManifiesto
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdImprimirManifiesto = value
        End Set
    End Property
    Private Function CanImprimirManifiesto(arg As Object) As Boolean
        Return Not IsNothing(listaEnviosTramitados)
    End Function
    Private Sub OnImprimirManifiesto(arg As Object)
        Dim region As IRegion = regionManager.Regions("MainRegion")
        Dim vista = container.Resolve(Of Object)("frmCRInforme")
        region.Add(vista, "Clientes")
        region.Activate(vista)
        'regionManager.RequestNavigate("MainRegion", New Uri("/Clientes", UriKind.Relative))
    End Sub

    Private _cmdRecibirRetorno As DelegateCommand(Of Object)
    Public Property cmdRecibirRetorno As DelegateCommand(Of Object)
        Get
            Return _cmdRecibirRetorno
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdRecibirRetorno = value
        End Set
    End Property
    Private Function CanRecibirRetorno(arg As Object) As Boolean
        Return Not IsNothing(lineaRetornoSeleccionado)
    End Function
    Private Sub OnRecibirRetorno(arg As Object)
        If Not IsNothing(lineaRetornoSeleccionado) Then
            Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = "¿Confirma que ha recibido el retorno del pedido " + lineaRetornoSeleccionado.Pedido.ToString + "?", .Title = "Retorno"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

            If InteractionResultMessage = "KO" Or IsNothing(lineaRetornoSeleccionado) Then
                Return
            End If
            lineaRetornoSeleccionado.FechaRetornoRecibido = Today
            If DbContext.SaveChanges Then
                mensajeError = "Fecha retorno del cliente " + lineaRetornoSeleccionado.Cliente.Trim + " actualizada correctamente"
                listaRetornos.Remove(lineaRetornoSeleccionado)
            Else
                mensajeError = "Se ha producido un error al actualizar la fecha del retorno"
                NotificationRequest.Raise(New Notification() With { _
                    .Title = "Error", _
                    .Content = "No se ha podido actualizar la fecha del retorno" _
                })
            End If
        Else
            mensajeError = "No hay ninguna línea seleccionada"
        End If
    End Sub

    Private _cmdRehusarEnvio As DelegateCommand(Of Object)
    Public Property cmdRehusarEnvio As DelegateCommand(Of Object)
        Get
            Return _cmdRehusarEnvio
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdRehusarEnvio = value
        End Set
    End Property
    Private Function CanRehusarEnvio(arg As Object) As Boolean
        Return Not IsNothing(envioActual)
    End Function
    Private Sub OnRehusarEnvio(arg As Object)
        Dim tipoRetorno As tipoIdDescripcion = (From l In listaTiposRetorno Where l.id = agenciaEspecifica.retornoSinRetorno).FirstOrDefault

        modificarEnvio(envioActual, 0, tipoRetorno, envioActual.Estado, True)
    End Sub



#End Region

#Region "Funciones de Ayuda"



    Public Function telefonoUnico(listaTelefonos As String, Optional tipo As String = "F") As String
        ' tipo = F -> teléfono fijo
        ' tipo = M -> teléfono móvil

        Dim telefonos() As String
        Dim stringSeparators() As String = {"/"}

        telefonos = listaTelefonos.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries)
        For Each t As String In telefonos
            If (t.Length = 9) And (
                (tipo = "F" And t.Substring(0, 1) = "9") Or
                (tipo = "M" And t.Substring(0, 1) = "6") Or
                (tipo = "M" And t.Substring(0, 1) = "7") Or
                (tipo = "M" And t.Substring(0, 1) = "8")
                ) Then
                Return t
            End If
        Next
        Return ""

    End Function

    Public Function correoUnico(Optional listaPersonas As List(Of PersonasContactoCliente) = Nothing) As String
        Dim correo As String
        Dim personaAgencia As PersonasContactoCliente

        If IsNothing(listaPersonas) Then
            If IsNothing(pedidoSeleccionado.Clientes.PersonasContactoCliente.FirstOrDefault) Then
                Return ""
            Else
                listaPersonas = pedidoSeleccionado.Clientes.PersonasContactoCliente.ToList
            End If
        End If

        If IsNothing(listaPersonas.FirstOrDefault) Then
            Return ""
        End If
        personaAgencia = (From c In listaPersonas Where c.Cargo = CARGO_AGENCIA And c.CorreoElectrónico <> "").FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If correo <> "" Then
                Return correo
            End If
        End If

        personaAgencia = (From c In listaPersonas Where c.CorreoElectrónico <> "").FirstOrDefault
        If Not IsNothing(personaAgencia) AndAlso Not IsNothing(personaAgencia.CorreoElectrónico) Then
            correo = personaAgencia.CorreoElectrónico.Trim
            If correo <> "" Then
                Return correo
            End If
        End If

        If IsNothing(listaPersonas.FirstOrDefault.CorreoElectrónico) Then
            Return ""
        Else
            Return listaPersonas.FirstOrDefault.CorreoElectrónico.Trim
        End If



    End Function

    Public Function importeReembolso() As Decimal
        ' Miramos los casos en los que no hay contra reembolso
        If IsNothing(pedidoSeleccionado) Then
            Return 0
        End If
        If pedidoSeleccionado.CCC IsNot Nothing Then
            Return 0
        End If
        If pedidoSeleccionado.Periodo_Facturacion = "FDM" Then
            Return 0
        End If
        If (pedidoSeleccionado.Forma_Pago = "CNF" Or pedidoSeleccionado.Forma_Pago = "TRN") Then
            Return 0
        End If
        If pedidoSeleccionado.NotaEntrega Then
            Return 0
        End If
        If pedidoSeleccionado.PlazosPago = "PRE" Then
            Return 0
        End If


        If pedidoSeleccionado.MantenerJunto Then
            Dim lineasSinFacturar As ObjectQuery(Of LinPedidoVta)
            lineasSinFacturar = (From l In DbContext.LinPedidoVta Where l.Número = pedidoSeleccionado.Número And l.Estado = ESTADO_LINEA_PENDIENTE)
            If lineasSinFacturar.Any Then
                Return 0
            End If
        End If

        ' Para el resto de los casos ponemos el importe correcto
        Dim lineas As ObjectQuery(Of LinPedidoVta)
        lineas = (From l In DbContext.LinPedidoVta Where l.Número = pedidoSeleccionado.Número And l.Picking <> 0 And l.Estado = ESTADO_SIN_FACTURAR)
        If Not lineas.Any Then
            Return 0
        End If

        Return Math.Round(
            (Aggregate l In lineas _
            Select l.Total Into Sum()) _
            , 2)

    End Function

    Public Function calcularDigitoControl(ByVal number As String) As Integer

        If (number.Length <> 17) Then
            number = ""
            Throw New System.ArgumentException
        Else
            Dim ch As Char
            For Each ch In number
                If (Not Char.IsNumber(ch)) Then
                    number = ""
                    Throw New System.ArgumentException
                End If
            Next
        End If

        Dim x, digito, sumaCod As Integer

        ' Extraigo el valor del dígito, y voy
        ' sumando los valores resultantes.
        For x = 1 To number.Length
            digito = CInt(number.Substring(x - 1, 1))
            If (x Mod 2) <> 0 Then
                ' Las posiciones impares se multiplican por 3
                sumaCod += (digito * 3)
            Else
                ' Las posiciones pares se multiplican por 1
                sumaCod += (digito * 1)
            End If
        Next

        ' Calculo la decena superior
        digito = (sumaCod Mod 10)

        ' Calculo el dígito de control
        If digito <> 0 Then
            digito = 10 - digito
        End If
        ' Devuelvo el dígito de control
        Return digito

    End Function

    Public Sub insertarRegistro()
        envioActual = New EnviosAgencia
        With envioActual
            .Empresa = pedidoSeleccionado.Empresa
            .Agencia = agenciaSeleccionada.Numero
            .Cliente = pedidoSeleccionado.Nº_Cliente
            .Contacto = pedidoSeleccionado.Contacto
            .Pedido = pedidoSeleccionado.Número
            .Fecha = fechaEnvio
            .Servicio = servicioActual.id
            .Horario = horarioActual.id
            .Bultos = bultos
            .Retorno = retornoActual.id
            .Nombre = nombreEnvio
            .Direccion = direccionEnvio
            .CodPostal = codPostalEnvio
            .Poblacion = poblacionEnvio
            .Provincia = provinciaEnvio
            .Telefono = telefonoEnvio
            .Movil = movilEnvio
            .Email = correoEnvio
            .Observaciones = Left(observacionesEnvio, 80)
            .Atencion = attEnvio
            .Reembolso = reembolso
            '.CodigoBarras = calcularCodigoBarras()
            .Vendedor = pedidoSeleccionado.Vendedor
            agenciaEspecifica.calcularPlaza(codPostalEnvio, .Nemonico, .NombrePlaza, .TelefonoPlaza, .EmailPlaza)
        End With

        DbContext.AddToEnviosAgencia(envioActual)
        listaEnvios.Add(envioActual)
        listaEnviosPedido.Add(envioActual)
        DbContext.SaveChanges()
        envioActual.CodigoBarras = agenciaEspecifica.calcularCodigoBarras()
    End Sub

    Public Function contabilizarReembolso(envio As EnviosAgencia) As Integer

        Const diarioReembolsos As String = "_Reembolso"

        If IsNothing(envio.AgenciasTransporte.CuentaReembolsos) Then
            mensajeError = "Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar."
            Return -1
        End If

        Dim lineaInsertar As New PreContabilidad
        Dim asiento As Integer
        Dim movimientoLiq As ExtractoCliente
        movimientoLiq = calcularMovimientoLiq(envio)


        With lineaInsertar
            .Empresa = envio.Empresa.Trim
            .Diario = diarioReembolsos.Trim
            .TipoApunte = "3" 'Pago
            .TipoCuenta = "2" 'Cliente
            .Nº_Cuenta = envio.Cliente.Trim
            .Contacto = envio.Contacto.Trim
            .Fecha = Today 'envio.Fecha
            .FechaVto = Today ' envio.Fecha
            .Haber = envio.Reembolso
            .Concepto = generarConcepto(envio)
            .Contrapartida = envio.AgenciasTransporte.CuentaReembolsos.Trim
            .Asiento_Automático = False
            .FormaPago = envio.Empresas.FormaPagoEfectivo
            .Vendedor = envio.Vendedor
            If IsNothing(movimientoLiq) Then
                .Nº_Documento = envio.Pedido
                .Delegación = envio.Empresas.DelegaciónVarios
                .FormaVenta = envio.Empresas.FormaVentaVarios
            Else
                .Nº_Documento = movimientoLiq.Nº_Documento
                .Liquidado = movimientoLiq.Nº_Orden
                .Delegación = movimientoLiq.Delegación
                .FormaVenta = movimientoLiq.FormaVenta
                .Ruta = movimientoLiq.Ruta
                .Efecto = movimientoLiq.Efecto
            End If
        End With

        ' Iniciamos transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()


            Try
                DbContext.AddToPreContabilidad(lineaInsertar)
                DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                asiento = DbContext.prdContabilizar(envio.Empresa, diarioReembolsos)
                transaction.Complete()
                success = True
            Catch e As Exception
                'DbContext.DeleteObject(lineaInsertar) 'Lo suyo sería hacer una transacción con todo
                'DbContext.SaveChanges()
                transaction.Dispose()
                Return -1
            End Try

            ' Comprobamos que las transacciones sean correctas
            If success Then
                ' Reset the context since the operation succeeded. 
                DbContext.AcceptAllChanges()
            Else
                NotificationRequest.Raise(New Notification() With { _
                     .Title = "¡Error!", _
                    .Content = "Se ha producido un error y no se grabado los datos" _
                })
            End If

        End Using


        Return asiento

    End Function

    Private Function calcularMovimientoLiq(env As EnviosAgencia) As ExtractoCliente
        Return calcularMovimientoLiq(env, env.Reembolso)
    End Function
    Private Function calcularMovimientoLiq(env As EnviosAgencia, reembolsoAnterior As Double) As ExtractoCliente
        Dim movimientos As ObservableCollection(Of ExtractoCliente)
        Dim movimientosConImporte As ObservableCollection(Of ExtractoCliente)

        If reembolsoAnterior > 0 Then
            movimientos = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = env.Empresa And e.Número = env.Cliente And e.ImportePdte > 0 And (e.Estado = "NRM" Or e.Estado Is Nothing))
        Else
            movimientos = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = env.Empresa And e.Número = env.Cliente And e.ImportePdte < 0 And (e.Estado = "NRM" Or e.Estado Is Nothing))
        End If


        If movimientos.Count = 0 Then
            Return Nothing
        ElseIf movimientos.Count = 1 Then
            Return movimientos.SingleOrDefault
        Else
            If reembolsoAnterior > 0 Then
                movimientosConImporte = New ObservableCollection(Of ExtractoCliente)(From m In movimientos Where m.ImportePdte = reembolsoAnterior)
            Else
                movimientosConImporte = New ObservableCollection(Of ExtractoCliente)(From m In movimientos Where m.ImportePdte = env.Reembolso And m.Fecha = Today) ' con env.Fecha hay problemas cuando la etiqueta es del día anterior
            End If

            If movimientosConImporte.Count = 0 Then
                Return movimientos.FirstOrDefault
            Else
                Return movimientosConImporte.FirstOrDefault
            End If
        End If
    End Function

    Private Function calcularMovimientoDesliq(env As EnviosAgencia, importeAnterior As Double) As ExtractoCliente
        Dim movimientos As ObservableCollection(Of ExtractoCliente)
        Dim concepto As String = generarConcepto(env)

        movimientos = New ObservableCollection(Of ExtractoCliente)(From e In DbContext.ExtractoCliente Where e.Empresa = env.Empresa _
                            And e.Número = env.Cliente And e.Contacto = env.Contacto And e.Fecha = env.Fecha And e.TipoApunte = 3 _
                            And e.Concepto = concepto And e.Importe = -importeAnterior)

        If movimientos.Count = 0 Then
            Return Nothing
        Else
            Return movimientos.LastOrDefault
        End If
    End Function

    Private Sub configurarAgenciaPedido(ByRef agenciaConfigurar As AgenciasTransporte)
        If IsNothing(pedidoSeleccionado) Or IsNothing(agenciaConfigurar) Then
            Return
        End If

        Dim agenciaNueva As AgenciasTransporte = (From a In DbContext.AgenciasTransporte Where a.Empresa = pedidoSeleccionado.Empresa And a.Ruta = pedidoSeleccionado.Ruta).FirstOrDefault

        'Carlos 20/03/15. Hay que cambiar esto, que es una chapuza, pero lo pongo para que funcione hoy al menos
        Dim dobleCiclo As Boolean = False
        If IsNothing(agenciaNueva) AndAlso Not IsNothing(pedidoSeleccionado.Ruta) AndAlso pedidoSeleccionado.Ruta.Trim = "OT" Then
            agenciaNueva = (From a In DbContext.AgenciasTransporte Where a.Empresa = pedidoSeleccionado.Empresa And a.Nombre = "OnTime").FirstOrDefault
            dobleCiclo = True
        End If

        If Not IsNothing(agenciaNueva) AndAlso (agenciaConfigurar.Numero <> agenciaNueva.Numero Or agenciaConfigurar.Empresa <> agenciaNueva.Empresa) Then
            'empresaSeleccionada = (From e In DbContext.Empresas Where e.Número = pedidoSeleccionado.Empresa).FirstOrDefault
            empresaSeleccionada = agenciaNueva.Empresas
            agenciaConfigurar = agenciaNueva
            'OnPropertyChanged("agenciaSeleccionada")
        End If

        'Carlos 20/03/15, también hay que cambiarlo
        'If dobleCiclo Then
        '    horarioActual = listaHorarios(1) 'Doble ciclo
        '    OnPropertyChanged("horarioActual")
        'End If
    End Sub

    Private Sub modificarEnvio(ByRef envio As EnviosAgencia, reembolso As Double, retorno As tipoIdDescripcion, estado As Integer)
        modificarEnvio(envio, reembolso, retorno, estado, False)
    End Sub

    Private Sub modificarEnvio(ByRef envio As EnviosAgencia, reembolso As Double, retorno As tipoIdDescripcion, estado As Integer, rehusar As Boolean)
        Dim historia As New EnviosHistoria
        Dim modificado As Boolean = False
        Dim reembolsoAnterior As Double = envio.Reembolso

        If Math.Abs(reembolso) > Math.Abs(envio.Reembolso * 10) Then 'es demasiado grande
            Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = "¿Es correcto el importe de " + reembolso.ToString("C") + "?", .Title = "¡Atención!"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

            If InteractionResultMessage = "KO" Then
                Return
            End If
        End If


        ' Iniciamos transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()


            Try
                If envio.Reembolso <> reembolso Then

                    historia.NumeroEnvio = envio.Numero
                    historia.Campo = "Reembolso"
                    historia.ValorAnterior = envio.Reembolso.ToString("C")
                    'reembolsoAnterior = envio.Reembolso
                    envio.Reembolso = reembolso
                    DbContext.EnviosHistoria.AddObject(historia)
                    modificado = True
                End If
                If envio.Retorno <> retorno.id Then
                    historia.NumeroEnvio = envio.Numero
                    historia.Campo = "Retorno"
                    Dim tipoEnvioAnterior As Byte = envio.Retorno
                    historia.ValorAnterior = (From l In listaTiposRetorno Where l.id = tipoEnvioAnterior Select l.descripcion).FirstOrDefault
                    envio.Retorno = retorno.id
                    DbContext.EnviosHistoria.AddObject(historia)
                    modificado = True
                End If
                If envio.Estado <> estado Then
                    historia.NumeroEnvio = envio.Numero
                    historia.Campo = "Estado"
                    historia.ValorAnterior = envio.Estado
                    envio.Estado = estado
                    DbContext.EnviosHistoria.AddObject(historia)
                    modificado = True
                End If
                If modificado Then
                    historia.Observaciones = observacionesModificacion
                    DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                    If reembolsoAnterior <> reembolso Then
                        contabilizarModificacionReembolso(envio, reembolsoAnterior, reembolso)
                        DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                    End If
                End If

                If rehusar Then
                    Dim movimientoFactura As ExtractoCliente = calcularMovimientoLiq(envio, reembolsoAnterior)
                    Dim estadoRehusado As New ObjectParameter("Estado", GetType(String))
                    estadoRehusado.Value = "RHS"
                    DbContext.prdModificarEfectoCliente(movimientoFactura.Nº_Orden, movimientoFactura.FechaVto, movimientoFactura.CCC, movimientoFactura.Ruta, estadoRehusado, movimientoFactura.Concepto)
                End If

                transaction.Complete()
                success = True
            Catch ex As Exception
                'DbContext.DeleteObject(lineaInsertar) 'Lo suyo sería hacer una transacción con todo
                'DbContext.SaveChanges()
                transaction.Dispose()
                success = False
                listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
                envioActual = listaEnviosTramitados.FirstOrDefault
            End Try

            ' Comprobamos que las transacciones sean correctas
            If success Then
                ' Reset the context since the operation succeeded. 
                DbContext.AcceptAllChanges()
            Else
                NotificationRequest.Raise(New Notification() With { _
                     .Title = "¡Error!", _
                    .Content = "Se ha producido un error y no se grabado los datos" _
                })
            End If

        End Using

    End Sub

    Public Function contabilizarModificacionReembolso(envio As EnviosAgencia, importeAnterior As Double, importeNuevo As Double) As Integer

        ' Parámetro Rehusar es para marcar el ExtractoCliente como RHS (rehusado)

        Const diarioReembolsos As String = "_Reembolso"

        If IsNothing(envio.AgenciasTransporte.CuentaReembolsos) Then
            mensajeError = "Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar."
            Return -1
        End If

        Dim lineaDeshago, lineaRehago As New PreContabilidad
        Dim asiento As Integer
        Dim movimientoLiq As ExtractoCliente
        Dim movimientoDesliq As ExtractoCliente
        'movimientoLiq = calcularMovimientoLiq(envio)

        ' Iniciamos transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()


            Try
                ' desliquidamos el reembolso
                movimientoDesliq = calcularMovimientoDesliq(envio, importeAnterior)
                If Not IsNothing(movimientoDesliq) AndAlso movimientoDesliq.Importe <> movimientoDesliq.ImportePdte Then
                    DbContext.prdDesliquidar(empresaSeleccionada.Número, movimientoDesliq.Nº_Orden)
                End If

                If importeAnterior <> 0 Then
                    With lineaDeshago
                        .Empresa = envio.Empresa.Trim
                        .Diario = diarioReembolsos.Trim
                        .Asiento = 1
                        .TipoApunte = "3" 'Pago
                        .TipoCuenta = "2" 'Cliente
                        .Nº_Cuenta = envio.Cliente.Trim
                        .Contacto = envio.Contacto.Trim
                        .Fecha = Today
                        .FechaVto = Today
                        .Debe = importeAnterior
                        .Concepto = Left("Deshago Reembolso " + envio.Pedido.ToString + " a " + envio.AgenciasTransporte.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
                        .Contrapartida = envio.AgenciasTransporte.CuentaReembolsos.Trim
                        .Asiento_Automático = False
                        .FormaPago = envio.Empresas.FormaPagoEfectivo
                        .Vendedor = envio.Vendedor
                        .Nº_Documento = envio.Pedido
                        .Delegación = envio.Empresas.DelegaciónVarios
                        .FormaVenta = envio.Empresas.FormaVentaVarios
                        If Not IsNothing(movimientoDesliq) Then
                            .Liquidado = movimientoDesliq.Nº_Orden
                        End If
                    End With
                End If

                movimientoLiq = calcularMovimientoLiq(envio)

                If importeNuevo <> 0 Then
                    With lineaRehago
                        .Empresa = envio.Empresa.Trim
                        .Diario = diarioReembolsos.Trim
                        .Asiento = 2
                        .TipoApunte = "3" 'Pago
                        .TipoCuenta = "2" 'Cliente
                        .Nº_Cuenta = envio.Cliente.Trim
                        .Contacto = envio.Contacto.Trim
                        .Fecha = Today
                        .FechaVto = Today
                        .Haber = importeNuevo
                        .Concepto = Left("Rehago Reembolso " + envio.Pedido.ToString + " a " + envio.AgenciasTransporte.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
                        .Contrapartida = envio.AgenciasTransporte.CuentaReembolsos.Trim
                        .Asiento_Automático = False
                        .FormaPago = envio.Empresas.FormaPagoEfectivo
                        .Vendedor = envio.Vendedor

                        .Nº_Documento = envio.Pedido
                        .Delegación = envio.Empresas.DelegaciónVarios
                        .FormaVenta = envio.Empresas.FormaVentaVarios
                        If Not IsNothing(movimientoLiq) Then
                            .Liquidado = movimientoLiq.Nº_Orden
                        End If
                    End With
                End If


                If importeAnterior <> 0 Then
                    DbContext.AddToPreContabilidad(lineaDeshago)
                End If
                If importeNuevo <> 0 Then
                    DbContext.AddToPreContabilidad(lineaRehago)
                End If
                DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                asiento = DbContext.prdContabilizar(envio.Empresa, diarioReembolsos)

                transaction.Complete()
                success = True
            Catch ex As Exception
                'DbContext.DeleteObject(lineaInsertar) 'Lo suyo sería hacer una transacción con todo
                'DbContext.SaveChanges()
                transaction.Dispose()
                success = False
                'listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
                'envioActual = listaEnviosTramitados.FirstOrDefault
            End Try

            ' Comprobamos que las transacciones sean correctas
            If success Then
                ' Reset the context since the operation succeeded. 
                DbContext.AcceptAllChanges()
            Else
                NotificationRequest.Raise(New Notification() With { _
                     .Title = "¡Error!", _
                    .Content = "Se ha producido un error y no se grabado los datos" _
                })
                Return -1
            End If

        End Using

        Return asiento
    End Function

    Private Function generarConcepto(envio As EnviosAgencia) As String
        Return Left("S/Pago pedido " + envio.Pedido.ToString + " a " + envio.AgenciasTransporte.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
    End Function

#End Region




End Class

Public Structure tipoIdDescripcion
    Public Sub New( _
   ByVal _id As Byte,
   ByVal _descripcion As String
   )
        id = _id
        descripcion = _descripcion
    End Sub
    Property id As Byte
    Property descripcion As String
End Structure

#Region "ClasesAuxiliares"

Public Class estadoEnvio
    Inherits BindableBase

    Private _listaExpediciones As New ObservableCollection(Of expedicion)
    Public Property listaExpediciones As ObservableCollection(Of expedicion)
        Get
            Return _listaExpediciones
        End Get
        Set(value As ObservableCollection(Of expedicion))
            SetProperty(_listaExpediciones, value)
            'OnPropertyChanged("listaExpediciones")
        End Set
    End Property

    Private _listaDigitalizaciones As New ObservableCollection(Of digitalizacion)
    Public Property listaDigitalizaciones As ObservableCollection(Of digitalizacion)
        Get
            Return _listaDigitalizaciones
        End Get
        Set(value As ObservableCollection(Of digitalizacion))
            SetProperty(_listaDigitalizaciones, value)
            'OnPropertyChanged("listaDigitalizaciones")
        End Set
    End Property

End Class

Public Class tracking
    Inherits BindableBase
    Private _estadoTracking As String
    Public Property estadoTracking As String
        Get
            Return _estadoTracking
        End Get
        Set(value As String)
            SetProperty(_estadoTracking, value)
            'OnPropertyChanged("estadoTracking")
        End Set
    End Property

    Private _fechaTracking As DateTime
    Public Property fechaTracking As DateTime
        Get
            Return _fechaTracking
        End Get
        Set(value As DateTime)
            SetProperty(_fechaTracking, value)
            'OnPropertyChanged("fechaTracking")
        End Set
    End Property


End Class

Public Class digitalizacion
    Inherits BindableBase

    Private _tipo As String
    Public Property tipo As String
        Get
            Return _tipo
        End Get
        Set(value As String)
            SetProperty(_tipo, value)
        End Set
    End Property

    Private _urlDigitalizacion As Uri
    Public Property urlDigitalizacion As Uri
        Get
            Return _urlDigitalizacion
        End Get
        Set(value As Uri)
            SetProperty(_urlDigitalizacion, value)
        End Set
    End Property
End Class

Public Class expedicion
    Inherits BindableBase
    Private _numeroExpedicion As String
    Public Property numeroExpedicion As String
        Get
            Return _numeroExpedicion
        End Get
        Set(value As String)
            SetProperty(_numeroExpedicion, value)
            'OnPropertyChanged("numeroExpedicion")
        End Set
    End Property

    Private _fecha As Date
    Public Property fecha As Date
        Get
            Return _fecha
        End Get
        Set(value As Date)
            SetProperty(_fecha, value)
            'OnPropertyChanged("fecha")
        End Set
    End Property

    Private _fechaEstimada As Date
    Public Property fechaEstimada As Date
        Get
            Return _fechaEstimada
        End Get
        Set(value As Date)
            SetProperty(_fechaEstimada, value)
            'OnPropertyChanged("fechaEstimada")
        End Set
    End Property

    Private _listaTracking As New ObservableCollection(Of tracking)
    Public Property listaTracking As ObservableCollection(Of tracking)
        Get
            Return _listaTracking
        End Get
        Set(value As ObservableCollection(Of tracking))
            SetProperty(_listaTracking, value)
            'OnPropertyChanged("listaTracking")
        End Set
    End Property

End Class

' The RequestState class passes data across async calls.
'Public Class RequestState

'    Public RequestData As New StringBuilder("")
'    Public BufferRead(1024) As Byte
'    Public Request As HttpWebRequest
'    Public ResponseStream As Stream
'    ' Create Decoder for appropriate encoding type.
'    Public StreamDecode As Decoder = Encoding.UTF8.GetDecoder()

'    Public Sub New()
'        Request = Nothing
'        ResponseStream = Nothing
'    End Sub
'End Class




#End Region

Public Interface IAgencia
    Function cargarEstado(envio As EnviosAgencia) As XDocument
    Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio
    'Function calcularMensajeError(numeroError As Integer) As String
    Function calcularCodigoBarras() As String
    Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String)
    'Function construirXMLdeSalida() As XDocument
    Sub llamadaWebService(DBContext As NestoEntities)
    Sub imprimirEtiqueta()
    ReadOnly Property visibilidadSoloImprimir As Visibility
    ReadOnly Property retornoSoloCobros As Integer
    ReadOnly Property servicioSoloCobros As Integer
    ReadOnly Property horarioSoloCobros As Integer
    ReadOnly Property retornoSinRetorno As Integer ' Especifica la forma de retorno = NO, es decir, cuando no debe mostrarse en la lista de retornos pendientes
End Interface

Public Class AgenciaASM
    Implements IAgencia

    ' Propiedades de Prism
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    'Private agenciaSeleccionada As AgenciasTransporte
    Private agenciaVM As AgenciasViewModel

    Public Sub New(agencia As AgenciasViewModel)
        If Not IsNothing(agencia) Then

            NotificationRequest = New InteractionRequest(Of INotification)
            'ConfirmationRequest = New InteractionRequest(Of IConfirmation)

            agencia.listaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion)
            agencia.retornoActual = New tipoIdDescripcion(0, "Sin Retorno")
            agencia.listaTiposRetorno.Add(agencia.retornoActual)
            agencia.listaTiposRetorno.Add(New tipoIdDescripcion(1, "Con Retorno"))
            agencia.listaTiposRetorno.Add(New tipoIdDescripcion(2, "Retorno Opcional"))
            agencia.listaServicios = New ObservableCollection(Of tipoIdDescripcion)
            agencia.servicioActual = New tipoIdDescripcion(1, "Courier")
            agencia.listaServicios.Add(agencia.servicioActual)
            agencia.listaServicios.Add(New tipoIdDescripcion(37, "Economy"))
            agencia.listaServicios.Add(New tipoIdDescripcion(54, "EuroEstándar"))
            agencia.listaHorarios = New ObservableCollection(Of tipoIdDescripcion)
            agencia.horarioActual = New tipoIdDescripcion(3, "ASM24")
            agencia.listaHorarios.Add(agencia.horarioActual)
            agencia.listaHorarios.Add(New tipoIdDescripcion(2, "ASM14"))
            agencia.listaHorarios.Add(New tipoIdDescripcion(18, "Economy"))

            'agenciaSeleccionada = agencia.agenciaSeleccionada
            agenciaVM = agencia
        End If



    End Sub

    ' Funciones
    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado
        If IsNothing(envio) Then
            NotificationRequest.Raise(New Notification() With { _
                 .Title = "Error", _
                .Content = "No hay ningún envío seleccionado, no se puede cargar el estado" _
            })
            Return Nothing
        End If
        Dim myUri As New Uri("http://www.asmred.com/WebSrvs/MiraEnvios.asmx/GetExpCli?codigo=" + envio.CodigoBarras + "&uid=" + agenciaVM.agenciaSeleccionada.Identificador)
        If myUri.Scheme = Uri.UriSchemeHttp Then
            'Dim myRequest As HttpWebRequest = HttpWebRequest.Create(myUri)
            Dim myRequest As HttpWebRequest = CType(WebRequest.Create(myUri), HttpWebRequest)
            myRequest.Method = WebRequestMethods.Http.Get

            Dim myResponse As HttpWebResponse = myRequest.GetResponse()
            If myResponse.StatusCode = HttpStatusCode.OK Then

                Dim reader As New StreamReader(myResponse.GetResponseStream())
                Dim responseData As String = reader.ReadToEnd()
                myResponse.Close()
                Return XDocument.Parse(responseData)
            End If
        End If
        Return Nothing
    End Function
    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Dim estado As New estadoEnvio
        Dim expedicion As New expedicion
        Dim trackinglistxml As XElement
        Dim tracking As tracking
        Dim digitalizacionesxml As XElement
        Dim digitalizacion As digitalizacion

        If IsNothing(envio) Then
            Return Nothing
        End If

        For Each nodo In envio.Root.Descendants("exp")
            expedicion.numeroExpedicion = nodo.Descendants("expedicion").FirstOrDefault.Value
            expedicion.fecha = nodo.Descendants("fecha").FirstOrDefault.Value
            expedicion.fechaEstimada = nodo.Descendants("FPEntrega").FirstOrDefault.Value

            trackinglistxml = nodo.Descendants("tracking_list").FirstOrDefault
            For Each track In trackinglistxml.Descendants("tracking")
                tracking = New tracking
                tracking.estadoTracking = track.Descendants("evento").FirstOrDefault.Value
                tracking.fechaTracking = track.Descendants("fecha").FirstOrDefault.Value
                expedicion.listaTracking.Add(tracking)
                tracking = Nothing
            Next

            digitalizacionesxml = nodo.Descendants("digitalizaciones").FirstOrDefault
            For Each dig In digitalizacionesxml.Descendants("digitalizacion")
                digitalizacion = New digitalizacion
                digitalizacion.tipo = dig.Descendants("tipo").FirstOrDefault.Value
                digitalizacion.urlDigitalizacion = New Uri(dig.Descendants("imagen").FirstOrDefault.Value)
                estado.listaDigitalizaciones.Add(digitalizacion)
                digitalizacion = Nothing
            Next
        Next
        agenciaVM.digitalizacionActual = estado.listaDigitalizaciones.LastOrDefault
        estado.listaExpediciones.Add(expedicion)
        agenciaVM.cmdDescargarImagen.RaiseCanExecuteChanged()
        Return estado
    End Function
    Private Function calcularMensajeError(numeroError As Integer) As String 'Implements IAgencia.calcularMensajeError
        Select Case numeroError
            Case -33
                Return "Ya existe el código de barras de la expedición"
            Case -69
                Return "No se pudo canalizar el envío"
            Case -70
                Return "Ya existe se ha enviado este pedido para esta fecha y cliente"
            Case -108
                Return "El nombre del remitente debe tener al menos tres caracteres"
            Case -109
                Return "La dirección del remitente debe tener al menos tres caracteres"
            Case -110
                Return "La población del remitente debe tener al menos tres caracteres"
            Case -111
                Return "El código postal del remitente debe tener al menos cuatro caracteres"
            Case -111
                Return "La referencia del cliente está duplicada"
            Case -119
                Return "Error no controlado por el webservice de la agencia"
            Case -128
                Return "El nombre del destinatario debe tener al menos tres caracteres"
            Case -129
                Return "La dirección del destinatario debe tener al menos tres caracteres"
            Case -130
                Return "La población del destinatario debe tener al menos tres caracteres"
            Case -131
                Return "El código postal del destinatario debe tener al menos cuatro caracteres"
            Case Else
                Return "El código de error " + numeroError + " no está controlado por Nesto"
        End Select
    End Function
    Public Function calcularCodigoBarras() As String Implements IAgencia.calcularCodigoBarras
        Return agenciaVM.agenciaSeleccionada.PrefijoCodigoBarras.ToString + agenciaVM.envioActual.Numero.ToString("D7")
    End Function
    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" & _
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " & _
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " & _
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" & _
              "<soap:Body>" & _
                    "<GetPlazaXCP xmlns=""http://www.asmred.com/"">" & _
                        "<codPais>" + AgenciasViewModel.COD_PAIS + "</codPais>" & _
                        "<cp>" + codPostal + "</cp>" & _
                    "</GetPlazaXCP>" & _
                "</soap:Body>" & _
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GetPlazaXCP")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GetPlazaXCP""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Using stm As Stream = req.GetRequestStream()
            Using stmw As StreamWriter = New StreamWriter(stm)
                stmw.Write(soap)
            End Using
        End Using

        Dim response As WebResponse = req.GetResponse()
        Dim responseStream As New StreamReader(response.GetResponseStream())
        soap = responseStream.ReadToEnd

        Dim respuestaXML As XDocument
        respuestaXML = XDocument.Parse(soap)

        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = respuestaXML.Descendants(Xns + "GetPlazaXCPResult").First().FirstNode
        respuestaXML = New XDocument
        respuestaXML.AddFirst(elementoXML)

        'Debug.Print(respuestaXML.ToString)
        If Not IsNothing(elementoXML.Element("Nemonico")) Then
            nemonico = elementoXML.Element("Nemonico").Value
            nombrePlaza = elementoXML.Element("Nombre").Value
            telefonoPlaza = elementoXML.Element("Telefono").Value
            telefonoPlaza = Regex.Replace(telefonoPlaza, "([^0-9])", "")
            'telefonoPlaza = elementoXML.Element("Telefono").Value.Replace(" "c, String.Empty)
            emailPlaza = elementoXML.Element("Mail").Value
        End If
    End Sub
    Private Function construirXMLdeSalida() As XDocument 'Implements IAgencia.construirXMLdeSalida
        Dim xml As New XDocument
        'xml = XDocument.Load("C:\Users\Carlos.NUEVAVISION\Desktop\ASM\webservice\XML-IN-B.xml")
        'xml.Descendants("Servicios").FirstOrDefault().Add(New XElement("Servicios", New XAttribute("uidcliente", ""), New XAttribute("xmlns", "http://www.asmred.com/")))

        ' Si no hay envioActual devolvemos el xml vacío
        If IsNothing(agenciaVM.envioActual) Then
            Return xml
        End If

        'Añadimos el nodo raíz (Servicios)
        xml.AddFirst(
            <Servicios uidcliente=<%= agenciaVM.envioActual.AgenciasTransporte.Identificador %> xmlns="http://www.asmred.com/">
                <Envio codbarras=<%= agenciaVM.envioActual.CodigoBarras %>>
                    <Fecha><%= agenciaVM.envioActual.Fecha.ToShortDateString %></Fecha>
                    <Portes>P</Portes>
                    <Servicio><%= agenciaVM.envioActual.Servicio %></Servicio>
                    <Horario><%= agenciaVM.envioActual.Horario %></Horario>
                    <Bultos><%= agenciaVM.envioActual.Bultos %></Bultos>
                    <Peso>1</Peso>
                    <Retorno><%= agenciaVM.envioActual.Retorno %></Retorno>
                    <Pod>N</Pod>
                    <Remite>
                        <Plaza></Plaza>
                        <Nombre><%= agenciaVM.envioActual.Empresas.Nombre.Trim %></Nombre>
                        <Direccion><%= agenciaVM.envioActual.Empresas.Dirección.Trim %></Direccion>
                        <Poblacion><%= agenciaVM.envioActual.Empresas.Población.Trim %></Poblacion>
                        <Provincia><%= agenciaVM.envioActual.Empresas.Provincia.Trim %></Provincia>
                        <Pais>34</Pais>
                        <CP><%= agenciaVM.envioActual.Empresas.CodPostal.Trim %></CP>
                        <Telefono><%= agenciaVM.envioActual.Empresas.Teléfono.Trim %></Telefono>
                        <Movil></Movil>
                        <Email><%= agenciaVM.envioActual.Empresas.Email.Trim %></Email>
                        <Observaciones></Observaciones>
                    </Remite>
                    <Destinatario>
                        <Codigo></Codigo>
                        <Plaza></Plaza>
                        <Nombre><%= agenciaVM.envioActual.Nombre.Normalize %></Nombre>
                        <Direccion><%= agenciaVM.envioActual.Direccion %></Direccion>
                        <Poblacion><%= agenciaVM.envioActual.Poblacion %></Poblacion>
                        <Provincia><%= agenciaVM.envioActual.Provincia %></Provincia>
                        <Pais><%= AgenciasViewModel.COD_PAIS %></Pais>
                        <CP><%= agenciaVM.envioActual.CodPostal %></CP>
                        <Telefono><%= agenciaVM.envioActual.Telefono %></Telefono>
                        <Movil><%= agenciaVM.envioActual.Movil %></Movil>
                        <Email><%= agenciaVM.envioActual.Email %></Email>
                        <Observaciones><%= agenciaVM.envioActual.Observaciones %></Observaciones>
                        <ATT><%= agenciaVM.envioActual.Atencion %></ATT>
                    </Destinatario>
                    <Referencias><!-- cualquier numero, siempre distinto a cada prueba-->
                        <Referencia tipo="C"><%= agenciaVM.envioActual.Cliente.Trim %>/<%= agenciaVM.envioActual.Pedido %></Referencia>
                    </Referencias>
                    <Importes>
                        <Debidos>0</Debidos>
                        <Reembolso><%= agenciaVM.envioActual.Reembolso %></Reembolso>
                    </Importes>
                    <Seguro tipo="">
                        <Descripcion></Descripcion>
                        <Importe></Importe>
                    </Seguro>
                    <DevuelveAdicionales>
                        <PlazaDestino/>
                    </DevuelveAdicionales>
                </Envio>
            </Servicios>
        )

        'xml.Root.Attribute("xmlns").Value = "http://www.asmred.com/"
        'Debug.Print(xml.ToString)
        Return xml
    End Function
    Public Sub llamadaWebService(DbContext As NestoEntities) Implements IAgencia.llamadaWebService
        agenciaVM.XMLdeSalida = construirXMLdeSalida()
        'If IsNothing(envioActual.Agencia) Then
        '    resultadoWebservice = "No se pudo llamar al webservice (no hay ninguna agencia seleccionada)."
        'Else
        '    resultadoWebservice = "0"
        'End If

        'If resultadoWebservice <> "0" Then
        '    Return
        'End If

        'Comenzamos la llamada
        Dim soap As String = "<?xml version=""1.0"" encoding=""utf-8""?>" & _
             "<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" " & _
              "xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" " & _
              "xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">" & _
              "<soap:Body>" & _
                    "<GrabaServicios xmlns=""http://www.asmred.com/"">" & _
                        "<docIn>" & agenciaVM.XMLdeSalida.ToString & "</docIn>" & _
                    "</GrabaServicios>" & _
                "</soap:Body>" & _
             "</soap:Envelope>"

        Dim req As HttpWebRequest = WebRequest.Create("http://www.asmred.com/WebSrvs/b2b.asmx?op=GrabaServicios")
        req.Headers.Add("SOAPAction", """http://www.asmred.com/GrabaServicios""")
        req.ContentType = "text/xml; charset=""utf-8"""
        req.Accept = "text/xml"
        req.Method = "POST"

        Try
            Using stm As Stream = req.GetRequestStream()
                Using stmw As StreamWriter = New StreamWriter(stm)
                    stmw.Write(soap)
                End Using
            End Using

            Dim response As WebResponse = req.GetResponse()
            Dim responseStream As New StreamReader(response.GetResponseStream())
            soap = responseStream.ReadToEnd
            agenciaVM.XMLdeEntrada = XDocument.Parse(soap)
        Catch ex As Exception
            agenciaVM.mensajeError = "El servidor de la agencia no está respondiendo"
            Return
        End Try


        Dim elementoXML As XElement
        Dim Xns As XNamespace = XNamespace.Get("http://www.asmred.com/")
        elementoXML = agenciaVM.XMLdeEntrada.Descendants(Xns + "GrabaServiciosResult").First().FirstNode
        agenciaVM.XMLdeEntrada = New XDocument
        agenciaVM.XMLdeEntrada.AddFirst(elementoXML)

        If elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value <> "0" Then
            If elementoXML.Element("Envio").Element("Errores").HasElements Then
                agenciaVM.mensajeError = elementoXML.Element("Envio").Element("Errores").Element("Error").Value
            Else
                agenciaVM.mensajeError = calcularMensajeError(elementoXML.Element("Envio").Element("Resultado").Attribute("return").Value)
            End If

        Else
            'lo que tenemos que hacer es cambiar el estado del envío: cambiarEstadoEnvio(1)
            agenciaVM.envioActual.Estado = AgenciasViewModel.ESTADO_TRAMITADO_ENVIO 'Enviado
            DbContext.SaveChanges()
            If agenciaVM.envioActual.Reembolso > 0 Then
                agenciaVM.contabilizarReembolso(agenciaVM.envioActual)
            End If
            agenciaVM.mensajeError = "Envío del pedido " + agenciaVM.envioActual.Pedido.ToString + " tramitado correctamente."
            agenciaVM.listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = agenciaVM.empresaSeleccionada.Número And e.Agencia = agenciaVM.agenciaSeleccionada.Numero And e.Estado = AgenciasViewModel.ESTADO_INICIAL_ENVIO Order By e.Numero)
            agenciaVM.envioActual = agenciaVM.listaEnvios.LastOrDefault ' lo pongo para que no se vaya al último
        End If


        'Debug.Print(XMLdeEntrada.ToString)

    End Sub
    Public Sub imprimirEtiqueta() Implements IAgencia.imprimirEtiqueta
        Dim mainModel As New Nesto.Models.MainModel
        Dim puerto As String = mainModel.leerParametro(agenciaVM.envioActual.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer



        Try
            For i = 1 To agenciaVM.bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A40,10,0,4,1,1,N,""" + agenciaVM.envioActual.Nombre + """")
                objStream.Writeline("A40,50,0,4,1,1,N,""" + agenciaVM.envioActual.Direccion + """")
                objStream.Writeline("A40,90,0,4,1,1,N,""" + agenciaVM.envioActual.CodPostal + " " + agenciaVM.envioActual.Poblacion + """")
                objStream.Writeline("A40,130,0,4,1,1,N,""" + agenciaVM.envioActual.Provincia + """")
                objStream.Writeline("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + agenciaVM.bultos.ToString _
                                    + ". Cliente: " + agenciaVM.envioActual.Cliente.Trim + ". Fecha: " + agenciaVM.envioActual.Fecha + """")
                objStream.Writeline("B40,210,0,2C,4,8,200,B,""" + agenciaVM.envioActual.CodigoBarras + i.ToString("D3") + """")
                objStream.Writeline("A40,450,0,4,1,2,N,""" + agenciaVM.envioActual.Nemonico + " " + agenciaVM.envioActual.NombrePlaza + """")
                objStream.Writeline("A40,510,0,4,1,2,N,""" + agenciaVM.listaHorarios.Where(Function(x) x.id = agenciaVM.envioActual.Horario).FirstOrDefault.descripcion + """")
                objStream.Writeline("A590,265,0,5,2,2,N,""" + agenciaVM.envioActual.Nemonico + """")
                objStream.Writeline("P1")
                objStream.Writeline("")
            Next

            '' Insertamos la etiqueta en la tabla
            'Dim etiqueta As New EtiquetasPicking With { _
            '.Empresa = envioActual.Empresa,
            '.Número = envioActual.Pedido,
            '.Picking = envioActual.Empresas.MaxPickingListado, 'esto está mal
            '.NºBultos = envioActual.Bultos,
            '.UsuarioQuePrepara = multiusuario.Número,
            '.NºCliente = envioActual.Cliente,
            '.Contacto = envioActual.Contacto}
            'DbContext.AddToEtiquetasPicking(etiqueta)
            'DbContext.SaveChanges()


        Catch ex As Exception
            agenciaVM.mensajeError = ex.InnerException.Message
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try
    End Sub
    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Hidden
        End Get
    End Property
    Public ReadOnly Property retornoSoloCobros As Integer Implements IAgencia.retornoSoloCobros
        Get
            Return 0 ' Sin retorno
        End Get
    End Property
    Public ReadOnly Property servicioSoloCobros As Integer Implements IAgencia.servicioSoloCobros
        Get
            Return 1 ' Courier
        End Get
    End Property
    Public ReadOnly Property horarioSoloCobros As Integer Implements IAgencia.horarioSoloCobros
        Get
            Return 3 ' ASM24
        End Get
    End Property
    Public ReadOnly Property retornoSinRetorno As Integer Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' Sin Retorno
        End Get
    End Property
End Class
Public Class AgenciaOnTime
    Implements IAgencia


    ' Propiedades de Prism
    Private _NotificationRequest As InteractionRequest(Of INotification)
    Public Property NotificationRequest As InteractionRequest(Of INotification)
        Get
            Return _NotificationRequest
        End Get
        Private Set(value As InteractionRequest(Of INotification))
            _NotificationRequest = value
        End Set
    End Property

    'Private agenciaSeleccionada As AgenciasTransporte
    Private agenciaVM As AgenciasViewModel

    Public Sub New(agencia As AgenciasViewModel)
        If Not IsNothing(agencia) Then

            NotificationRequest = New InteractionRequest(Of INotification)
            'ConfirmationRequest = New InteractionRequest(Of IConfirmation)

            agencia.listaTiposRetorno = New ObservableCollection(Of tipoIdDescripcion)
            agencia.retornoActual = New tipoIdDescripcion(0, "NO")
            agencia.listaTiposRetorno.Add(agencia.retornoActual)
            agencia.listaTiposRetorno.Add(New tipoIdDescripcion(1, "SI"))
            'agencia.listaTiposRetorno.Add(New tipoIdDescripcion(2, "Retorno Opcional"))
            agencia.listaServicios = New ObservableCollection(Of tipoIdDescripcion)
            agencia.servicioActual = New tipoIdDescripcion(1, "Normal")
            agencia.listaServicios.Add(agencia.servicioActual)
            'agencia.listaServicios.Add(New tipoIdDescripcion(37, "Economy"))
            'agencia.listaServicios.Add(New tipoIdDescripcion(54, "EuroEstándar"))
            agencia.listaHorarios = New ObservableCollection(Of tipoIdDescripcion)
            agencia.horarioActual = New tipoIdDescripcion(0, "")
            agencia.listaHorarios.Add(agencia.horarioActual)
            agencia.listaHorarios.Add(New tipoIdDescripcion(1, "Doble ciclo"))
            agencia.listaHorarios.Add(New tipoIdDescripcion(2, "14 Horas"))
            'agencia.listaHorarios.Add(New tipoIdDescripcion(18, "Economy"))

            'agenciaSeleccionada = agencia.agenciaSeleccionada
            agenciaVM = agencia
        End If



    End Sub

    ' Funciones
    Public Function cargarEstado(envio As EnviosAgencia) As XDocument Implements IAgencia.cargarEstado

        NotificationRequest.Raise(New Notification() With { _
             .Title = "Error", _
            .Content = "OnTime no permite integración. Consulte el estado en la página web de OnTime." _
        })
        Return Nothing

    End Function
    Public Function transformarXMLdeEstado(envio As XDocument) As estadoEnvio Implements IAgencia.transformarXMLdeEstado
        Dim estado As New estadoEnvio
        Dim expedicion As New expedicion
        'Dim trackinglistxml As XElement
        'Dim tracking As tracking
        'Dim digitalizacionesxml As XElement
        'Dim digitalizacion As digitalizacion

        If IsNothing(envio) Then
            Return Nothing
        End If

        Return estado
    End Function
    Public Function calcularCodigoBarras() As String Implements IAgencia.calcularCodigoBarras
        Return agenciaVM.envioActual.Numero.ToString("D7")
    End Function
    Public Sub calcularPlaza(ByVal codPostal As String, ByRef nemonico As String, ByRef nombrePlaza As String, ByRef telefonoPlaza As String, ByRef emailPlaza As String) Implements IAgencia.calcularPlaza
        nemonico = "OT"
        nombrePlaza = "OnTime"
        telefonoPlaza = "902112820"
        emailPlaza = "traficodistribucion@ontimelogistica.com"
    End Sub
    Public Sub llamadaWebService(DbContext As NestoEntities) Implements IAgencia.llamadaWebService

        ' Código puesto como asíncrono el 01/06/15
        agenciaVM.envioActual.Estado = AgenciasViewModel.ESTADO_TRAMITADO_ENVIO 'Enviado
        DbContext.SaveChanges()

        'Await cambiarEstadoAsync(agenciaVM.envioActual)

        If agenciaVM.envioActual.Reembolso > 0 Then
            agenciaVM.contabilizarReembolso(agenciaVM.envioActual)
        End If


        agenciaVM.mensajeError = "Envío del pedido " + agenciaVM.envioActual.Pedido.ToString + " tramitado correctamente."
        agenciaVM.listaEnvios = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia Where e.Empresa = agenciaVM.empresaSeleccionada.Número And e.Agencia = agenciaVM.agenciaSeleccionada.Numero And e.Estado = AgenciasViewModel.ESTADO_INICIAL_ENVIO Order By e.Numero)
        agenciaVM.envioActual = agenciaVM.listaEnvios.LastOrDefault ' lo pongo para que no se vaya al último
    End Sub
    Public Sub imprimirEtiqueta() Implements IAgencia.imprimirEtiqueta
        Dim mainModel As New Nesto.Models.MainModel
        Dim puerto As String = mainModel.leerParametro(agenciaVM.envioActual.Empresa, "ImpresoraBolsas")

        Dim objFSO
        Dim objStream
        objFSO = CreateObject("Scripting.FileSystemObject")
        objStream = objFSO.CreateTextFile(puerto) 'Puerto al cual se envía la impresión  
        Dim i As Integer



        Try
            For i = 1 To agenciaVM.bultos
                objStream.Writeline("I8,A,034")
                objStream.Writeline("N")
                objStream.Writeline("A40,10,0,4,1,1,N,""" + agenciaVM.envioActual.Nombre + """")
                objStream.Writeline("A40,50,0,4,1,1,N,""" + agenciaVM.envioActual.Direccion + """")
                objStream.Writeline("A40,90,0,4,1,1,N,""" + agenciaVM.envioActual.CodPostal + " " + agenciaVM.envioActual.Poblacion + """")
                objStream.Writeline("A40,130,0,4,1,1,N,""" + agenciaVM.envioActual.Provincia + """")
                objStream.Writeline("A40,170,0,4,1,1,N,""Bulto: " + i.ToString + "/" + agenciaVM.bultos.ToString _
                                    + ". Cliente: " + agenciaVM.envioActual.Cliente.Trim + ". Fecha: " + agenciaVM.envioActual.Fecha + """")
                'objStream.Writeline("B40,210,0,2C,4,8,200,B,""" + agenciaVM.envioActual.CodigoBarras + i.ToString("D3") + """")
                objStream.Writeline("A40,450,0,4,1,2,N,""" + agenciaVM.envioActual.Nemonico + " " + agenciaVM.envioActual.NombrePlaza + """")
                objStream.Writeline("A40,510,0,4,1,2,N,""" + agenciaVM.listaHorarios.Where(Function(x) x.id = agenciaVM.envioActual.Horario).FirstOrDefault.descripcion + """")
                objStream.Writeline("A590,265,0,5,2,2,N,""" + agenciaVM.envioActual.Nemonico + """")
                objStream.Writeline("P1")
                objStream.Writeline("")
            Next



        Catch ex As Exception
            agenciaVM.mensajeError = ex.InnerException.Message
        Finally
            objStream.Close()
            objFSO = Nothing
            objStream = Nothing
        End Try
    End Sub
    Public ReadOnly Property visibilidadSoloImprimir As Visibility Implements IAgencia.visibilidadSoloImprimir
        Get
            Return Visibility.Visible
        End Get
    End Property
    Public ReadOnly Property retornoSoloCobros As Integer Implements IAgencia.retornoSoloCobros
        Get
            Return 0 'NO
        End Get
    End Property
    Public ReadOnly Property servicioSoloCobros As Integer Implements IAgencia.servicioSoloCobros
        Get
            Return 1 ' Normal
        End Get
    End Property
    Public ReadOnly Property horarioSoloCobros As Integer Implements IAgencia.horarioSoloCobros
        Get
            Return 2 ' 14 horas
        End Get
    End Property
    Public ReadOnly Property retornoSinRetorno As Integer Implements IAgencia.retornoSinRetorno
        Get
            Return 0 ' NO
        End Get
    End Property
    Private Async Function cambiarEstadoAsync(enviosAgencia As EnviosAgencia) As Task(Of HttpResponseMessage)
        Dim response As HttpResponseMessage
        Dim urlLlamada As String = "http://localhost:53364/api/EnviosAgencias/" + enviosAgencia.Numero.ToString

        Using cliente As New HttpClient
            cliente.BaseAddress = New Uri("http://localhost:53364/")
            cliente.DefaultRequestHeaders.Accept.Clear()
            cliente.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
            'enviosAgencia.Estado = AgenciasViewModel.ESTADO_TRAMITADO_ENVIO 'Enviado
            response = Await cliente.PutAsJsonAsync(urlLlamada, enviosAgencia)
        End Using

        Return response.EnsureSuccessStatusCode
    End Function


End Class