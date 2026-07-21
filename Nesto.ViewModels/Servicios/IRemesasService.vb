Imports Nesto.Infrastructure.Models

' Nesto#340 Fase 1C.14: servicio API del módulo de Remesas (sustituye los accesos EF del VM).
Public Interface IRemesasService
    ' Slice 1: empresas para el combo de la cabecera.
    Function LeerEmpresas() As Task(Of List(Of EmpresaModel))
    ' Slice 2: remesas del grid (top = 100 en la carga inicial; Nothing = botón "Ver Todas").
    Function LeerRemesas(empresa As String, top As Integer?) As Task(Of List(Of RemesaModel))
    ' Slice 3: efectos incluidos en la remesa seleccionada.
    Function LeerMovimientos(empresa As String, remesa As Integer) As Task(Of List(Of MovimientoRemesaModel))
    ' Slice 4: asientos de impagados agrupados (grid izquierdo de la pestaña Impagados).
    Function LeerImpagados(empresa As String, top As Integer?) As Task(Of List(Of impagado))
    ' Slice 5: movimientos del asiento de impagados seleccionado (grid derecho).
    Function LeerMovimientosImpagado(empresa As String, asiento As Integer) As Task(Of List(Of MovimientoRemesaModel))
End Interface
