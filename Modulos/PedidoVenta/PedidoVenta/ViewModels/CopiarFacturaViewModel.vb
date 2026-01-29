Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs
Imports Nesto.Modulos.PedidoVenta.Models.Rectificativas
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports ControlesUsuario.Models

Public Class CopiarFacturaViewModel
    Inherits BindableBase
    Implements IDialogAware

    Private ReadOnly _servicio As IPedidoVentaService
    Private ReadOnly _configuracion As IConfiguracion

#Region "Propiedades"

    Private _titulo As String = "Copiar Factura / Crear Rectificativa"
    Public ReadOnly Property Title As String Implements IDialogAware.Title
        Get
            Return _titulo
        End Get
    End Property

    Private _empresa As String
    Public Property Empresa As String
        Get
            Return _empresa
        End Get
        Set(value As String)
            SetProperty(_empresa, value)
        End Set
    End Property

    Private _cliente As String
    Public Property Cliente As String
        Get
            Return _cliente
        End Get
        Set(value As String)
            If SetProperty(_cliente, value) Then
                RaisePropertyChanged(NameOf(TieneCliente))
            End If
        End Set
    End Property

    Private _contacto As String = "0"
    Public Property Contacto As String
        Get
            Return _contacto
        End Get
        Set(value As String)
            SetProperty(_contacto, value)
        End Set
    End Property

    Private _clienteCompleto As ControlesUsuario.Models.ClienteDTO
    Public Property ClienteCompleto As ControlesUsuario.Models.ClienteDTO
        Get
            Return _clienteCompleto
        End Get
        Set(value As ControlesUsuario.Models.ClienteDTO)
            SetProperty(_clienteCompleto, value)
        End Set
    End Property

    Private _numeroFactura As String
    Public Property NumeroFactura As String
        Get
            Return _numeroFactura
        End Get
        Set(value As String)
            If SetProperty(_numeroFactura, value) Then
                EjecutarCommand.RaiseCanExecuteChanged()
                BuscarClienteCommand?.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _invertirCantidades As Boolean = True
    Public Property InvertirCantidades As Boolean
        Get
            Return _invertirCantidades
        End Get
        Set(value As Boolean)
            SetProperty(_invertirCantidades, value)
        End Set
    End Property

    Private _anadirAPedidoOriginal As Boolean = False
    Public Property AnadirAPedidoOriginal As Boolean
        Get
            Return _anadirAPedidoOriginal
        End Get
        Set(value As Boolean)
            SetProperty(_anadirAPedidoOriginal, value)
        End Set
    End Property

    Private _mantenerCondicionesOriginales As Boolean = True
    Public Property MantenerCondicionesOriginales As Boolean
        Get
            Return _mantenerCondicionesOriginales
        End Get
        Set(value As Boolean)
            SetProperty(_mantenerCondicionesOriginales, value)
        End Set
    End Property

    Private _crearAlbaranYFactura As Boolean = True
    Public Property CrearAlbaranYFactura As Boolean
        Get
            Return _crearAlbaranYFactura
        End Get
        Set(value As Boolean)
            SetProperty(_crearAlbaranYFactura, value)
        End Set
    End Property

    Private _crearAbonoYCargo As Boolean = False
    ''' <summary>
    ''' Si true, crea abono al cliente origen + cargo al cliente destino en un solo clic.
    ''' Util para corregir errores de direccion o traspasos de cliente.
    ''' </summary>
    Public Property CrearAbonoYCargo As Boolean
        Get
            Return _crearAbonoYCargo
        End Get
        Set(value As Boolean)
            SetProperty(_crearAbonoYCargo, value)
        End Set
    End Property

    Private _comentarios As String
    ''' <summary>
    ''' Comentarios a anadir en el pedido creado.
    ''' </summary>
    Public Property Comentarios As String
        Get
            Return _comentarios
        End Get
        Set(value As String)
            SetProperty(_comentarios, value)
        End Set
    End Property

    Private _clienteDestino As String
    Public Property ClienteDestino As String
        Get
            Return _clienteDestino
        End Get
        Set(value As String)
            SetProperty(_clienteDestino, value)
            RaisePropertyChanged(NameOf(EsCambioCliente))
        End Set
    End Property

    Private _contactoDestino As String = "0"
    Public Property ContactoDestino As String
        Get
            Return _contactoDestino
        End Get
        Set(value As String)
            SetProperty(_contactoDestino, value)
        End Set
    End Property

    Private _clienteCompletoDestino As ControlesUsuario.Models.ClienteDTO
    Public Property ClienteCompletoDestino As ControlesUsuario.Models.ClienteDTO
        Get
            Return _clienteCompletoDestino
        End Get
        Set(value As ControlesUsuario.Models.ClienteDTO)
            SetProperty(_clienteCompletoDestino, value)
        End Set
    End Property

    Public ReadOnly Property EsCambioCliente As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(ClienteDestino) AndAlso ClienteDestino.Trim() <> Cliente?.Trim()
        End Get
    End Property

    Private _estaProcesando As Boolean = False
    Public Property EstaProcesando As Boolean
        Get
            Return _estaProcesando
        End Get
        Set(value As Boolean)
            SetProperty(_estaProcesando, value)
            EjecutarCommand.RaiseCanExecuteChanged()
            CancelarCommand.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _mensaje As String
    Public Property Mensaje As String
        Get
            Return _mensaje
        End Get
        Set(value As String)
            SetProperty(_mensaje, value)
        End Set
    End Property

    Private _resultado As CopiarFacturaResponseDTO
    Public Property Resultado As CopiarFacturaResponseDTO
        Get
            Return _resultado
        End Get
        Set(value As CopiarFacturaResponseDTO)
            SetProperty(_resultado, value)
            RaisePropertyChanged(NameOf(MostrarResultado))
            RaisePropertyChanged(NameOf(TieneAlbaran))
            RaisePropertyChanged(NameOf(TieneFactura))
        End Set
    End Property

    ''' <summary>
    ''' Indica si hay un resultado exitoso para mostrar
    ''' </summary>
    Public ReadOnly Property MostrarResultado As Boolean
        Get
            Return Resultado IsNot Nothing AndAlso Resultado.Exitoso
        End Get
    End Property

    ''' <summary>
    ''' Indica si el resultado tiene un numero de albaran
    ''' </summary>
    Public ReadOnly Property TieneAlbaran As Boolean
        Get
            Return Resultado IsNot Nothing AndAlso Resultado.NumeroAlbaran.HasValue
        End Get
    End Property

    ''' <summary>
    ''' Indica si el resultado tiene un numero de factura
    ''' </summary>
    Public ReadOnly Property TieneFactura As Boolean
        Get
            Return Resultado IsNot Nothing AndAlso Not String.IsNullOrEmpty(Resultado.NumeroFactura)
        End Get
    End Property

    ''' <summary>
    ''' Indica si hay un cliente seleccionado para mostrar el SelectorFacturas.
    ''' </summary>
    Public ReadOnly Property TieneCliente As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(Cliente)
        End Get
    End Property

    Private _facturasSeleccionadas As ObservableCollection(Of FacturaClienteDTO)
    ''' <summary>
    ''' Facturas seleccionadas en el SelectorFacturas.
    ''' </summary>
    Public Property FacturasSeleccionadas As ObservableCollection(Of FacturaClienteDTO)
        Get
            Return _facturasSeleccionadas
        End Get
        Set(value As ObservableCollection(Of FacturaClienteDTO))
            If SetProperty(_facturasSeleccionadas, value) Then
                RaisePropertyChanged(NameOf(TieneMultiplesFacturasSeleccionadas))
                EjecutarCommand?.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _facturaSeleccionadaEnLista As FacturaClienteDTO
    ''' <summary>
    ''' Factura actualmente seleccionada (fila activa) en el SelectorFacturas.
    ''' </summary>
    Public Property FacturaSeleccionadaEnLista As FacturaClienteDTO
        Get
            Return _facturaSeleccionadaEnLista
        End Get
        Set(value As FacturaClienteDTO)
            If SetProperty(_facturaSeleccionadaEnLista, value) Then
                ' Actualizar NumeroFactura con el documento de la factura seleccionada
                If value IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(value.Documento) Then
                    NumeroFactura = value.Documento.Trim()
                End If
            End If
        End Set
    End Property

    Private _agruparFacturas As Boolean = True
    ''' <summary>
    ''' Si true, agrupa todas las facturas seleccionadas en una sola rectificativa.
    ''' Si false, crea una rectificativa por cada factura seleccionada.
    ''' </summary>
    Public Property AgruparFacturas As Boolean
        Get
            Return _agruparFacturas
        End Get
        Set(value As Boolean)
            SetProperty(_agruparFacturas, value)
        End Set
    End Property

    ''' <summary>
    ''' Indica si hay mas de una factura seleccionada (para mostrar opcion de agrupar).
    ''' </summary>
    Public ReadOnly Property TieneMultiplesFacturasSeleccionadas As Boolean
        Get
            Return FacturasSeleccionadas IsNot Nothing AndAlso FacturasSeleccionadas.Count > 1
        End Get
    End Property

