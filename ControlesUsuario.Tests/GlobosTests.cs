using ControlesUsuario.Globos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Windows.Controls;

namespace ControlesUsuario.Tests
{
    /// <summary>Nesto#592 (NestoAPI#774): la clave de pantalla de cada vista y el registro de anclas.</summary>
    [TestClass]
    public class GlobosTests
    {
        [DataTestMethod]
        [DataRow("EnviarReposicionView", null, "EnviarReposicion")]
        [DataRow("MainWindow", null, "Principal")]
        [DataRow("CampanaNotificaciones", null, null)]
        [DataRow("View", null, null)]
        [DataRow("SelectorCliente", "PedidoVenta", "PedidoVenta")]
        [DataRow(null, null, null)]
        public void ClavePantalla(string tipo, string puesta, string esperada)
        {
            Assert.AreEqual(esperada, PantallasNesto.Clave(tipo, puesta));
        }

        [TestMethod]
        public void Ancla_SeRegistraPorClave_YSinCargarNoEstaALaVista()
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    var texto = new TextBlock();
                    Globo.SetAncla(texto, " Prueba.Control ");

                    // Registrada, pero no está cargada en ninguna ventana: no cuenta como visible (el globo saldría centrado)
                    Assert.IsNull(RegistroAnclas.Visible("Prueba.Control"));
                    Assert.AreEqual(" Prueba.Control ", Globo.GetAncla(texto));
                    Globo.SetAncla(texto, null);
                    Assert.IsNull(RegistroAnclas.Visible("Prueba.Control"));
                    Assert.IsNull(RegistroAnclas.Visible("No.Existe"));
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw error;
            }
        }
    }
}
