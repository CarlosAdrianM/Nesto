Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels

''' <summary>
''' Incidente del 28/08/2026. Enrique tramitó 15 envíos: GLS los aceptó TODOS (15 llamadas con
''' Exito=1 en AgenciasLlamadasWeb, 12:48:22), pero el cierre en Nesto falló justo después. Al
''' reintentar, GLS contestaba "Ya existe el codigo de barras" y Nesto lo trataba como error, así
''' que los 15 se quedaron registrados en la agencia y abiertos en Nesto, sin forma de cerrarlos.
'''
''' "Ya existe" no es un fallo: es el estado al que se quería llegar. El ViewModel ya contempla
''' esto (respuesta.Exito OrElse RespuestaYaTramitada(...)), pero ASM devolvía False siempre.
''' </summary>
<TestClass()>
Public Class AgenciaAsmYaTramitadaTests

    <TestMethod()>
    Public Sub RespuestaYaTramitada_ElTextoQueMandaGls_EsQueYaEstaba()
        ' Tal cual llega en el nodo Error del web service, sin acentos.
        Assert.IsTrue(New AgenciaASM().RespuestaYaTramitada("Ya existe el codigo de barras"))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_ElTextoDeCalcularMensajeError_TambienCuenta()
        ' El mismo -33 traducido por nosotros cuando el WS no manda nodo Error: lleva acentos y
        ' además va seguido de "de la expedición".
        Assert.IsTrue(New AgenciaASM().RespuestaYaTramitada("Ya existe el código de barras de la expedición"))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_NoDistingueMayusculas()
        Assert.IsTrue(New AgenciaASM().RespuestaYaTramitada("YA EXISTE EL CODIGO DE BARRAS"))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_ElCodigoPeladoTambienCuenta()
        ' GLS contesta el mismo -33 de dos maneras: con su texto (257 veces en la tabla de
        ' llamadas) y con el codigo pelado, "Error -33" (3 veces, todas el 05/08/2026).
        Dim agencia As New AgenciaASM()
        Assert.IsTrue(agencia.RespuestaYaTramitada("Error -33"))
        Assert.IsTrue(agencia.RespuestaYaTramitada("ERROR  -33"))
        Assert.IsTrue(agencia.RespuestaYaTramitada("  error-33  "))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_OtroCodigoQueEmpiezaIgual_NoCuenta()
        ' El patron va anclado a proposito: -330 o -331 son errores distintos, y perdonarlos
        ' cerraria en Nesto un envio que la agencia no acepto.
        Dim agencia As New AgenciaASM()
        Assert.IsFalse(agencia.RespuestaYaTramitada("Error -330"))
        Assert.IsFalse(agencia.RespuestaYaTramitada("Error -133"))
        Assert.IsFalse(agencia.RespuestaYaTramitada("Error -119"))
        Assert.IsFalse(agencia.RespuestaYaTramitada("No se ha podido conectar. Error -33 en el log"))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_YaExisteElAlbaran_NO_SePerdona_DeMomento()
        ' Decision consciente, no un olvido: "Ya existe el albaran" salio 10 veces entre
        ' jun/2024 y jun/2025 y ninguna desde entonces. Suena a lo mismo, pero podria ser el -70
        ' ("ya se ha enviado este pedido para esta fecha y cliente"), que es otra cosa. Hasta que
        ' GLS lo confirme, se queda como error. Si algun dia se anade, este test cambia de signo.
        Assert.IsFalse(New AgenciaASM().RespuestaYaTramitada("Ya existe el albaran"))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_OtrosErroresSiguenSiendoErrores()
        ' Ojo con pasarse de listo: un error de verdad NO puede colarse como "ya tramitada", o se
        ' cerraría en Nesto un envío que la agencia nunca aceptó.
        Dim agencia As New AgenciaASM()
        Assert.IsFalse(agencia.RespuestaYaTramitada("No se pudo canalizar el envío"))
        Assert.IsFalse(agencia.RespuestaYaTramitada("El nombre del destinatario debe tener al menos tres caracteres"))
        Assert.IsFalse(agencia.RespuestaYaTramitada("Error no controlado por el webservice de la agencia"))
    End Sub

    <TestMethod()>
    Public Sub RespuestaYaTramitada_SinTexto_NoEsQueYaEstuviera()
        Dim agencia As New AgenciaASM()
        Assert.IsFalse(agencia.RespuestaYaTramitada(Nothing))
        Assert.IsFalse(agencia.RespuestaYaTramitada(""))
        Assert.IsFalse(agencia.RespuestaYaTramitada("   "))
    End Sub

End Class
