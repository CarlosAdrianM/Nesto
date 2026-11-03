Imports System.Collections.ObjectModel
Imports System.Globalization
Imports CommunityToolkit.Mvvm.ComponentModel
Imports CommunityToolkit.Mvvm.Input
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#597 (gemela de NestoAPI#790): las diferencias del inventario (StockReal ≠ StockCalculado), valoradas como diga la API
''' (PVP para las tiendas, coste para el resto: aquí no se calcula ningún coste, solo se enseña y se suma lo que llega). Se abre
''' tras contabilizar, con los traspasos, o sola por fecha (lo pendiente de ese día) o por número de traspaso.
''' </summary>
Public Class DiferenciasInventarioViewModel
    Inherits ObservableObject
    Implements IReceptorNavegacionPestanaNueva

    Public Const NOMBRE_VISTA As String = "DiferenciasInventarioView"
    Public Const PARAMETRO_ALMACEN As String = "almacen"
    Public Const PARAMETRO_FECHA As String = "fecha"
    Public Const PARAMETRO_TRASPASOS As String = "traspasos"

    Private ReadOnly _servicio As IServicioInventario
    Private ReadOnly _dialogos As IServicioDialogos
    Private _todas As New List(Of LineaDiferenciaInventarioDTO)

    Public Sub New(servicio As IServicioInventario, dialogos As IServicioDialogos)
        _servicio = servicio
        _dialogos = dialogos
        Titulo = "Diferencias de inventario"
        CargarCommand = New AsyncRelayCommand(AddressOf Cargar)
    End Sub

    Public Property Titulo As String
    Public ReadOnly Property CargarCommand As AsyncRelayCommand

    Public ReadOnly Property Lineas As New ObservableCollection(Of LineaDiferenciaInventarioDTO)
    Public ReadOnly Property PorGrupo As New ObservableCollection(Of TotalGrupoDiferenciasInventarioDTO)
    Public ReadOnly Property PorFamilia As New ObservableCollection(Of TotalFamiliaDiferenciasInventarioDTO)

    Private _almacen As String
    Public Property Almacen As String
        Get
            Return _almacen
        End Get
        Set(value As String)
            SetProperty(_almacen, value)
        End Set
    End Property

    Private _fecha As Date?
    Public Property Fecha As Date?
        Get
            Return _fecha
        End Get
        Set(value As Date?)
            SetProperty(_fecha, value)
        End Set
    End Property

    ''' <summary>Números de traspaso separados por comas o espacios; si hay alguno, manda sobre la fecha.</summary>
    Private _textoTraspasos As String
    Public Property TextoTraspasos As String
        Get
            Return _textoTraspasos
        End Get
        Set(value As String)
            SetProperty(_textoTraspasos, value)
        End Set
    End Property

    ''' <summary>Solo las líneas con |ValorDiferencia| ≥ este importe (0: todas).</summary>
    Private _importeMinimo As Decimal
    Public Property ImporteMinimo As Decimal
        Get
            Return _importeMinimo
        End Get
        Set(value As Decimal)
            If SetProperty(_importeMinimo, Math.Abs(value)) Then
                Filtrar()
            End If
        End Set
    End Property

    Private _valoracion As String
    ''' <summary>Lo que dice la API: «PVP» o «Coste».</summary>
    Public Property Valoracion As String
        Get
            Return _valoracion
        End Get
        Private Set(value As String)
            If SetProperty(_valoracion, value) Then
                OnPropertyChanged(NameOf(TextoValoracion))
            End If
        End Set
    End Property

    Public ReadOnly Property TextoValoracion As String
        Get
            If String.IsNullOrWhiteSpace(Valoracion) Then
                Return String.Empty
            End If
            Dim valor = Valoracion.Trim()
            Return "Valoración: " + If(String.Equals(valor, "PVP", StringComparison.OrdinalIgnoreCase), "PVP", valor.ToLower(CultureInfo.CurrentCulture))
        End Get
    End Property

    Private _cabecera As String
    Public Property Cabecera As String
        Get
            Return _cabecera
        End Get
        Private Set(value As String)
            SetProperty(_cabecera, value)
        End Set
    End Property

    Private _totalFilas As Integer
    Public Property TotalFilas As Integer
        Get
            Return _totalFilas
        End Get
        Private Set(value As Integer)
            SetProperty(_totalFilas, value)
        End Set
    End Property

    Private _totalUnidades As Integer
    Public Property TotalUnidades As Integer
        Get
            Return _totalUnidades
        End Get
        Private Set(value As Integer)
            SetProperty(_totalUnidades, value)
        End Set
    End Property

    Private _totalValor As Decimal
    Public Property TotalValor As Decimal
        Get
            Return _totalValor
        End Get
        Private Set(value As Decimal)
            SetProperty(_totalValor, value)
        End Set
    End Property

    Private _textoTotales As String
    Public Property TextoTotales As String
        Get
            Return _textoTotales
        End Get
        Private Set(value As String)
            SetProperty(_textoTotales, value)
        End Set
    End Property

    Private _estaOcupado As Boolean
    Public Property EstaOcupado As Boolean
        Get
            Return _estaOcupado
        End Get
        Private Set(value As Boolean)
            SetProperty(_estaOcupado, value)
        End Set
    End Property

    Public Async Sub AlLlegar(parametros As ParametrosNavegacion) Implements IReceptorNavegacion.AlLlegar
        If parametros Is Nothing Then
            Return
        End If
        Almacen = parametros.GetValue(Of String)(PARAMETRO_ALMACEN)
        If parametros.ContainsKey(PARAMETRO_FECHA) Then
            Fecha = parametros.GetValue(Of Date?)(PARAMETRO_FECHA)
        End If
        Dim traspasos = parametros.GetValue(Of IEnumerable(Of Integer))(PARAMETRO_TRASPASOS)
        TextoTraspasos = If(traspasos Is Nothing, String.Empty, String.Join(", ", traspasos))
        If Not String.IsNullOrWhiteSpace(Almacen) AndAlso (Fecha.HasValue OrElse LeerTraspasos().Any()) Then
            Await Cargar()
        End If
    End Sub

    ''' <summary>Los números de <see cref="TextoTraspasos"/> (lo que no es un número positivo se ignora).</summary>
    Public Function LeerTraspasos() As List(Of Integer)
        Dim resultado As New List(Of Integer)
        If String.IsNullOrWhiteSpace(TextoTraspasos) Then
            Return resultado
        End If
        For Each trozo In TextoTraspasos.Split({","c, ";"c, " "c, ControlChars.Tab}, StringSplitOptions.RemoveEmptyEntries)
            Dim numero As Integer
            If Integer.TryParse(trozo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, numero) AndAlso numero > 0 AndAlso Not resultado.Contains(numero) Then
                resultado.Add(numero)
            End If
        Next
        Return resultado
    End Function

    Public Async Function Cargar() As Task
        If String.IsNullOrWhiteSpace(Almacen) Then
            _dialogos.ShowError("Indique el almacén.")
            Return
        End If
        Dim traspasos = LeerTraspasos()
        If Not Fecha.HasValue AndAlso Not traspasos.Any() Then
            _dialogos.ShowError("Indique la fecha del inventario o el traspaso.")
            Return
        End If
        EstaOcupado = True
        Try
            Dim fechaPedida As Date? = If(traspasos.Any(), CType(Nothing, Date?), Fecha.Value.Date)
            Dim diferencias = Await _servicio.Diferencias(Constantes.Empresas.EMPRESA_DEFECTO, Almacen.Trim(), fechaPedida, traspasos)
            Mostrar(diferencias)
        Catch ex As Exception
            _dialogos.ShowError(ex.Message)
        Finally
            EstaOcupado = False
        End Try
    End Function

    ''' <summary>Pone lo que devuelve la API (público para los tests).</summary>
    Public Sub Mostrar(diferencias As DiferenciasInventarioDTO)
        _todas = If(diferencias?.Lineas, New List(Of LineaDiferenciaInventarioDTO))
        Valoracion = diferencias?.Valoracion
        If diferencias Is Nothing Then
            Cabecera = String.Empty
        Else
            Dim de = If(diferencias.Traspasos IsNot Nothing AndAlso diferencias.Traspasos.Any(),
                        If(diferencias.Traspasos.Count = 1, "traspaso ", "traspasos ") + String.Join(", ", diferencias.Traspasos),
                        If(diferencias.Fecha.HasValue, "pendiente del " + diferencias.Fecha.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), String.Empty))
            Cabecera = $"Inventario de {diferencias.Almacen?.Trim()} · {de}"
        End If
        Filtrar()
    End Sub

    ''' <summary>Aplica el importe mínimo, ordena por valor (de mayor a menor en valor absoluto) y rehace los totales.</summary>
    Private Sub Filtrar()
        Dim minimo = ImporteMinimo
        Dim filtradas = _todas.
            Where(Function(l) Math.Abs(l.ValorDiferencia) >= minimo).
            OrderByDescending(Function(l) Math.Abs(l.ValorDiferencia)).
            ThenBy(Function(l) l.Producto, StringComparer.OrdinalIgnoreCase).
            ToList()

        Lineas.Clear()
        For Each linea In filtradas
            Lineas.Add(linea)
        Next

        TotalFilas = filtradas.Count
        TotalUnidades = filtradas.Sum(Function(l) l.Diferencia)
        TotalValor = filtradas.Sum(Function(l) l.ValorDiferencia)
        Dim textoUnidades = TotalUnidades.ToString("+#,##0;−#,##0;0", CultureInfo.CurrentCulture)
        TextoTotales = $"{TextoFilas(TotalFilas)} · {textoUnidades} unidades · {TotalValor.ToString("C2", CultureInfo.CurrentCulture)}" +
            If(_todas.Count <> filtradas.Count, $" (de {_todas.Count} con diferencia)", String.Empty)

        PorGrupo.Clear()
        For Each g In filtradas.GroupBy(Function(l) If(l.Grupo, String.Empty).Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(Function(g2) g2.Key, StringComparer.OrdinalIgnoreCase)
            PorGrupo.Add(New TotalGrupoDiferenciasInventarioDTO With {
                .Grupo = g.Key, .Filas = g.Count(), .Unidades = g.Sum(Function(l) l.Diferencia), .Valor = g.Sum(Function(l) l.ValorDiferencia)})
        Next
        PorFamilia.Clear()
        For Each f In filtradas.GroupBy(Function(l) If(l.Familia, String.Empty).Trim(), StringComparer.OrdinalIgnoreCase).OrderBy(Function(f2) f2.Key, StringComparer.OrdinalIgnoreCase)
            PorFamilia.Add(New TotalFamiliaDiferenciasInventarioDTO With {
                .Familia = f.Key, .Filas = f.Count(), .Unidades = f.Sum(Function(l) l.Diferencia), .Valor = f.Sum(Function(l) l.ValorDiferencia)})
        Next
    End Sub
End Class
