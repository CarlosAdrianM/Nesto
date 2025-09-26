Imports System.Windows.Media

Public Interface IEtiquetaComisionAcumulada
    Inherits IEtiquetaComisionVenta

    Property FaltaParaSalto As Decimal
    Property InicioTramo As Decimal
    Property FinalTramo As Decimal
    Property BajaSaltoMesSiguiente As Boolean
    Property Proyeccion As Decimal
    Property VentaAcumulada As Decimal
    Property ComisionAcumulada As Decimal
    Property TipoConseguido As Decimal
    ReadOnly Property TipoReal As Decimal

    ' Propiedades de estrategia
    Property EstrategiaUtilizada As String
    Property TipoCorrespondePorTramo As Decimal?
    Property TipoRealmenteAplicado As Decimal?
    Property MotivoEstrategia As String
    Property ComisionSinEstrategia As Decimal?

    ' Propiedades calculadas
    ReadOnly Property TieneEstrategiaEspecial As Boolean
    ReadOnly Property EsSobrepago As Boolean
    ReadOnly Property TextoSobrepago As String
    ReadOnly Property ComisionRecuperadaEsteMes As Decimal

    ' Propiedades para UI
    ReadOnly Property ColorProgreso As Brush
    ReadOnly Property TipoConseguidoYReal As String
    ReadOnly Property ColorTipoConseguidoYReal As Brush
    ReadOnly Property ColorEsSobrepago As Brush
End Interface