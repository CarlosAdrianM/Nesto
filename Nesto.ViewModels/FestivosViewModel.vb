Imports System.Collections.ObjectModel
Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services

''' <summary>
''' Nesto#616 (NestoAPI#779): mantenimiento del calendario de festivos (Herramientas › Recursos Humanos › Festivos).
''' Hasta ahora los festivos iban escritos en el código de la API y cada año hacía falta publicarla; un olvido
''' (como el 02/11/2026) descuadraba remesas, fechas de entrega, picking y avisos. Ahora están en la tabla Festivos.
'''
''' Nesto no decide nada de negocio: quién puede escribir (Administración, Dirección e Informática) y si la tabla existe
''' lo dice la API, y aquí se enseña su mensaje tal cual (403, 409).
''' </summary>
Public Class FestivosViewModel
    Inherits ObservableObject

    Public Const AVISO_ORIGEN_CODIGO As String = "Estos festivos vienen del programa, no de la tabla: dé de alta los del año para que cuenten."

    Private ReadOnly _servicio As IServicioFestivos
    Private ReadOnly _dialogService As IServicioDialogos

    Public Const AVISO_SOLO_REPARTO_PROPIO As String = "Usted solo puede dar de alta y de baja los días sin reparto propio (furgoneta); el resto de festivos los lleva Administración."
    Public Const ERROR_SOLO_REPARTO_PROPIO As String = "Usted solo puede dar de alta y de baja los días sin reparto propio (furgoneta)."

    Public Sub New(servicio As IServicioFestivos, dialogService As IServicioDialogos, configuracion As IConfiguracion)
        _servicio = servicio
        _dialogService = dialogService
        ' NestoAPI#841: Almacén y Logística (sin ser Administración, Dirección o Informática) solo el tipo RepartoPropio
        _soloRepartoPropio = PermisosFestivos.PuedeMantenerSoloRepartoPropio(configuracion)
        _opcionesTipo = If(_soloRepartoPropio,
            TiposFestivo.Opciones.Where(Function(o) o.Valor = TiposFestivo.REPARTO_PROPIO).ToList(),
            TiposFestivo.Opciones)
        Titulo = "Festivos"
        _anio = Date.Today.Year
        _nuevoTipo = If(_soloRepartoPropio, TiposFestivo.REPARTO_PROPIO, TiposFestivo.NACIONAL)
        _festivos = New ObservableCollection(Of Festivo)

        CargarCommand = New AsyncRelayCommand(AddressOf CargarAsync)
        AnioAnteriorCommand = New RelayCommand(Sub() Anio -= 1)
        AnioSiguienteCommand = New RelayCommand(Sub() Anio += 1)
        AnadirCommand = New AsyncRelayCommand(AddressOf AnadirAsync, AddressOf PuedeAnadir)
        BorrarCommand = New AsyncRelayCommand(AddressOf BorrarAsync, AddressOf PuedeBorrar)

        Dim unused = CargarAsync()
    End Sub