#End Region

#Region "Commands"

    Private _ejecutarCommand As DelegateCommand
    Public Property EjecutarCommand As DelegateCommand
        Get
            If _ejecutarCommand Is Nothing Then
                _ejecutarCommand = New DelegateCommand(AddressOf OnEjecutar, AddressOf CanEjecutar)
            End If
            Return _ejecutarCommand
        End Get
        Set(value As DelegateCommand)
            _ejecutarCommand = value
        End Set
    End Property

    Private _cancelarCommand As DelegateCommand
    Public Property CancelarCommand As DelegateCommand
        Get
            If _cancelarCommand Is Nothing Then
                _cancelarCommand = New DelegateCommand(AddressOf OnCancelar, AddressOf CanCancelar)
            End If
            Return _cancelarCommand
        End Get
        Set(value As DelegateCommand)
            _cancelarCommand = value
        End Set
    End Property

    Private _abrirPedidoCommand As DelegateCommand
    Public Property AbrirPedidoCommand As DelegateCommand
        Get
            If _abrirPedidoCommand Is Nothing Then
                _abrirPedidoCommand = New DelegateCommand(AddressOf OnAbrirPedido, AddressOf CanAbrirPedido)
            End If
            Return _abrirPedidoCommand
        End Get
        Set(value As DelegateCommand)
            _abrirPedidoCommand = value
        End Set
    End Property

    Private _buscarClienteCommand As DelegateCommand
    Public Property BuscarClienteCommand As DelegateCommand
        Get
            If _buscarClienteCommand Is Nothing Then
                _buscarClienteCommand = New DelegateCommand(AddressOf OnBuscarCliente, AddressOf CanBuscarCliente)
            End If
            Return _buscarClienteCommand
        End Get
        Set(value As DelegateCommand)
            _buscarClienteCommand = value
        End Set
    End Property

