Imports System.Windows.Input
Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Data.Entity.Core.Objects
Imports Microsoft.Practices.Prism.Mvvm
Imports Microsoft.Practices.Prism.Commands
Imports Microsoft.Practices.Prism.Interactivity.InteractionRequest
Imports Microsoft.Win32
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports System.Transactions
Imports System.Threading.Tasks
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Newtonsoft.Json

Public Class AgenciasViewModel
    Inherits BindableBase
    '    Implements IActiveAware

    ' El modo cuadre sirve para cuadrar los saldos iniciales de cada agencia con la contabilidad
    ' En este modo al contabilizar los reembolsos no se toca la contabilidad, pero sí se pone la 
    ' fecha de cobro a 01/01/15
    Const MODO_CUADRE = False

    Const CARGO_AGENCIA = 26
    Const LONGITUD_TELEFONO = 15

    'Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly servicio As IAgenciaService
    Private ReadOnly configuracion As IConfiguracion
    'Private Shared DbContext As NestoEntities

    Dim empresaDefecto As String

    Dim factory As New Dictionary(Of String, Func(Of IAgencia))

    Private imprimirEtiqueta As Boolean

    Public Sub New(regionManager As IRegionManager, servicio As IAgenciaService, configuracion As IConfiguracion)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If

        'Me.container = container
        Me.regionManager = regionManager
        Me.servicio = servicio
        Me.configuracion = configuracion

        'DbContext = New NestoEntities

        Titulo = "Agencias"

        ' Prism
        cmdCargarDatos = New DelegateCommand(AddressOf OnCargarDatos)
        cmdCargarEstado = New DelegateCommand(Of Object)(AddressOf OnCargarEstado, AddressOf CanCargarEstado)
        cmdAgregarReembolsoContabilizar = New DelegateCommand(Of Object)(AddressOf OnAgregarReembolsoContabilizar, AddressOf CanAgregarReembolsoContabilizar)
        cmdRecibirRetorno = New DelegateCommand(Of Object)(AddressOf OnRecibirRetorno, AddressOf CanRecibirRetorno)
        cmdQuitarReembolsoContabilizar = New DelegateCommand(Of Object)(AddressOf OnQuitarReembolsoContabilizar, AddressOf CanQuitarReembolsoContabilizar)
        cmdContabilizarReembolso = New DelegateCommand(Of Object)(AddressOf OnContabilizarReembolso, AddressOf CanContabilizarReembolso)
        cmdDescargarImagen = New DelegateCommand(Of Object)(AddressOf OnDescargarImagen, AddressOf CanDescargarImagen)
        cmdModificar = New DelegateCommand(Of Object)(AddressOf OnModificar, AddressOf CanModificar)
        cmdModificarEnvio = New DelegateCommand(Of Object)(AddressOf OnModificarEnvio, AddressOf CanModificarEnvio)
        cmdImprimirManifiesto = New DelegateCommand(Of Object)(AddressOf OnImprimirManifiesto, AddressOf CanImprimirManifiesto)
        cmdRehusarEnvio = New DelegateCommand(Of Object)(AddressOf OnRehusarEnvio, AddressOf CanRehusarEnvio)
        cmdInsertar = New DelegateCommand(Of Object)(AddressOf OnInsertar, AddressOf CanInsertar)
        InsertarEnvioPendienteCommand = New DelegateCommand(AddressOf OnInsertarEnvioPendiente, AddressOf CanInsertarEnvioPendiente)
        BorrarEnvioPendienteCommand = New DelegateCommand(AddressOf OnBorrarEnvioPendiente, AddressOf CanBorrarEnvioPendiente)
        GuardarEnvioPendienteCommand = New DelegateCommand(AddressOf OnGuardarEnvioPendiente, AddressOf CanGuardarEnvioPendiente)
        AbrirEnlaceSeguimientoCommand = New DelegateCommand(AddressOf OnAbrirEnlaceSeguimientoCommand, AddressOf CanAbrirEnlaceSeguimientoCommand)

        NotificationRequest = New InteractionRequest(Of INotification)
        ConfirmationRequest = New InteractionRequest(Of IConfirmation)

        factory.Add("ASM", Function() New AgenciaASM(Me))
        factory.Add("OnTime", Function() New AgenciaOnTime(Me))
        factory.Add("Glovo", Function() New AgenciaGlovo(Me))
        factory.Add("Correos Express", Function() New AgenciaCorreosExpress())
    End Sub