#Region "Propiedades"

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(value As String)
            Dim unused = SetProperty(_titulo, value)
        End Set
    End Property

    Private _anio As Integer
    ''' <summary>El año que se ve. Por defecto, el actual; al cambiarlo se recargan los festivos.</summary>
    Public Property Anio As Integer
        Get
            Return _anio
        End Get
        Set(value As Integer)
            If SetProperty(_anio, value) Then
                Dim unused = CargarAsync()
            End If
        End Set
    End Property

    Private _festivos As ObservableCollection(Of Festivo)
    Public Property Festivos As ObservableCollection(Of Festivo)
        Get
            Return _festivos
        End Get
        Private Set(value As ObservableCollection(Of Festivo))
            Dim unused = SetProperty(_festivos, value)
            OnPropertyChanged(NameOf(VienenDelCodigo))
            OnPropertyChanged(NameOf(AvisoOrigen))
            OnPropertyChanged(NameOf(Resumen))
        End Set
    End Property

    ''' <summary>La API los ha sacado de la lista del programa: la tabla todavía no tiene ese año.</summary>
    Public ReadOnly Property VienenDelCodigo As Boolean
        Get
            Return Festivos IsNot Nothing AndAlso Festivos.Any(Function(f) f.VieneDelCodigo)
        End Get
    End Property

    Public ReadOnly Property AvisoOrigen As String
        Get
            Return If(VienenDelCodigo, AVISO_ORIGEN_CODIGO, Nothing)
        End Get
    End Property

    Public ReadOnly Property Resumen As String
        Get
            Dim cuantos = If(Festivos Is Nothing, 0, Festivos.Count)
            If cuantos = 0 Then
                Return $"No hay festivos de {Anio}."
            End If
            Return If(cuantos = 1, $"1 festivo en {Anio}.", $"{cuantos} festivos en {Anio}.")
        End Get
    End Property

    Private _festivoSeleccionado As Festivo
    Public Property FestivoSeleccionado As Festivo
        Get
            Return _festivoSeleccionado
        End Get
        Set(value As Festivo)
            If SetProperty(_festivoSeleccionado, value) Then
                BorrarCommand?.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private ReadOnly _soloRepartoPropio As Boolean
    ''' <summary>NestoAPI#841: Almacén o Logística: solo puede dar de alta y de baja los días sin reparto propio (ve todos).</summary>
    Public ReadOnly Property SoloRepartoPropio As Boolean
        Get
            Return _soloRepartoPropio
        End Get
    End Property

    Public ReadOnly Property AvisoPermisos As String
        Get
            Return If(_soloRepartoPropio, AVISO_SOLO_REPARTO_PROPIO, Nothing)
        End Get
    End Property

    ''' <summary>Los tipos que puede dar de alta (NestoAPI#841: Almacén y Logística, solo RepartoPropio).</summary>
    Public ReadOnly Property Tipos As IReadOnlyList(Of String)
        Get
            Return OpcionesTipo.Select(Function(o) o.Valor).ToList()
        End Get
    End Property

    Private ReadOnly _opcionesTipo As IReadOnlyList(Of OpcionTipoFestivo)
    ''' <summary>NestoAPI#841: los tipos para el desplegable, con su etiqueta («Sin reparto propio (furgoneta)»); el valor va a <see cref="NuevoTipo"/>.</summary>
    Public ReadOnly Property OpcionesTipo As IReadOnlyList(Of OpcionTipoFestivo)
        Get
            Return _opcionesTipo
        End Get
    End Property

    Private Function PuedeTocarTipo(tipo As String) As Boolean
        Return Not _soloRepartoPropio OrElse String.Equals(tipo?.Trim(), TiposFestivo.REPARTO_PROPIO, StringComparison.OrdinalIgnoreCase)
    End Function

    Private _nuevaFecha As Date?
    Public Property NuevaFecha As Date?
        Get
            Return _nuevaFecha
        End Get
        Set(value As Date?)
            If SetProperty(_nuevaFecha, value) Then
                AnadirCommand?.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _nuevoTipo As String
    Public Property NuevoTipo As String
        Get
            Return _nuevoTipo
        End Get
        Set(value As String)
            If SetProperty(_nuevoTipo, value) Then
                AnadirCommand?.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    Private _nuevaDescripcion As String
    Public Property NuevaDescripcion As String
        Get
            Return _nuevaDescripcion
        End Get
        Set(value As String)
            If SetProperty(_nuevaDescripcion, value) Then
                AnadirCommand?.NotifyCanExecuteChanged()
            End If
        End Set
    End Property

    ''' <summary>Para el MaxLength de la descripción en la vista.</summary>
    Public ReadOnly Property LongitudDescripcion As Integer
        Get
            Return TiposFestivo.LONGITUD_DESCRIPCION
        End Get
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

#End Region

#Region "Comandos"

    Public Property CargarCommand As AsyncRelayCommand
    Public Property AnioAnteriorCommand As RelayCommand
    Public Property AnioSiguienteCommand As RelayCommand
    Public Property AnadirCommand As AsyncRelayCommand
    Public Property BorrarCommand As AsyncRelayCommand

    Public Async Function CargarAsync() As Task
        Dim anioPedido = Anio
        Try
            EstaOcupado = True
            Dim lista = Await _servicio.Leer(anioPedido)
            ' Si mientras tanto se ha cambiado de año, esta respuesta ya no vale (la del año nuevo llega por su cuenta)
            If anioPedido <> Anio Then
                Return
            End If
            Festivos = New ObservableCollection(Of Festivo)(If(lista, New List(Of Festivo)))
        Catch ex As Exception
            Festivos = New ObservableCollection(Of Festivo)
            _dialogService.ShowError($"No se han podido cargar los festivos de {anioPedido}: {ex.Message}")
        Finally
            EstaOcupado = False
        End Try
    End Function

    Private Function PuedeAnadir() As Boolean
        Return NuevaFecha.HasValue AndAlso Not String.IsNullOrWhiteSpace(NuevoTipo) AndAlso Not String.IsNullOrWhiteSpace(NuevaDescripcion) AndAlso
            PuedeTocarTipo(NuevoTipo)
    End Function

    ''' <summary>
    ''' Alta (o cambio de la descripción si ya está esa fecha con ese tipo) y recarga. Si la fecha es de otro año, se pasa a
    ''' ese año para que se vea lo dado de alta.
    ''' </summary>
    Public Async Function AnadirAsync() As Task
        If Not PuedeTocarTipo(NuevoTipo) Then
            _dialogService.ShowError(ERROR_SOLO_REPARTO_PROPIO)
            Return
        End If
        If Not PuedeAnadir() Then
            _dialogService.ShowError("Indique la fecha, el tipo y la descripción del festivo.")
            Return
        End If
        Dim peticion As New PeticionFestivo With {
            .Fecha = NuevaFecha.Value.Date,
            .Tipo = NuevoTipo,
            .Descripcion = NuevaDescripcion.Trim()
        }
        Try
            EstaOcupado = True
            Await _servicio.Guardar(peticion)
        Catch ex As FestivosException
            ' 403 sin permiso, 409 sin tabla, 400 con el motivo: el mensaje de la API, tal cual
            _dialogService.ShowError(ex.Message)
            Return
        Catch ex As Exception
            _dialogService.ShowError($"No se ha podido dar de alta el festivo: {ex.Message}")
            Return
        Finally
            EstaOcupado = False
        End Try

        NuevaDescripcion = Nothing
        If peticion.Fecha.Year <> Anio Then
            Anio = peticion.Fecha.Year ' el setter recarga
        Else
            Await CargarAsync()
        End If
    End Function

    Private Function PuedeBorrar() As Boolean
        ' Los que vienen del programa no están en la tabla: no hay nada que dar de baja
        ' NestoAPI#841: Almacén y Logística, solo los días sin reparto propio
        Return FestivoSeleccionado IsNot Nothing AndAlso Not FestivoSeleccionado.VieneDelCodigo AndAlso PuedeTocarTipo(FestivoSeleccionado.Tipo)
    End Function

    ''' <summary>Baja del festivo seleccionado, con confirmación, y recarga.</summary>
    Public Async Function BorrarAsync() As Task
        Dim festivo = FestivoSeleccionado
        If festivo Is Nothing OrElse festivo.VieneDelCodigo Then
            Return
        End If
        If Not PuedeTocarTipo(festivo.Tipo) Then
            _dialogService.ShowError(ERROR_SOLO_REPARTO_PROPIO)
            Return
        End If
        Dim pregunta = $"¿Quiere dar de baja el festivo del {festivo.DiaSemana.ToLower()} {festivo.Fecha:dd/MM/yyyy} ({festivo.TipoEtiqueta}): {festivo.Descripcion}?"
        If Not _dialogService.ShowConfirmationAnswer("Festivos", pregunta) Then
            Return
        End If
        Try
            EstaOcupado = True
            Await _servicio.Borrar(festivo.Fecha, festivo.Tipo)
        Catch ex As FestivosException
            _dialogService.ShowError(ex.Message)
            Return
        Catch ex As Exception
            _dialogService.ShowError($"No se ha podido dar de baja el festivo: {ex.Message}")
            Return
        Finally
            EstaOcupado = False
        End Try

        FestivoSeleccionado = Nothing
        Await CargarAsync()
    End Function

#End Region

End Class
