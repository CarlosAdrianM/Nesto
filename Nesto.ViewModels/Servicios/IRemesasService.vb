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
    ' NestoAPI#332: candidatos a remesa (modo simulación, con gating #172 y puerta de neteo).
    ' NestoAPI#345: hasta = vencimientos incluidos hasta esa fecha (Nothing = solo vencidos a hoy).
    Function LeerEfectosCandidatos(empresa As String, hasta As Date?) As Task(Of List(Of EfectoCandidatoModel))
    ' NestoAPI#345: fecha "hasta" propuesta (hoy + DiasAntelacionRemesa del usuario, saltando
    ' fines de semana y festivos en el servidor).
    Function LeerFechaCargoPropuesta() As Task(Of Date)
    ' NestoAPI#332: crea la remesa (revalida server-side; lanza Exception con el motivo si no puede).
    ' NestoAPI#345: respetarVencimientos=True → cada efecto conserva su vencimiento (suelo hoy, un
    ' cargo por fecha); False → todos a fechaCargo (nunca anterior a hoy, el servidor lo asegura).
    ' seleccionHasta = la fecha con la que se cargaron los candidatos (el servidor revalida igual).
    Function CrearRemesa(empresa As String, banco As String, efectos As List(Of Integer),
                         respetarVencimientos As Boolean, fechaCargo As Date, seleccionHasta As Date?) As Task(Of CrearRemesaResponseModel)
    ' Slice 6: el fichero SEPA ISO 20022 lo genera el servidor (único call site del SP).
    Function CrearFicheroRemesa(remesa As Integer, codigo As String, fechaCobro As Date) As Task(Of String)
    ' Slice 7: contabiliza las devoluciones del fichero SEPA de impagados del banco.
    Function ContabilizarImpagados(fichero As String) As Task
    ' Slice 8: efectos del asiento con datos del cliente para las tareas de Planner.
    Function LeerTareasImpagado(empresa As String, asiento As Integer) As Task(Of List(Of TareaImpagadoModel))
    ' NestoAPI#353: informe de la remesa (QuestPDF en el backend): IBAN completo, subtotales
    ' por fecha de cargo y total general.
    Function DescargarInformeRemesaPdf(empresa As String, remesa As Integer) As Task(Of Byte())
End Interface
