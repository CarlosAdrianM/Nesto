Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels

' Innovatrans (DataTrans DTX): agencia "registrar al imprimir". El SOAP vive en NestoAPI; aquí solo
' verificamos los flags polimórficos que hacen que el ViewModel la trate distinto (imprimir = registrar
' en la agencia vía InsertarYEtiquetar, en vez del flujo clásico de montar la etiqueta en local).
<TestClass()>
Public Class AgenciaInnovatransTests

    Private Shared Function CrearAgencia() As AgenciaInnovatrans
        ' El constructor guarda el ViewModel pero no lo usa para estas propiedades; Nothing vale.
        Return New AgenciaInnovatrans(Nothing)
    End Function

    <TestMethod()>
    Public Sub Innovatrans_FlujoEsRegistrarAlImprimir()
        Assert.AreEqual(TipoFlujoTramitacion.RegistrarAlImprimir, CrearAgencia().FlujoTramitacion)
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_ImplementaGestionRemota()
        Assert.IsInstanceOfType(CrearAgencia(), GetType(IAgenciaConGestionRemota))
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_NoPermiteEditarCodigoBarras()
        ' El albarán lo asigna la plataforma al tramitar; no se edita a mano.
        Assert.IsFalse(CrearAgencia().PermiteEditarCodigoBarras)
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_NoExigeDimensionesBultos()
        ' El servidor manda una caja estándar por defecto; no se piden medidas.
        Assert.IsFalse(CrearAgencia().DimensionesBultosObligatorias)
    End Sub

    <TestMethod()>
    Public Sub Innovatrans_PaisDefectoEsEspana()
        Assert.AreEqual(34, CrearAgencia().paisDefecto)
    End Sub

End Class
