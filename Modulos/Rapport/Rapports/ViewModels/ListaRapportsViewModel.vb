Imports System.Collections.ObjectModel
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO
Imports Prism
Imports Prism.Commands
Imports Prism.Events
Imports Prism.Regions
Imports Prism.Services.Dialogs
Imports Unity

Public Class ListaRapportsViewModel
    Inherits ViewModelBase
    Implements INavigationAware, IActiveAware

    Private ReadOnly regionManager As IRegionManager
    Public Property configuracion As IConfiguracion
    Private ReadOnly servicio As IRapportService
    Private ReadOnly container As IUnityContainer
    Private ReadOnly _dialogService As IDialogService
    Private ReadOnly _eventAggregator As IEventAggregator
    Private _subscriptionToken As SubscriptionToken
    Private ReadOnly _empresaPorDefecto As String = Constantes.Empresas.EMPRESA_DEFECTO
    Public Property vendedor As String



    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicio As IRapportService, container As IUnityContainer, dialogService As IDialogService, eventAggregator As IEventAggregator)
        Me.regionManager = regionManager
        Me.configuracion = configuracion
        Me.servicio = servicio
        Me.container = container
        _dialogService = dialogService
        _eventAggregator = eventAggregator

        cmdAbrirModulo = New DelegateCommand(Of Object)(AddressOf OnAbrirModulo, AddressOf CanAbrirModulo)
        CambiarModoComparativaCommand = New DelegateCommand(Of String)(Sub(valor) ModoComparativa = valor)
        CambiarAgruparPorCommand = New DelegateCommand(Of String)(Sub(valor) AgruparPor = valor)
        cmdCargarListaRapports = New DelegateCommand(Of Object)(AddressOf OnCargarListaRapports, AddressOf CanCargarListaRapports)
        cmdCargarListaRapportsFiltrada = New DelegateCommand(AddressOf OnCargarListaRapportsFiltrada, AddressOf CanCargarListaRapportsFiltrada)
        cmdCrearRapport = New DelegateCommand(Of ClienteProbabilidadVenta)(AddressOf OnCrearRapport, AddressOf CanCrearRapport)
        GenerarResumenCommand = New DelegateCommand(AddressOf OnGenerarResumen, AddressOf CanGenerarResumen)
        VerDetalleVentasCommand = New DelegateCommand(Of VentaClienteResumenDTO)(AddressOf OnVerDetalleVentas, AddressOf CanVerDetalleVentas)
        VolverAResumenVentasCommand = New DelegateCommand(AddressOf OnVolverAResumenVentas)
        AbrirFichaProductoCommand = New DelegateCommand(Of VentaClienteResumenDTO)(AddressOf OnAbrirFichaProducto, AddressOf CanAbrirFichaProducto)

        CopiarSeguimientosCommand = New DelegateCommand(AddressOf OnCopiarSeguimientos, AddressOf CanCopiarSeguimientos)

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
            cmdCargarListaRapports.RaiseCanExecuteChanged()
            CopiarSeguimientosCommand.RaiseCanExecuteChanged()
            If _clienteSeleccionado = String.Empty Then
                cmdCargarListaRapports.Execute(Nothing) ' Por fecha
            End If
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
            cmdCargarListaRapportsFiltrada.Execute()
        End Set
    End Property


    Private _grupoSubgrupoSeleccionado As String
    Public Property GrupoSubgrupoSeleccionado As String
        Get
            Return _grupoSubgrupoSeleccionado
        End Get
        Set(value As String)
            If SetProperty(_grupoSubgrupoSeleccionado, value) Then
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
                    Dim unused = SetProperty(_rapportSeleccionado, value)
                    Application.Current.Dispatcher.Invoke(Sub()
                                                              Dim parameters As New NavigationParameters From {
                                                                  {"rapportParameter", rapportSeleccionado}
                                                              }
                                                              regionManager.RequestNavigate("RapportDetailRegion", "RapportView", parameters)
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
    Private _cmdAbrirModulo As DelegateCommand(Of Object)
    Public Property cmdAbrirModulo As DelegateCommand(Of Object)
        Get
            Return _cmdAbrirModulo
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdAbrirModulo, value)
        End Set
    End Property
    Private Function CanAbrirModulo(arg As Object) As Boolean
        Return True
    End Function
    Private Sub OnAbrirModulo(arg As Object)
        regionManager.RequestNavigate("MainRegion", "ListaRapportsView")
    End Sub


    Public Property CambiarModoComparativaCommand As DelegateCommand(Of String)
    Public Property CambiarAgruparPorCommand As DelegateCommand(Of String)
    Private Async Sub OnCargarResumenVentas()
        MostrandoDetalleVentas = False
        LlamarApiResumenVentasAsync()
    End Sub


    Private _cmdCargarListaRapports As DelegateCommand(Of Object)
    Public Property cmdCargarListaRapports As DelegateCommand(Of Object)
        Get
            Return _cmdCargarListaRapports
        End Get
        Private Set(value As DelegateCommand(Of Object))
            Dim unused = SetProperty(_cmdCargarListaRapports, value)
        End Set
    End Property
    Private Function CanCargarListaRapports(arg As Object) As Boolean
        Return Not IsNothing(clienteSeleccionado) Or Not IsNothing(fechaSeleccionada)
    End Function
    Private Async Sub OnCargarListaRapports(arg As Object)
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(_empresaPorDefecto, "Vendedor")
        End If
        ResumenListaRapports = String.Empty
        If Not IsNothing(clienteSeleccionado) Then
            listaRapports = Await servicio.cargarListaRapports(_empresaPorDefecto, clienteSeleccionado, contactoSeleccionado)
            Await Task.Run(Sub() LlamarApiResumenVentasAsync())
        Else
            Dim parametroVendedor As String
            parametroVendedor = IIf(esUsuarioElVendedor, configuracion.usuario, vendedor)
            listaRapports = Await servicio.cargarListaRapports(parametroVendedor, fechaSeleccionada)
            rapportSeleccionado = listaRapports.FirstOrDefault
        End If
        GenerarResumenCommand.RaiseCanExecuteChanged()
    End Sub

    Private _cmdCargarListaRapportsFiltrada As DelegateCommand
    Public Property cmdCargarListaRapportsFiltrada As DelegateCommand
        Get
            Return _cmdCargarListaRapportsFiltrada
        End Get
        Private Set(value As DelegateCommand)
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

    Private _cmdCrearRapport As DelegateCommand(Of ClienteProbabilidadVenta)
    Public Property cmdCrearRapport As DelegateCommand(Of ClienteProbabilidadVenta)
        Get
            Return _cmdCrearRapport
        End Get
        Private Set(value As DelegateCommand(Of ClienteProbabilidadVenta))
            Dim unused = SetProperty(_cmdCrearRapport, value)
        End Set
    End Property

    Private Function CanCrearRapport(cliente As ClienteProbabilidadVenta) As Boolean
        Return True
    End Function
    Private Async Sub OnCrearRapport(cliente As ClienteProbabilidadVenta)

        If Not IsNothing(cliente) Then
            clienteSeleccionado = cliente.cliente
            contactoSeleccionado = cliente.contacto
            Try
                Await Task.Run(Sub() cmdCargarListaRapports.Execute(Nothing))
            Catch
                _dialogService.ShowError("No se ha podido cargar la lista de rapports")
            End Try
        End If


        Dim rapportNuevo As New SeguimientoClienteDTO With {
            .Empresa = _empresaPorDefecto,
            .Estado = SeguimientoClienteDTO.EstadoSeguimientoDTO.Vigente,
            .Fecha = IIf(fechaSeleccionada >= Today, Now, fechaSeleccionada),
            .Tipo = TipoRapportSeleccionado.id,
            .TipoCentro = SeguimientoClienteDTO.TiposCentro.NoSeSabe,
            .Vendedor = vendedor,
            .Usuario = configuracion.usuario
        }

        If Not IsNothing(cliente) Then
            rapportNuevo.Cliente = cliente.cliente
            rapportNuevo.Contacto = cliente.contacto
        End If

        'no entiendo por qué es necesaria esta línea
        'pero si la quitamos solo crea bien un rapport de cada dos
        rapportSeleccionado = Nothing

        rapportSeleccionado = rapportNuevo
        If IsNothing(listaRapports) Then
            listaRapports = New ObservableCollection(Of SeguimientoClienteDTO)
        End If
        listaRapports.Add(rapportNuevo)
    End Sub

    Public Property GenerarResumenCommand As DelegateCommand
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
            GenerarResumenCommand.RaiseCanExecuteChanged() ' Deshabilitar el botón tras generar el resumen
        End Try
    End Sub


    Public Property VerDetalleVentasCommand As DelegateCommand(Of VentaClienteResumenDTO)
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

    Public Property VolverAResumenVentasCommand As DelegateCommand
    Private Sub OnVolverAResumenVentas()
        MostrandoDetalleVentas = False
    End Sub

    Public Property AbrirFichaProductoCommand As DelegateCommand(Of VentaClienteResumenDTO)
    Private Function CanAbrirFichaProducto(venta As VentaClienteResumenDTO) As Boolean
        Return venta IsNot Nothing AndAlso venta.Nombre <> "TOTAL" AndAlso venta.Nombre.Contains(" - ")
    End Function
    Private Sub OnAbrirFichaProducto(venta As VentaClienteResumenDTO)
        If Not CanAbrirFichaProducto(venta) Then Exit Sub
        Dim productoId = venta.Nombre.Split({" - "}, 2, StringSplitOptions.None)(0).Trim()
        Dim parameters As New NavigationParameters From {
            {"numeroProductoParameter", productoId}
        }
        regionManager.RequestNavigate("MainRegion", "ProductoView", parameters)
    End Sub

    ' Comando para actualizar SelectedAction usando DelegateCommand
    Private _tipoRapportCambiaCommand As DelegateCommand(Of String)
    Public ReadOnly Property TipoRapportCambiaCommand As DelegateCommand(Of String)
        Get
            If _tipoRapportCambiaCommand Is Nothing Then
                _tipoRapportCambiaCommand = New DelegateCommand(Of String)(AddressOf OnTipoRapportCambia)
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

    Public Property CopiarSeguimientosCommand As DelegateCommand
    Private Function CanCopiarSeguimientos() As Boolean
        Return Not String.IsNullOrWhiteSpace(clienteSeleccionado)
    End Function
    Private Sub OnCopiarSeguimientos()
        Dim parameters As New DialogParameters From {
            {"empresa", _empresaPorDefecto},
            {"cliente", clienteSeleccionado},
            {"contacto", contactoSeleccionado}
        }
        _dialogService.ShowDialog("CopiarSeguimientosView", parameters, Sub(result)
                                                                            If result.Result = ButtonResult.OK Then
                                                                                cmdCargarListaRapports.Execute(Nothing)
                                                                            End If
                                                                        End Sub)
    End Sub




    Private Async Sub ActualizarClientesProbabilidad(grupoSubgrupo As String)
        IsLoadingClientesProbabilidad = True
        Try
            ListaClientesProbabilidad = New ObservableCollection(Of ClienteProbabilidadVenta)(Await servicio.CargarClientesProbabilidad(vendedor, TipoRapportSeleccionado.descripcion, grupoSubgrupo))
        Finally
            IsLoadingClientesProbabilidad = False
        End Try
    End Sub

    Private Sub OnLoaded()
        ' Suscríbete solo si no hay una suscripción activa
        If _subscriptionToken Is Nothing Then
            _subscriptionToken = _eventAggregator.GetEvent(Of RapportGuardadoEvent).Subscribe(AddressOf ActualizarClientesProbabilidad)
        End If
    End Sub

    Private Sub OnUnloaded()
        ' Desuscribirse del evento si hay una suscripción activa
        If _subscriptionToken IsNot Nothing Then
            _eventAggregator.GetEvent(Of RapportGuardadoEvent).Unsubscribe(_subscriptionToken)
            _subscriptionToken = Nothing
        End If
    End Sub


    Public Overrides Async Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo
        If IsNothing(vendedor) Then
            vendedor = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.Vendedor)
        End If
        If IsNothing(TipoRapportSeleccionado) OrElse IsNothing(TipoRapportSeleccionado.id) Then
            Dim tipo = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.UltTipoSeguimientoCliente)
            TipoRapportCambiaCommand.Execute(tipo)
        Else
            ActualizarClientesProbabilidad(GrupoSubgrupoSeleccionado)
        End If

        Dim permitirCopiar = Await configuracion.leerParametro(_empresaPorDefecto, Parametros.Claves.PermitirCopiarSeguimientos)
        PuedeCopiarSeguimientos = permitirCopiar = "1"
    End Sub

    Public Overrides Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return True
    End Function

    Public Overloads Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

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

    Private Async Sub LlamarApiResumenVentasAsync()
        If String.IsNullOrEmpty(clienteSeleccionado) Then Exit Sub

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
    End Sub

#End Region
End Class
