﻿Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Data
Imports System.Data.Entity.Core.Objects
Imports System.Data.Entity.Validation
Imports System.Data.SqlClient
Imports System.IO
Imports System.Reflection
Imports System.Text.RegularExpressions
Imports System.Transactions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports ControlesUsuario.Dialogs
Imports Microsoft.Reporting.NETCore
Imports Microsoft.Win32
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.Cajas.Interfaces
Imports Nesto.Modulos.PedidoVenta
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Regions
Imports Prism.Services.Dialogs

Public Class AgenciasViewModel
    Inherits BindableBase
    '    Implements IActiveAware

    ' El modo cuadre sirve para cuadrar los saldos iniciales de cada agencia con la contabilidad
    ' En este modo al contabilizar los reembolsos no se toca la contabilidad, pero sí se pone la 
    ' fecha de cobro a 01/01/15
    Private Const MODO_CUADRE = False

    Private Const LONGITUD_TELEFONO = 15

    Private ReadOnly _regionManager As IRegionManager
    Private ReadOnly _servicio As IAgenciaService
    Private ReadOnly _configuracion As IConfiguracion
    Public ReadOnly _dialogService As IDialogService
    Private ReadOnly _servicioPedidos As IPedidoVentaService
    Private ReadOnly _contabilidadService As IContabilidadService

    Private empresaDefecto As String

    Private ReadOnly factory As New Dictionary(Of String, Func(Of IAgencia))

    Private imprimirEtiqueta As Boolean
    Private EstaInsertandoEnvio As Boolean
    Private _estaCambiandoDePedido As Boolean

    Public Sub New(regionManager As IRegionManager, servicio As IAgenciaService, configuracion As IConfiguracion, dialogService As IDialogService, servicioPedidos As IPedidoVentaService)
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If

        _regionManager = regionManager
        _servicio = servicio
        _configuracion = configuracion
        _dialogService = dialogService
        _servicioPedidos = servicioPedidos

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

        factory.Add("ASM", Function() New AgenciaASM(Me))
        'factory.Add("OnTime", Function() New AgenciaOnTime(Me))
        'factory.Add("Glovo", Function() New AgenciaGlovo(Me))
        factory.Add("Correos Express", Function() New AgenciaCorreosExpress())
        factory.Add("Sending", Function() New AgenciaSending())
    End Sub


    Public Event SolicitarFocoNumeroPedido As EventHandler