#End Region

#Region "Constructor"

    Public Sub New(servicio As IPedidoVentaService, configuracion As IConfiguracion)
        _servicio = servicio
        _configuracion = configuracion
        _empresa = Constantes.Empresas.EMPRESA_DEFECTO
    End Sub

#End Region

#Region "IDialogAware"

    Public Event RequestClose As Action(Of IDialogResult) Implements IDialogAware.RequestClose

    Public Sub OnDialogOpened(parameters As IDialogParameters) Implements IDialogAware.OnDialogOpened
        If parameters IsNot Nothing Then
            If parameters.ContainsKey("empresa") Then
                Empresa = parameters.GetValue(Of String)("empresa")
            End If
            If parameters.ContainsKey("cliente") Then
                Cliente = parameters.GetValue(Of String)("cliente")
            End If
            If parameters.ContainsKey("numeroFactura") Then
                NumeroFactura = parameters.GetValue(Of String)("numeroFactura")
            End If
        End If
    End Sub

    Public Sub OnDialogClosed() Implements IDialogAware.OnDialogClosed
        ' Cleanup si necesario
    End Sub

    Public Function CanCloseDialog() As Boolean Implements IDialogAware.CanCloseDialog
        Return Not EstaProcesando
    End Function

#End Region

#Region "Command Handlers"

    Private Function CanEjecutar() As Boolean
        ' Puede ejecutar si no esta procesando Y (tiene numero de factura O tiene facturas seleccionadas)
        Dim tieneFacturaManual = Not String.IsNullOrWhiteSpace(NumeroFactura)
        Dim tieneFacturasSeleccionadas = FacturasSeleccionadas IsNot Nothing AndAlso FacturasSeleccionadas.Any()
        Return Not EstaProcesando AndAlso (tieneFacturaManual OrElse tieneFacturasSeleccionadas)
    End Function

    Private Async Sub OnEjecutar()
        EstaProcesando = True
        Mensaje = "Procesando..."

        Try
            ' Construir lista de facturas a copiar
            Dim numerosFactura As List(Of String) = Nothing
            If FacturasSeleccionadas IsNot Nothing AndAlso FacturasSeleccionadas.Any() Then
                numerosFactura = FacturasSeleccionadas.Select(Function(f) f.Documento?.Trim()).Where(Function(d) Not String.IsNullOrWhiteSpace(d)).ToList()
            End If

            Dim request As New CopiarFacturaRequestDTO With {
                .Empresa = Empresa,
                .Cliente = Cliente,
                .NumeroFactura = NumeroFactura,
                .NumerosFactura = numerosFactura,
                .AgruparEnUnaRectificativa = AgruparFacturas,
                .InvertirCantidades = InvertirCantidades,
                .AnadirAPedidoOriginal = AnadirAPedidoOriginal,
                .MantenerCondicionesOriginales = MantenerCondicionesOriginales,
                .CrearAlbaranYFactura = CrearAlbaranYFactura,
                .CrearAbonoYCargo = CrearAbonoYCargo,
                .Comentarios = Comentarios,
                .ClienteDestino = If(EsCambioCliente OrElse CrearAbonoYCargo, ClienteDestino, Nothing),
                .ContactoDestino = If(EsCambioCliente OrElse CrearAbonoYCargo, ContactoDestino, Nothing)
            }

            Resultado = Await _servicio.CopiarFactura(request)

            If Resultado.Exitoso Then
                Mensaje = Resultado.Mensaje
                AbrirPedidoCommand.RaiseCanExecuteChanged()
            Else
                Mensaje = $"Error: {Resultado.Mensaje}"
            End If

        Catch ex As Exception
            Mensaje = $"Error: {ex.Message}"
        Finally
            EstaProcesando = False
        End Try
    End Sub

    Private Function CanCancelar() As Boolean
        Return Not EstaProcesando
    End Function

    Private Sub OnCancelar()
        RaiseEvent RequestClose(New DialogResult(ButtonResult.Cancel))
    End Sub

    Private Function CanAbrirPedido() As Boolean
        Return Resultado IsNot Nothing AndAlso Resultado.Exitoso AndAlso Resultado.NumeroPedido > 0
    End Function

    Private Sub OnAbrirPedido()
        ' Cerrar y devolver el numero de pedido para que el llamador lo abra
        Dim parametros As New DialogParameters From {
            {"numeroPedido", Resultado.NumeroPedido}
        }
        RaiseEvent RequestClose(New DialogResult(ButtonResult.OK, parametros))
    End Sub

