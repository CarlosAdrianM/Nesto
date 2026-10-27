Imports Microsoft.Extensions.DependencyInjection
Imports Nesto.Infrastructure.Composicion
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Menu
Imports Nesto.Infrastructure.Navegacion

''' <summary>
''' Nesto#490 (4C.5, tramo 4): el arranque de Nesto sin PrismApplication, sobre Microsoft.Extensions.DependencyInjection.
''' Lo usan Application.OnStartup y la prueba de arranque sombra (Nesto.Tests), así que la prueba hace exactamente lo
''' mismo que la aplicación salvo la red y el Show(). Los pasos, en el orden de PrismApplication.Initialize:
''' <list type="number">
''' <item><see cref="Componer"/>: ComposicionNesto y todos los módulos registran, y se construye el contenedor con
''' ValidateOnBuild y ValidateScopes (lanza aquí si a algún registro le falta una dependencia).</item>
''' <item><see cref="CrearShell"/>: la ventana principal (IMainWindow) y el gestor de regiones colgado de ella.</item>
''' <item><see cref="InicializarModulos"/>: cada módulo pone su menú en la cinta, en el orden de
''' <see cref="ComposicionNesto.Modulos"/> (el mismo que el catálogo de Prism: sin dependencias entre módulos, Prism no
''' lo reordenaba). La cinta depende del orden: los grupos que se fusionan en la misma pestaña salen en el que llegan.
''' Al final, el catálogo del menú lateral (IServicioCinta.Completar).</item>
''' <item><see cref="MontarNavegacion"/>: con el piloto NavegacionLateral, el menú lateral en lugar de la cinta.</item>
''' </list>
''' </summary>
Public NotInheritable Class ArranqueNesto

    Private Sub New(resolutor As ResolutorMicrosoft)
        Me.Resolutor = resolutor
    End Sub

    ''' <summary>El contenedor ya construido.</summary>
    Public ReadOnly Property Resolutor As ResolutorMicrosoft

    ''' <summary>
    ''' Registra todo (ComposicionNesto y los módulos de <see cref="ComposicionNesto.Modulos"/>) y construye el contenedor.
    ''' <paramref name="despuesDeRegistrar"/> registra encima de todo lo anterior (en MS.DI gana el último registro): la
    ''' prueba de arranque sombra lo usa para cambiar la API y la autenticación por fakes; la aplicación no lo usa.
    ''' </summary>
    Public Shared Function Componer(Optional despuesDeRegistrar As Action(Of IRegistroNesto) = Nothing) As ArranqueNesto
        Dim contenedor As New ContenedorMicrosoft()
        ComposicionNesto.Registrar(contenedor)

        ' Con Prism, el módulo se pedía al contenedor y se le llamaba a RegisterTypes con el contenedor ya en marcha. MS.DI
        ' no admite registros después de construirse: el módulo registra antes, desde una instancia propia (Registrar no
        ' guarda estado), y después se inicializa el que da el contenedor (ver InicializarModulos).
        For Each tipoModulo In ComposicionNesto.Modulos
            Dim modulo = TryCast(Activator.CreateInstance(ImplementacionDe(contenedor.Servicios, tipoModulo)), IModuloNesto)
            If modulo Is Nothing Then
                Throw New InvalidOperationException($"El módulo {tipoModulo.Name} no implementa IModuloNesto")
            End If
            modulo.Registrar(contenedor)
        Next

        despuesDeRegistrar?.Invoke(contenedor)

        Return New ArranqueNesto(contenedor.Construir(validar:=True))
    End Function

    ''' <summary>
    ''' La ventana principal, sin mostrar, con el gestor de regiones del shell colgado (MainRegion se registra en él). Es lo
    ''' que hacía Application.CreateShell con Prism.
    ''' </summary>
    Public Function CrearShell() As Window
        ' Nesto#490 (panel lateral): el piloto NavegacionLateral se lee una vez, aquí, antes de crear la ventana; vale
        ' hasta cerrar Nesto (cambiarlo desde Parámetros pide reiniciar)
        Dim unused = Resolutor.Resolver(Of ConmutadorNavegacionLateral)().Activa
        Dim shell As Window = CType(Resolutor.Resolver(Of IMainWindow)(), Window)
        Resolutor.Resolver(Of RegionesDelShell)().AlCrearShell(shell)
        Return shell
    End Function

    ''' <summary>
    ''' Inicializa los módulos en el orden de <see cref="ComposicionNesto.Modulos"/>. Como con el ModuleManager de Prism, el
    ''' módulo que se inicializa es el que da el contenedor por el tipo del catálogo (para IMenuBar, el MenuBarView
    ''' singleton: el que acaba en la cinta).
    ''' </summary>
    Public Sub InicializarModulos()
        For Each tipoModulo In ComposicionNesto.Modulos
            DirectCast(Resolutor.Resolver(tipoModulo), IModuloNesto).Inicializar(Resolutor)
        Next
        ' Nesto#490 (panel lateral, tramo 1): con todos los menús ya en la cinta, el catálogo del menú lateral (para todos,
        ' tengan o no el piloto: no carga nada de WPF-UI y lo comprueba la prueba sombra)
        Resolutor.Resolver(Of IServicioCinta)().Completar()
    End Sub

    ''' <summary>
    ''' Nesto#490 (panel lateral, tramo 1): con el piloto NavegacionLateral, pone el menú lateral en la ventana principal
    ''' (después de <see cref="InicializarModulos"/> y antes de Show). Sin el piloto no hace nada. Si el panel falla al
    ''' crearse, el error va a ELMAH y se queda la cinta: nunca impide arrancar Nesto. Devuelve si lo ha puesto.
    ''' </summary>
    Public Function MontarNavegacion(shell As Window) As Boolean
        Dim conmutador = Resolutor.Resolver(Of ConmutadorNavegacionLateral)()
        Dim ventana = TryCast(shell, MainWindow)
        If Not conmutador.Activa OrElse ventana Is Nothing Then
            Return False
        End If
        Try
            ventana.MontarNavegacionLateral(Resolutor.Resolver(Of IServicioCinta)().Catalogo, conmutador)
            Return True
        Catch ex As Exception
            conmutador.Registrar(ex, "NavegacionLateral: montar el menú lateral (se queda la cinta)")
            ventana.regionRibbon.Visibility = Visibility.Visible
            Return False
        End Try
    End Function

    ' La clase que se crea para registrar el módulo: la registrada para el tipo del catálogo (IPedidoVenta → PedidoVenta)
    ' o, si el catálogo ya trae la clase (CanalesExternos), ella misma.
    Private Shared Function ImplementacionDe(servicios As IServiceCollection, tipoModulo As Type) As Type
        Dim registro = servicios.LastOrDefault(Function(r) Not r.IsKeyedService AndAlso r.ServiceType Is tipoModulo)
        Return If(registro?.ImplementationType, tipoModulo)
    End Function
End Class
