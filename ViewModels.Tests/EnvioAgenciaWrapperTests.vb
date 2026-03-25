Imports Nesto.ViewModels
Imports Nesto.Models.Nesto.Models
Imports Nesto.Infrastructure.Shared

''' <summary>
''' Tests para EnvioAgenciaWrapper - Issue #135.
''' El sentinel (-1) es un detalle de persistencia. El usuario solo ve importes reales.
'''   CobrarReembolso = True  → se cobra reembolso (0 = auto, >0 = fijado)
'''   CobrarReembolso = False → no se cobra reembolso (ToEnvioAgencia escribe sentinel)
''' </summary>
<TestClass()>
Public Class EnvioAgenciaWrapperTests

    <TestMethod()>
    Public Sub CobrarReembolso_PorDefecto_EsTrue()
        Dim wrapper As New EnvioAgenciaWrapper
        Assert.IsTrue(wrapper.CobrarReembolso)
        Assert.AreEqual(0D, wrapper.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub CobrarReembolso_AlDesmarcar_NoModificaImporte()
        Dim wrapper As New EnvioAgenciaWrapper With {
            .Reembolso = 100D,
            .CobrarReembolso = True
        }

        wrapper.CobrarReembolso = False

        ' El importe se conserva para que al volver a marcar no se pierda
        Assert.AreEqual(100D, wrapper.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub CobrarReembolso_AlternarMarcadoYDesmarcado_ConservaImporte()
        Dim wrapper As New EnvioAgenciaWrapper With {
            .Reembolso = 75.50D,
            .CobrarReembolso = True
        }

        wrapper.CobrarReembolso = False
        wrapper.CobrarReembolso = True

        Assert.AreEqual(75.50D, wrapper.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub ToEnvioAgencia_CobrarFalse_PersisteSentinel()
        Dim wrapper As New EnvioAgenciaWrapper With {
            .Empresa = "1  ", .Agencia = 3, .Pais = 1,
            .Reembolso = 100D,
            .CobrarReembolso = False
        }

        Dim envio = wrapper.ToEnvioAgencia()

        ' Al persistir, el sentinel reemplaza el importe
        Assert.AreEqual(Constantes.Agencias.REEMBOLSO_NO_COBRAR, envio.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub ToEnvioAgencia_CobrarTrue_PersisteImporte()
        Dim wrapper As New EnvioAgenciaWrapper With {
            .Empresa = "1  ", .Agencia = 3, .Pais = 1,
            .CobrarReembolso = True,
            .Reembolso = 75D
        }

        Dim envio = wrapper.ToEnvioAgencia()

        Assert.AreEqual(75D, envio.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub ToEnvioAgencia_CobrarTrueImporteCero_PersisteCero()
        ' 0 = se calculara automaticamente al tramitar
        Dim wrapper As New EnvioAgenciaWrapper With {
            .Empresa = "1  ", .Agencia = 3, .Pais = 1,
            .CobrarReembolso = True,
            .Reembolso = 0D
        }

        Dim envio = wrapper.ToEnvioAgencia()

        Assert.AreEqual(0D, envio.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub EnvioAgenciaAWrapper_ReembolsoCero_CobrarTrueImporteCero()
        Dim envio As New EnviosAgencia With {
            .Reembolso = 0D,
            .Estado = -1, .Pais = 1, .Pedido = 12345,
            .Fecha = Today, .FechaEntrega = Today
        }

        Dim wrapper = EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio)

        Assert.IsTrue(wrapper.CobrarReembolso)
        Assert.AreEqual(0D, wrapper.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub EnvioAgenciaAWrapper_ReembolsoPositivo_CobrarTrueConImporte()
        Dim envio As New EnviosAgencia With {
            .Reembolso = 150.5D,
            .Estado = -1, .Pais = 1, .Pedido = 12345,
            .Fecha = Today, .FechaEntrega = Today
        }

        Dim wrapper = EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio)

        Assert.IsTrue(wrapper.CobrarReembolso)
        Assert.AreEqual(150.5D, wrapper.Reembolso)
    End Sub

    <TestMethod()>
    Public Sub EnvioAgenciaAWrapper_ReembolsoNegativo_CobrarFalseImporteCero()
        ' El sentinel se convierte a importe limpio para el usuario
        Dim envio As New EnviosAgencia With {
            .Reembolso = Constantes.Agencias.REEMBOLSO_NO_COBRAR,
            .Estado = -1, .Pais = 1, .Pedido = 12345,
            .Fecha = Today, .FechaEntrega = Today
        }

        Dim wrapper = EnvioAgenciaWrapper.EnvioAgenciaAWrapper(envio)

        Assert.IsFalse(wrapper.CobrarReembolso)
        Assert.AreEqual(0D, wrapper.Reembolso)
    End Sub

End Class
