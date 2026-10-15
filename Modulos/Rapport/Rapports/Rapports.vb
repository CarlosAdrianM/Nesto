Imports ControlesUsuario.Dialogs
Imports Prism.Modularity
Imports Nesto.Contratos
Imports Prism.Ioc
Imports Nesto.Infrastructure.Composicion
Imports Nesto.Infrastructure.Contracts

Public Class Rapports
    Implements IModule, IModuloNesto, IRapports

    ' Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
    Private Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        Registrar(New RegistroPrism(containerRegistry))
    End Sub

    Private Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Inicializar(New ResolutorPrism(containerProvider))
    End Sub

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of ListaRapportsView)("ListaRapportsView")
        registro.Vista(Of RapportView)("RapportView")
        registro.RegistrarDialogo(Of CopiarSeguimientosView, CopiarSeguimientosViewModel)

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar
        registro.Transitorio(Of RapportsMenuBar)()
        registro.Transitorio(Of ListaRapportsViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of RapportsMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
