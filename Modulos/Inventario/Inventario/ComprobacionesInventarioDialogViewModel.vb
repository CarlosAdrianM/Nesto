Imports System.Collections.ObjectModel
Imports CommunityToolkit.Mvvm.Input
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#597 (gemela de NestoAPI#790): lo que se comprueba antes de contabilizar el inventario. Los bloqueantes (en rojo) no
''' dejan seguir; con solo avisos (en ámbar) se puede «Continuar de todos modos». Vuelve OK para seguir y Cancel si no.
''' NestoAPI#784: los bloqueantes que se confirman (SeConfirma, hoy RecontadaSinContar) dejan seguir solo si el usuario marca
''' «He contado y de verdad hay 0»; entonces el OK lleva <see cref="PARAMETRO_CONFIRMAR_CONTADO_CERO"/> = True. Los demás
''' bloqueantes (FilasDuplicadas…) no se saltan nunca.
''' </summary>
Public Class ComprobacionesInventarioDialogViewModel
    Inherits DialogoNestoBase

    Public Const NOMBRE As String = "ComprobacionesInventarioDialog"
    Public Const PARAMETRO_COMPROBACIONES As String = "comprobaciones"
    ''' <summary>NestoAPI#784: en el resultado OK, True si el usuario ha confirmado el contado 0 (ConfirmarContadoCero).</summary>
    Public Const PARAMETRO_CONFIRMAR_CONTADO_CERO As String = "confirmarContadoCero"
    Public Const TEXTO_CONFIRMAR_CONTADO_CERO As String = "He contado y de verdad hay 0"

    ''' <summary>Incidencia 1186: la empresa del inventario, para volver a pedir las comprobaciones tras borrar una fila.</summary>
    Public Const PARAMETRO_EMPRESA As String = "empresa"
    Public Const EMPRESA_DEFECTO As String = "1"
    Public Const TITULO_BORRAR_FILA As String = "Borrar esta fila"

    Private ReadOnly _servicio As IServicioInventario
    Private ReadOnly _dialogos As IServicioDialogos
    Private _empresa As String = EMPRESA_DEFECTO
    Private _comprobaciones As ComprobacionesInventarioDTO

    Public Sub New(servicio As IServicioInventario, dialogos As IServicioDialogos)
        _servicio = servicio
        _dialogos = dialogos
        Title = "Comprobaciones del inventario"
        ContinuarCommand = New RelayCommand(AddressOf OnContinuar, Function() PuedeContinuar AndAlso Not EstaOcupado)
        CancelarCommand = New RelayCommand(Sub() RaiseRequestClose(ResultadoBoton.Cancel))
        BorrarFilaCommand = New AsyncRelayCommand(Of FilaComprobacionInventarioDTO)(AddressOf BorrarFila,
            Function(f) f IsNot Nothing AndAlso f.SePuedeBorrar AndAlso Not EstaOcupado)
    End Sub

    Public Property ContinuarCommand As RelayCommand
    Public Property CancelarCommand As RelayCommand
    ''' <summary>Incidencia 1186: «Borrar esta fila» (una fila PENDIENTE de inventario; el stock no cambia).</summary>
    Public Property BorrarFilaCommand As AsyncRelayCommand(Of FilaComprobacionInventarioDTO)

    ''' <summary>Las comprobaciones que se enseñan (las de la apertura o las vueltas a pedir tras borrar una fila).</summary>
    Public ReadOnly Property Comprobaciones As ComprobacionesInventarioDTO
        Get
            Return _comprobaciones
        End Get
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Private Set(value As Boolean)
            If SetProperty(_estaOcupado, value) Then
                ContinuarCommand.NotifyCanExecuteChanged()
                BorrarFilaCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Public ReadOnly Property Bloqueantes As New ObservableCollection(Of ComprobacionInventarioDTO)
    Public ReadOnly Property Avisos As New ObservableCollection(Of ComprobacionInventarioDTO)
    Public ReadOnly Property Recuento As New ObservableCollection(Of RecuentoUsuarioInventarioDTO)

    Private _cabecera As String
    Public Property Cabecera As String
        Get
            Return _cabecera
        End Get
        Private Set(value As String)
            SetProperty(_cabecera, value)
        End Set
    End Property

    Private _confirmadoContadoCero As Boolean
    ''' <summary>NestoAPI#784: la casilla «He contado y de verdad hay 0».</summary>
    Public Property ConfirmadoContadoCero As Boolean
        Get
            Return _confirmadoContadoCero
        End Get
        Set(value As Boolean)
            If SetProperty(_confirmadoContadoCero, value) Then
                OnPropertyChanged(NameOf(PuedeContinuar))
                ContinuarCommand.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Public ReadOnly Property HayBloqueantes As Boolean
        Get
            Return Bloqueantes.Count > 0
        End Get
    End Property

    ''' <summary>NestoAPI#784: bloqueantes que no se pueden confirmar (FilasDuplicadas, CantidadDisparatada…).</summary>
    Public ReadOnly Property HayBloqueantesSinConfirmacion As Boolean
        Get
            Return Bloqueantes.Any(Function(b) Not b.SeConfirma)
        End Get
    End Property

    ''' <summary>NestoAPI#784: hay bloqueantes que se confirman y ninguno que no: se enseña la casilla.</summary>
    Public ReadOnly Property SePuedeConfirmarContadoCero As Boolean
        Get
            Return HayBloqueantes AndAlso Not HayBloqueantesSinConfirmacion
        End Get
    End Property

    Public ReadOnly Property HayAvisos As Boolean
        Get
            Return Avisos.Count > 0
        End Get
    End Property

    ''' <summary>El botón se enseña si no hay bloqueantes que no se confirman (con la casilla sin marcar sale desactivado).</summary>
    Public ReadOnly Property MostrarContinuar As Boolean
        Get
            Return Not HayBloqueantesSinConfirmacion
        End Get
    End Property

    Public ReadOnly Property PuedeContinuar As Boolean
        Get
            If Not HayBloqueantes Then
                Return True
            End If
            Return SePuedeConfirmarContadoCero AndAlso ConfirmadoContadoCero
        End Get
    End Property

    Public ReadOnly Property TextoContinuar As String
        Get
            Return If(HayAvisos OrElse HayBloqueantes, "Continuar de todos modos", "Continuar")
        End Get
    End Property

    Public ReadOnly Property Explicacion As String
        Get
            If HayBloqueantesSinConfirmacion Then
                Return "Hay problemas que impiden contabilizar el inventario. Corríjalos y vuelva a intentarlo. Si una fila sobra, " +
                    "bórrela con «" + TITULO_BORRAR_FILA + "» (el stock no cambia)."
            End If
            If HayBloqueantes Then
                Return "Hay productos que ya se contabilizaron ese día, se han vuelto a abrir y siguen con contado 0: si los " +
                    "contabiliza, su stock se quedará a cero. Cuéntelos o, si de verdad no hay ninguno, marque «" +
                    TEXTO_CONFIRMAR_CONTADO_CERO + "»."
            End If
            If HayAvisos Then
                Return "Revise los avisos: no impiden contabilizar, pero conviene comprobarlos antes."
            End If
            Return "No se ha encontrado ningún problema."
        End Get
    End Property

    Public Overrides Sub OnDialogOpened(parameters As ParametrosDialogo)
        If parameters IsNot Nothing AndAlso parameters.ContainsKey(PARAMETRO_EMPRESA) Then
            Dim empresa = parameters.GetValue(Of String)(PARAMETRO_EMPRESA)
            If Not String.IsNullOrWhiteSpace(empresa) Then
                _empresa = empresa.Trim()
            End If
        End If
        Cargar(parameters?.GetValue(Of ComprobacionesInventarioDTO)(PARAMETRO_COMPROBACIONES))
    End Sub

    ''' <summary>
    ''' Incidencia 1186: «Borrar esta fila» solo en filas de Inventarios (con Nº orden) y nunca en «Ubicaciones con stock que no
    ''' se han inventariado» (son huecos de Ubicaciones, no filas).
    ''' </summary>
    Public Shared Function SePuedeBorrar(comprobacion As ComprobacionInventarioDTO, fila As FilaComprobacionInventarioDTO) As Boolean
        If comprobacion Is Nothing OrElse fila Is Nothing Then
            Return False
        End If
        If String.Equals(comprobacion.Codigo, CodigosComprobacionInventario.UBICACION_SIN_RELLENAR, StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If
        Return fila.NumOrden.HasValue AndAlso fila.NumOrden.Value > 0
    End Function

    Public Sub Cargar(comprobaciones As ComprobacionesInventarioDTO)
        _comprobaciones = comprobaciones
        Bloqueantes.Clear()
        Avisos.Clear()
        Recuento.Clear()
        _confirmadoContadoCero = False
        If comprobaciones IsNot Nothing Then
            For Each c In If(comprobaciones.Bloqueantes, New List(Of ComprobacionInventarioDTO))
                MarcarFilasBorrables(c)
                Bloqueantes.Add(c)
            Next
            For Each c In If(comprobaciones.Avisos, New List(Of ComprobacionInventarioDTO))
                MarcarFilasBorrables(c)
                Avisos.Add(c)
            Next
            For Each r In If(comprobaciones.Recuento, New List(Of RecuentoUsuarioInventarioDTO))
                Recuento.Add(r)
            Next
            Cabecera = $"Inventario de {comprobaciones.Almacen?.Trim()} del {comprobaciones.Fecha:dd/MM/yyyy}"
        Else
            Cabecera = String.Empty
        End If
        OnPropertyChanged(NameOf(ConfirmadoContadoCero))
        OnPropertyChanged(NameOf(HayBloqueantes))
        OnPropertyChanged(NameOf(HayBloqueantesSinConfirmacion))
        OnPropertyChanged(NameOf(SePuedeConfirmarContadoCero))
        OnPropertyChanged(NameOf(HayAvisos))
        OnPropertyChanged(NameOf(MostrarContinuar))
        OnPropertyChanged(NameOf(PuedeContinuar))
        OnPropertyChanged(NameOf(TextoContinuar))
        OnPropertyChanged(NameOf(Explicacion))
        ContinuarCommand.NotifyCanExecuteChanged()
        BorrarFilaCommand.NotifyCanExecuteChanged()
    End Sub

    Private Shared Sub MarcarFilasBorrables(comprobacion As ComprobacionInventarioDTO)
        For Each f In If(comprobacion.Filas, New List(Of FilaComprobacionInventarioDTO))
            If f IsNot Nothing Then
                f.SePuedeBorrar = SePuedeBorrar(comprobacion, f)
            End If
        Next
    End Sub

    ''' <summary>
    ''' Incidencia 1186 (un mozo con una fila bloqueante que sobraba; hubo que borrarla por SQL): confirma (por defecto No), llama
    ''' a DELETE api/Inventarios/Filas/{Nº orden} y vuelve a pedir las comprobaciones. Si falla, enseña el mensaje y la ventana
    ''' sigue abierta.
    ''' </summary>
    Public Async Function BorrarFila(fila As FilaComprobacionInventarioDTO) As Task
        If fila Is Nothing OrElse Not fila.SePuedeBorrar OrElse Not fila.NumOrden.HasValue OrElse EstaOcupado Then
            Return
        End If
        If Not _dialogos.ShowConfirmationAnswer(TITULO_BORRAR_FILA, TextoConfirmarBorrado(fila), True) Then
            Return
        End If
        EstaOcupado = True
        Try
            Try
                Await _servicio.BorrarFila(fila.NumOrden.Value)
            Catch ex As FilaInventarioNoEncontradaException
                ' Ya no está: se avisa y se refresca igual.
                _dialogos.ShowError(ex.Message)
            Catch ex As Exception
                _dialogos.ShowError(ex.Message)
                Return
            End Try
            Await Refrescar()
        Finally
            EstaOcupado = False
        End Try
    End Function

    Private Async Function Refrescar() As Task
        If _comprobaciones Is Nothing Then
            Return
        End If
        Try
            Dim nuevas = Await _servicio.Comprobaciones(_empresa, _comprobaciones.Almacen?.Trim(), _comprobaciones.Fecha)
            If nuevas IsNot Nothing Then
                Cargar(nuevas)
            End If
        Catch ex As Exception
            _dialogos.ShowError("La fila se ha borrado, pero no se han podido volver a pedir las comprobaciones: " + ex.Message)
        End Try
    End Function

    Public Shared Function TextoConfirmarBorrado(fila As FilaComprobacionInventarioDTO) As String
        Dim producto = fila.Producto?.Trim()
        Dim descripcion = fila.Descripcion?.Trim()
        Dim ubicacion = If(String.IsNullOrWhiteSpace(fila.Ubicacion), "sin ubicación", fila.Ubicacion)
        Dim lineas As New List(Of String) From {
            "Se va a borrar esta fila del recuento de hoy. El stock no cambia.",
            String.Empty,
            "Producto: " + producto + If(String.IsNullOrWhiteSpace(descripcion), String.Empty, " - " + descripcion),
            "Ubicación: " + ubicacion,
            "Calculado: " + If(fila.StockCalculado.HasValue, fila.StockCalculado.Value.ToString(), "-"),
            "Contado: " + If(fila.StockReal.HasValue, fila.StockReal.Value.ToString(), "-"),
            String.Empty,
            "¿Borrarla?"
        }
        Return String.Join(Environment.NewLine, lineas)
    End Function

    Private Sub OnContinuar()
        If Not PuedeContinuar Then
            Return
        End If
        ' Incidencia 1186: se devuelven las comprobaciones que se ven (tras borrar filas ya no son las de la apertura).
        Dim parametros As New ParametrosDialogo From {
            {PARAMETRO_CONFIRMAR_CONTADO_CERO, SePuedeConfirmarContadoCero AndAlso ConfirmadoContadoCero},
            {PARAMETRO_COMPROBACIONES, _comprobaciones}
        }
        RaiseRequestClose(New ResultadoDialogo(ResultadoBoton.OK, parametros))
    End Sub
End Class
