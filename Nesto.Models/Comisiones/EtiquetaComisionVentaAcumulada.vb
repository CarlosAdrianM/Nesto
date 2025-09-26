Imports System.Windows.Media

Public Class EtiquetaComisionVentaAcumulada
    Implements IEtiquetaComisionAcumulada

    Public Property Id As Integer
    Public Property Nombre As String Implements IEtiquetaComision.Nombre
    Public Property Venta As Decimal Implements IEtiquetaComisionVenta.Venta
    Public Property Tipo As Decimal Implements IEtiquetaComision.Tipo
    Public Property Comision As Decimal Implements IEtiquetaComision.Comision

    ' Propiedades específicas de acumulada
    Public Property FaltaParaSalto As Decimal Implements IEtiquetaComisionAcumulada.FaltaParaSalto
    Public Property InicioTramo As Decimal Implements IEtiquetaComisionAcumulada.InicioTramo
    Public Property FinalTramo As Decimal Implements IEtiquetaComisionAcumulada.FinalTramo
    Public Property BajaSaltoMesSiguiente As Boolean Implements IEtiquetaComisionAcumulada.BajaSaltoMesSiguiente
    Public Property Proyeccion As Decimal Implements IEtiquetaComisionAcumulada.Proyeccion
    Public Property VentaAcumulada As Decimal Implements IEtiquetaComisionAcumulada.VentaAcumulada
    Public Property ComisionAcumulada As Decimal Implements IEtiquetaComisionAcumulada.ComisionAcumulada
    Public Property TipoConseguido As Decimal Implements IEtiquetaComisionAcumulada.TipoConseguido

    Public ReadOnly Property TipoReal As Decimal Implements IEtiquetaComisionAcumulada.TipoReal
        Get
            Return If(VentaAcumulada = 0, 0, Math.Round(ComisionAcumulada / VentaAcumulada, 4, MidpointRounding.AwayFromZero))
        End Get
    End Property

    Public ReadOnly Property EsComisionAcumulada As Boolean Implements IEtiquetaComision.EsComisionAcumulada

    ' Propiedades de estrategia
    Public Property EstrategiaUtilizada As String Implements IEtiquetaComisionAcumulada.EstrategiaUtilizada
    Public Property TipoCorrespondePorTramo As Decimal? Implements IEtiquetaComisionAcumulada.TipoCorrespondePorTramo
    Public Property TipoRealmenteAplicado As Decimal? Implements IEtiquetaComisionAcumulada.TipoRealmenteAplicado
    Public Property MotivoEstrategia As String Implements IEtiquetaComisionAcumulada.MotivoEstrategia
    Public Property ComisionSinEstrategia As Decimal? Implements IEtiquetaComisionAcumulada.ComisionSinEstrategia

    ' Propiedades calculadas
    Public ReadOnly Property TieneEstrategiaEspecial As Boolean Implements IEtiquetaComisionAcumulada.TieneEstrategiaEspecial
        Get
            Return Not String.IsNullOrEmpty(EstrategiaUtilizada)
        End Get
    End Property

    Public ReadOnly Property EsSobrepago As Boolean Implements IEtiquetaComisionAcumulada.EsSobrepago
        Get
            Return TipoCorrespondePorTramo.HasValue AndAlso TipoRealmenteAplicado.HasValue AndAlso TipoCorrespondePorTramo.Value <> TipoRealmenteAplicado.Value
        End Get
    End Property

    Public Property TextoSobrepago As String Implements IEtiquetaComisionAcumulada.TextoSobrepago

    Public Property ComisionRecuperadaEsteMes As Decimal Implements IEtiquetaComisionAcumulada.ComisionRecuperadaEsteMes

    ' Propiedades para UI
    Public ReadOnly Property ColorProgreso As Brush Implements IEtiquetaComisionAcumulada.ColorProgreso
        Get
            Return If(BajaSaltoMesSiguiente, Brushes.Red, Brushes.Green)
        End Get
    End Property

    Public ReadOnly Property TipoConseguidoYReal As String Implements IEtiquetaComisionAcumulada.TipoConseguidoYReal
        Get
            Return String.Format("{0} / {1}", TipoConseguido.ToString("P"), TipoReal.ToString("P"))
        End Get
    End Property

    Public ReadOnly Property ColorTipoConseguidoYReal As Brush Implements IEtiquetaComisionAcumulada.ColorTipoConseguidoYReal
        Get
            Return If(TipoReal = TipoConseguido, Brushes.Green, Brushes.Black)
        End Get
    End Property

    Public ReadOnly Property ColorEsSobrepago As Brush Implements IEtiquetaComisionAcumulada.ColorEsSobrepago
        Get
            If ComisionRecuperadaEsteMes = Venta * Tipo And Venta > 0 Then
                Return Brushes.Red
            End If
            If Not EsSobrepago And ComisionRecuperadaEsteMes > 0 Then
                Return Brushes.DarkSlateGray
            End If
            If Not EsSobrepago And ComisionRecuperadaEsteMes < 0 Then
                Return Brushes.GreenYellow
            End If
            Return If(EsSobrepago, Brushes.Orange, Brushes.Green)
        End Get
    End Property

    Public Property CifraAnual As Decimal Implements IEtiquetaComision.CifraAnual
    Public Property ComisionAnual As Decimal Implements IEtiquetaComision.ComisionAnual
    Public ReadOnly Property UnidadCifra As String Implements IEtiquetaComision.UnidadCifra
        Get
            Return "€"
        End Get
    End Property

    Public ReadOnly Property PorcentajeAnual As Decimal Implements IEtiquetaComision.PorcentajeAnual
        Get
            Return If(CifraAnual = 0, 0, Math.Round(ComisionAnual / CifraAnual, 4, MidpointRounding.AwayFromZero))
        End Get
    End Property
End Class