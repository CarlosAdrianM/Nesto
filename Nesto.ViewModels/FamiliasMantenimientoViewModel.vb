Imports System.Collections.ObjectModel
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Prism.Commands
Imports Prism.Mvvm
Imports Prism.Services.Dialogs

''' <summary>
''' NestoAPI#406: mantenimiento de familias. Existe para una sola cosa: marcar qué familias se
''' venden al público al MISMO precio que al profesional (Weelko, Staleks, Unión Láser, Fama
''' Fabre, DDUUEETT...). Antes eso vivía en un script y un producto nuevo de esas marcas salía
''' a la venta un 42,86 % más caro sin avisar.
'''
''' Deliberadamente NO se pueden editar los demás campos de la familia: los porcentajes de
''' comisión mueven dinero de los vendedores y no tienen por qué tocarse desde aquí.
''' </summary>
Public Class FamiliasMantenimientoViewModel
    Inherits BindableBase

    Private ReadOnly _servicio As IServicioFamiliasMantenimiento
    Private ReadOnly _configuracion As IConfiguracion
    Private ReadOnly _dialogService As IDialogService

    Public Sub New(servicio As IServicioFamiliasMantenimiento, configuracion As IConfiguracion, dialogService As IDialogService)
        _servicio = servicio
        _configuracion = configuracion
        _dialogService = dialogService
        Titulo = "Mant. familias"
        _empresaSeleccionada = Constantes.Empresas.EMPRESA_DEFECTO
        GuardarCommand = New DelegateCommand(AddressOf OnGuardar, AddressOf CanGuardar)
        Dim unused = CargarAsync()
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

    ' Para el SelectorEmpresa de la vista.
    Public ReadOnly Property configuracion As IConfiguracion
        Get
            Return _configuracion
        End Get
    End Property

    Private _familias As ObservableCollection(Of FamiliaMantenimiento)
    Public Property Familias As ObservableCollection(Of FamiliaMantenimiento)
        Get
            Return _familias
        End Get
        Set(value As ObservableCollection(Of FamiliaMantenimiento))
            Dim unused = SetProperty(_familias, value)
            AplicarFiltro()
            GuardarCommand?.RaiseCanExecuteChanged()
        End Set
    End Property

    Private _familiasFiltradas As ObservableCollection(Of FamiliaMantenimiento)
    ''' <summary>Lo que ve la rejilla: mismas instancias que Familias, filtradas por el texto.</summary>
    Public Property FamiliasFiltradas As ObservableCollection(Of FamiliaMantenimiento)
        Get
            Return _familiasFiltradas
        End Get
        Private Set(value As ObservableCollection(Of FamiliaMantenimiento))
            Dim unused = SetProperty(_familiasFiltradas, value)
        End Set
    End Property

    Private _empresaSeleccionada As String
    Public Property empresaSeleccionada As String
        Get
            Return _empresaSeleccionada
        End Get
        Set(value As String)
            If SetProperty(_empresaSeleccionada, value) Then
                Dim unused = CargarAsync()
            End If
        End Set
    End Property

    Private _filtro As String
    ''' <summary>Son casi 300 familias: sin buscador no hay quien encuentre la suya.</summary>
    Public Property Filtro As String
        Get
            Return _filtro
        End Get
        Set(value As String)
            If SetProperty(_filtro, value) Then
                AplicarFiltro()
            End If
        End Set
    End Property

    Private _soloMarcadas As Boolean
    ''' <summary>Para ver de un vistazo cuáles están marcadas hoy.</summary>
    Public Property SoloMarcadas As Boolean
        Get
            Return _soloMarcadas
        End Get
        Set(value As Boolean)
            If SetProperty(_soloMarcadas, value) Then
                AplicarFiltro()
            End If
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

    Public Property GuardarCommand As DelegateCommand

    Public Async Function CargarAsync() As Task
        Try
            EstaOcupado = True
            Dim empresa = If(String.IsNullOrWhiteSpace(empresaSeleccionada), Constantes.Empresas.EMPRESA_DEFECTO, empresaSeleccionada)
            Dim lista = Await _servicio.LeerFamilias(empresa.Trim())

            ' Cargar NO es modificar. Newtonsoft deserializa asignando la PROPIEDAD, y el setter de
            ' PublicoIgualQueProfesional marca Modificada: las familias que ya venian marcadas
            ' llegaban dadas por modificadas sin que nadie las hubiera tocado. Efecto (31/08/2026):
            ' abrir la pantalla, marcar UNA familia y guardar mandaba al servidor las 6 que estaban
            ' marcadas, y el aviso decia "Guardadas 6 familia(s)". No republico el catalogo de las
            ' otras cinco solo porque la API ignora los PUT que no cambian nada; si no, habrian
            ' sido 1.064 productos republicados por marcar una casilla.
            For Each familia In lista
                familia.Modificada = False
            Next

            Familias = New ObservableCollection(Of FamiliaMantenimiento)(lista)
        Catch ex As Exception
            _dialogService.ShowError($"No se han podido cargar las familias: {ex.Message}")
        Finally
            EstaOcupado = False
        End Try
    End Function

    Private Sub AplicarFiltro()
        If Familias Is Nothing Then
            FamiliasFiltradas = New ObservableCollection(Of FamiliaMantenimiento)
            Return
        End If

        Dim consulta = Familias.AsEnumerable()

        If SoloMarcadas Then
            consulta = consulta.Where(Function(f) f.PublicoIgualQueProfesional)
        End If

        If Not String.IsNullOrWhiteSpace(Filtro) Then
            Dim texto = Filtro.Trim()
            consulta = consulta.Where(Function(f)
                                          Return (f.Numero IsNot Nothing AndAlso f.Numero.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0) OrElse
                                                 (f.Descripcion IsNot Nothing AndAlso f.Descripcion.IndexOf(texto, StringComparison.OrdinalIgnoreCase) >= 0)
                                      End Function)
        End If

        FamiliasFiltradas = New ObservableCollection(Of FamiliaMantenimiento)(consulta)
    End Sub

    Private Function CanGuardar() As Boolean
        Return Familias IsNot Nothing AndAlso Familias.Any()
    End Function

    ''' <summary>
    ''' Solo se envían las que ha tocado el usuario. Mandarlas todas haría que el servidor
    ''' republicara el catálogo entero cada vez que alguien abre la pantalla y pulsa Guardar.
    ''' </summary>
    Public Async Function GuardarAsync() As Task
        Dim modificadas = Familias.Where(Function(f) f.Modificada).ToList()

        For Each familia In modificadas
            Await _servicio.GuardarFamilia(familia)
            familia.Modificada = False
        Next
    End Function

    Private Async Sub OnGuardar()
        Try
            EstaOcupado = True
            Dim cuantas = If(Familias Is Nothing, 0, Familias.Where(Function(f) f.Modificada).Count())

            If cuantas = 0 Then
                _dialogService.ShowNotification("No hay ningún cambio que guardar")
                Return
            End If

            Await GuardarAsync()
            ' Se avisa de la consecuencia, que no es evidente: al marcar una familia cambia el
            ' precio público de TODOS sus productos en la web.
            _dialogService.ShowNotification(
                $"Guardadas {cuantas} familia(s). Los productos afectados se republicarán en la tienda en los próximos minutos.")
        Catch ex As Exception
            _dialogService.ShowError($"Error al guardar las familias: {ex.Message}")
        Finally
            EstaOcupado = False
        End Try
    End Sub

End Class