#Region "Propiedades"
    ' Carlos 03/09/14
    ' Las propiedades que terminan en "envio" son las que se usan de manera temporal para que el usuario
    ' pueda modificar los datos. Por ejemplo, nombreEnvio se actualiza con el nombre del cliente cada vez que
    ' cambiamos de pedido, pero el usuario puede modificarlas. En el momento de hacer la inserción en la tabla
    ' EnviosAgencia coge el valor que tenga esta propiedad. Así permitimos hacer excepciones y no hay que 
    ' mandarlo siempre con el valor que tiene el campo en la tabla.
    ' Carlos 26/10/17 -> estas propiedades "envio" habría que quitarlas y crear EnvioAgenciaWrapper donde
    ' tengamos todas esas propiedades con un PropertyChanged y change tracking

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

    Public Shared Sub CrearEtiquetaPendiente(etiqueta As EnvioAgenciaWrapper, regionManager As IRegionManager, configuracion As IConfiguracion)
        Dim agenciasVM = New AgenciasViewModel(regionManager, New AgenciaService(configuracion), configuracion)
        agenciasVM.InsertarEnvioPendienteCommand.Execute()
        agenciasVM.agenciaSeleccionada = agenciasVM.listaAgencias.Single(Function(a) a.Nombre = Constantes.Agencias.AGENCIA_INTERNACIONAL)
        agenciasVM.EnvioPendienteSeleccionado.Pedido = etiqueta.Pedido
        agenciasVM.EnvioPendienteSeleccionado.Agencia = agenciasVM.listaAgencias.Single(Function(a) a.Nombre = Constantes.Agencias.AGENCIA_INTERNACIONAL).Numero
        agenciasVM.EnvioPendienteSeleccionado.Nombre = etiqueta.Nombre
        agenciasVM.EnvioPendienteSeleccionado.Direccion = etiqueta.Direccion
        agenciasVM.EnvioPendienteSeleccionado.CodPostal = etiqueta.CodPostal
        agenciasVM.EnvioPendienteSeleccionado.Poblacion = etiqueta.Poblacion
        agenciasVM.EnvioPendienteSeleccionado.Provincia = etiqueta.Provincia
        agenciasVM.EnvioPendienteSeleccionado.Telefono = Left(etiqueta.Telefono, LONGITUD_TELEFONO)
        agenciasVM.EnvioPendienteSeleccionado.Movil = Left(etiqueta.Movil, LONGITUD_TELEFONO)
        agenciasVM.EnvioPendienteSeleccionado.Email = etiqueta.Email
        agenciasVM.EnvioPendienteSeleccionado.Atencion = etiqueta.Nombre
        agenciasVM.EnvioPendienteSeleccionado.Observaciones = Left(etiqueta.Observaciones, 80)
        agenciasVM.EnvioPendienteSeleccionado.Reembolso = etiqueta.Reembolso
        Dim pais As Pais = agenciasVM.listaPaises.SingleOrDefault(Function(p) p.CodigoAlfa = etiqueta.PaisISO)
        If Not IsNothing(pais) Then
            agenciasVM.EnvioPendienteSeleccionado.Pais = pais.Id
        End If
        agenciasVM.EnvioPendienteSeleccionado.Horario = 0
        If etiqueta.PaisISO = "ES" Then
            agenciasVM.EnvioPendienteSeleccionado.Servicio = 93 ' Epaq 24
        ElseIf etiqueta.PaisISO = "PT" Then
            agenciasVM.EnvioPendienteSeleccionado.Servicio = 63 ' Paq24
        Else
            agenciasVM.EnvioPendienteSeleccionado.Servicio = 90 ' Internacional monobulto
        End If

        agenciasVM.GuardarEnvioPendienteCommand.Execute()
    End Sub

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            SetProperty(_titulo, value)
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
            Dim agenciaConfigurar = ConfigurarAgenciaPedido()
            ' Lo hacemos así porque si no sale en blanco
            If Not IsNothing(agenciaConfigurar) Then
                agenciaSeleccionada = listaAgencias.Single(Function(a) a.Numero = agenciaConfigurar.Numero)
            End If
        End Set
    End Property

    Private agenciaEspecifica As IAgencia

    Private _agenciaSeleccionada As AgenciasTransporte
    Public Property agenciaSeleccionada As AgenciasTransporte
        Get
            Return _agenciaSeleccionada
        End Get
        Set(value As AgenciasTransporte)
            Try
                SetProperty(_agenciaSeleccionada, value)
                If Not IsNothing(value) Then
                    agenciaEspecifica = factory(value.Nombre).Invoke
                    If IsNothing(PestañaSeleccionada) Then
                        Return
                    End If
                    If PestañaSeleccionada.Name = Pestannas.PEDIDOS OrElse PestañaSeleccionada.Name = Pestannas.EN_CURSO OrElse PestañaSeleccionada.Name = Pestannas.ETIQUETAS Then
                        ActualizarListas()
                        Dim nombrePais As String = paisActual?.Nombre
                        If Not IsNothing(nombrePais) AndAlso value.Nombre = Constantes.Agencias.AGENCIA_INTERNACIONAL Then
                            paisActual = listaPaises.Single(Function(p) p.Nombre = nombrePais)
                        Else
                            paisActual = listaPaises.Single(Function(p) p.Id = agenciaEspecifica.paisDefecto)
                        End If
                        retornoActual = listaTiposRetorno.Single(Function(r) r.id = agenciaEspecifica.retornoSinRetorno)
                        servicioActual = listaServicios.Single(Function(s) s.id = agenciaEspecifica.ServicioDefecto)
                        horarioActual = listaHorarios.Single(Function(h) h.id = agenciaEspecifica.HorarioDefecto)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.PENDIENTES Then
                        ActualizarListas()
                        If Not IsNothing(EnvioPendienteSeleccionado) Then
                            EnvioPendienteSeleccionado.Pais = agenciaEspecifica.paisDefecto
                            EnvioPendienteSeleccionado.Retorno = agenciaEspecifica.retornoSinRetorno
                            EnvioPendienteSeleccionado.Servicio = agenciaEspecifica.ServicioDefecto
                            EnvioPendienteSeleccionado.Horario = agenciaEspecifica.HorarioDefecto
                        End If
                        Dim envioCreandose = listaPendientes.SingleOrDefault(Function(e) e.Numero = 0)
                        If Not IsNothing(envioCreandose) Then
                            envioCreandose.Empresa = empresaSeleccionada.Número
                            envioCreandose.Agencia = agenciaSeleccionada.Numero
                        End If
                    End If
                    If PestañaSeleccionada.Name = Pestannas.PEDIDOS AndAlso Not IsNothing(empresaSeleccionada) Then
                        listaEnviosPedido = servicio.CargarListaEnviosPedido(empresaSeleccionada.Número, pedidoSeleccionado.Número)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.EN_CURSO Then
                        listaEnvios = servicio.CargarListaEnvios(value.Numero)
                        If Not IsNothing(envioActual) AndAlso envioActual.Agencia <> value.Numero Then
                            envioActual = listaEnvios.FirstOrDefault
                        End If
                    End If
                    If PestañaSeleccionada.Name = Pestannas.REEMBOLSOS Then
                        listaReembolsos = servicio.CargarListaReembolsos(empresaSeleccionada.Número, value.Numero)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.RETORNOS Then
                        listaRetornos = servicio.CargarListaRetornos(empresaSeleccionada.Número, value.Numero, agenciaEspecifica.retornoSinRetorno)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.TRAMITADOS Then
                        listaEnviosTramitados = servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, value.Numero, fechaFiltro)
                    End If

                    OnPropertyChanged("") ' para que actualice todos los enlaces
                End If
            Catch ex As Exception
                NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "No se encuentra la implementación de la agencia " + value.Nombre
                })
                OnPropertyChanged("agenciaSeleccionada")
            End Try

        End Set
    End Property

    Private Sub ActualizarListas()
        If IsNothing(agenciaEspecifica) Then
            Return
        End If
        listaPaises = agenciaEspecifica.ListaPaises()
        listaTiposRetorno = agenciaEspecifica.ListaTiposRetorno
        listaServicios = agenciaEspecifica.ListaServicios
        listaHorarios = agenciaEspecifica.ListaHorarios
    End Sub

    Public ReadOnly Property HayUnEnvioPendienteSeleccionado() As Boolean
        Get
            Return Not IsNothing(EnvioPendienteSeleccionado)
        End Get
    End Property

    Private _empresaSeleccionada As Empresas
    Public Property empresaSeleccionada As Empresas
        Get
            Return _empresaSeleccionada
        End Get
        Set(value As Empresas)
            SetProperty(_empresaSeleccionada, value)
            Try
                listaAgencias = servicio.CargarListaAgencias(empresaSeleccionada.Número)
                If numeroPedido = "" Then
                    agenciaSeleccionada = listaAgencias.FirstOrDefault
                End If
                If Not IsNothing(agenciaSeleccionada) Then
                    If PestañaSeleccionada.Name = Pestannas.EN_CURSO Then
                        listaEnvios = servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.REEMBOLSOS Then
                        listaReembolsos = servicio.CargarListaReembolsos(empresaSeleccionada.Número, agenciaSeleccionada.Numero)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.RETORNOS Then
                        listaRetornos = servicio.CargarListaRetornos(empresaSeleccionada.Número, agenciaSeleccionada.Numero, agenciaEspecifica.retornoSinRetorno)
                    End If
                    If PestañaSeleccionada.Name = Pestannas.TRAMITADOS Then
                        listaEnviosTramitados = servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, agenciaSeleccionada.Numero, fechaFiltro)
                    End If
                Else
                    listaEnvios = New ObservableCollection(Of EnviosAgencia)
                    listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)
                    listaReembolsos = New ObservableCollection(Of EnviosAgencia)
                    listaRetornos = New ObservableCollection(Of EnviosAgencia)
                End If
                listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia)

                OnPropertyChanged("sumaContabilidad")
                OnPropertyChanged("descuadreContabilidad")
                OnPropertyChanged("etiquetaBultosTramitados")
                'actualizar lista de pedidos o de envíos, dependiendo de la pestaña que esté seleccionada
                'una vez actualizadas, seleccionar el pedido o el envío actual también
            Catch ex As Exception
                Return
            End Try

        End Set
    End Property

    Private _pedidoSeleccionado As CabPedidoVta
    Public Property pedidoSeleccionado As CabPedidoVta
        Get
            Return _pedidoSeleccionado
        End Get
        Set(value As CabPedidoVta)
            SetProperty(_pedidoSeleccionado, value)

            If Not IsNothing(cmdInsertar) Then
                cmdInsertar.RaiseCanExecuteChanged()
            End If

            If Not IsNothing(pedidoSeleccionado) Then
                Try
                    Dim cliente = servicio.CargarPedido(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número).Clientes
                    reembolso = importeReembolso(pedidoSeleccionado)
                    bultos = 1
                    nombreEnvio = If(cliente.Nombre IsNot Nothing, cliente.Nombre.Trim, "")
                    direccionEnvio = If(cliente.Dirección IsNot Nothing, cliente.Dirección.Trim, "")
                    poblacionEnvio = If(cliente.Población IsNot Nothing, cliente.Población.Trim, "")
                    provinciaEnvio = If(cliente.Provincia IsNot Nothing, cliente.Provincia.Trim, "")
                    codPostalEnvio = If(cliente.CodPostal IsNot Nothing, cliente.CodPostal.Trim, "")
                    If cliente.Teléfono IsNot Nothing Then
                        telefonoEnvio = telefonoUnico(cliente.Teléfono.Trim, "F")
                        movilEnvio = telefonoUnico(cliente.Teléfono.Trim, "M")
                    Else
                        telefonoEnvio = ""
                        movilEnvio = ""
                    End If
                    correoEnvio = correoUnico()
                    observacionesEnvio = pedidoSeleccionado.Comentarios
                    attEnvio = nombreEnvio
                    If IsNothing(empresaSeleccionada) OrElse IsNothing(empresaSeleccionada.FechaPicking) Then
                        fechaEnvio = Today
                    Else
                        fechaEnvio = empresaSeleccionada.FechaPicking
                    End If
                    listaEnviosPedido = servicio.CargarListaEnviosPedido(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
                    envioActual = listaEnviosPedido.LastOrDefault
                    Dim agenciaConfigurar = ConfigurarAgenciaPedido()
                    If Not IsNothing(agenciaConfigurar) AndAlso (IsNothing(empresaSeleccionada) OrElse agenciaConfigurar.Empresa <> empresaSeleccionada.Número) AndAlso Not IsNothing(listaEmpresas) Then
                        empresaSeleccionada = listaEmpresas.Single(Function(e) e.Número = agenciaConfigurar.Empresa)
                    End If
                    If Not IsNothing(listaAgencias) AndAlso Not IsNothing(agenciaConfigurar) Then
                        agenciaSeleccionada = listaAgencias.Single(Function(a) a.Numero = agenciaConfigurar.Numero)
                    End If
                Catch ex As Exception
                    If Not IsNothing(NotificationRequest) Then
                        NotificationRequest.Raise(New Notification() With {
                         .Title = "Error",
                        .Content = ex.Message
                        })
                    End If
                End Try

            Else
                'NotificationRequest.Raise(New Notification() With {
                ' .Title = "Error",
                '.Content = "El pedido seleccionado no existe"
                '})
                numeroPedido = 36
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

    Private _listaPaises As ObservableCollection(Of Pais)
    Public Property listaPaises() As ObservableCollection(Of Pais)
        Get
            Return _listaPaises
        End Get
        Set(ByVal value As ObservableCollection(Of Pais))
            SetProperty(_listaPaises, value)
        End Set
    End Property

    Private _paisActual As Pais
    Public Property paisActual() As Pais
        Get
            Return _paisActual
        End Get
        Set(ByVal value As Pais)
            If IsNothing(value) Then
                Return
            End If
            SetProperty(_paisActual, value)
            If (_paisActual.Id <> agenciaEspecifica.paisDefecto AndAlso agenciaSeleccionada.Nombre <> Constantes.Agencias.AGENCIA_INTERNACIONAL) Then
                agenciaSeleccionada = listaAgencias.Single(Function(a) a.Nombre = Constantes.Agencias.AGENCIA_INTERNACIONAL)
            End If
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

    Private _enlaceSeguimientoEnvio As String
    Public Property EnlaceSeguimientoEnvio As String
        Get
            Return _enlaceSeguimientoEnvio
        End Get
        Set(value As String)
            SetProperty(_enlaceSeguimientoEnvio, value)
            AbrirEnlaceSeguimientoCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _envioActual As EnviosAgencia
    Public Property envioActual As EnviosAgencia
        Get
            Return _envioActual
        End Get
        Set(value As EnviosAgencia)
            SetProperty(_envioActual, value)

            Try
                estadoEnvioCargado = Nothing
                mensajeError = ""
                Dim agenciaEnvio As AgenciasTransporte = Nothing
                If Not IsNothing(envioActual) AndAlso Not IsNothing(envioActual.Agencia) Then
                    agenciaEnvio = servicio.CargarAgencia(envioActual.Agencia)
                End If

                If IsNothing(clienteFiltro) AndAlso Not IsNothing(envioActual) AndAlso Not IsNothing(agenciaEnvio) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso agenciaSeleccionada.Numero <> agenciaEnvio.Numero Then
                    agenciaSeleccionada = agenciaEnvio
                End If

                If Not IsNothing(cmdCargarEstado) Then
                    cmdCargarEstado.RaiseCanExecuteChanged()
                End If

                If Not IsNothing(envioActual) AndAlso Not IsNothing(listaTiposRetorno) Then
                    reembolsoModificar = envioActual.Reembolso
                    EnlaceSeguimientoEnvio = agenciaEspecifica.EnlaceSeguimiento(envioActual)
                    retornoModificar = (From l In listaTiposRetorno Where l.id = envioActual.Retorno).FirstOrDefault
                    estadoModificar = envioActual.Estado
                    fechaEntregaModificar = envioActual.FechaEntrega
                    listaHistoriaEnvio = servicio.CargarListaHistoriaEnvio(envioActual.Numero)
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
                OnPropertyChanged("sePuedeModificarEstado")
                OnPropertyChanged("agenciaSeleccionada")
            Catch ex As Exception
                Return
            End Try
            cmdModificar.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _envioPendienteSeleccionado As EnvioAgenciaWrapper
    Public Property EnvioPendienteSeleccionado() As EnvioAgenciaWrapper
        Get
            Return _envioPendienteSeleccionado
        End Get
        Set(ByVal value As EnvioAgenciaWrapper)

            If Not IsNothing(value) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso value.Agencia <> agenciaSeleccionada.Numero Then
                empresaSeleccionada = listaEmpresas.Single(Function(e) e.Número = value.Empresa)
                agenciaSeleccionada = listaAgencias.Single(Function(a) a.Numero = value.Agencia)
            End If
            SetProperty(_envioPendienteSeleccionado, value)
            OnPropertyChanged(NameOf(HayUnEnvioPendienteSeleccionado))
            ActualizarEstadoComandos()
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
            listaEnviosTramitados = servicio.CargarListaEnviosTramitadosPorFecha(empresaSeleccionada.Número, fechaFiltro)
            If Not IsNothing(PestañaSeleccionada) AndAlso PestañaSeleccionada.Name = Pestannas.TRAMITADOS Then
                envioActual = listaEnviosTramitados.FirstOrDefault(Function(e) e.Agencia = agenciaSeleccionada.Numero)
                If IsNothing(envioActual) Then
                    envioActual = listaEnviosTramitados.FirstOrDefault()
                End If
            End If
        End Set
    End Property

    Private _clienteFiltro As String
    Public Property clienteFiltro As String
        Get
            Return _clienteFiltro
        End Get
        Set(value As String)
            SetProperty(_clienteFiltro, value)
            listaEnviosTramitados = servicio.CargarListaEnviosTramitadosPorCliente(empresaSeleccionada.Número, clienteFiltro)
            If PestañaSeleccionada.Name = "tabTramitados" Then
                envioActual = listaEnviosTramitados.FirstOrDefault
            End If
        End Set
    End Property

    Private _nombreFiltro As String
    Public Property nombreFiltro As String
        Get
            Return _nombreFiltro
        End Get
        Set(value As String)
            SetProperty(_nombreFiltro, value)
            listaEnviosTramitados = servicio.CargarListaEnviosTramitadosPorNombre(empresaSeleccionada.Número, nombreFiltro)
            envioActual = listaEnviosTramitados.FirstOrDefault
        End Set
    End Property

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
                Dim pedidoBuscado As CabPedidoVta = servicio.CargarPedidoPorNumero(pedidoNumerico, False)
                If IsNothing(pedidoBuscado) OrElse IsNothing(pedidoBuscado.Empresa) Then
                    pedidoSeleccionado = servicio.CargarPedidoPorNumero(pedidoNumerico)
                Else
                    pedidoSeleccionado = pedidoBuscado
                End If
            Else ' si no es numérico (es una factura, lo tratamos como un cobro)
                pedidoSeleccionado = CalcularPedidoTexto(numeroPedido)
                ' Carlos 22/09/15: para que permita meter los que solo llevan contra reembolso
                'If IsNothing(agenciaEspecifica) AndAlso IsNothing(empresaSeleccionada) Then
                '    agenciaSeleccionada = DbContext.AgenciasTransporte.OrderByDescending(Function(o) o.Numero).FirstOrDefault(Function(a) a.Empresa = pedidoSeleccionado.Empresa)
                'End If
                If Not IsNothing(pedidoSeleccionado) AndAlso Not IsNothing(agenciaEspecifica) Then
                    retornoActual = (From s In listaTiposRetorno Where s.id = agenciaEspecifica.retornoSoloCobros).FirstOrDefault
                    servicioActual = (From s In listaServicios Where s.id = agenciaEspecifica.servicioSoloCobros).FirstOrDefault
                    horarioActual = (From s In listaHorarios Where s.id = agenciaEspecifica.horarioSoloCobros).FirstOrDefault
                    paisActual = (From s In listaPaises Where s.Id = agenciaEspecifica.paisDefecto).SingleOrDefault
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

            If Not IsNothing(pedidoAnterior) AndAlso Not IsNothing(pedidoSeleccionado) AndAlso pedidoSeleccionado.Empresa <> pedidoAnterior.Empresa Then
                empresaSeleccionada = listaEmpresas.Single(Function(e) e.Número = pedidoSeleccionado.Empresa)
            End If

            'Dim agenciaNueva As AgenciasTransporte = (From a In DbContext.AgenciasTransporte Where a.Ruta = pedidoSeleccionado.Ruta).FirstOrDefault
            OnPropertyChanged("empresaSeleccionada")
            OnPropertyChanged("agenciaSeleccionada")
        End Set
    End Property

    Private Function CalcularPedidoTexto(numeroPedido As String) As CabPedidoVta
        Dim pedidoEncontrado As CabPedidoVta = servicio.CargarPedidoPorFactura(numeroPedido)
        '
        If Not IsNothing(pedidoEncontrado) Then
            Return pedidoEncontrado
        End If

        Dim clienteEncontrado = servicio.CargarClientePorUnDato(empresaSeleccionada.Número, numeroPedido)

        If IsNothing(clienteEncontrado) Then
            Return Nothing
        End If

        pedidoEncontrado = clienteEncontrado.CabPedidoVta.OrderByDescending(Function(c) c.Número).FirstOrDefault
        Return pedidoEncontrado
    End Function

    Private _numeroMultiusuario As Integer
    Public Property numeroMultiusuario As Integer
        Get
            Return _numeroMultiusuario
        End Get
        Set(value As Integer)
            SetProperty(_numeroMultiusuario, value)
            multiusuario = servicio.CargarMultiusuario(empresaSeleccionada.Número, numeroMultiusuario)
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
            If PestañaSeleccionada.Name = Pestannas.PEDIDOS Then
                numeroPedido = numeroPedido 'para que ejecute el código
            End If
            If PestañaSeleccionada.Name = Pestannas.PENDIENTES Then
                Dim listaNueva As IEnumerable(Of EnvioAgenciaWrapper) = servicio.CargarListaPendientes()
                If Not IsNothing(listaPendientes) Then
                    listaPendientes.Clear()
                Else
                    listaPendientes = New ObservableCollection(Of EnvioAgenciaWrapper)
                End If

                For Each envio In listaNueva
                    AddHandler envio.PropertyChanged, New PropertyChangedEventHandler(AddressOf EnvioPendienteSeleccionadoPropertyChangedEventHandler)
                    listaPendientes.Add(envio)
                Next
                If listaPendientes.Count > 0 And IsNothing(EnvioPendienteSeleccionado) Then
                    EnvioPendienteSeleccionado = listaPendientes.FirstOrDefault
                Else
                    ActualizarEstadoComandos()
                End If
            End If
            If PestañaSeleccionada.Name = Pestannas.EN_CURSO AndAlso Not IsNothing(agenciaSeleccionada) Then
                ActualizarListas()
                listaEnvios = servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
            End If

            If PestañaSeleccionada.Name = Pestannas.REEMBOLSOS AndAlso Not IsNothing(empresaSeleccionada) Then
                listaReembolsos = servicio.CargarListaReembolsos(empresaSeleccionada.Número, agenciaSeleccionada.Numero)
            End If

            If PestañaSeleccionada.Name = Pestannas.RETORNOS AndAlso Not IsNothing(empresaSeleccionada) Then
                listaRetornos = servicio.CargarListaRetornos(empresaSeleccionada.Número, agenciaSeleccionada.Numero, agenciaEspecifica.retornoSinRetorno)
            End If
            If PestañaSeleccionada.Name = Pestannas.TRAMITADOS AndAlso Not IsNothing(empresaSeleccionada) Then
                listaEnviosTramitados = servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, agenciaSeleccionada.Numero, fechaFiltro)
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
            OnPropertyChanged("sePuedeModificarEstado")
            OnPropertyChanged("etiquetaBultosTramitados")
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

    Private _listaPendientes As ObservableCollection(Of EnvioAgenciaWrapper)
    Public Property listaPendientes() As ObservableCollection(Of EnvioAgenciaWrapper)
        Get
            Return _listaPendientes
        End Get
        Set(ByVal value As ObservableCollection(Of EnvioAgenciaWrapper))
            SetProperty(_listaPendientes, value)
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
            OnPropertyChanged("descuadreContabilidad")
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
            Try
                If Not IsNothing(agenciaSeleccionada) AndAlso agenciaSeleccionada.Empresa = empresaSeleccionada.Número Then
                    Dim suma As Nullable(Of Double) = servicio.CalcularSumaContabilidad(empresaSeleccionada.Número, agenciaSeleccionada.CuentaReembolsos)

                    If IsNothing(suma) Then
                        Return 0
                    Else
                        Return CType(suma, Double)
                    End If
                Else
                    Return 0
                End If
            Catch ex As Exception
                Return 0
            End Try
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

    Private _fechaEntregaModificar As Date?
    Public Property fechaEntregaModificar() As Date?
        Get
            Return _fechaEntregaModificar
        End Get
        Set(ByVal value As Date?)
            SetProperty(_fechaEntregaModificar, value)
        End Set
    End Property

    Public ReadOnly Property sePuedeModificarReembolso As Boolean
        Get
            Return Not IsNothing(envioActual) AndAlso Not IsNothing(listaEnviosTramitados) AndAlso listaEnviosTramitados.Count > 0
        End Get
    End Property

    Public ReadOnly Property sePuedeModificarEstado As Boolean
        Get
            Return sePuedeModificarReembolso AndAlso envioActual.Reembolso = 0
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

    Public ReadOnly Property etiquetaBultosTramitados As String
        Get
            If Not IsNothing(listaEnviosTramitados) AndAlso listaEnviosTramitados.Count > 0 Then
                Dim totalBultos As Integer = (Aggregate e In listaEnviosTramitados Into Sum(e.Bultos))
                Dim totalEnvios As Integer = (Aggregate e In listaEnviosTramitados Into Count())
                Return "Hay " + totalEnvios.ToString + " envíos tramitados, que suman un total de " + totalBultos.ToString + " bultos."
            Else
                Return "No hay ningún envío tramitado"
            End If
        End Get
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
        Return Not IsNothing(envioActual) AndAlso Not IsNothing(listaEnvios) AndAlso listaEnvios.Count > 0 AndAlso envioActual.Estado <= 0
    End Function
    Private Async Sub Tramitar(ByVal param As Object)
        If envioActual.Estado >= Constantes.Agencias.ESTADO_TRAMITADO_ENVIO Then
            Throw New Exception("No se puede tramitar un pedido ya tramitado")
        End If
        Try
            Dim respuesta = Await agenciaEspecifica.LlamadaWebService(envioActual, servicio)
            If respuesta = "OK" OrElse respuesta.StartsWith("ENVIO DUPLICADO") Then
                mensajeError = servicio.TramitarEnvio(envioActual)
                listaEnvios = servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
                envioActual = listaEnvios.LastOrDefault ' lo pongo para que no se vaya al último
            Else
                mensajeError = respuesta
            End If
        Catch ex As Exception
            mensajeError = ex.Message
        End Try
        OnPropertyChanged("listaReembolsos")
        OnPropertyChanged("mensajeError")
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
        agenciaEspecifica.imprimirEtiqueta(envioActual)
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
        Return envioActual IsNot Nothing AndAlso Not IsNothing(listaEnvios) AndAlso listaEnvios.Count > 0 AndAlso envioActual.Estado <= 0
    End Function
    Private Sub Borrar(ByVal param As Object)
        If envioActual.Estado > 0 Then
            Throw New Exception("No se puede borrar un pedido tramitado")
        End If
        Dim mensajeMostrar = String.Format("¿Confirma que desea borrar el envío del cliente {1}?{0}{0}{2}", Environment.NewLine, envioActual.Cliente?.Trim, envioActual.Direccion)
        Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = mensajeMostrar, .Title = "Borrar Envío"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

        If InteractionResultMessage = "KO" Then
            Return
        End If
        servicio.Borrar(envioActual.Numero)
        Dim copiaEnvio = envioActual
        listaEnviosPedido.Remove(copiaEnvio)
        listaEnvios.Remove(copiaEnvio)
        envioActual = listaEnvios.LastOrDefault
        OnPropertyChanged("listaEnvios")
    End Sub

    Private _cmdInsertar As DelegateCommand(Of Object)
    Public Property cmdInsertar As DelegateCommand(Of Object)
        Get
            Return _cmdInsertar
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdInsertar = value
        End Set
    End Property
    Private Function CanInsertar(arg As Object) As Boolean
        Return Not IsNothing(pedidoSeleccionado) AndAlso Not IsNothing(agenciaSeleccionada) 'AndAlso pedidoSeleccionado.Empresa <> EMPRESA_ESPEJO
    End Function
    Private Sub OnInsertar(arg As Object)
        Try
            InsertarRegistro(servicioActual.id = agenciaEspecifica.ServicioCreaEtiquetaRetorno)
        Catch e As Exception
            imprimirEtiqueta = False
            NotificationRequest.Raise(New Notification() With {
                 .Title = "Error",
                .Content = e.Message
            })
        End Try
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
            If imprimirEtiqueta Then
                cmdImprimirEtiquetaPedido.Execute(Nothing)
            End If
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With {
             .Title = "Error",
            .Content = ex.Message
            })
            Return
        End Try
        Dim textoImprimir As String
        If imprimirEtiqueta Then
            textoImprimir = "Envío insertado correctamente e impresa la etiqueta"
        Else
            textoImprimir = "Envío ampliado correctamente"
        End If
        NotificationRequest.Raise(New Notification() With {
             .Title = "Envío",
            .Content = textoImprimir
        })
    End Sub

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

    Private _cmdCargarDatos As DelegateCommand
    Public Property cmdCargarDatos As DelegateCommand
        Get
            Return _cmdCargarDatos
        End Get
        Private Set(value As DelegateCommand)
            _cmdCargarDatos = value
        End Set
    End Property
    Private Async Function OnCargarDatos() As Task
        Try
            empresaDefecto = String.Format("{0,-3}", Await configuracion.leerParametro("1", "EmpresaPorDefecto")).Trim()
            If IsNothing(listaEmpresas) Then
                listaEmpresas = servicio.CargarListaEmpresas()
            End If

            numeroPedido = Await configuracion.leerParametro(empresaDefecto, "UltNumPedidoVta")

            If IsNothing(empresaSeleccionada) Then
                empresaSeleccionada = (From e In listaEmpresas Where e.Número.Trim() = empresaDefecto).SingleOrDefault
            End If

            If IsNothing(listaEnvios) Then
                listaEnvios = New ObservableCollection(Of EnviosAgencia)
            End If
            listaPendientes = New ObservableCollection(Of EnvioAgenciaWrapper)

            If IsNothing(agenciaSeleccionada) Then
                agenciaSeleccionada = listaAgencias.FirstOrDefault
            End If

            bultos = 1
            fechaFiltro = Today

            'If Not IsNothing(agenciaSeleccionada) Then
            '    listaEnvios = servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
            'End If
            If IsNothing(envioActual) Then
                envioActual = listaEnvios.LastOrDefault
            End If

            'listaEnviosTramitados = servicio.CargarListaEnviosTramitadosPorFecha(empresaSeleccionada.Número, fechaFiltro)
            'listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia)
        Catch ex As Exception
            Throw ex
        End Try

    End Function

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
        Dim cliente As Clientes = servicio.CargarClientePrincipal(empresaSeleccionada.Número, numClienteContabilizar)
        If IsNothing(cliente) Then
            NotificationRequest.Raise(New Notification() With {
                .Title = "Error al contabilizar",
                .Content = "El cliente " + numClienteContabilizar + " no existe en " + empresaSeleccionada.Nombre
            })
            Return
        End If

        Dim asiento As Integer = 0 'para guardar el asiento que devuelve prdContabilizar

        ' Empezamos una transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities
                Try
                    If Not MODO_CUADRE Then
                        For Each linea In listaReembolsosSeleccionados
                            Dim agencia As AgenciasTransporte = DbContext.AgenciasTransporte.Where(Function(a) a.Numero = linea.Agencia).SingleOrDefault
                            Dim numDocAgencia As String
                            If agencia.Nombre.Length > 10 Then
                                numDocAgencia = agencia.Nombre.Substring(0, 10)
                            Else
                                numDocAgencia = agencia.Nombre
                            End If
                            DbContext.PreContabilidad.Add(New PreContabilidad With {
                            .Empresa = empresaSeleccionada.Número,
                            .Diario = "_PagoReemb",
                            .Asiento = 1,
                            .Fecha = Today,
                            .FechaVto = Today,
                            .TipoApunte = "3",
                            .TipoCuenta = "1",
                            .Nº_Cuenta = agencia.CuentaReembolsos,
                            .Concepto = "Pago reembolso " + linea.Cliente,
                            .Haber = linea.Reembolso,
                            .Nº_Documento = numDocAgencia,
                            .Delegación = "ALG",
                            .FormaVenta = "VAR"
                        })
                        Next
                        Dim numDoc As String
                        If agenciaSeleccionada.Nombre.Length > 10 Then
                            numDoc = agenciaSeleccionada.Nombre.Substring(0, 10)
                        Else
                            numDoc = agenciaSeleccionada.Nombre
                        End If
                        DbContext.PreContabilidad.Add(New PreContabilidad With {
                            .Empresa = empresaSeleccionada.Número,
                            .Diario = "_PagoReemb",
                            .Asiento = 1,
                            .Fecha = Today,
                            .FechaVto = Today,
                            .TipoApunte = "3",
                            .TipoCuenta = "2",
                            .Nº_Cuenta = numClienteContabilizar,
                            .Contacto = "0",
                            .Concepto = "Pago reembolso " + agenciaSeleccionada.Nombre,
                            .Debe = sumaSeleccionadas,
                            .Nº_Documento = numDoc,
                            .Delegación = "ALG",
                            .FormaVenta = "VAR",
                            .FormaPago = empresaSeleccionada.FormaPagoEfectivo,
                            .Vendedor = "NV"
                        })
                        'DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                        success = DbContext.SaveChanges()

                        asiento = DbContext.prdContabilizar(empresaSeleccionada.Número, "_PagoReemb")
                    End If
                    If success AndAlso (asiento > 0 OrElse MODO_CUADRE) Then
                        Dim fechaAFijar As Date = Today
                        If MODO_CUADRE Then
                            fechaAFijar = "01/01/2015"
                        End If
                        Dim lineaEncontrada As EnviosAgencia
                        For Each linea In listaReembolsosSeleccionados
                            lineaEncontrada = DbContext.EnviosAgencia.Where(Function(e) e.Numero = linea.Numero).Single
                            lineaEncontrada.FechaPagoReembolso = fechaAFijar
                        Next
                        'DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                        If DbContext.SaveChanges() Then
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
                    Else
                        transaction.Dispose()
                        success = False
                    End If

                    ' Comprobamos que las transacciones sean correctas
                    If success Then
                        ' Reset the context since the operation succeeded. 
                        DbContext.SaveChanges()
                        NotificationRequest.Raise(New Notification() With {
                         .Title = "Contabilizado Correctamente",
                        .Content = "Nº Asiento: " + asiento.ToString
                    })
                    Else
                        transaction.Dispose()
                        NotificationRequest.Raise(New Notification() With {
                         .Title = "¡Error!",
                        .Content = "Se ha producido un error y no se han grabado los datos"
                    })
                    End If
                Catch ex As Exception
                    transaction.Dispose()
                    NotificationRequest.Raise(New Notification() With {
                         .Title = "¡Error! Se ha producido un error y no se han grabado los datos",
                        .Content = ex.Message
                    })
                End Try
            End Using ' Cerramos el contexto
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



    Private _cmdModificar As DelegateCommand(Of Object)
    Public Property cmdModificar As DelegateCommand(Of Object)
        Get
            Return _cmdModificar
        End Get
        Private Set(value As DelegateCommand(Of Object))
            _cmdModificar = value
        End Set
    End Property
    Private Function CanModificar(arg As Object) As Boolean
        Return Not IsNothing(envioActual)
    End Function
    Private Sub OnModificar(arg As Object)
        Dim mensajeMostrar = String.Format("¿Confirma que desea modificar el envío del cliente {1}?{0}{0}{2}", Environment.NewLine, envioActual.Cliente?.Trim, envioActual.Direccion)
        Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = mensajeMostrar, .Title = "Modificar Envío"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

        If InteractionResultMessage = "KO" Then
            Return
        End If
        servicio.Modificar(envioActual)
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
        Dim mensajeMostrar = String.Format("¿Confirma que desea modificar el envío del cliente {1}?{0}{0}{2}", Environment.NewLine, envioActual.Cliente?.Trim, envioActual.Direccion)
        Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = mensajeMostrar, .Title = "Modificar Envío"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

        If InteractionResultMessage = "KO" Then
            Return
        End If
        modificarEnvio(envioActual, reembolsoModificar, retornoModificar, estadoModificar, fechaEntregaModificar)
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
        'Dim region As IRegion = regionManager.Regions("MainRegion")
        'Dim vista = container.Resolve(Of Object)("frmCRInforme")
        'Dim report As New ReportClass
        'report.FileName = "C:\Users\Carlos.NUEVAVISION\Documents\Visual Studio 2013\Projects\Nesto\Nesto\Informes\ImpresoUbicaciones_Inventario.rpt"
        'report.Load()
        ''report.SetDataSource(listaEnviosTramitados)
        ''Dim stream As IO.Stream = rptH.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat)
        ''Return File(stream, "application/pdf")
        'vista.crvInforme = report

        'region.Add(vista, "frmCRInforme")
        'region.Activate(vista)
        ''regionManager.RequestNavigate("MainRegion", New Uri("/Clientes", UriKind.Relative))
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

            Using DbContext As New NestoEntities
                Dim lineaEncontrada As EnviosAgencia = DbContext.EnviosAgencia.Where(Function(e) e.Numero = lineaRetornoSeleccionado.Numero).Single
                lineaEncontrada.FechaRetornoRecibido = Today

                If DbContext.SaveChanges Then
                    mensajeError = "Fecha retorno del cliente " + lineaRetornoSeleccionado.Cliente.Trim + " actualizada correctamente"
                    listaRetornos.Remove(lineaRetornoSeleccionado)
                Else
                    mensajeError = "Se ha producido un error al actualizar la fecha del retorno"
                    NotificationRequest.Raise(New Notification() With {
                    .Title = "Error",
                    .Content = "No se ha podido actualizar la fecha del retorno"
                })
                End If
            End Using
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
        Return Not IsNothing(envioActual) AndAlso IsNothing(envioActual.FechaPagoReembolso)
    End Function
    Private Sub OnRehusarEnvio(arg As Object)
        Dim tipoRetorno As tipoIdDescripcion = (From l In listaTiposRetorno Where l.id = agenciaEspecifica.retornoObligatorio).FirstOrDefault
        modificarEnvio(envioActual, 0, tipoRetorno, envioActual.Estado, True, envioActual.FechaEntrega)
    End Sub

    Public Property BorrarEnvioPendienteCommand() As DelegateCommand
    Private Function CanBorrarEnvioPendiente() As Boolean
        Return Not IsNothing(EnvioPendienteSeleccionado)
    End Function
    Private Sub OnBorrarEnvioPendiente()
        Dim mensajeMostrar = String.Format("¿Confirma que desea borrar el envío pendiente del cliente {1}?{0}{0}{2}", Environment.NewLine, EnvioPendienteSeleccionado.Cliente?.Trim, EnvioPendienteSeleccionado.Direccion)
        Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = mensajeMostrar, .Title = "Borrar Envío"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

        If InteractionResultMessage = "KO" Then
            Return
        End If

        Dim envioBorrar As EnvioAgenciaWrapper = EnvioPendienteSeleccionado

        'If listaPendientes?.Count > 1 Then
        '    EnvioPendienteSeleccionado = listaPendientes.FirstOrDefault
        'Else
        '    EnvioPendienteSeleccionado = Nothing
        'End If

        servicio.Borrar(envioBorrar.Numero)
        listaPendientes.Remove(envioBorrar)
        ActualizarEstadoComandos()
    End Sub

    Public Property InsertarEnvioPendienteCommand() As DelegateCommand
    Private Function CanInsertarEnvioPendiente() As Boolean
        Return Not IsNothing(empresaSeleccionada) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso Not HayCambiosSinGuardarEnPendientes()
    End Function
    Private Sub OnInsertarEnvioPendiente()
        ' Preparamos si se llama desde fuera
        If IsNothing(listaEmpresas) Then
            listaEmpresas = servicio.CargarListaEmpresas
        End If
        If IsNothing(listaPendientes) Then
            listaPendientes = New ObservableCollection(Of EnvioAgenciaWrapper)
        End If
        If IsNothing(PestañaSeleccionada) Then
            PestañaSeleccionada = New TabItem With {.Name = Pestannas.PENDIENTES}
        End If
        If IsNothing(empresaSeleccionada) AndAlso Not IsNothing(listaEmpresas) Then
            empresaSeleccionada = listaEmpresas.FirstOrDefault
        End If
        If IsNothing(agenciaSeleccionada) AndAlso Not IsNothing(listaAgencias) Then
            agenciaSeleccionada = listaAgencias.LastOrDefault
        End If

        ' Comenzamos a ejecutar
        Dim envioNuevo As EnvioAgenciaWrapper = New EnvioAgenciaWrapper With {
            .Agencia = agenciaSeleccionada.Numero,
            .Empresa = empresaSeleccionada.Número,
            .Estado = Constantes.Agencias.ESTADO_PENDIENTE_ENVIO,
            .TieneCambios = True
        }
        listaPendientes.Add(envioNuevo)
        EnvioPendienteSeleccionado = envioNuevo
        AddHandler EnvioPendienteSeleccionado.PropertyChanged, New PropertyChangedEventHandler(AddressOf EnvioPendienteSeleccionadoPropertyChangedEventHandler)
    End Sub

    Public Property GuardarEnvioPendienteCommand As DelegateCommand
    Private Function CanGuardarEnvioPendiente() As Boolean
        Return HayCambiosSinGuardarEnPendientes()
    End Function
    Private Sub OnGuardarEnvioPendiente()
        Try
            Dim envio = EnvioPendienteSeleccionado.ToEnvioAgencia
            If EnvioPendienteSeleccionado.Numero = 0 Then
                servicio.Insertar(envio)
            Else
                servicio.Modificar(envio)
            End If
            listaPendientes.Remove(EnvioPendienteSeleccionado)
            EnvioPendienteSeleccionado = EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio)
            EnvioPendienteSeleccionado.TieneCambios = False
            listaPendientes.Add(EnvioPendienteSeleccionado)
        Catch ex As Exception
            NotificationRequest.Raise(New Notification() With {
                 .Title = "Error al modificar envío",
                .Content = ex.Message
            })
        End Try
    End Sub

    Public Property AbrirEnlaceSeguimientoCommand As DelegateCommand
    Private Function CanAbrirEnlaceSeguimientoCommand() As Boolean
        Return EnlaceSeguimientoEnvio <> ""
    End Function
    Private Sub OnAbrirEnlaceSeguimientoCommand()
        System.Diagnostics.Process.Start(EnlaceSeguimientoEnvio)
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
    Public Function importeReembolso(pedidoSeleccionado As CabPedidoVta) As Decimal

        ' Miramos la deuda que tenga en su extracto. 
        ' Esa deuda la tiene que pagar independientemente de la forma de pago
        Dim importeDeuda As Double = 0 'calcularDeuda()

        ' Miramos los casos en los que no hay contra reembolso
        If IsNothing(pedidoSeleccionado) Then
            Return importeDeuda
        End If
        If pedidoSeleccionado.CCC IsNot Nothing Then
            Return importeDeuda
        End If
        If pedidoSeleccionado.Periodo_Facturacion = "FDM" Then
            Return importeDeuda
        End If
        If (pedidoSeleccionado.Forma_Pago = "CNF" Or
            pedidoSeleccionado.Forma_Pago = "TRN" Or
            pedidoSeleccionado.Forma_Pago = "CHC" Or
            pedidoSeleccionado.Forma_Pago = "TAR") Then
            Return importeDeuda
        End If
        If pedidoSeleccionado.NotaEntrega Then
            Return importeDeuda
        End If
        If Not IsNothing(pedidoSeleccionado.PlazosPago) AndAlso pedidoSeleccionado.PlazosPago.Trim = "PRE" Then
            Return importeDeuda
        End If

        If pedidoSeleccionado.MantenerJunto Then
            Dim lineasSinFacturar As List(Of LinPedidoVta)
            lineasSinFacturar = servicio.CargarLineasPedidoPendientes(pedidoSeleccionado.Número)
            If lineasSinFacturar.Any Then
                Return importeDeuda
            End If
        End If

        ' Para el resto de los casos ponemos el importe correcto
        Dim lineas As List(Of LinPedidoVta)
        lineas = servicio.CargarLineasPedidoSinPicking(pedidoSeleccionado.Número)
        If IsNothing(lineas) OrElse Not lineas.Any Then
            Return importeDeuda
        End If

        Dim importeFinal As Double = Math.Round(
            (Aggregate l In lineas
            Select l.Total Into Sum()) _
            + importeDeuda, 2, MidpointRounding.AwayFromZero)

        ' Evitamos los reembolsos negativos
        If importeFinal < 0 Then
            importeFinal = 0
        End If


        Return importeFinal

    End Function

    Public Sub InsertarRegistro(Optional ByVal conEtiquetaRecogida As Boolean = False)
        Dim envioPendiente As EnviosAgencia = buscarEnvioPendiente(pedidoSeleccionado)
        Dim estabaPendiente As Boolean = Not IsNothing(envioPendiente)
        If Not estabaPendiente Then
            envioActual = buscarPedidoAmpliacion(pedidoSeleccionado)
        Else
            envioActual = envioPendiente
        End If

        Dim textoConfirmar As String
        Dim esAmpliacion As Boolean = Not IsNothing(envioActual.Pedido) 'AndAlso envioActual.Pedido <> pedidoSeleccionado.Número
        If esAmpliacion And Not estabaPendiente Then
            If envioActual.Bultos = bultos Then
                textoConfirmar = "Si el nº total de bultos sigue siendo " + envioActual.Bultos.ToString
                imprimirEtiqueta = False
            Else
                textoConfirmar = "Si el nº total de bultos pasa de " + envioActual.Bultos.ToString + " a " + bultos.ToString
                imprimirEtiqueta = True
            End If
            Me.ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = "Este pedido es una ampliación del pedido " + envioActual.Pedido.ToString + ". " +
                    textoConfirmar +
                    " puede actualizar los datos." + vbCrLf +
                    "En caso contrario, pulse Cancelar, modifique el nº de bultos y vuelva a intentarlo." + vbCrLf + vbCrLf +
                    "¿Desea actualizar los datos?",
                    .Title = "Ampliación"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

            If InteractionResultMessage = "KO" Then
                Throw New Exception("Cancelado por el usuario")
                Return
            End If
        Else
            imprimirEtiqueta = True
        End If

        Dim hayAlgunaLineaConPicking As Boolean = servicio.HayAlgunaLineaConPicking(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
        If Not hayAlgunaLineaConPicking Then
            ConfirmationRequest.Raise(
                New Confirmation() With {
                    .Content = "Este pedido no tiene ninguna línea con picking. ¿Desea insertar el pedido de todos modos?",
                    .Title = "Pedido Sin Picking"
                },
                Sub(c)
                    InteractionResultMessage = If(c.Confirmed, "OK", "KO")
                End Sub
            )

            If InteractionResultMessage = "KO" Then
                Throw New Exception("Cancelado por el usuario")
                Return
            End If
        End If

        ' Carlos 16/09/15: hacemos que se pueda cobrar en efectivo por la agencia
        If reembolso > 0 AndAlso IsNothing(pedidoSeleccionado.IVA) AndAlso agenciaSeleccionada.Empresa <> Constantes.Empresas.EMPRESA_ESPEJO Then
            agenciaSeleccionada = servicio.CargarAgenciaPorNombreYCuentaReembolsos(Constantes.Empresas.EMPRESA_ESPEJO, agenciaSeleccionada.CuentaReembolsos, agenciaSeleccionada.Nombre)
        End If

        ' Carlos: 08/02/20: la transacción vale para hacer el dispose si no coge código de barras
        Dim success As Boolean = False
        Using transaction As New TransactionScope()
            Try
                If estabaPendiente Then
                    envioActual.Estado = Constantes.Agencias.ESTADO_INICIAL_ENVIO
                    envioActual.Bultos = bultos
                Else
                    With envioActual
                        .Empresa = agenciaSeleccionada.Empresa
                        .Agencia = agenciaSeleccionada.Numero
                        .Cliente = pedidoSeleccionado.Nº_Cliente
                        .Contacto = pedidoSeleccionado.Contacto
                        .Pedido = pedidoSeleccionado.Número
                        .Fecha = fechaEnvio
                        .FechaEntrega = fechaEnvio.AddDays(1) 'Se entrega al día siguiente
                        .Servicio = servicioActual.id
                        .Horario = horarioActual.id
                        .Bultos = bultos
                        .Retorno = retornoActual.id
                        .Nombre = nombreEnvio
                        .Direccion = direccionEnvio
                        .CodPostal = codPostalEnvio
                        .Poblacion = poblacionEnvio
                        .Provincia = provinciaEnvio
                        .Pais = paisActual.Id
                        .Telefono = telefonoEnvio
                        .Movil = movilEnvio
                        .Email = correoEnvio
                        .Observaciones = Left(observacionesEnvio, 80)
                        .Atencion = attEnvio
                        .Reembolso = IIf(pedidoSeleccionado.Número = envioActual.Pedido, reembolso, envioActual.Reembolso + reembolso) ' por si es ampliación
                        '.CodigoBarras = calcularCodigoBarras()
                        .Vendedor = If(pedidoSeleccionado.Vendedor.Trim <> "", pedidoSeleccionado.Vendedor, "NV")
                        agenciaEspecifica.calcularPlaza(codPostalEnvio, .Nemonico, .NombrePlaza, .TelefonoPlaza, .EmailPlaza)
                    End With
                End If




                If Not esAmpliacion Then
                    servicio.Insertar(envioActual)
                    listaEnvios.Add(envioActual)
                    listaEnviosPedido.Add(envioActual)
                End If

                envioActual.CodigoBarras = agenciaEspecifica.calcularCodigoBarras(Me)
                servicio.Modificar(envioActual)

                If conEtiquetaRecogida Then
                    ' Realmente no hacemos nada más que aumentar en 1 el contador de Id
                    Dim envioEtiquetaRetorno As EnviosAgencia = New EnviosAgencia With {
                        .Empresa = envioActual.Empresa,
                        .Agencia = envioActual.Agencia,
                        .Cliente = envioActual.Cliente,
                        .Contacto = envioActual.Contacto,
                        .Pedido = envioActual.Pedido,
                        .Fecha = envioActual.Fecha,
                        .FechaEntrega = envioActual.FechaEntrega,
                        .Servicio = agenciaEspecifica.ServicioAuxiliar,
                        .Horario = envioActual.Horario,
                        .Bultos = 1,
                        .Retorno = envioActual.Retorno,
                        .Nombre = envioActual.Nombre,
                        .Direccion = envioActual.Direccion,
                        .CodPostal = envioActual.CodPostal,
                        .Poblacion = envioActual.Poblacion,
                        .Provincia = envioActual.Provincia,
                        .Pais = envioActual.Pais,
                        .Telefono = envioActual.Telefono,
                        .Movil = envioActual.Movil,
                        .Email = envioActual.Email,
                        .Observaciones = envioActual.Observaciones,
                        .Atencion = envioActual.Atencion,
                        .Reembolso = 0,
                        .Vendedor = envioActual.Vendedor
                    }
                    servicio.Insertar(envioEtiquetaRetorno)
                    servicio.Borrar(envioEtiquetaRetorno.Numero)
                End If

                If Not IsNothing(envioActual.CodigoBarras) Then
                    transaction.Complete()
                    success = True ' Marcamos correctas las transacciones
                Else
                    transaction.Dispose()
                    success = False
                End If

            Catch ex As Exception
                transaction.Dispose()
                NotificationRequest.Raise(New Notification() With {
                         .Title = "¡Error! Se ha producido un error y no se han grabado los datos",
                        .Content = ex.Message
                    })
            End Try

            If Not success Then
                NotificationRequest.Raise(New Notification() With {
                         .Title = "¡Error!",
                        .Content = "Se ha producido un error y no se ha creado la etiqueta correctamente"
                    })
            End If

        End Using ' finaliza la transacción

        If success AndAlso Not servicio.EsTodoElPedidoOnline(envioActual.Empresa, envioActual.Pedido) Then
            servicio.EnviarCorreoEntregaAgencia(EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envioActual))
        End If
    End Sub

    Private Function buscarEnvioPendiente(pedidoSeleccionado As CabPedidoVta) As EnviosAgencia
        Dim envio As EnviosAgencia = servicio.CargarEnvio(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
        Return envio
    End Function

    Private Function CalcularMovimientoDesliq(env As EnviosAgencia, importeAnterior As Double) As ExtractoCliente
        Dim movimientos As ObservableCollection(Of ExtractoCliente)
        Dim concepto As String = servicio.GenerarConcepto(env)

        movimientos = servicio.CargarPagoExtractoClientePorEnvio(env, concepto, importeAnterior)

        If movimientos.Count = 0 Then
            Return Nothing
        Else
            Return movimientos.LastOrDefault
        End If
    End Function
    Private Function ConfigurarAgenciaPedido() As AgenciasTransporte
        ' agenciaConfigurar es agenciaSeleccionada. Lo pongo por si se busca agenciaSeleccionada.
        If IsNothing(pedidoSeleccionado) OrElse IsNothing(pedidoSeleccionado.Empresa) OrElse IsNothing(listaAgencias) Then
            Return agenciaSeleccionada
        End If

        'Return listaAgencias.Single(Function(a) a.Empresa = pedidoSeleccionado.Empresa AndAlso a.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS)

        If reembolso <> 0 AndAlso Not IsNothing(pedidoSeleccionado.IVA) AndAlso pedidoSeleccionado.Empresa <> Constantes.Empresas.EMPRESA_ESPEJO AndAlso Constantes.Agencias.AGENCIA_REEMBOLSOS <> String.Empty Then
            Return listaAgencias.Single(Function(a) a.Empresa = pedidoSeleccionado.Empresa AndAlso a.Nombre = Constantes.Agencias.AGENCIA_REEMBOLSOS)
        End If

        Dim agenciaNueva As AgenciasTransporte

        If IsNothing(pedidoSeleccionado.Ruta) AndAlso Not IsNothing(agenciaSeleccionada) Then
            pedidoSeleccionado.Ruta = agenciaSeleccionada.Ruta
        End If

        ' Carlos 16/09/15. Ponemos cobros de agencia en efectivo.
        If IsNothing(pedidoSeleccionado.IVA) AndAlso importeReembolso(pedidoSeleccionado) > 0 Then
            agenciaNueva = servicio.CargarAgenciaPorRuta(Constantes.Empresas.EMPRESA_ESPEJO, pedidoSeleccionado.Ruta)
        Else
            agenciaNueva = servicio.CargarAgenciaPorRuta(pedidoSeleccionado.Empresa, pedidoSeleccionado.Ruta)
        End If

        ' Carlos 22/09/15. Para que se puedan meter reembolsos
        If IsNothing(agenciaNueva) Then
            Dim cliente As Clientes = servicio.CargarCliente(pedidoSeleccionado.Empresa, pedidoSeleccionado.Nº_Cliente, pedidoSeleccionado.Contacto)
            agenciaNueva = servicio.CargarListaAgencias(pedidoSeleccionado.Empresa).OrderByDescending(Function(o) o.Numero).FirstOrDefault(Function(a) a.Ruta = cliente.Ruta)
        End If

        If Not IsNothing(agenciaNueva) AndAlso (IsNothing(agenciaSeleccionada) OrElse (agenciaSeleccionada.Numero <> agenciaNueva.Numero Or agenciaSeleccionada.Empresa <> agenciaNueva.Empresa)) Then
            Return agenciaNueva
            'empresaSeleccionada = listaEmpresas.Where(Function(e) e.Número = agenciaNueva.Empresa).Single()
            'agenciaConfigurar = listaAgencias.Single(Function(a) a.Numero = agenciaNueva.Numero)
        End If

        If (IsNothing(agenciaSeleccionada) AndAlso IsNothing(agenciaNueva)) OrElse agenciaSeleccionada.Empresa <> pedidoSeleccionado.Empresa Then
            Return listaAgencias.LastOrDefault()
        End If

        Return agenciaSeleccionada
    End Function
    Private Sub modificarEnvio(ByRef envio As EnviosAgencia, reembolso As Double, retorno As tipoIdDescripcion, estado As Integer, fechaEntrega As Date)
        modificarEnvio(envio, reembolso, retorno, estado, False, fechaEntrega)
    End Sub
    Private Sub modificarEnvio(ByRef envio As EnviosAgencia, reembolso As Double, retorno As tipoIdDescripcion, estado As Integer, rehusar As Boolean, fechaEntrega As Date)
        Dim historia As New EnviosHistoria
        Dim modificado As Boolean = False
        Dim reembolsoAnterior As Double = envio.Reembolso

        ' Carlos 14/12/16: no se pueden modificar los envíos que estén cobrados
        If Not IsNothing(envio.FechaPagoReembolso) Then
            NotificationRequest.Raise(New Notification() With {
                     .Title = "¡Error!",
                    .Content = "No se puede modificar este envío, porque ya está cobrado"
                })
            Return
        End If

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
            Using DbContext As New NestoEntities
                Dim numeroEnvio As Integer = envio.Numero ' porque no me deja usar envio en una lambda
                Dim envioEncontrado As EnviosAgencia = DbContext.EnviosAgencia.Where(Function(e) e.Numero = numeroEnvio).Single
                Try

                    If envio.Reembolso <> reembolso Then

                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "Reembolso"
                        historia.ValorAnterior = envio.Reembolso.ToString("C")
                        'reembolsoAnterior = envio.Reembolso
                        envioEncontrado.Reembolso = reembolso
                        DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If
                    If envio.Retorno <> retorno.id Then
                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "Retorno"
                        Dim tipoEnvioAnterior As Byte = envio.Retorno
                        historia.ValorAnterior = (From l In listaTiposRetorno Where l.id = tipoEnvioAnterior Select l.descripcion).FirstOrDefault
                        envioEncontrado.Retorno = retorno.id
                        DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If
                    If envio.Estado <> estado Then
                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "Estado"
                        historia.ValorAnterior = envio.Estado
                        envioEncontrado.Estado = estado
                        DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If
                    If envio.FechaEntrega <> fechaEntrega Then
                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "FechaEntrega"
                        historia.ValorAnterior = envio.FechaEntrega.ToString
                        envioEncontrado.FechaEntrega = fechaEntrega
                        DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If

                    If modificado Then
                        historia.Observaciones = observacionesModificacion
                        'DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                        If DbContext.SaveChanges Then
                            If reembolsoAnterior <> reembolso Then
                                contabilizarModificacionReembolso(envio, reembolsoAnterior, reembolso)
                                ''DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                                'If Not DbContext.SaveChanges() Then
                                '    Throw New Exception("No se ha podido contabilizar la modificación del reembolso")
                                'End If
                            End If
                        Else
                            Throw New Exception("No se han podido guardar los cambios")
                        End If
                        ' Si el envío está en listaReembolsos lo actualizamos
                        Dim envioLista As EnviosAgencia = listaEnviosTramitados.Where(Function(l) l.Numero = numeroEnvio).SingleOrDefault
                        If Not IsNothing(envioLista) Then
                            envioLista.Reembolso = reembolso
                            envioLista.Retorno = retorno.id
                            envioLista.Estado = estado
                            envioLista.FechaEntrega = fechaEntrega
                        End If
                    End If

                    If rehusar Then
                        Dim movimientoFactura As ExtractoCliente = servicio.CalcularMovimientoLiq(envio, reembolsoAnterior)
                        Dim estadoRehusado As New ObjectParameter("Estado", GetType(String))
                        estadoRehusado.Value = "RHS"
                        envio.Retorno = retorno.id
                        DbContext.SaveChanges()
                        DbContext.prdModificarEfectoCliente(movimientoFactura.Nº_Orden, movimientoFactura.FechaVto, movimientoFactura.CCC, movimientoFactura.Ruta, estadoRehusado, movimientoFactura.Concepto)
                    End If



                    transaction.Complete()
                    success = True

                Catch ex As Exception
                    'DbContext.DeleteObject(lineaInsertar) 'Lo suyo sería hacer una transacción con todo
                    'DbContext.SaveChanges()
                    transaction.Dispose()
                    success = False
                    listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)(From e In DbContext.EnviosAgencia.Include("AgenciasTransporte") Where e.Empresa = empresaSeleccionada.Número And e.Fecha = fechaFiltro And e.Estado = Constantes.Agencias.ESTADO_TRAMITADO_ENVIO Order By e.Fecha Descending)
                    envioActual = listaEnviosTramitados.FirstOrDefault
                End Try

                ' Comprobamos que las transacciones sean correctas
                If success Then
                    ' Reset the context since the operation succeeded. 
                    DbContext.SaveChanges()
                    envio = envioEncontrado
                    OnPropertyChanged("listaEnviosTramitados")
                Else
                    NotificationRequest.Raise(New Notification() With {
                     .Title = "¡Error!",
                    .Content = "Se ha producido un error y no se grabado los datos"
                })
                End If
            End Using ' cerramos el contexto breve
        End Using ' Cerramos la transaccion

    End Sub
    Public Function contabilizarModificacionReembolso(envio As EnviosAgencia, importeAnterior As Double, importeNuevo As Double) As Integer

        ' Parámetro Rehusar es para marcar el ExtractoCliente como RHS (rehusado)

        Const diarioReembolsos As String = "_Reembolso"

        Dim agenciaEnvio As AgenciasTransporte = servicio.CargarAgencia(envio.Agencia)
        If IsNothing(agenciaEnvio.CuentaReembolsos) Then
            mensajeError = "Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar."
            Return -1
        End If

        Dim empresaEnvio As Empresas = servicio.CargarListaEmpresas().Where(Function(e) e.Número = envio.Empresa).Single

        Dim lineaDeshago, lineaRehago As New PreContabilidad
        Dim asiento As Integer
        Dim movimientoLiq As ExtractoCliente
        Dim movimientoDesliq As ExtractoCliente
        'movimientoLiq = calcularMovimientoLiq(envio)

        ' Iniciamos transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities

                Try
                    ' desliquidamos el reembolso
                    movimientoDesliq = CalcularMovimientoDesliq(envio, importeAnterior)
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
                            .Concepto = Left("Deshago Reembolso " + envio.Pedido.ToString + " a " + agenciaEnvio.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
                            .Contrapartida = agenciaEnvio.CuentaReembolsos.Trim
                            .Asiento_Automático = False
                            .FormaPago = empresaEnvio.FormaPagoEfectivo
                            .Vendedor = envio.Vendedor
                            .Nº_Documento = envio.Pedido
                            .Delegación = empresaEnvio.DelegaciónVarios
                            .FormaVenta = empresaEnvio.FormaVentaVarios
                            If Not IsNothing(movimientoDesliq) Then
                                .Liquidado = movimientoDesliq.Nº_Orden
                            End If
                        End With
                    End If

                    movimientoLiq = servicio.CalcularMovimientoLiq(envio)

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
                            .Concepto = Left("Rehago Reembolso " + envio.Pedido.ToString + " a " + agenciaEnvio.Nombre.Trim + " c/" + envio.Cliente.Trim, 50)
                            .Contrapartida = agenciaEnvio.CuentaReembolsos.Trim
                            .Asiento_Automático = False
                            .FormaPago = empresaEnvio.FormaPagoEfectivo
                            .Vendedor = envio.Vendedor

                            .Nº_Documento = envio.Pedido
                            .Delegación = empresaEnvio.DelegaciónVarios
                            .FormaVenta = empresaEnvio.FormaVentaVarios
                            If Not IsNothing(movimientoLiq) AndAlso movimientoLiq.ImportePdte > 0 Then
                                .Liquidado = movimientoLiq.Nº_Orden
                            End If
                        End With
                    End If


                    If importeAnterior <> 0 Then
                        DbContext.PreContabilidad.Add(lineaDeshago)
                    End If
                    If importeNuevo <> 0 Then
                        DbContext.PreContabilidad.Add(lineaRehago)
                    End If
                    'DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                    If DbContext.SaveChanges() Then
                        asiento = DbContext.prdContabilizar(envio.Empresa, diarioReembolsos)
                        transaction.Complete()
                        success = True
                    Else
                        transaction.Dispose()
                        success = False
                    End If

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
                    DbContext.SaveChanges()
                Else
                    NotificationRequest.Raise(New Notification() With {
                     .Title = "¡Error!",
                    .Content = "Se ha producido un error y no se han grabado los datos"
                })
                    Return -1
                End If
            End Using ' Cerramos contexto breve
        End Using ' Cerramos transacción

        Return asiento
    End Function
    Private Function buscarPedidoAmpliacion(pedido As CabPedidoVta) As EnviosAgencia
        Dim direccion As String
        If Not String.IsNullOrWhiteSpace(direccionEnvio) Then
            direccion = direccionEnvio
        Else
            direccion = pedido.Clientes.Dirección
        End If
        Dim pedidoEncontrado As EnviosAgencia = servicio.CargarEnvioPorClienteYDireccion(pedido.Nº_Cliente, pedido.Contacto, direccion)
        If IsNothing(pedidoEncontrado) Then
            ' Si no es ampliación devolvemos un envío nuevo
            Return New EnviosAgencia
        Else
            'si sí es ampliación devolvemos el registro que está metido
            Return pedidoEncontrado
        End If
    End Function
    Private Function calcularDeuda() As Double
        Dim deudas As List(Of ExtractoCliente)
        Dim fechaReclamar As Date = Today.AddDays(-7)
        deudas = servicio.CargarDeudasCliente(pedidoSeleccionado.Nº_Cliente, fechaReclamar)
        If Not deudas.Any Then
            Return 0
        End If

        Return Math.Round(
            (Aggregate l In deudas
            Select l.ImportePdte Into Sum()) _
            , 2, MidpointRounding.AwayFromZero)
    End Function
    'Private Function EstamosEditandoEnvioPendiente() As Boolean
    '    ' Hay que cambiar el nombre a la función, porqeu ya no existe contextPendientes
    '    ' TODO: tiene que devolver true solo si estamos editando o añadiendo un envío
    '    Return Not IsNothing(EnvioPendienteSeleccionado) AndAlso EnvioPendienteSeleccionado.Numero = 0
    'End Function
    Private Function HayCambiosSinGuardarEnPendientes() As Boolean
        If IsNothing(listaPendientes) Then
            Return False
        End If
        Dim changes = listaPendientes.Where(Function(l) l.TieneCambios = True)
        Return changes.Any
    End Function
    Private Sub ActualizarEstadoComandos()
        InsertarEnvioPendienteCommand.RaiseCanExecuteChanged()
        BorrarEnvioPendienteCommand.RaiseCanExecuteChanged()
        GuardarEnvioPendienteCommand.RaiseCanExecuteChanged()
    End Sub
    Public Sub EnvioPendienteSeleccionadoPropertyChangedEventHandler(sender As Object, e As PropertyChangedEventArgs)
        Dim envio = CType(sender, EnvioAgenciaWrapper)
        If e.PropertyName = "Pedido" Then
            CopiarDatosPedidoOriginal(envio.Pedido)
        End If
        If e.PropertyName <> "TieneCambios" Then
            envio.TieneCambios = True
            GuardarEnvioPendienteCommand.RaiseCanExecuteChanged()
            BorrarEnvioPendienteCommand.RaiseCanExecuteChanged()
        End If
    End Sub

    Private Sub CopiarDatosPedidoOriginal(numeroPedido As Integer?)
        If IsNothing(numeroPedido) Then
            Return
        End If
        Dim pedido As CabPedidoVta = servicio.CargarPedido(empresaSeleccionada.Número, numeroPedido)

        If IsNothing(pedido) Then
            NotificationRequest.Raise(New Notification() With {
                 .Title = "Error",
                .Content = "No se encuentra el pedido " + numeroPedido.ToString
            })
            Return
        End If

        With EnvioPendienteSeleccionado
            .Cliente = pedido.Nº_Cliente
            .Contacto = pedido.Contacto
            .Direccion = If(pedido.Clientes.Dirección IsNot Nothing, pedido.Clientes.Dirección.Trim, "")
            .CodPostal = If(pedido.Clientes.CodPostal IsNot Nothing, pedido.Clientes.CodPostal.Trim, "")
            .Email = correoUnico(pedido.Clientes.PersonasContactoCliente.ToList)
            .Fecha = pedido.Fecha
            .FechaEntrega = pedido.Fecha
            .Nombre = If(pedido.Clientes.Nombre IsNot Nothing, pedido.Clientes.Nombre.Trim, "")
            .Atencion = If(pedido.Clientes.Nombre IsNot Nothing, pedido.Clientes.Nombre.Trim, "")
            .Observaciones = pedido.Comentarios
            .Poblacion = If(pedido.Clientes.Población IsNot Nothing, pedido.Clientes.Población.Trim, "")
            .Provincia = If(pedido.Clientes.Provincia IsNot Nothing, pedido.Clientes.Provincia.Trim, "")
            If pedido.Clientes.Teléfono IsNot Nothing Then
                .Telefono = telefonoUnico(pedido.Clientes.Teléfono.Trim, "F")
                .Movil = telefonoUnico(pedido.Clientes.Teléfono.Trim, "M")
            Else
                .Telefono = ""
                .Movil = ""
            End If
            .Reembolso = importeReembolso(pedido)
            .Pais = agenciaEspecifica.paisDefecto
            .Retorno = agenciaEspecifica.retornoSinRetorno
            .Servicio = agenciaEspecifica.servicioSoloCobros ' comprobar
            .Horario = agenciaEspecifica.horarioSoloCobros ' comprobar
        End With
    End Sub

#End Region

End Class

Public Structure tipoIdDescripcion
    Public Sub New(
   ByVal _id As Byte,
   ByVal _descripcion As String
   )
        id = _id
        descripcion = _descripcion
    End Sub
    Property id As Byte
    Property descripcion As String
End Structure

Public Structure tipoIdIntDescripcion
    Public Sub New(
   ByVal _id As Integer,
   ByVal _descripcion As String
   )
        id = _id
        descripcion = _descripcion
    End Sub
    Property id As Integer
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

Public Class Pais
    Public Sub New(id As Integer, nombre As String)
        Me.Id = id
        Me.Nombre = nombre
    End Sub

    Public Sub New(id As Integer, nombre As String, codigoAlfa As String)
        Me.Id = id
        Me.Nombre = nombre
        Me.CodigoAlfa = codigoAlfa
    End Sub

    Public ReadOnly Property Id As Integer
    Public ReadOnly Property Nombre As String
    Public ReadOnly Property CodigoAlfa As String
End Class
#End Region

Public Class Pestannas
    Public Const PEDIDOS As String = "tabPedidos"
    Public Const PENDIENTES As String = "tabPendientes"
    Public Const EN_CURSO As String = "tabCurso"
    Public Const TRAMITADOS As String = "tabTramitados"
    Public Const REEMBOLSOS As String = "tabReembolsos"
    Public Const RETORNOS As String = "tabRetornos"
    Public Const ETIQUETAS As String = "tabEtiquetas"
End Class