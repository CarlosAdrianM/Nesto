using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.Ganavisiones.Models;
using Nesto.Modulos.Ganavisiones.ViewModels;
using System;

namespace PlantillaVentaTests
{
    [TestClass]
    public class GanavisionWrapperTests
    {
        #region EsActivo Tests

        [TestMethod]
        public void EsActivo_FechaHastaNula_FechaDesdePasada_EsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = null
            });

            Assert.IsTrue(wrapper.EsActivo);
        }

        [TestMethod]
        public void EsActivo_FechaHastaFutura_FechaDesdePasada_EsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = DateTime.Today.AddDays(10)
            });

            Assert.IsTrue(wrapper.EsActivo);
        }

        [TestMethod]
        public void EsActivo_FechaHastaHoy_FechaDesdePasada_EsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = DateTime.Today
            });

            Assert.IsTrue(wrapper.EsActivo);
        }

        [TestMethod]
        public void EsActivo_FechaHastaPasada_NoEsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = DateTime.Today.AddDays(-1)
            });

            Assert.IsFalse(wrapper.EsActivo);
        }

        [TestMethod]
        public void EsActivo_FechaDesdeFutura_FechaHastaNula_NoEsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(1),
                FechaHasta = null
            });

            Assert.IsFalse(wrapper.EsActivo);
        }

        [TestMethod]
        public void EsActivo_FechaDesdeFutura_FechaHastaFutura_NoEsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(1),
                FechaHasta = DateTime.Today.AddDays(2)
            });

            Assert.IsFalse(wrapper.EsActivo);
        }

        [TestMethod]
        public void EsActivo_FechaDesdeHoy_EsActivo()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today,
                FechaHasta = null
            });

            Assert.IsTrue(wrapper.EsActivo);
        }

        #endregion

        #region Toggle Activo Tests

        [TestMethod]
        public void Toggle_DesactivarGanavisionActivo_PoneFechaHastaAyer()
        {
            // Un ganavision activo (FechaDesde pasada, FechaHasta nula)
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = null
            });

            Assert.IsTrue(wrapper.EsActivo);

            // Simular toggle: desactivar (FechaHasta = ayer para desactivación inmediata)
            wrapper.FechaHasta = DateTime.Today.AddDays(-1);

            Assert.IsFalse(wrapper.EsActivo);
            Assert.AreEqual(DateTime.Today.AddDays(-1), wrapper.FechaHasta);
        }

        [TestMethod]
        public void Toggle_ActivarGanavisionInactivoPorFechaHasta_QuitaFechaHasta()
        {
            // Un ganavision inactivo por FechaHasta pasada
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = DateTime.Today.AddDays(-1)
            });

            Assert.IsFalse(wrapper.EsActivo);

            // Simular toggle: activar (FechaHasta = null)
            wrapper.FechaHasta = null;

            Assert.IsTrue(wrapper.EsActivo);
        }

        [TestMethod]
        public void Toggle_ActivarGanavisionInactivoPorFechaDesdeFutura_AjustaFechaDesdeAHoy()
        {
            // Un ganavision inactivo porque FechaDesde es futura
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(5),
                FechaHasta = null
            });

            Assert.IsFalse(wrapper.EsActivo);

            // Simular lo que hace OnToggleActivo: FechaHasta = null + FechaDesde = hoy
            wrapper.FechaHasta = null;
            wrapper.FechaDesde = DateTime.Today;

            Assert.IsTrue(wrapper.EsActivo);
            Assert.AreEqual(DateTime.Today, wrapper.FechaDesde);
        }

        [TestMethod]
        public void Toggle_ActivarGanavisionInactivoPorFechaDesdeFutura_ConserveFechaHasta()
        {
            // FechaDesde = mañana, FechaHasta = pasado mañana → inactivo por FechaDesde
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(1),
                FechaHasta = DateTime.Today.AddDays(2)
            });

            Assert.IsFalse(wrapper.EsActivo);

            // Simular OnToggleActivo: solo FechaDesde debe cambiar
            if (wrapper.FechaHasta != null && wrapper.FechaHasta < DateTime.Today)
                wrapper.FechaHasta = null;
            if (wrapper.FechaDesde > DateTime.Today)
                wrapper.FechaDesde = DateTime.Today;

            Assert.IsTrue(wrapper.EsActivo);
            Assert.AreEqual(DateTime.Today, wrapper.FechaDesde);
            Assert.AreEqual(DateTime.Today.AddDays(2), wrapper.FechaHasta); // conservada
        }

        [TestMethod]
        public void Toggle_ActivarGanavisionInactivoPorAmbasFechas_AjustaAmbas()
        {
            // Inactivo por FechaDesde futura Y FechaHasta pasada (caso raro pero posible)
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(1),
                FechaHasta = DateTime.Today.AddDays(-1)
            });

            Assert.IsFalse(wrapper.EsActivo);

            // Simular OnToggleActivo: FechaHasta = null + FechaDesde = hoy
            wrapper.FechaHasta = null;
            wrapper.FechaDesde = DateTime.Today;

            Assert.IsTrue(wrapper.EsActivo);
        }

        #endregion

        #region EsActivo notifica cambios

        [TestMethod]
        public void EsActivo_NotificaCuandoCambiaFechaHasta()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(-10),
                FechaHasta = null
            });

            bool notificado = false;
            wrapper.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GanavisionWrapper.EsActivo))
                    notificado = true;
            };

            wrapper.FechaHasta = DateTime.Today.AddDays(-1);

            Assert.IsTrue(notificado);
        }

        [TestMethod]
        public void EsActivo_NotificaCuandoCambiaFechaDesde()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today.AddDays(5),
                FechaHasta = null
            });

            bool notificado = false;
            wrapper.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GanavisionWrapper.EsActivo))
                    notificado = true;
            };

            wrapper.FechaDesde = DateTime.Today;

            Assert.IsTrue(notificado);
        }

        #endregion

        #region ImporteMinimoPedido Tests

        [TestMethod]
        public void ImporteMinimoPedido_SeInicializaDesdeModel()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today,
                ImporteMinimoPedido = 150.50m
            });

            Assert.AreEqual(150.50m, wrapper.ImporteMinimoPedido);
        }

        [TestMethod]
        public void ImporteMinimoPedido_CambioMarcaHaCambiado()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today,
                ImporteMinimoPedido = 0m
            });

            Assert.IsFalse(wrapper.HaCambiado);

            wrapper.ImporteMinimoPedido = 200m;

            Assert.IsTrue(wrapper.HaCambiado);
        }

        [TestMethod]
        public void ImporteMinimoPedido_ActualizarDesdeServidor_NoMarcaHaCambiado()
        {
            var wrapper = new GanavisionWrapper(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today,
                ImporteMinimoPedido = 100m
            });

            wrapper.ActualizarDesdeServidor(new GanavisionModel
            {
                Id = 1,
                FechaDesde = DateTime.Today,
                ImporteMinimoPedido = 200m
            });

            Assert.AreEqual(200m, wrapper.ImporteMinimoPedido);
            Assert.IsFalse(wrapper.HaCambiado);
        }

        #endregion
    }
}