#End Region

#Region "Buscar Cliente"

    Private Function CanBuscarCliente() As Boolean
        Return Not EstaProcesando AndAlso Not String.IsNullOrWhiteSpace(NumeroFactura)
    End Function

    Private Async Sub OnBuscarCliente()
        Await BuscarClientePorFacturaAsync()
    End Sub

    ''' <summary>
    ''' Busca el cliente de la factura y lo rellena automaticamente
    ''' </summary>
    Private Async Function BuscarClientePorFacturaAsync() As Task
        If String.IsNullOrWhiteSpace(NumeroFactura) Then
            Return
        End If

        Try
            Mensaje = "Buscando cliente..."
            EstaProcesando = True
            Dim resultado = Await _servicio.ObtenerClientePorFactura(Empresa, NumeroFactura)
            If resultado IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(resultado.Cliente) Then
                ' Actualizar empresa si es diferente (factura encontrada en otra empresa)
                If Not String.IsNullOrWhiteSpace(resultado.Empresa) AndAlso resultado.Empresa.Trim() <> Empresa?.Trim() Then
                    Empresa = resultado.Empresa.Trim()
                End If
                ' Limpiar primero para forzar que el SelectorCliente detecte el cambio
                Cliente = Nothing
                Contacto = Nothing
                ' Dar un momento para que el binding se actualice
                Await Task.Delay(50)
                ' Ahora establecer los valores correctos
                Cliente = resultado.Cliente.Trim()
                Contacto = "0" ' Contacto por defecto
                Mensaje = $"Cliente {resultado.Cliente} encontrado (Empresa: {resultado.Empresa})"
            Else
                Mensaje = "No se encontró la factura ni el pedido"
            End If
        Catch ex As Exception
            Mensaje = $"Error al buscar factura: {ex.Message}"
        Finally
            EstaProcesando = False
        End Try
    End Function

#End Region

End Class
