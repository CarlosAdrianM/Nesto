Imports ControlesUsuario.Dialogs
Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts

Public Class PlantillaVenta
    Implements IModuloNesto, IPlantillaVenta

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of PlantillaVentaView)("PlantillaVentaView")
        ' Nesto#543: elegir evento y modalidad al meter un curso
        registro.RegistrarDialogo(Of ElegirEventoCursoDialog, ElegirEventoCursoDialogViewModel)(ElegirEventoCursoDialogViewModel.NOMBRE)
        registro.RegistrarDialogo(Of ElegirPedidoPendienteDialog, ElegirPedidoPendienteDialogViewModel)(ElegirPedidoPendienteDialogViewModel.NOMBRE) ' Novedad 735

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar
        registro.Transitorio(Of PlantillaVentaMenuBar)()
        registro.Transitorio(Of PlantillaVentaViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of PlantillaVentaMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
