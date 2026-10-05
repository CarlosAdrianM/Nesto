using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): la navegación llega a los ViewModels por <see cref="IReceptorNavegacion"/>, sin tipos de
    /// Prism. La vista de destino se busca como la busca Prism (por el nombre de su tipo) y se le entrega a ella o a
    /// su DataContext, con los parámetros de la navegación.
    /// </summary>
    [TestClass]
    public class ReceptorNavegacionTests
    {
        private sealed class ViewModelReceptor : IReceptorNavegacion
        {
            public List<ParametrosNavegacion> Recibidas { get; } = new();
            public void AlLlegar(ParametrosNavegacion parametros) => Recibidas.Add(parametros);
        }

        // Vistas de mentira con nombre de tipo propio (como «ExtractoClienteView»)
        private sealed class ExtractoClienteView : Border { }
        private sealed class OtraView : Border { }
        private sealed class VistaReceptora : IReceptorNavegacion
        {
            public int Veces { get; private set; }
            public void AlLlegar(ParametrosNavegacion parametros) => Veces++;
        }

        private static void EnSta(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try { accion(); } catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.Message, error);
            }
        }

        [TestMethod]
        public void Entregar_AlDataContextDeLaVistaDeDestino_ConSusParametros()
        {
            EnSta(() =>
            {
                var vm = new ViewModelReceptor();
                var otroVm = new ViewModelReceptor();
                var destino = new ExtractoClienteView { DataContext = vm };
                var otra = new OtraView { DataContext = otroVm };

                IReceptorNavegacion entregado = ReceptorNavegacion.Entregar(new object[] { otra, destino }, new object[] { otra, destino },
                    "ExtractoClienteView", new ParametrosNavegacion { { "cliente", "15191" } });

                Assert.AreSame(vm, entregado);
                Assert.AreEqual("15191", vm.Recibidas[0].GetValue<string>("cliente"));
                Assert.AreEqual(0, otroVm.Recibidas.Count, "Solo a la vista de destino");
            });
        }

        [TestMethod]
        public void Entregar_ConElNombreCompletoOConConsulta_TambienLaEncuentra()
        {
            EnSta(() =>
            {
                var vm = new ViewModelReceptor();
                var destino = new ExtractoClienteView { DataContext = vm };

                ReceptorNavegacion.Entregar(new object[] { destino }, new object[] { destino }, typeof(ExtractoClienteView).FullName, null);
                ReceptorNavegacion.Entregar(new object[] { destino }, new object[] { destino }, "ExtractoClienteView?cliente=1", null);

                Assert.AreEqual(2, vm.Recibidas.Count);
                Assert.IsNotNull(vm.Recibidas[0], "Sin parámetros llega una lista vacía, nunca null");
            });
        }

        [TestMethod]
        public void Entregar_SiNoEstaActivaLaBuscaEntreTodasLasVistas()
        {
            EnSta(() =>
            {
                var vm = new ViewModelReceptor();
                var destino = new ExtractoClienteView { DataContext = vm };

                ReceptorNavegacion.Entregar(Array.Empty<object>(), new object[] { destino }, "ExtractoClienteView", null);

                Assert.AreEqual(1, vm.Recibidas.Count);
            });
        }

        [TestMethod]
        public void Entregar_ALaPropiaVistaSiEsReceptora()
        {
            var vista = new VistaReceptora();

            IReceptorNavegacion entregado = ReceptorNavegacion.Entregar(new object[] { vista }, new object[] { vista }, nameof(VistaReceptora), null);

            Assert.AreSame(vista, entregado);
            Assert.AreEqual(1, vista.Veces);
        }

        [TestMethod]
        public void Entregar_UnaVistaQueNoEsReceptora_NoHaceNada()
        {
            EnSta(() =>
            {
                var destino = new ExtractoClienteView { DataContext = new object() };

                Assert.IsNull(ReceptorNavegacion.Entregar(new object[] { destino }, new object[] { destino }, "ExtractoClienteView", null));
                Assert.IsNull(ReceptorNavegacion.Entregar(null, null, null, null));
            });
        }

        [TestMethod]
        public void DesdeParametrosPrism_ConservaClavesYValores()
        {
            var prism = new NavigationParameters { { "cliente", "15191" }, { "numero", 5 } };

            ParametrosNavegacion parametros = ServicioNavegacionPrism.DesdeParametrosPrism(prism);

            Assert.AreEqual("15191", parametros.GetValue<string>("cliente"));
            Assert.AreEqual(5, parametros.GetValue<int>("numero"));
            Assert.AreEqual(0, ServicioNavegacionPrism.DesdeParametrosPrism(null).Count);
        }
    }
}
