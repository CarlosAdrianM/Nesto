Imports System.Threading
Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.Infrastructure.Contracts
Imports Nesto.ViewModels
Imports ControlesUsuario.Dialogs

''' <summary>
''' ELMAH 21/08/26 (MariaJose): "A Task's exception(s) were not observed... (Object reference not
''' set to an instance of an object)" con stack en ComisionesViewModel.CalcularComisionAsync.
'''
''' Dos fallos encadenados: sin vendedor seleccionado se desreferenciaba vendedorActual, y los dos
''' checkbox llamaban al metodo async SIN Await, asi que la excepcion no la observaba nadie: el
''' usuario no veia nada y a ELMAH llegaba un mensaje sin pista de donde salia.
''' </summary>
<TestClass()>
Public Class ComisionesViewModelTests

    ''' <summary>
    ''' El ViewModel crea un DependencyObject en el constructor (DesignerProperties), y WPF exige
    ''' hilo STA. Mismo patron que los tests de ControlesUsuario.
    ''' </summary>
    Private Shared Sub EnHiloSta(prueba As Action)
        Dim fallo As Exception = Nothing
        Dim hilo As New Thread(Sub()
                                   Try
                                       prueba()
                                   Catch ex As Exception
                                       fallo = ex
                                   End Try
                               End Sub)
        hilo.SetApartmentState(ApartmentState.STA)
        hilo.Start()
        hilo.Join()
        If fallo IsNot Nothing Then
            Throw New Exception("La prueba fallo en el hilo STA: " & fallo.Message, fallo)
        End If
    End Sub

    Private Shared Function CrearViewModel(Optional abridorPedidos As IAbridorPedidos = Nothing) As ComisionesViewModel
        Dim configuracion = A.Fake(Of IConfiguracion)()
        Dim dialogService = A.Fake(Of IServicioDialogos)()

        ' Nesto#490 (4C.5, tramo 3a): la autenticación y la factoría llegan por el constructor, no del IUnityContainer
        Return New ComisionesViewModel(If(abridorPedidos, A.Fake(Of IAbridorPedidos)()), configuracion, dialogService,
                                       A.Fake(Of IServicioAutenticacion)(), A.Fake(Of IClienteApiFactory)())
    End Function

    ''' <summary>
    ''' Sin vendedor (listaVendedores puede venir vacia, y vendedorActual sale de su
    ''' FirstOrDefault) marcar el checkbox reventaba con NullReferenceException.
    ''' </summary>
    <TestMethod()>
    Public Sub ComisionesViewModel_SinVendedorSeleccionado_MarcarIncluirPickingNoRevienta()
        EnHiloSta(Sub()
                      Dim vm = CrearViewModel()
                      Assert.IsNull(vm.vendedorActual, "El caso del error: nadie ha seleccionado vendedor")

                      vm.IncluirPicking = True

                      Assert.IsNull(vm.ComisionAnualResumenActual, "Sin vendedor no hay comision que calcular")
                      ' Sin la guarda, la NullReferenceException la recoge el Try de RecalcularComisionAsync
                      ' y acaba en un dialogo de error delante de la usuaria. No debe salir nada.
                      A.CallTo(Sub() vm.DialogService.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
                  End Sub)
    End Sub

    <TestMethod()>
    Public Sub ComisionesViewModel_SinVendedorSeleccionado_DesmarcarIncluirAlbaranesNoRevienta()
        EnHiloSta(Sub()
                      Dim vm = CrearViewModel()

                      vm.IncluirAlbaranes = False

                      Assert.IsNull(vm.ComisionAnualResumenActual)
                  End Sub)
    End Sub

    ''' <summary>
    ''' Nesto#490 (4C.5, tramo 3a): el doble clic en un pedido del detalle de comisiones lo abre con IAbridorPedidos
    ''' (antes, PedidoVentaViewModel.CargarPedido con el IUnityContainer).
    ''' </summary>
    <TestMethod()>
    Public Sub ComisionesViewModel_AbrirPedido_LoAbreConElAbridorDePedidos()
        EnHiloSta(Sub()
                      Dim abridor = A.Fake(Of IAbridorPedidos)()
                      Dim vm = CrearViewModel(abridor)

                      vm.cmdAbrirPedido.Execute(New With {.Empresa = "1", .Numero = 926125})

                      A.CallTo(Sub() abridor.Abrir("1", 926125)).MustHaveHappenedOnceExactly()
                  End Sub)
    End Sub

End Class
