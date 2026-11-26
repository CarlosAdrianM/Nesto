using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#857j (OK de Carlos 26/11/26): en «Envíos de proveedor por asignar», los correos de agencia que la API no ha sabido leer, solo
    /// lectura, con «Copiar» (asunto, remitente y extracto) para pasarlos a Informática. Con 503, 403 o ninguno, no se pinta.
    /// </summary>
    [TestClass]
    public class CorreosAgenciaSinReconocerTests
    {
        private readonly IServicioEnviosProveedorPorAsignar servicio = A.Fake<IServicioEnviosProveedorPorAsignar>();

        private EnviosProveedorPorAsignarViewModel Crear() => new(servicio, A.Fake<IServicioDialogos>());

        private static CorreoAgenciaSinReconocer Correo(int id, DateTime recibido) => new()
        {
            Id = id, Recibido = recibido, Remitente = "avisos@seur.com", Agencia = "SEUR", Asunto = "Tu envío llega hoy", Extracto = "Identificador de envío: A-2609942"
        };

        [TestMethod]
        public async Task ConCorreos_SeVen_DelMasNuevoAlMasAntiguo_YElPrimeroElegido()
        {
            A.CallTo(() => servicio.LeerCorreosSinReconocer(30)).Returns(new List<CorreoAgenciaSinReconocer>
            {
                Correo(1, new DateTime(2026, 11, 20)), Correo(2, new DateTime(2026, 11, 26, 9, 15, 0))
            });
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await vm.CargarCorreosSinReconocerAsync();

            Assert.IsTrue(vm.HayCorreosSinReconocer);
            Assert.AreEqual(2, vm.CorreosSinReconocer[0].Id);
            Assert.AreSame(vm.CorreosSinReconocer[0], vm.CorreoSinReconocerSeleccionado);
            StringAssert.StartsWith(vm.TextoCorreosSinReconocer, "2 correos de agencia");
        }

        [TestMethod]
        public async Task SinNada_403O503_NoSePinta()
        {
            A.CallTo(() => servicio.LeerCorreosSinReconocer(A<int>._)).Returns(Task.FromResult<List<CorreoAgenciaSinReconocer>>(null));
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await vm.CargarCorreosSinReconocerAsync();

            Assert.IsFalse(vm.HayCorreosSinReconocer);
            Assert.IsFalse(vm.CopiarCorreoSinReconocerCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SiFalla_NoSePintaNiRompe()
        {
            A.CallTo(() => servicio.LeerCorreosSinReconocer(A<int>._)).Throws(new Exception("red"));
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await vm.CargarCorreosSinReconocerAsync();

            Assert.IsFalse(vm.HayCorreosSinReconocer);
        }

        [TestMethod]
        public async Task Copiar_AsuntoRemitenteYExtracto()
        {
            A.CallTo(() => servicio.LeerCorreosSinReconocer(30)).Returns(new List<CorreoAgenciaSinReconocer> { Correo(1, new DateTime(2026, 11, 26, 9, 15, 0)) });
            EnviosProveedorPorAsignarViewModel vm = Crear();
            string copiado = null;
            vm.CopiarAlPortapapeles = t => copiado = t;
            await vm.CargarCorreosSinReconocerAsync();

            vm.CopiarCorreoSinReconocerCommand.Execute(null);

            StringAssert.Contains(copiado, "Asunto: Tu envío llega hoy");
            StringAssert.Contains(copiado, "Remitente: avisos@seur.com");
            StringAssert.Contains(copiado, "Identificador de envío: A-2609942");
        }

        [TestMethod]
        public void Url()
        {
            Assert.AreEqual("Compras/AgenciasEntrada/Correos/SinReconocer?dias=30", ServicioEnviosProveedorPorAsignar.UrlCorreosSinReconocer(30));
        }
    }
}
