Imports System.Collections.ObjectModel
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Interface IRapportService
    Function cargarListaRapports(empresa As String, cliente As String, contacto As String) As Task(Of ObservableCollection(Of SeguimientoClienteDTO))
    Function cargarListaRapports(vendedor As String, fecha As Date) As Task(Of ObservableCollection(Of SeguimientoClienteDTO))
    Function crearRapport(rapport As SeguimientoClienteDTO) As Task(Of String)
    Function CrearCita(rapport As SeguimientoClienteDTO, fechaAviso As Date) As Task(Of String)
    Function CrearTareaPlanner(rapport As SeguimientoClienteDTO) As Task(Of String)

    Function cargarListaRapportsFiltrada(vendedor As String, filtro As String) As Task(Of ObservableCollection(Of SeguimientoClienteDTO))
    Function CargarListaEstados() As List(Of idShortDescripcion)
    Function CargarListaTipos() As List(Of idDescripcion)
    Function QuitarDeMiListado(rapport As SeguimientoClienteDTO, vendedorEstetica As String, vendedorPeluqueria As String) As Task(Of Boolean)
End Interface
