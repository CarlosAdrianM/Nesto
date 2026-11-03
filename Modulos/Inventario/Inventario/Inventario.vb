Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts

Public Class Inventario
    Implements IModuloNesto, IInventario

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of InventarioView)("InventarioView")

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar
        registro.Transitorio(Of InventarioMenuBar)()
        registro.Transitorio(Of InventarioViewModel)()
        ' Nesto#596: las llamadas a api/Inventarios.
        registro.Transitorio(Of IServicioInventario, ServicioInventario)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of InventarioMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