#Region "Propiedades"
    ' Carlos 03/09/14
    ' Las propiedades que terminan en "envio" son las que se usan de manera temporal para que el usuario
    ' pueda modificar los datos. Por ejemplo, nombreEnvio se actualiza con el nombre del cliente cada vez que
    ' cambiamos de pedido, pero el usuario puede modificarlas. En el momento de hacer la inserción en la tabla
    ' EnviosAgencia coge el valor que tenga esta propiedad. Así permitimos hacer excepciones y no hay que 
    ' mandarlo siempre con el valor que tiene el campo en la tabla.
    ' Carlos 26/10/17 -> estas propiedades "envio" habría que quitarlas y crear EnvioAgenciaWrapper donde
    ' tengamos todas esas propiedades con un PropertyChanged y change tracking

    Public Shared Sub CrearEtiquetaPendiente(etiqueta As EnvioAgenciaWrapper, regionManager As IRegionManager, configuracion As IConfiguracion, dialogService As IDialogService)
        Dim agenciasVM = New AgenciasViewModel(regionManager, New AgenciaService(configuracion, dialogService), configuracion, dialogService, New PedidoVentaService(configuracion))
        agenciasVM.InsertarEnvioPendienteCommand.Execute()
        If etiqueta.Agencia = 0 Then
            agenciasVM.agenciaSeleccionada = agenciasVM.listaAgencias.Single(Function(a) a.Numero = 1) ' ASM/GLS
        Else
            agenciasVM.agenciaSeleccionada = agenciasVM.listaAgencias.Single(Function(a) a.Numero = etiqueta.Agencia)
        End If
        agenciasVM.EnvioPendienteSeleccionado.Pedido = etiqueta.Pedido
        agenciasVM.EnvioPendienteSeleccionado.Agencia = agenciasVM.agenciaSeleccionada.Numero
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

        ' Esto hay que refactorizarlo para llevar la lógica a cada agencia
        If agenciasVM.EnvioPendienteSeleccionado.Agencia = 8 Then 'CEX
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
        ElseIf agenciasVM.EnvioPendienteSeleccionado.Agencia = 10 Then  'Sending
            Dim codigoAlfaEtiqueta As String = String.Empty
            If etiqueta.PaisISO = "ES" Then
                codigoAlfaEtiqueta = "034"
            ElseIf etiqueta.PaisISO = "PT" Then
                codigoAlfaEtiqueta = "035"
            End If
            Dim pais As Pais = agenciasVM.listaPaises.SingleOrDefault(Function(p) p.CodigoAlfa = codigoAlfaEtiqueta)
            If Not IsNothing(pais) Then
                agenciasVM.EnvioPendienteSeleccionado.Pais = pais.Id
            End If
            agenciasVM.EnvioPendienteSeleccionado.Horario = agenciasVM.listaHorarios.FirstOrDefault().id
            agenciasVM.EnvioPendienteSeleccionado.Servicio = agenciasVM.listaServicios.FirstOrDefault().ServicioId
        ElseIf agenciasVM.EnvioPendienteSeleccionado.Agencia = 1 Then ' GLS
            agenciasVM.EnvioPendienteSeleccionado.Servicio = 96 'BusinessParcel
            agenciasVM.EnvioPendienteSeleccionado.Horario = 18
        Else
            Throw New Exception("Agencia no contemplada")
        End If

        agenciasVM.GuardarEnvioPendienteCommand.Execute()
    End Sub

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_titulo, value)
        End Set
    End Property

    '*** Propiedades de Nesto
    Private _resultadoWebservice As String
    Public Property resultadoWebservice As String
        Get
            Return _resultadoWebservice
        End Get
        Set(value As String)
            Dim unused = SetProperty(_resultadoWebservice, value)
        End Set
    End Property

    Private _listaAgencias As ObservableCollection(Of AgenciasTransporte)
    Public Property listaAgencias As ObservableCollection(Of AgenciasTransporte)
        Get
            Return _listaAgencias
        End Get
        Set(value As ObservableCollection(Of AgenciasTransporte))
            Dim unused = SetProperty(_listaAgencias, value)
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
                Dim unused = SetProperty(_agenciaSeleccionada, value)
                If Not IsNothing(value) Then
                    agenciaEspecifica = factory(value.Nombre).Invoke
                    numClienteContabilizar = agenciaEspecifica.NumeroCliente
                    If String.IsNullOrEmpty(PestannaNombre) Then
                        Return
                    End If
                    If PestannaNombre = Pestannas.PEDIDOS OrElse PestannaNombre = Pestannas.EN_CURSO OrElse PestannaNombre = Pestannas.ETIQUETAS Then
                        ActualizarListas()
                        Dim nombrePais As String = paisActual?.Nombre
                        paisActual = If(Not IsNothing(nombrePais) AndAlso value.Nombre = Constantes.Agencias.AGENCIA_INTERNACIONAL,
                            listaPaises.Single(Function(p) p.Nombre = nombrePais),
                            listaPaises.Single(Function(p) p.Id = agenciaEspecifica.paisDefecto))
                        retornoActual = listaTiposRetorno.Single(Function(r) r.id = agenciaEspecifica.retornoSinRetorno)
                        servicioActual = listaServicios.Single(Function(s) s.ServicioId = agenciaEspecifica.ServicioDefecto)
                        horarioActual = listaHorarios.Single(Function(h) h.id = agenciaEspecifica.HorarioDefecto)
                    End If
                    If PestannaNombre = Pestannas.PENDIENTES Then
                        ActualizarListas()
                        If Not IsNothing(EnvioPendienteSeleccionado) AndAlso Not _estaCambiandoDePedido Then
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
                    If PestannaNombre = Pestannas.PEDIDOS AndAlso Not IsNothing(empresaSeleccionada) AndAlso Not IsNothing(pedidoSeleccionado) Then
                        listaEnviosPedido = _servicio.CargarListaEnviosPedido(empresaSeleccionada.Número, pedidoSeleccionado.Número)
                    End If
                    If PestannaNombre = Pestannas.EN_CURSO Then
                        listaEnvios = _servicio.CargarListaEnvios(value.Numero)
                        If Not IsNothing(envioActual) AndAlso envioActual.Agencia <> value.Numero Then
                            envioActual = listaEnvios.FirstOrDefault
                        End If
                    End If
                    If PestannaNombre = Pestannas.REEMBOLSOS Then
                        listaReembolsos = _servicio.CargarListaReembolsos(empresaSeleccionada.Número, value.Numero)
                    End If
                    If PestannaNombre = Pestannas.RETORNOS Then
                        listaRetornos = _servicio.CargarListaRetornos(empresaSeleccionada.Número, value.Numero, agenciaEspecifica.retornoSinRetorno)
                    End If
                    If PestannaNombre = Pestannas.TRAMITADOS AndAlso String.IsNullOrEmpty(nombreFiltro) Then
                        listaEnviosTramitados = _servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, value.Numero, fechaFiltro)
                    End If

                    RaisePropertyChanged("") ' para que actualice todos los enlaces
                End If
            Catch ex As Exception
                _dialogService.ShowError("No se encuentra la implementación de la agencia " + value.Nombre)
                RaisePropertyChanged(NameOf(agenciaSeleccionada))
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
            Dim unused = SetProperty(_empresaSeleccionada, value)
            Try
                listaAgencias = _servicio.CargarListaAgencias(empresaSeleccionada.Número)
                If numeroPedido = "" Then
                    agenciaSeleccionada = listaAgencias.FirstOrDefault
                End If
                If Not IsNothing(agenciaSeleccionada) Then
                    If PestannaNombre = Pestannas.EN_CURSO Then
                        listaEnvios = _servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
                    End If
                    If PestannaNombre = Pestannas.REEMBOLSOS Then
                        listaReembolsos = _servicio.CargarListaReembolsos(empresaSeleccionada.Número, agenciaSeleccionada.Numero)
                    End If
                    If PestannaNombre = Pestannas.RETORNOS Then
                        listaRetornos = _servicio.CargarListaRetornos(empresaSeleccionada.Número, agenciaSeleccionada.Numero, agenciaEspecifica.retornoSinRetorno)
                    End If
                    If PestannaNombre = Pestannas.TRAMITADOS Then
                        listaEnviosTramitados = _servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, agenciaSeleccionada.Numero, fechaFiltro)
                    End If
                Else
                    listaEnvios = New ObservableCollection(Of EnviosAgencia)
                    listaEnviosTramitados = New ObservableCollection(Of EnviosAgencia)
                    listaReembolsos = New ObservableCollection(Of EnviosAgencia)
                    listaRetornos = New ObservableCollection(Of EnviosAgencia)
                End If
                listaReembolsosSeleccionados = New ObservableCollection(Of EnviosAgencia)

                RaisePropertyChanged(NameOf(sumaContabilidad))
                RaisePropertyChanged(NameOf(descuadreContabilidad))
                RaisePropertyChanged(NameOf(etiquetaBultosTramitados))
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
            Dim unused1 = SetProperty(_pedidoSeleccionado, value)

            If Not IsNothing(cmdInsertar) Then
                cmdInsertar.RaiseCanExecuteChanged()
            End If

            Dim unused = ActualizarPedidoSeleccionado()
        End Set
    End Property

    Private Async Function ActualizarPedidoSeleccionado() As Task
        If Not IsNothing(pedidoSeleccionado) Then
            Try
                Dim cliente = _servicio.CargarPedido(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número).Clientes
                reembolso = Await _servicio.ImporteReembolso(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
                bultos = 1
                nombreEnvio = If(cliente.Nombre IsNot Nothing, cliente.Nombre.Trim, "")
                direccionEnvio = If(cliente.Dirección IsNot Nothing, cliente.Dirección.Trim, "")
                poblacionEnvio = If(cliente.Población IsNot Nothing, cliente.Población.Trim, "")
                provinciaEnvio = If(cliente.Provincia IsNot Nothing, cliente.Provincia.Trim, "")
                codPostalEnvio = If(cliente.CodPostal IsNot Nothing, cliente.CodPostal.Trim, "")
                Dim telefono As New Telefono(cliente.Teléfono)
                telefonoEnvio = telefono.FijoUnico
                movilEnvio = telefono.MovilUnico
                correoEnvio = correoUnico()
                observacionesEnvio = pedidoSeleccionado.Comentarios
                attEnvio = nombreEnvio
                fechaEnvio = If(empresaSeleccionada?.FechaPicking, Today)
                listaEnviosPedido = _servicio.CargarListaEnviosPedido(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
                envioActual = listaEnviosPedido.LastOrDefault

                Dim envioPendiente As EnviosAgencia = buscarEnvioPendiente(pedidoSeleccionado)
                Dim estabaPendiente As Boolean = Not IsNothing(envioPendiente)
                Dim agenciaConfigurar = If(estabaPendiente, envioPendiente.AgenciasTransporte, DirectCast(ConfigurarAgenciaPedido(), Object))

                If Not IsNothing(agenciaConfigurar) AndAlso (IsNothing(empresaSeleccionada) OrElse agenciaConfigurar.Empresa <> empresaSeleccionada.Número) AndAlso Not IsNothing(listaEmpresas) Then
                    empresaSeleccionada = listaEmpresas.Single(Function(e) e.Número = agenciaConfigurar.Empresa)
                End If
                If Not IsNothing(listaAgencias) AndAlso Not IsNothing(agenciaConfigurar) Then
                    agenciaSeleccionada = listaAgencias.Single(Function(a) a.Numero = agenciaConfigurar.Numero)
                End If
            Catch ex As Exception
                reembolso = 0
                bultos = 1
                nombreEnvio = String.Empty
                direccionEnvio = String.Empty
                poblacionEnvio = String.Empty
                provinciaEnvio = String.Empty
                codPostalEnvio = String.Empty
                telefonoEnvio = String.Empty
                movilEnvio = String.Empty
                correoEnvio = String.Empty
                observacionesEnvio = String.Empty
                attEnvio = String.Empty
                fechaEnvio = Today
                _dialogService.ShowError(ex.Message)
            End Try

        Else
            numeroPedido = 36
        End If
    End Function

    Private _listaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
    Public Property listaTiposRetorno As ObservableCollection(Of tipoIdDescripcion)
        Get
            Return _listaTiposRetorno
        End Get
        Set(value As ObservableCollection(Of tipoIdDescripcion))
            Dim unused = SetProperty(_listaTiposRetorno, value)
            RaisePropertyChanged(NameOf(retornoModificar))
        End Set
    End Property

    Private _retornoActual As tipoIdDescripcion
    Public Property retornoActual As tipoIdDescripcion
        Get
            Return _retornoActual
        End Get
        Set(value As tipoIdDescripcion)
            Dim unused = SetProperty(_retornoActual, value)
        End Set
    End Property

    Private _reembolso As Decimal
    Public Property reembolso As Decimal
        Get
            Return _reembolso
        End Get
        Set(value As Decimal)
            Dim unused = SetProperty(_reembolso, value)
        End Set
    End Property

    Private _importeAsegurado As Decimal
    Public Property importeAsegurado As Decimal
        Get
            Return _importeAsegurado
        End Get
        Set(value As Decimal)
            Dim unused = SetProperty(_importeAsegurado, value)
        End Set
    End Property

    Private _bultos As Integer
    Public Property bultos As Integer
        Get
            Return _bultos
        End Get
        Set(value As Integer)
            Dim unused = SetProperty(_bultos, value)
        End Set
    End Property

    Private _peso As Decimal
    Public Property Peso As Decimal
        Get
            Return _peso
        End Get
        Set(value As Decimal)
            If SetProperty(_peso, value) Then
                Dim agenciaConfigurar = ConfigurarAgenciaPedido()
                If Not IsNothing(agenciaConfigurar) Then
                    agenciaSeleccionada = listaAgencias.Single(Function(a) a.Numero = agenciaConfigurar.Numero)
                End If
            End If
        End Set
    End Property

    Private _costeEnvio As Decimal
    Public Property CosteEnvio As Decimal
        Get
            Return _costeEnvio
        End Get
        Set(value As Decimal)
            Dim unused = SetProperty(_costeEnvio, value)
        End Set
    End Property

    Private _mensajeError As String
    Public Property mensajeError As String
        Get
            Return _mensajeError
        End Get
        Set(value As String)
            Dim unused = SetProperty(_mensajeError, value)
        End Set
    End Property

    Private _listaServicios As ObservableCollection(Of ITarifaAgencia)
    Public Property listaServicios As ObservableCollection(Of ITarifaAgencia)
        Get
            Return _listaServicios
        End Get
        Set(value As ObservableCollection(Of ITarifaAgencia))
            Dim unused = SetProperty(_listaServicios, value)
        End Set
    End Property

    Private _servicioActual As ITarifaAgencia
    Public Property servicioActual As ITarifaAgencia
        Get
            Return _servicioActual
        End Get
        Set(value As ITarifaAgencia)
            Dim unused = SetProperty(_servicioActual, value)
        End Set
    End Property

    Private _listaPaises As ObservableCollection(Of Pais)
    Public Property listaPaises() As ObservableCollection(Of Pais)
        Get
            Return _listaPaises
        End Get
        Set(ByVal value As ObservableCollection(Of Pais))
            Dim unused = SetProperty(_listaPaises, value)
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
            Dim unused = SetProperty(_paisActual, value)
            If _paisActual.Id <> agenciaEspecifica.paisDefecto AndAlso agenciaSeleccionada.Nombre <> Constantes.Agencias.AGENCIA_INTERNACIONAL Then
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
            Dim unused = SetProperty(_listaHorarios, value)
        End Set
    End Property

    Private _horarioActual As tipoIdDescripcion
    Public Property horarioActual As tipoIdDescripcion
        Get
            Return _horarioActual
        End Get
        Set(value As tipoIdDescripcion)
            Dim unused = SetProperty(_horarioActual, value)
        End Set
    End Property

    Private _nombreEnvio As String
    Public Property nombreEnvio As String
        Get
            Return _nombreEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_nombreEnvio, value)
        End Set
    End Property

    Private _direccionEnvio As String
    Public Property direccionEnvio As String
        Get
            Return _direccionEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_direccionEnvio, value)
        End Set
    End Property

    Private _poblacionEnvio As String
    Public Property poblacionEnvio As String
        Get
            Return _poblacionEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_poblacionEnvio, value)
        End Set
    End Property

    Private _provinciaEnvio As String
    Public Property provinciaEnvio As String
        Get
            Return _provinciaEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_provinciaEnvio, value)
        End Set
    End Property

    Private _codPostalEnvio As String
    Public Property codPostalEnvio As String
        Get
            Return _codPostalEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_codPostalEnvio, value)
        End Set
    End Property

    Private _telefonoEnvio As String
    Public Property telefonoEnvio As String
        Get
            Return _telefonoEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_telefonoEnvio, value)
        End Set
    End Property

    Private _movilEnvio As String
    Public Property movilEnvio As String
        Get
            Return _movilEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_movilEnvio, value)
        End Set
    End Property

    Private _correoEnvio As String
    Public Property correoEnvio As String
        Get
            Return _correoEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_correoEnvio, value)
        End Set
    End Property

    Private _observacionesEnvio As String
    Public Property observacionesEnvio As String
        Get
            Return _observacionesEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_observacionesEnvio, value)
        End Set
    End Property

    Private _attEnvio As String
    Public Property attEnvio As String
        Get
            Return _attEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_attEnvio, value)
        End Set
    End Property

    Private _fechaEnvio As Date
    Public Property fechaEnvio As Date
        Get
            Return _fechaEnvio
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaEnvio, value)
        End Set
    End Property

    Private _enlaceSeguimientoEnvio As String
    Public Property EnlaceSeguimientoEnvio As String
        Get
            Return _enlaceSeguimientoEnvio
        End Get
        Set(value As String)
            Dim unused = SetProperty(_enlaceSeguimientoEnvio, value)
            AbrirEnlaceSeguimientoCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _envioActual As EnviosAgencia
    Public Property envioActual As EnviosAgencia
        Get
            Return _envioActual
        End Get
        Set(value As EnviosAgencia)
            Dim unused = SetProperty(_envioActual, value)

            Try
                estadoEnvioCargado = Nothing
                mensajeError = ""
                Dim agenciaEnvio As AgenciasTransporte = Nothing
                If Not IsNothing(envioActual) AndAlso Not IsNothing(envioActual.Agencia) Then
                    agenciaEnvio = _servicio.CargarAgencia(envioActual.Agencia)
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
                    listaHistoriaEnvio = _servicio.CargarListaHistoriaEnvio(envioActual.Numero)
                Else
                    listaHistoriaEnvio = Nothing
                End If

                If Not IsNothing(cmdModificarEnvio) Then
                    cmdModificarEnvio.RaiseCanExecuteChanged()
                End If

                If Not IsNothing(cmdRehusarEnvio) Then
                    cmdRehusarEnvio.RaiseCanExecuteChanged()
                End If

                RaisePropertyChanged(NameOf(sePuedeModificarReembolso))
                RaisePropertyChanged(NameOf(sePuedeModificarEstado))
                RaisePropertyChanged(NameOf(agenciaSeleccionada))
            Catch ex As Exception
                Return
            End Try
            cmdModificar.RaiseCanExecuteChanged()
        End Set
    End Property

    Private Function CalcularZonaEnvio(codigoPostal As String) As ZonasEnvioAgencia
        codigoPostal = codigoPostal.Trim()
        ' Comprobar si es código postal de Portugal
        Dim regex As New Regex("^\d{4}[ -]\d{3}$")
        If regex.IsMatch(codigoPostal) Then
            Return ZonasEnvioAgencia.Portugal
        End If

        If codigoPostal.Length <> 5 OrElse codigoPostal = "EXTER" Then
            Return ZonasEnvioAgencia.Extranjero
        End If

        Dim codigosMallorcaMayores() As String = {
            "07001", "07002", "07003", "07004", "07005", "07006", "07007", "07008", "07009", "07010",
            "07011", "07012", "07013", "07014", "07015", "07070", "07071", "07080", "07120", "07121",
            "07122", "07198", "07199", "07600", "07610", "07611", "07710"
        }
        Dim codigosCanariasMayores() As String = {
            "38001", "38002", "38003", "38004", "38005", "38006", "38007", "38008", "38009", "38010",
            "38070", "38071", "38080", "38111", "38150", "38170",
            "35001", "35002", "35003", "35004", "35005", "35006", "35007", "35008", "35009", "35010",
            "35011", "35012", "35013", "35014", "35015", "35016", "35017", "35018", "35019", "35070",
            "35071", "35080", "35220", "35229"
        }
        If codigoPostal.StartsWith("28") Then
            Return ZonasEnvioAgencia.Provincial
        ElseIf codigoPostal.StartsWith("07") And Not codigosMallorcaMayores.Contains(codigoPostal) Then
            Return ZonasEnvioAgencia.BalearesMenores
        ElseIf codigosMallorcaMayores.Contains(codigoPostal) Then
            Return ZonasEnvioAgencia.BalearesMayores
        ElseIf (codigoPostal.StartsWith("35") OrElse codigoPostal.StartsWith("38")) And Not codigosCanariasMayores.Contains(codigoPostal) Then
            Return ZonasEnvioAgencia.CanariasMenores
        ElseIf codigosCanariasMayores.Contains(codigoPostal) Then
            Return ZonasEnvioAgencia.CanariasMayores
        Else
            Return ZonasEnvioAgencia.Peninsular
        End If
    End Function

    Private _envioPendienteSeleccionado As EnvioAgenciaWrapper
    Public Property EnvioPendienteSeleccionado() As EnvioAgenciaWrapper
        Get
            Return _envioPendienteSeleccionado
        End Get
        Set(ByVal value As EnvioAgenciaWrapper)
            Dim unused = SetProperty(_envioPendienteSeleccionado, value)
            _estaCambiandoDePedido = True
            If Not IsNothing(value) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso value.Agencia <> agenciaSeleccionada.Numero Then
                empresaSeleccionada = listaEmpresas.Single(Function(e) e.Número = value.Empresa)
                agenciaSeleccionada = listaAgencias.Single(Function(a) a.Numero = value.Agencia)
            End If
            _estaCambiandoDePedido = False
            RaisePropertyChanged(NameOf(HayUnEnvioPendienteSeleccionado))
            ActualizarEstadoComandos()
        End Set
    End Property

    Private _listaEnvios As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnvios As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnvios
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            Dim unused = SetProperty(_listaEnvios, value)
        End Set
    End Property

    Private _fechaFiltro As Date
    Public Property fechaFiltro As Date
        Get
            Return _fechaFiltro
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaFiltro, value)
            listaEnviosTramitados = _servicio.CargarListaEnviosTramitadosPorFecha(empresaSeleccionada.Número, fechaFiltro)
            If PestannaNombre = Pestannas.TRAMITADOS Then
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
            Dim unused = SetProperty(_clienteFiltro, value)
            listaEnviosTramitados = _servicio.CargarListaEnviosTramitadosPorCliente(empresaSeleccionada.Número, clienteFiltro)
            If PestannaNombre = Pestannas.TRAMITADOS Then
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
            Dim unused = SetProperty(_nombreFiltro, value)
            listaEnviosTramitados = If(String.IsNullOrEmpty(nombreFiltro),
                _servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, agenciaSeleccionada.Numero, fechaFiltro),
                _servicio.CargarListaEnviosTramitadosPorNombre(empresaSeleccionada.Número, nombreFiltro))
            envioActual = listaEnviosTramitados.FirstOrDefault
        End Set
    End Property

    Private _numeroPedido As String
    Public Property numeroPedido As String
        Get
            Return _numeroPedido
        End Get
        Set(value As String)
            Dim unused3 = SetProperty(_numeroPedido, value)
            Dim pedidoAnterior As CabPedidoVta
            pedidoAnterior = pedidoSeleccionado
            Dim pedidoNumerico As Integer
            If Integer.TryParse(numeroPedido, pedidoNumerico) Then ' si el pedido es numérico
                Dim pedidoBuscado As CabPedidoVta = _servicio.CargarPedidoPorNumero(pedidoNumerico, False)
                pedidoSeleccionado = If(IsNothing(pedidoBuscado) OrElse IsNothing(pedidoBuscado.Empresa),
                    _servicio.CargarPedidoPorNumero(pedidoNumerico),
                    pedidoBuscado)
            Else ' si no es numérico (es una factura, lo tratamos como un cobro)
                pedidoSeleccionado = CalcularPedidoTexto(numeroPedido)
                ' Carlos 22/09/15: para que permita meter los que solo llevan contra reembolso
                'If IsNothing(agenciaEspecifica) AndAlso IsNothing(empresaSeleccionada) Then
                '    agenciaSeleccionada = DbContext.AgenciasTransporte.OrderByDescending(Function(o) o.Numero).FirstOrDefault(Function(a) a.Empresa = pedidoSeleccionado.Empresa)
                'End If
                If Not IsNothing(pedidoSeleccionado) AndAlso Not IsNothing(agenciaEspecifica) Then
                    retornoActual = (From s In listaTiposRetorno Where s.id = agenciaEspecifica.retornoSoloCobros).FirstOrDefault
                    servicioActual = (From s In listaServicios Where s.ServicioId = agenciaEspecifica.servicioSoloCobros).FirstOrDefault
                    horarioActual = (From s In listaHorarios Where s.id = agenciaEspecifica.horarioSoloCobros).FirstOrDefault
                    paisActual = (From s In listaPaises Where s.Id = agenciaEspecifica.paisDefecto).SingleOrDefault
                    bultos = 0
                    Dim unused2 = SetProperty(_numeroPedido, pedidoSeleccionado.Número.ToString)
                End If
            End If
            If IsNothing(pedidoSeleccionado) OrElse IsNothing(pedidoSeleccionado.Clientes) Then
                If IsNothing(pedidoAnterior) Then
                    Dim unused1 = SetProperty(_numeroPedido, "36") 'ñapa a arreglar cuando esté inspirado
                Else
                    pedidoSeleccionado = pedidoAnterior
                    Dim unused = SetProperty(_numeroPedido, pedidoAnterior.Número.ToString)
                End If
            End If

            If Not IsNothing(pedidoAnterior) AndAlso Not IsNothing(pedidoSeleccionado) AndAlso pedidoSeleccionado.Empresa <> pedidoAnterior.Empresa Then
                empresaSeleccionada = listaEmpresas.Single(Function(e) e.Número = pedidoSeleccionado.Empresa)
            End If

            'Dim agenciaNueva As AgenciasTransporte = (From a In DbContext.AgenciasTransporte Where a.Ruta = pedidoSeleccionado.Ruta).FirstOrDefault
            RaisePropertyChanged(NameOf(empresaSeleccionada))
            RaisePropertyChanged(NameOf(agenciaSeleccionada))
        End Set
    End Property

    Private Function CalcularPedidoTexto(numeroPedido As String) As CabPedidoVta
        Dim pedidoEncontrado As CabPedidoVta = _servicio.CargarPedidoPorFactura(numeroPedido)
        '
        If Not IsNothing(pedidoEncontrado) Then
            Return pedidoEncontrado
        End If

        Dim clienteEncontrado = _servicio.CargarClientePorUnDato(empresaSeleccionada.Número, numeroPedido)

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
            Dim unused = SetProperty(_numeroMultiusuario, value)
            multiusuario = _servicio.CargarMultiusuario(empresaSeleccionada.Número, numeroMultiusuario)
        End Set
    End Property

    Private _multiusuario As MultiUsuarios
    Public Property multiusuario As MultiUsuarios
        Get
            Return _multiusuario
        End Get
        Set(value As MultiUsuarios)
            Dim unused = SetProperty(_multiusuario, value)
        End Set
    End Property

    Private _pestannaNombre As String
    Public Property PestannaNombre As String
        Get
            Return _pestannaNombre
        End Get
        Set(value As String)
            Dim unused = SetProperty(_pestannaNombre, value)
            If String.IsNullOrEmpty(value) Then
                Return
            End If
            If PestannaNombre = Pestannas.PEDIDOS Then
                numeroPedido = numeroPedido 'para que ejecute el código
            End If
            If PestannaNombre = Pestannas.PENDIENTES Then
                Dim listaNueva As IEnumerable(Of EnvioAgenciaWrapper) = _servicio.CargarListaPendientes()
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
            If PestannaNombre = Pestannas.EN_CURSO AndAlso Not IsNothing(agenciaSeleccionada) Then
                ActualizarListas()
                listaEnvios = _servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
            End If

            If PestannaNombre = Pestannas.REEMBOLSOS AndAlso Not IsNothing(empresaSeleccionada) Then
                listaReembolsos = _servicio.CargarListaReembolsos(empresaSeleccionada.Número, agenciaSeleccionada.Numero)
            End If

            If PestannaNombre = Pestannas.RETORNOS AndAlso Not IsNothing(empresaSeleccionada) Then
                listaRetornos = _servicio.CargarListaRetornos(empresaSeleccionada.Número, agenciaSeleccionada.Numero, agenciaEspecifica.retornoSinRetorno)
            End If
            If PestannaNombre = Pestannas.TRAMITADOS AndAlso Not IsNothing(empresaSeleccionada) Then
                listaEnviosTramitados = _servicio.CargarListaEnviosTramitados(empresaSeleccionada.Número, agenciaSeleccionada.Numero, fechaFiltro)
            End If
        End Set
    End Property

    Private _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            Dim unused = SetProperty(_PestañaSeleccionada, value)
            PestannaNombre = If(IsNothing(value), String.Empty, value.Name)
        End Set
    End Property

    Private _listaEmpresas As ObservableCollection(Of Empresas)
    Public Property listaEmpresas As ObservableCollection(Of Empresas)
        Get
            Return _listaEmpresas
        End Get
        Set(value As ObservableCollection(Of Empresas))
            Dim unused = SetProperty(_listaEmpresas, value)
        End Set
    End Property

    Private _listaEnviosPedido As ObservableCollection(Of EnviosAgencia)
    Public Property listaEnviosPedido As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaEnviosPedido
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            Dim unused = SetProperty(_listaEnviosPedido, value)
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
            Dim unused = SetProperty(_listaEnviosTramitados, value)
            RaisePropertyChanged(NameOf(sePuedeModificarReembolso))
            RaisePropertyChanged(NameOf(sePuedeModificarEstado))
            RaisePropertyChanged(NameOf(etiquetaBultosTramitados))
            cmdImprimirManifiesto.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _XMLdeEstado As XDocument
    Public Property XMLdeEstado As XDocument
        Get
            Return _XMLdeEstado
        End Get
        Set(value As XDocument)
            Dim unused = SetProperty(_XMLdeEstado, value)
        End Set
    End Property

    Private Property _barraProgresoFinal As Integer
    Public Property barraProgresoFinal As Integer
        Get
            Return _barraProgresoFinal
        End Get
        Set(value As Integer)
            Dim unused = SetProperty(_barraProgresoFinal, value)
        End Set
    End Property

    Private Property _barraProgresoActual As Integer
    Public Property barraProgresoActual As Integer
        Get
            Return _barraProgresoActual
        End Get
        Set(value As Integer)
            Dim unused = SetProperty(_barraProgresoActual, value)
        End Set
    End Property

    Private _estadoEnvioCargado As estadoEnvio
    Public Property estadoEnvioCargado As estadoEnvio
        Get
            Return _estadoEnvioCargado
        End Get
        Set(value As estadoEnvio)
            Dim unused = SetProperty(_estadoEnvioCargado, value)
        End Set
    End Property

    Private _listaPendientes As ObservableCollection(Of EnvioAgenciaWrapper)
    Public Property listaPendientes() As ObservableCollection(Of EnvioAgenciaWrapper)
        Get
            Return _listaPendientes
        End Get
        Set(ByVal value As ObservableCollection(Of EnvioAgenciaWrapper))
            Dim unused = SetProperty(_listaPendientes, value)
        End Set
    End Property

    Private _listaReembolsos As ObservableCollection(Of EnviosAgencia)
    Public Property listaReembolsos As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaReembolsos
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            Dim unused = SetProperty(_listaReembolsos, value)
            RaisePropertyChanged(NameOf(sumaReembolsos))
            RaisePropertyChanged(NameOf(descuadreContabilidad))
        End Set
    End Property

    Private _listaReembolsosSeleccionados As ObservableCollection(Of EnviosAgencia)
    Public Property listaReembolsosSeleccionados As ObservableCollection(Of EnviosAgencia)
        Get
            Return _listaReembolsosSeleccionados
        End Get
        Set(value As ObservableCollection(Of EnviosAgencia))
            Dim unused = SetProperty(_listaReembolsosSeleccionados, value)
            RaisePropertyChanged(NameOf(sumaSeleccionadas))
            cmdContabilizarReembolso.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _lineaReembolsoSeleccionado As EnviosAgencia
    Public Property lineaReembolsoSeleccionado As EnviosAgencia
        Get
            Return _lineaReembolsoSeleccionado
        End Get
        Set(value As EnviosAgencia)
            Dim unused = SetProperty(_lineaReembolsoSeleccionado, value)
        End Set
    End Property

    Private _lineaReembolsoContabilizar As EnviosAgencia
    Public Property lineaReembolsoContabilizar As EnviosAgencia
        Get
            Return _lineaReembolsoContabilizar
        End Get
        Set(value As EnviosAgencia)
            Dim unused = SetProperty(_lineaReembolsoContabilizar, value)
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
            Dim unused = SetProperty(_numClienteContabilizar, value)
            cmdContabilizarReembolso.RaiseCanExecuteChanged()
        End Set
    End Property

    Public ReadOnly Property sumaSeleccionadas As Double
        Get
            Return If(listaReembolsosSeleccionados IsNot Nothing,
                      listaReembolsosSeleccionados.Sum(Function(r) r.Reembolso),
                      0.0)
        End Get
    End Property

    Public ReadOnly Property sumaReembolsos As Double
        Get
            Return If(listaReembolsos IsNot Nothing,
                      listaReembolsos.Sum(Function(r) r.Reembolso),
                      0.0)
        End Get
    End Property

    Public ReadOnly Property sumaContabilidad As Double
        Get
            Try
                If Not IsNothing(agenciaSeleccionada) AndAlso agenciaSeleccionada.Empresa = empresaSeleccionada.Número Then
                    Dim suma As Nullable(Of Double) = _servicio.CalcularSumaContabilidad(empresaSeleccionada.Número, agenciaSeleccionada.CuentaReembolsos)

                    Return If(IsNothing(suma), 0, CType(suma, Double))
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
            Dim unused = SetProperty(_digitalizacionActual, value)
            RaisePropertyChanged(NameOf(cmdDescargarImagen))
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
            Dim unused = SetProperty(_reembolsoModificar, value)
            RaisePropertyChanged(NameOf(envioActual))
        End Set
    End Property

    Private _retornoModificar As tipoIdDescripcion
    Public Property retornoModificar As tipoIdDescripcion
        Get
            Return _retornoModificar
        End Get
        Set(value As tipoIdDescripcion)
            Dim unused = SetProperty(_retornoModificar, value)
            RaisePropertyChanged(NameOf(envioActual))
        End Set
    End Property

    Private _estadoModificar As Integer
    Public Property estadoModificar As Integer
        Get
            Return _estadoModificar
        End Get
        Set(value As Integer)
            Dim unused = SetProperty(_estadoModificar, value)
            RaisePropertyChanged(NameOf(envioActual))
        End Set
    End Property

    Private _observacionesModificacion As String
    Public Property observacionesModificacion As String
        Get
            Return _observacionesModificacion
        End Get
        Set(value As String)
            Dim unused = SetProperty(_observacionesModificacion, value)
        End Set
    End Property

    Private _fechaEntregaModificar As Date?
    Public Property fechaEntregaModificar() As Date?
        Get
            Return _fechaEntregaModificar
        End Get
        Set(ByVal value As Date?)
            Dim unused = SetProperty(_fechaEntregaModificar, value)
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
            Dim unused = SetProperty(_listaHistoriaEnvio, value)
            RaisePropertyChanged(NameOf(mostrarHistoria))
        End Set
    End Property

    Private ReadOnly _mostrarHistoria As Visibility
    Public ReadOnly Property mostrarHistoria As Visibility
        Get
            Return If((Not IsNothing(listaHistoriaEnvio)) AndAlso listaHistoriaEnvio.Count > 0, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property

    Public ReadOnly Property visibilidadSoloImprimir
        Get
            Return If(Not IsNothing(agenciaEspecifica), agenciaEspecifica.visibilidadSoloImprimir, Visibility.Hidden)
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
            Dim unused = SetProperty(_listaRetornos, value)
        End Set
    End Property

    Private _lineaRetornoSeleccionado As EnviosAgencia
    Public Property lineaRetornoSeleccionado As EnviosAgencia
        Get
            Return _lineaRetornoSeleccionado
        End Get
        Set(value As EnviosAgencia)
            Dim unused = SetProperty(_lineaRetornoSeleccionado, value)
        End Set
    End Property

    Public ReadOnly Property etiquetaBultosTramitados As String
        Get
            If Not IsNothing(listaEnviosTramitados) AndAlso listaEnviosTramitados.Count > 0 Then
                Dim totalBultos As Integer = Aggregate e In listaEnviosTramitados Into Sum(e.Bultos)
                Dim totalEnvios As Integer = Aggregate e In listaEnviosTramitados Into Count()
                Return "Hay " + totalEnvios.ToString + " envíos tramitados, que suman un total de " + totalBultos.ToString + " bultos."
            Else
                Return "No hay ningún envío tramitado"
            End If
        End Get
    End Property
    Public ReadOnly Property NoEstaInsertandoPendiente As Boolean
        Get
            Return Not HayCambiosSinGuardarEnPendientes()
        End Get
    End Property

    Private _facturarAlImprimirEtiqueta As Boolean
    Public Property FacturarAlImprimirEtiqueta As Boolean
        Get
            Return _facturarAlImprimirEtiqueta
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_facturarAlImprimirEtiqueta, value)
        End Set
    End Property

    Private _imprimirFacturaAlFacturar As Boolean
    Public Property ImprimirFacturaAlFacturar As Boolean
        Get
            Return _imprimirFacturaAlFacturar
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_imprimirFacturaAlFacturar, value)
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
        Return Not IsNothing(envioActual) AndAlso Not IsNothing(listaEnvios) AndAlso listaEnvios.Count > 0 AndAlso envioActual.Estado <= 0
    End Function
    Private Async Sub Tramitar(ByVal param As Object)
        If envioActual.Estado >= Constantes.Agencias.ESTADO_TRAMITADO_ENVIO Then
            Throw New Exception("No se puede tramitar un pedido ya tramitado")
        End If
        Try
            Dim respuesta = Await agenciaEspecifica.LlamadaWebService(envioActual, _servicio)

            ' Guardamos la llamada
            Await _servicio.GuardarLlamadaAgencia(respuesta)

            If respuesta.Exito OrElse agenciaEspecifica.RespuestaYaTramitada(respuesta.TextoRespuestaError) Then
                mensajeError = _servicio.TramitarEnvio(envioActual)
                listaEnvios = _servicio.CargarListaEnvios(agenciaSeleccionada.Numero)
                envioActual = listaEnvios.LastOrDefault ' lo pongo para que no se vaya al último
            Else
                mensajeError = respuesta.TextoRespuestaError
            End If
        Catch ex As Exception
            mensajeError = ex.Message
        End Try
        RaisePropertyChanged(NameOf(listaReembolsos))
        RaisePropertyChanged(NameOf(mensajeError))
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
            barraProgresoActual += 1
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
        Return Not IsNothing(envioActual) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso envioActual.Agencia = agenciaSeleccionada.Numero
    End Function
    Private Async Sub ImprimirEtiquetaPedido(ByVal param As Object)
        Try
            agenciaEspecifica.imprimirEtiqueta(envioActual)
            If Not _facturarAlImprimirEtiqueta Then
                RaiseEvent SolicitarFocoNumeroPedido(Me, EventArgs.Empty)
                Return
            End If
            Dim albaran = Await _servicioPedidos.CrearAlbaranVenta(envioActual.Empresa, envioActual.Pedido)
            Dim factura = Await _servicioPedidos.CrearFacturaVenta(envioActual.Empresa, envioActual.Pedido)

            Dim mensaje = If(factura <> Constantes.PeriodosFacturacion.FIN_DE_MES,
                $"Pedido {envioActual.Pedido} facturado correctamente en albarán {albaran} y factura {factura}",
                $"Albarán del pedido {envioActual.Pedido} creado correctamente en albarán {albaran}")
            _dialogService.ShowNotification("Facturación", mensaje)
            If Not _imprimirFacturaAlFacturar OrElse factura = Constantes.PeriodosFacturacion.FIN_DE_MES Then
                Return
            End If
            Dim pathFactura = Await _servicioPedidos.DescargarFactura(envioActual.Empresa, envioActual.Pedido, envioActual.Cliente.Trim())
            Dim printProcess As New Process With {
                .StartInfo = New ProcessStartInfo With {
                    .FileName = pathFactura,
                    .Verb = "print",
                    .CreateNoWindow = True,
                    .UseShellExecute = True
                }
            }
            Dim unused = printProcess.Start()

            RaiseEvent SolicitarFocoNumeroPedido(Me, EventArgs.Empty)
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
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
        Return envioActual IsNot Nothing AndAlso Not IsNothing(listaEnvios) AndAlso listaEnvios.Count > 0 AndAlso envioActual.Estado <= 0 AndAlso
        (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) OrElse _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.FACTURACION))
    End Function
    Private Sub Borrar(ByVal param As Object)
        If envioActual.Estado > 0 Then
            Throw New Exception("No se puede borrar un pedido tramitado")
        End If
        Dim mensajeMostrar = String.Format("¿Confirma que desea borrar el envío del cliente {1}?{0}{0}{2}", Environment.NewLine, envioActual.Cliente?.Trim, envioActual.Direccion)
        Dim continuar As Boolean
        continuar = _dialogService.ShowConfirmationAnswer("Borrar Envío", mensajeMostrar)
        If Not continuar Then
            Return
        End If

        _servicio.Borrar(envioActual.Numero)
        Dim copiaEnvio = envioActual
        Dim unused1 = listaEnviosPedido.Remove(copiaEnvio)
        Dim unused = listaEnvios.Remove(copiaEnvio)
        envioActual = listaEnvios.LastOrDefault
        RaisePropertyChanged(NameOf(listaEnvios))
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
        Return Not IsNothing(pedidoSeleccionado) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso Not EstaInsertandoEnvio 'AndAlso pedidoSeleccionado.Empresa <> EMPRESA_ESPEJO
    End Function
    Private Sub OnInsertar(arg As Object)
        Try
            If Not EstaInsertandoEnvio Then
                EstaInsertandoEnvio = True
                cmdInsertar.RaiseCanExecuteChanged()
                InsertarRegistro(servicioActual.ServicioId = agenciaEspecifica.ServicioCreaEtiquetaRetorno)
            End If
        Catch e As Exception
            imprimirEtiqueta = False
            _dialogService.ShowError(e.Message)
        Finally
            EstaInsertandoEnvio = False
            cmdInsertar.RaiseCanExecuteChanged()
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
            _dialogService.ShowError(ex.Message)
            Return
        End Try
        Dim textoImprimir = If(imprimirEtiqueta, "Envío insertado correctamente e impresa la etiqueta", "Envío ampliado correctamente")
        _dialogService.ShowNotification("Envío", textoImprimir)
        RaiseEvent SolicitarFocoNumeroPedido(Me, EventArgs.Empty)
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
        Try
            XMLdeEstado = agenciaEspecifica.cargarEstado(envioActual)
            estadoEnvioCargado = agenciaEspecifica.transformarXMLdeEstado(XMLdeEstado)
            mensajeError = "Estado del envío " + envioActual.Numero.ToString + " cargado correctamente"
        Catch ex As Exception
            _dialogService.ShowError(ex.Message)
        End Try
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
            empresaDefecto = String.Format("{0,-3}", Await _configuracion.leerParametro("1", "EmpresaPorDefecto")).Trim()
            If IsNothing(listaEmpresas) Then
                listaEmpresas = _servicio.CargarListaEmpresas()
            End If

            numeroPedido = Await _configuracion.leerParametro(empresaDefecto, "UltNumPedidoVta")

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
            Dim unused = listaReembolsos.Remove(lineaReembolsoSeleccionado)
            RaisePropertyChanged(NameOf(sumaSeleccionadas))
            RaisePropertyChanged(NameOf(sumaReembolsos))
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
            Dim unused = listaReembolsosSeleccionados.Remove(lineaReembolsoContabilizar)
            RaisePropertyChanged(NameOf(sumaSeleccionadas))
            RaisePropertyChanged(NameOf(sumaReembolsos))
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
        Dim continuar As Boolean
        _dialogService.ShowConfirmation("Contabilizar", "¿Desea contabilizar?", Sub(r)
                                                                                    continuar = r.Result = ButtonResult.OK
                                                                                End Sub)
        If Not continuar OrElse IsNothing(listaReembolsosSeleccionados) Then
            Return
        End If

        ' Comprobamos si existe el cliente
        Dim cliente As Clientes = _servicio.CargarClientePrincipal(empresaSeleccionada.Número, numClienteContabilizar)
        If IsNothing(cliente) Then
            _dialogService.ShowError("El cliente " + numClienteContabilizar + " no existe en " + empresaSeleccionada.Nombre)
            Return
        End If

        Dim asiento As Integer = 0 'para guardar el asiento que devuelve prdContabilizar

        ' Carlos 02/01/24: esta parte hay que refactorizarla inyectando una dependencia de IContabilidadService
        ' Dim listaPreContabilidad As New List(Of PreContabilidadDTO)
        ' De momento no lo hacemos porque hay que actualizar la FechaPagoReembolso y eso no está contemplado


        ' Empezamos una transacción
        Dim success As Boolean = False
        Using transaction As New TransactionScope()
            Using DbContext As New NestoEntities
                Try
                    If Not MODO_CUADRE Then
                        For Each linea In listaReembolsosSeleccionados
                            Dim agencia As AgenciasTransporte = DbContext.AgenciasTransporte.Where(Function(a) a.Numero = linea.Agencia).SingleOrDefault
                            Dim numDocAgencia = If(agencia.Nombre.Length > 10, agencia.Nombre.Substring(0, 10), agencia.Nombre)
                            Dim unused3 = DbContext.PreContabilidad.Add(New PreContabilidad With {
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
                        Dim numDoc = If(agenciaSeleccionada.Nombre.Length > 10, agenciaSeleccionada.Nombre.Substring(0, 10), agenciaSeleccionada.Nombre)
                        Dim unused2 = DbContext.PreContabilidad.Add(New PreContabilidad With {
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

                        Dim empresaParam As New SqlParameter("@Empresa", SqlDbType.Char, 3) With {.Value = empresaSeleccionada.Número}
                        Dim diarioParam As New SqlParameter("@Diario", SqlDbType.Char, 10) With {.Value = "_PagoReemb"}
                        Dim usuarioParam As New SqlParameter("@Usuario", SqlDbType.Char, 30) With {.Value = _configuracion.usuario}
                        Dim resultadoParam As New SqlParameter("@Resultado", SqlDbType.Int) With {.Direction = ParameterDirection.Output}

                        Dim unused1 = DbContext.Database.ExecuteSqlCommand("EXEC @Resultado = prdContabilizar @Empresa, @Diario, @Usuario",
                                    resultadoParam, empresaParam, diarioParam, usuarioParam)

                        asiento = CInt(resultadoParam.Value)
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
                            RaisePropertyChanged(NameOf(sumaContabilidad))
                            RaisePropertyChanged(NameOf(descuadreContabilidad))
                            RaisePropertyChanged(NameOf(sumaReembolsos))

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
                        Dim unused = DbContext.SaveChanges()
                        _dialogService.ShowNotification("Contabilizado Correctamente", "Nº Asiento: " + asiento.ToString)
                    Else
                        transaction.Dispose()
                        _dialogService.ShowError("Se ha producido un error y no se han grabado los datos")
                    End If
                Catch ex As Exception
                    transaction.Dispose()
                    _dialogService.ShowError("Se ha producido un error y no se han grabado los datos:" + vbCr + ex.Message)
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
        Dim saveDialog As New SaveFileDialog With {
            .Title = "Guardar justificante de entrega",
            .Filter = "Imagen (*.jpg)|*.jpg"
        }
        Dim unused = saveDialog.ShowDialog()

        'exit if no file selected
        If saveDialog.FileName = "" Then
            Exit Sub
        End If

        Dim cln As New System.Net.WebClient
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
        Dim continuar As Boolean
        _dialogService.ShowConfirmation("Modificar Envío", mensajeMostrar, Sub(r)
                                                                               continuar = r.Result = ButtonResult.OK
                                                                           End Sub)
        If Not continuar Then
            Return
        End If

        _servicio.Modificar(envioActual)
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
        Dim continuar As Boolean
        _dialogService.ShowConfirmation("Modificar Envío", mensajeMostrar, Sub(r)
                                                                               continuar = r.Result = ButtonResult.OK
                                                                           End Sub)
        If Not continuar Then
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
        Return Not IsNothing(listaEnviosTramitados) AndAlso listaEnviosTramitados.Any
    End Function
    Private Async Sub OnImprimirManifiesto(arg As Object)
        Dim reportDefinition As Stream = Assembly.LoadFrom("Informes").GetManifestResourceStream("Nesto.Informes.ManifiestoAgencia.rdlc")
        Dim dataSource As List(Of Informes.ManifiestoAgenciaModel) = Await Informes.ManifiestoAgenciaModel.CargarDatos(empresaSeleccionada.Número, agenciaSeleccionada.Numero, fechaFiltro)
        Dim report As New LocalReport()
        report.LoadReportDefinition(reportDefinition)
        report.DataSources.Add(New ReportDataSource("ManifiestoAgenciaDataSet", dataSource))
        Dim listaParametros As New List(Of ReportParameter) From {
            New ReportParameter("Fecha", fechaFiltro),
            New ReportParameter("NombreAgencia", agenciaSeleccionada.Nombre)
        }
        report.SetParameters(listaParametros)
        Dim pdf As Byte() = report.Render("PDF")
        Dim fileName As String = Path.GetTempPath + "InformeManifiestoAgencia.pdf"
        File.WriteAllBytes(fileName, pdf)
        Dim unused = Process.Start(New ProcessStartInfo(fileName) With {
            .UseShellExecute = True
        })
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
            Dim mensajeMostrar As String = String.Format("¿Confirma que ha recibido el retorno del pedido {0}?", lineaRetornoSeleccionado.Pedido.ToString)
            Dim continuar As Boolean
            _dialogService.ShowConfirmation("Retorno", mensajeMostrar, Sub(r)
                                                                           continuar = r.Result = ButtonResult.OK
                                                                       End Sub)
            If Not continuar OrElse IsNothing(lineaRetornoSeleccionado) Then
                Return
            End If

            Using DbContext As New NestoEntities
                Dim lineaEncontrada As EnviosAgencia = DbContext.EnviosAgencia.Where(Function(e) e.Numero = lineaRetornoSeleccionado.Numero).Single
                lineaEncontrada.FechaRetornoRecibido = Today

                If DbContext.SaveChanges Then
                    mensajeError = "Fecha retorno del cliente " + lineaRetornoSeleccionado.Cliente.Trim + " actualizada correctamente"
                    Dim unused = listaRetornos.Remove(lineaRetornoSeleccionado)
                Else
                    mensajeError = "Se ha producido un error al actualizar la fecha del retorno"
                    _dialogService.ShowError("No se ha podido actualizar la fecha del retorno")
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
        Return Not IsNothing(EnvioPendienteSeleccionado) AndAlso
            (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION) OrElse _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.FACTURACION))
    End Function
    Private Sub OnBorrarEnvioPendiente()
        Dim mensajeMostrar = String.Format("¿Confirma que desea borrar el envío pendiente del cliente {1}?{0}{0}{2}", Environment.NewLine, EnvioPendienteSeleccionado.Cliente?.Trim, EnvioPendienteSeleccionado.Direccion)
        Dim continuar As Boolean
        continuar = _dialogService.ShowConfirmationAnswer("Borrar Envío", mensajeMostrar)
        If Not continuar Then
            Return
        End If

        Dim envioBorrar As EnvioAgenciaWrapper = EnvioPendienteSeleccionado

        'If listaPendientes?.Count > 1 Then
        '    EnvioPendienteSeleccionado = listaPendientes.FirstOrDefault
        'Else
        '    EnvioPendienteSeleccionado = Nothing
        'End If

        _servicio.Borrar(envioBorrar.Numero)
        Dim unused = listaPendientes.Remove(envioBorrar)
        If Not listaPendientes.Any Then
            EnvioPendienteSeleccionado = Nothing
        Else
            ActualizarEstadoComandos()
        End If

    End Sub

    Public Property InsertarEnvioPendienteCommand() As DelegateCommand
    Private Function CanInsertarEnvioPendiente() As Boolean
        Return Not IsNothing(empresaSeleccionada) AndAlso Not IsNothing(agenciaSeleccionada) AndAlso Not HayCambiosSinGuardarEnPendientes()
    End Function
    Private Sub OnInsertarEnvioPendiente()
        ' Preparamos si se llama desde fuera
        If IsNothing(listaEmpresas) Then
            listaEmpresas = _servicio.CargarListaEmpresas
        End If
        If IsNothing(listaPendientes) Then
            listaPendientes = New ObservableCollection(Of EnvioAgenciaWrapper)
        End If
        If String.IsNullOrEmpty(PestannaNombre) Then
            PestannaNombre = Pestannas.PENDIENTES
        End If
        If IsNothing(empresaSeleccionada) AndAlso Not IsNothing(listaEmpresas) Then
            empresaSeleccionada = listaEmpresas.FirstOrDefault
        End If
        If IsNothing(agenciaSeleccionada) AndAlso Not IsNothing(listaAgencias) Then
            agenciaSeleccionada = listaAgencias.LastOrDefault
        End If

        ' Comenzamos a ejecutar
        Dim envioNuevo As New EnvioAgenciaWrapper With {
            .Agencia = agenciaSeleccionada.Numero,
            .Empresa = empresaSeleccionada.Número,
            .Estado = Constantes.Agencias.ESTADO_PENDIENTE_ENVIO,
            .TieneCambios = True,
            .Horario = listaHorarios.FirstOrDefault().id,
            .Servicio = listaHorarios.FirstOrDefault().id
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
                Dim unused1 = _servicio.Insertar(envio)
            Else
                _servicio.Modificar(envio)
            End If
            Dim unused = listaPendientes.Remove(EnvioPendienteSeleccionado)
            EnvioPendienteSeleccionado = EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio)
            EnvioPendienteSeleccionado.TieneCambios = False
            listaPendientes.Add(EnvioPendienteSeleccionado)
        Catch ex As DbEntityValidationException
            Dim mensajeError As String = String.Empty
            For Each eve In ex.EntityValidationErrors
                For Each ve In eve.ValidationErrors
                    mensajeError += ve.ErrorMessage + vbCr
                Next
            Next
            _dialogService.ShowError("Error al modificar envío:" + vbCr + mensajeError)
        Catch ex As Exception
            'validatonerrors
            _dialogService.ShowError("Error al modificar envío:" + vbCr + ex.Message)
        End Try
    End Sub

    Public Property AbrirEnlaceSeguimientoCommand As DelegateCommand
    Private Function CanAbrirEnlaceSeguimientoCommand() As Boolean
        Return EnlaceSeguimientoEnvio <> ""
    End Function
    Private Sub OnAbrirEnlaceSeguimientoCommand()
        Dim unused = Process.Start(New ProcessStartInfo(EnlaceSeguimientoEnvio) With {
            .UseShellExecute = True
        })
    End Sub
#End Region

#Region "Funciones de Ayuda"
    Public Function correoUnico() As String
        Return If(Not pedidoSeleccionado.Clientes.PersonasContactoCliente.Any,
            String.Empty,
            correoUnico(pedidoSeleccionado.Clientes.PersonasContactoCliente.ToList))
    End Function

    Public Function correoUnico(listaPersonas As List(Of PersonasContactoCliente)) As String
        Dim correo As New CorreoCliente(listaPersonas)
        Return correo.CorreoAgencia
    End Function
    'Public Function importeReembolso(pedidoSeleccionado As CabPedidoVta) As Decimal

    '    ' Miramos la deuda que tenga en su extracto. 
    '    ' Esa deuda la tiene que pagar independientemente de la forma de pago
    '    Dim importeDeuda As Double = 0 'calcularDeuda()

    '    ' Miramos los casos en los que no hay contra reembolso
    '    If IsNothing(pedidoSeleccionado) Then
    '        Return importeDeuda
    '    End If
    '    If pedidoSeleccionado.CCC IsNot Nothing Then
    '        Return importeDeuda
    '    End If
    '    If pedidoSeleccionado.Periodo_Facturacion = "FDM" Then
    '        Return importeDeuda
    '    End If
    '    If (pedidoSeleccionado.Forma_Pago = "CNF" Or
    '        pedidoSeleccionado.Forma_Pago = "TRN" Or
    '        pedidoSeleccionado.Forma_Pago = "CHC" Or
    '        pedidoSeleccionado.Forma_Pago = "TAR") Then
    '        Return importeDeuda
    '    End If
    '    If pedidoSeleccionado.NotaEntrega Then
    '        Return importeDeuda
    '    End If
    '    If Not IsNothing(pedidoSeleccionado.PlazosPago) AndAlso pedidoSeleccionado.PlazosPago.Trim = "PRE" Then
    '        Return importeDeuda
    '    End If

    '    If pedidoSeleccionado.MantenerJunto Then
    '        Dim lineasSinFacturar As List(Of LinPedidoVta)
    '        lineasSinFacturar = _servicio.CargarLineasPedidoPendientes(pedidoSeleccionado.Número)
    '        If lineasSinFacturar.Any Then
    '            Return importeDeuda
    '        End If
    '    End If

    '    ' Para el resto de los casos ponemos el importe correcto
    '    Dim lineas As List(Of LinPedidoVta)
    '    lineas = _servicio.CargarLineasPedidoSinPicking(pedidoSeleccionado.Número)
    '    If IsNothing(lineas) OrElse Not lineas.Any Then
    '        Return importeDeuda
    '    End If

    '    Dim importeFinal As Double = Math.Round(
    '        (Aggregate l In lineas
    '        Select l.Total Into Sum()) _
    '        + importeDeuda, 2, MidpointRounding.AwayFromZero)

    '    ' Evitamos los reembolsos negativos
    '    If importeFinal < 0 Then
    '        importeFinal = 0
    '    End If


    '    Return importeFinal

    'End Function

    Public Sub InsertarRegistro(Optional ByVal conEtiquetaRecogida As Boolean = False)
        Dim envioPendiente As EnviosAgencia = buscarEnvioPendiente(pedidoSeleccionado)
        Dim estabaPendiente As Boolean = Not IsNothing(envioPendiente)
        If Not estabaPendiente Then
            envioActual = buscarPedidoAmpliacion(pedidoSeleccionado)
        Else
            envioActual = envioPendiente
            agenciaEspecifica.calcularPlaza(codPostalEnvio, envioActual.Nemonico, envioActual.NombrePlaza, envioActual.TelefonoPlaza, envioActual.EmailPlaza)
        End If

        If String.IsNullOrEmpty(envioActual?.Nemonico) Then
            agenciaEspecifica.calcularPlaza(codPostalEnvio, envioActual.Nemonico, envioActual.NombrePlaza, envioActual.TelefonoPlaza, envioActual.EmailPlaza)
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

            Dim mensajeMostrar As String = String.Format("Este pedido es una ampliación del pedido {0}. " +
                    "{1} puede actualizar los datos." + vbCrLf +
                    "En caso contrario, pulse Cancelar, modifique el nº de bultos y vuelva a intentarlo." + vbCrLf + vbCrLf +
                    "¿Desea actualizar los datos?", envioActual.Pedido.ToString, textoConfirmar)
            Dim continuar As Boolean
            _dialogService.ShowConfirmation("Ampliación", mensajeMostrar, Sub(r)
                                                                              continuar = r.Result = ButtonResult.OK
                                                                          End Sub)
            If Not continuar Then
                Throw New Exception("Cancelado por el usuario")
                Return
            End If


        Else
            imprimirEtiqueta = True
        End If

        Dim hayAlgunaLineaConPicking As Boolean = _servicio.HayAlgunaLineaConPicking(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
        If Not hayAlgunaLineaConPicking Then
            Dim continuar As Boolean
            _dialogService.ShowConfirmation("Pedido Sin Picking", "Este pedido no tiene ninguna línea con picking. ¿Desea insertar el pedido de todos modos?", Sub(r)
                                                                                                                                                                   continuar = r.Result = ButtonResult.OK
                                                                                                                                                               End Sub)
            If Not continuar Then
                Throw New Exception("Cancelado por el usuario")
                Return
            End If
        End If

        ' Carlos 16/09/15: hacemos que se pueda cobrar en efectivo por la agencia
        'If reembolso > 0 AndAlso IsNothing(pedidoSeleccionado.IVA) AndAlso agenciaSeleccionada.Empresa <> Constantes.Empresas.EMPRESA_ESPEJO Then
        '    agenciaSeleccionada = servicio.CargarAgenciaPorNombreYCuentaReembolsos(Constantes.Empresas.EMPRESA_ESPEJO, agenciaSeleccionada.CuentaReembolsos, agenciaSeleccionada.Nombre)
        'End If

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
                        .Servicio = servicioActual.ServicioId
                        .Horario = horarioActual.id
                        .Bultos = bultos
                        .Retorno = retornoActual.id
                        .Nombre = nombreEnvio?.Replace("""", String.Empty)
                        .Direccion = direccionEnvio?.Replace("""", String.Empty)
                        .CodPostal = codPostalEnvio
                        .Poblacion = poblacionEnvio
                        .Provincia = provinciaEnvio
                        .Pais = paisActual.Id
                        .Telefono = telefonoEnvio
                        .Movil = movilEnvio
                        .Email = correoEnvio
                        .Observaciones = Left(observacionesEnvio?.Replace("""", String.Empty), 80)
                        .Atencion = attEnvio
                        .Reembolso = IIf(pedidoSeleccionado.Número = envioActual.Pedido, reembolso, envioActual.Reembolso + reembolso) ' por si es ampliación
                        '.CodigoBarras = calcularCodigoBarras()
                        .Vendedor = If(pedidoSeleccionado.Vendedor.Trim <> "", pedidoSeleccionado.Vendedor, "NV")
                        .Peso = Peso
                        .ImporteGasto = CosteEnvio
                    End With
                End If
                agenciaEspecifica.calcularPlaza(codPostalEnvio, envioActual.Nemonico, envioActual.NombrePlaza, envioActual.TelefonoPlaza, envioActual.EmailPlaza)




                If Not esAmpliacion Then
                    Dim unused2 = _servicio.Insertar(envioActual)
                    listaEnvios.Add(envioActual)
                    listaEnviosPedido.Add(envioActual)
                End If

                envioActual.CodigoBarras = agenciaEspecifica.calcularCodigoBarras(Me)
                _servicio.Modificar(envioActual)

                If conEtiquetaRecogida Then
                    ' Realmente no hacemos nada más que aumentar en 1 el contador de Id
                    Dim envioEtiquetaRetorno As New EnviosAgencia With {
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
                        .Nombre = envioActual.Nombre?.Replace("""", String.Empty),
                        .Direccion = envioActual.Direccion?.Replace("""", String.Empty),
                        .CodPostal = envioActual.CodPostal,
                        .Poblacion = envioActual.Poblacion,
                        .Provincia = envioActual.Provincia,
                        .Pais = envioActual.Pais,
                        .Telefono = envioActual.Telefono,
                        .Movil = envioActual.Movil,
                        .Email = envioActual.Email,
                        .Observaciones = envioActual.Observaciones?.Replace("""", String.Empty),
                        .Atencion = envioActual.Atencion,
                        .Reembolso = 0,
                        .Vendedor = envioActual.Vendedor
                    }
                    Dim unused1 = _servicio.Insertar(envioEtiquetaRetorno)
                    _servicio.Borrar(envioEtiquetaRetorno.Numero)
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
                _dialogService.ShowError("Se ha producido un error y no se han grabado los datos: " + vbCr + ex.Message)
            End Try

            If Not success Then
                _dialogService.ShowError("Se ha producido un error y no se ha creado la etiqueta correctamente")
            End If

        End Using ' finaliza la transacción

        If success AndAlso Not esAmpliacion AndAlso Not _servicio.EsTodoElPedidoOnline(envioActual.Empresa, envioActual.Pedido) Then
            Dim unused = _servicio.EnviarCorreoEntregaAgencia(EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envioActual))
        End If
    End Sub

    Private Function buscarEnvioPendiente(pedidoSeleccionado As CabPedidoVta) As EnviosAgencia
        Dim envio As EnviosAgencia = _servicio.CargarEnvio(pedidoSeleccionado.Empresa, pedidoSeleccionado.Número)
        Return envio
    End Function

    Private Function CalcularMovimientoDesliq(env As EnviosAgencia, importeAnterior As Double) As ExtractoCliente
        Dim movimientos As ObservableCollection(Of ExtractoCliente)
        Dim concepto As String = _servicio.GenerarConcepto(env)

        movimientos = _servicio.CargarPagoExtractoClientePorEnvio(env, concepto, importeAnterior)

        Return If(movimientos.Count = 0, Nothing, movimientos.LastOrDefault)
    End Function
    Private Function ConfigurarAgenciaPedido() As AgenciasTransporte
        ' agenciaConfigurar es agenciaSeleccionada. Lo pongo por si se busca agenciaSeleccionada.
        If IsNothing(pedidoSeleccionado) OrElse IsNothing(pedidoSeleccionado.Empresa) OrElse IsNothing(listaAgencias) Then
            Return agenciaSeleccionada
        End If

        Dim cliente As Clientes = _servicio.CargarCliente(pedidoSeleccionado.Empresa, pedidoSeleccionado.Nº_Cliente, pedidoSeleccionado.Contacto)

        Dim parMasEconomico = TarifaMasEconomica(cliente.CodPostal, Peso, reembolso)
        CosteEnvio = parMasEconomico.Value
        Dim tarifaEconomica As ITarifaAgencia = parMasEconomico.Key

        Return listaAgencias.Single(Function(a) a.Empresa = pedidoSeleccionado.Empresa AndAlso a.Numero = tarifaEconomica.AgenciaId)

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
            _dialogService.ShowError("No se puede modificar este envío, porque ya está cobrado")
            Return
        End If

        If Math.Abs(reembolso) > Math.Abs(envio.Reembolso * 10) Then 'es demasiado grande
            Dim mensajeMostrar = String.Format("¿Es correcto el importe de {0}?", reembolso.ToString("C"))
            Dim continuar As Boolean
            _dialogService.ShowConfirmation("¡Atención!", mensajeMostrar, Sub(r)
                                                                              continuar = r.Result = ButtonResult.OK
                                                                          End Sub)
            If Not continuar Then
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
                        Dim unused7 = DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If
                    If envio.Retorno <> retorno.id Then
                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "Retorno"
                        Dim tipoEnvioAnterior As Byte = envio.Retorno
                        historia.ValorAnterior = (From l In listaTiposRetorno Where l.id = tipoEnvioAnterior Select l.descripcion).FirstOrDefault
                        envioEncontrado.Retorno = retorno.id
                        Dim unused6 = DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If
                    If envio.Estado <> estado Then
                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "Estado"
                        historia.ValorAnterior = envio.Estado
                        envioEncontrado.Estado = estado
                        Dim unused5 = DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If
                    If envio.FechaEntrega <> fechaEntrega Then
                        historia.NumeroEnvio = envio.Numero
                        historia.Campo = "FechaEntrega"
                        historia.ValorAnterior = envio.FechaEntrega.ToString
                        envioEncontrado.FechaEntrega = fechaEntrega
                        Dim unused4 = DbContext.EnviosHistoria.Add(historia)
                        modificado = True
                    End If

                    If modificado Then
                        historia.Observaciones = observacionesModificacion
                        'DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                        If DbContext.SaveChanges Then
                            If reembolsoAnterior <> reembolso Then
                                Dim unused3 = contabilizarModificacionReembolso(envio, reembolsoAnterior, reembolso)
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
                        Dim movimientoFactura As ExtractoCliente = _servicio.CalcularMovimientoLiq(envio, reembolsoAnterior)
                        Dim estadoRehusado As New ObjectParameter("Estado", GetType(String)) With {
                            .Value = "RHS"
                        }
                        envio.Retorno = retorno.id
                        Dim unused2 = DbContext.SaveChanges()
                        Dim unused1 = DbContext.prdModificarEfectoCliente(movimientoFactura.Nº_Orden, movimientoFactura.FechaVto, movimientoFactura.CCC, movimientoFactura.Ruta, estadoRehusado, movimientoFactura.Concepto)
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
                    Dim unused = DbContext.SaveChanges()
                    envio = envioEncontrado
                    RaisePropertyChanged(NameOf(listaEnviosTramitados))
                Else
                    _dialogService.ShowError("Se ha producido un error y no se grabado los datos")
                End If
            End Using ' cerramos el contexto breve
        End Using ' Cerramos la transaccion

    End Sub
    Public Function contabilizarModificacionReembolso(envio As EnviosAgencia, importeAnterior As Double, importeNuevo As Double) As Integer

        ' Parámetro Rehusar es para marcar el ExtractoCliente como RHS (rehusado)

        Const diarioReembolsos As String = "_Reembolso"

        Dim agenciaEnvio As AgenciasTransporte = _servicio.CargarAgencia(envio.Agencia)
        If IsNothing(agenciaEnvio.CuentaReembolsos) Then
            mensajeError = "Esta agencia no tiene establecida una cuenta de reembolsos. No se puede contabilizar."
            Return -1
        End If

        Dim empresaEnvio As Empresas = _servicio.CargarListaEmpresas().Where(Function(e) e.Número = envio.Empresa).Single

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
                        Dim unused3 = DbContext.prdDesliquidar(empresaSeleccionada.Número, movimientoDesliq.Nº_Orden)
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

                    movimientoLiq = _servicio.CalcularMovimientoLiq(envio)

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
                        Dim unused2 = DbContext.PreContabilidad.Add(lineaDeshago)
                    End If
                    If importeNuevo <> 0 Then
                        Dim unused1 = DbContext.PreContabilidad.Add(lineaRehago)
                    End If
                    'DbContext.SaveChanges(SaveOptions.DetectChangesBeforeSave)
                    If DbContext.SaveChanges() Then
                        asiento = DbContext.prdContabilizar(envio.Empresa, diarioReembolsos, _configuracion.usuario)
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
                    Dim unused = DbContext.SaveChanges()
                Else
                    _dialogService.ShowError("Se ha producido un error y no se han grabado los datos")
                    Return -1
                End If
            End Using ' Cerramos contexto breve
        End Using ' Cerramos transacción

        Return asiento
    End Function
    Private Function buscarPedidoAmpliacion(pedido As CabPedidoVta) As EnviosAgencia
        Dim direccion = If(Not String.IsNullOrWhiteSpace(direccionEnvio), direccionEnvio, pedido.Clientes.Dirección)
        Dim pedidoEncontrado As EnviosAgencia = _servicio.CargarEnvioPorClienteYDireccion(pedido.Nº_Cliente, pedido.Contacto, direccion)
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
        deudas = _servicio.CargarDeudasCliente(pedidoSeleccionado.Nº_Cliente, fechaReclamar)
        Return If(deudas.Count = 0,
                  0.0,
                  Math.Round(
                      deudas.Sum(Function(l) CDbl(l.ImportePdte)),
                      2, MidpointRounding.AwayFromZero))
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
        RaisePropertyChanged(NameOf(NoEstaInsertandoPendiente))
    End Sub
    Public Sub EnvioPendienteSeleccionadoPropertyChangedEventHandler(sender As Object, e As PropertyChangedEventArgs)
        Dim envio = CType(sender, EnvioAgenciaWrapper)
        If e.PropertyName = "Pedido" Then
            Dim unused = CopiarDatosPedidoOriginal(envio.Pedido)
        End If
        If e.PropertyName <> "TieneCambios" Then
            envio.TieneCambios = True
            ActualizarEstadoComandos()
        End If
    End Sub

    Private Async Function CopiarDatosPedidoOriginal(numeroPedido As Integer?) As Task
        If IsNothing(numeroPedido) Then
            Return
        End If

        Dim pedidoExistente = listaPendientes.SingleOrDefault(Function(p) p.Pedido = numeroPedido AndAlso Not p.Equals(EnvioPendienteSeleccionado))
        If Not IsNothing(pedidoExistente) Then
            Dim unused = listaPendientes.Remove(EnvioPendienteSeleccionado)
            EnvioPendienteSeleccionado = pedidoExistente
            Return
        End If

        Dim pedido As CabPedidoVta = _servicio.CargarPedido(empresaSeleccionada.Número, numeroPedido)

        If IsNothing(pedido) Then
            _dialogService.ShowError("No se encuentra el pedido " + numeroPedido.ToString)
            Return
        End If

        Dim telefono As New Telefono(pedido.Clientes.Teléfono)
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
            .Telefono = telefono.FijoUnico
            .Movil = telefono.MovilUnico
            .Reembolso = Await _servicio.ImporteReembolso(pedido.Empresa, pedido.Número)
            .Pais = agenciaEspecifica.paisDefecto
            .Retorno = agenciaEspecifica.retornoSinRetorno
            .Servicio = agenciaEspecifica.ServicioDefecto
            .Horario = agenciaEspecifica.HorarioDefecto
        End With
        Return
    End Function

    Private Function CalcularCostoEnvio(tarifa As ITarifaAgencia, zona As ZonasEnvioAgencia, peso As Decimal, reembolso As Decimal) As Decimal
        ' Filtra los costos de envío para la zona especificada
        Dim costosFiltrados = tarifa.CosteEnvio.Where(Function(c) c.Item2 = zona).OrderBy(Function(c) c.Item1)

        ' Si no hay costos de envío para la zona especificada, devuelve 0
        If Not costosFiltrados.Any() Then
            Return Decimal.MaxValue
        End If

        ' Encuentra el primer costo de envío cuyo peso sea mayor o igual al peso proporcionado
        Dim costoSiguiente = costosFiltrados.FirstOrDefault(Function(c) c.Item1 >= peso)

        ' Si no se encuentra ningún costo de envío para el peso dado, devuelve el último costo de envío
        If costoSiguiente.Equals(Nothing) Then
            Return costosFiltrados.Last().Item3 + ((peso - costosFiltrados.Last().Item1) * tarifa.CosteKiloAdicional(zona))
        End If

        Dim costoReembolso = If(reembolso <> 0, tarifa.CosteReembolso(reembolso), 0)
        ' Devuelve el costo de envío correspondiente
        Return costoSiguiente.Item3 + costoReembolso
    End Function

    Private Function TarifaMasEconomica(codigoPostal As String, peso As Decimal, reembolso As Decimal) As KeyValuePair(Of ITarifaAgencia, Decimal)
        Dim zona As ZonasEnvioAgencia = CalcularZonaEnvio(codigoPostal)
        Dim costosTotales As New Dictionary(Of ITarifaAgencia, Decimal)

        ' Recorre todas las agencias en el factory
        For Each agenciaFactory In factory
            Dim agenciaFunc As Func(Of IAgencia) = agenciaFactory.Value
            Dim agencia As IAgencia = agenciaFunc()
            ' Recorre todos los servicios de la agencia actual
            For Each servicio In agencia.ListaServicios
                ' Calcula el costo total del envío para el servicio actual
                Dim costoTotal = CalcularCostoEnvio(servicio, zona, peso, reembolso)
                ' Guarda el costo total del envío junto con el nombre del servicio de la agencia
                costosTotales.Add(servicio, costoTotal)
            Next
        Next

        ' Encuentra el servicio con el costo total mínimo
        Dim servicioMasEconomico = costosTotales.OrderBy(Function(x) x.Value).First()

        ' Devuelve el nombre del servicio más económico
        Return servicioMasEconomico
    End Function

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

    Public Property id As Byte
    Public Property descripcion As String
End Structure

Public Structure tipoIdIntDescripcion
    Public Sub New(
   ByVal _id As Integer,
   ByVal _descripcion As String
   )
        id = _id
        descripcion = _descripcion
    End Sub

    Public Property id As Integer
    Public Property descripcion As String
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
            Dim unused = SetProperty(_listaExpediciones, value)
        End Set
    End Property

    Private _listaDigitalizaciones As New ObservableCollection(Of digitalizacion)
    Public Property listaDigitalizaciones As ObservableCollection(Of digitalizacion)
        Get
            Return _listaDigitalizaciones
        End Get
        Set(value As ObservableCollection(Of digitalizacion))
            Dim unused = SetProperty(_listaDigitalizaciones, value)
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
            Dim unused = SetProperty(_estadoTracking, value)
        End Set
    End Property

    Private _fechaTracking As Date
    Public Property fechaTracking As Date
        Get
            Return _fechaTracking
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaTracking, value)
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
            Dim unused = SetProperty(_tipo, value)
        End Set
    End Property

    Private _urlDigitalizacion As Uri
    Public Property urlDigitalizacion As Uri
        Get
            Return _urlDigitalizacion
        End Get
        Set(value As Uri)
            Dim unused = SetProperty(_urlDigitalizacion, value)
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
            Dim unused = SetProperty(_numeroExpedicion, value)
        End Set
    End Property

    Private _fecha As Date
    Public Property fecha As Date
        Get
            Return _fecha
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fecha, value)
        End Set
    End Property

    Private _fechaEstimada As Date
    Public Property fechaEstimada As Date
        Get
            Return _fechaEstimada
        End Get
        Set(value As Date)
            Dim unused = SetProperty(_fechaEstimada, value)
        End Set
    End Property

    Private _listaTracking As New ObservableCollection(Of tracking)
    Public Property listaTracking As ObservableCollection(Of tracking)
        Get
            Return _listaTracking
        End Get
        Set(value As ObservableCollection(Of tracking))
            Dim unused = SetProperty(_listaTracking, value)
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