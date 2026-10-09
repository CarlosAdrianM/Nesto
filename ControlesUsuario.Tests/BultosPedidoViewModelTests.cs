using ControlesUsuario.BultosPedido;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#522: en el detalle del pedido, junto al seguimiento de la agencia, los bultos del packing de Ariadna con su
    /// foto: verla, descargarla y copiar el enlace para el cliente.
    /// </summary>
    [TestClass]
    public class BultosPedidoViewModelTests
    {
        private const string SERVIDOR = "http://api.nuevavision.es/api/";

        private sealed class AccionesFalsas : IAccionesFotoBulto
        {
            public string CarpetaTemporal => @"C:\Temporal";
            public string RutaElegida = @"D:\Reclamaciones\Pedido_928123_bulto_2.jpg";
            public readonly List<string> Propuestos = new List<string>();
            public readonly Dictionary<string, byte[]> Guardados = new Dictionary<string, byte[]>();
            public readonly List<string> Abiertos = new List<string>();
            public readonly List<string> Copiados = new List<string>();
            public bool FallarAlCopiar;

            public string ElegirDondeGuardar(string nombrePropuesto)
            {
                Propuestos.Add(nombrePropuesto);
                return RutaElegida;
            }

            public void Guardar(string ruta, byte[] datos) => Guardados[ruta] = datos;
            public void Abrir(string ruta) => Abiertos.Add(ruta);

            public void CopiarAlPortapapeles(string texto)
            {
                if (FallarAlCopiar)
                {
                    throw new InvalidOperationException("Portapapeles ocupado");
                }
                Copiados.Add(texto);
            }
        }

        private IServicioBultosAriadna _servicio;
        private AccionesFalsas _acciones;
        private BultosPedidoViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioBultosAriadna>();
            _acciones = new AccionesFalsas();
            _vm = new BultosPedidoViewModel(_servicio, SERVIDOR, _acciones);
        }

        private static BultoAriadna Bulto(int id, int bulto, bool foto = true, int? envio = 5001) => new BultoAriadna
        {
            Id = id,
            Pedido = 928123,
            Bulto = bulto,
            Peso = 3.5M,
            TieneFoto = foto,
            NumeroEnvio = envio,
            Usuario = @"NUEVAVISION\Andre",
            FechaFoto = new DateTime(2026, 10, 9, 10, 32, 0),
            RutaFotoPublica = foto ? $"api/Almacen/Fotos/{id}-firma" : null
        };

        private void ConBultos(params BultoAriadna[] bultos)
            => A.CallTo(() => _servicio.LeerBultosDelPedido("1", 928123)).Returns(bultos.ToList());

        private BultoPedidoItem Unico() => _vm.Grupos.Single().Bultos.Single();

        [TestMethod]
        public async Task Cargar_SinBultos_NoSeEnseñaNada()
        {
            ConBultos();

            await _vm.Cargar("1", 928123);

            Assert.IsFalse(_vm.HayBultos);
            Assert.AreEqual(0, _vm.Grupos.Count);
            Assert.AreEqual(string.Empty, _vm.Resumen);
        }

        [TestMethod]
        public async Task Cargar_PedidoSinGrabar_NoPreguntaALaApi()
        {
            await _vm.Cargar("1", 0);

            Assert.IsFalse(_vm.HayBultos);
            A.CallTo(() => _servicio.LeerBultosDelPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_LaApiFalla_NoRompeYNoSeEnseñaNada()
        {
            A.CallTo(() => _servicio.LeerBultosDelPedido("1", 928123)).ThrowsAsync(new HttpRequestException("500"));

            await _vm.Cargar("1", 928123);

            Assert.IsFalse(_vm.HayBultos);
            Assert.IsFalse(_vm.HayMensaje);
        }

        [TestMethod]
        public async Task Cargar_ConYSinFoto_EnseñaCadaBultoConSuTextoYSoloLosConFotoTienenAcciones()
        {
            ConBultos(Bulto(18, 2, foto: false), Bulto(17, 1));

            await _vm.Cargar("1", 928123);

            Assert.IsTrue(_vm.HayBultos);
            Assert.IsFalse(_vm.HayVariosEnvios);
            Assert.AreEqual("Ariadna: 2 bultos (1 con foto)", _vm.Resumen);
            List<BultoPedidoItem> bultos = _vm.Grupos.Single().Bultos.ToList();
            Assert.AreEqual("Bulto 1 · 3,5 kg · cerrado por Andre el 09/10/26 10:32", bultos[0].Texto);
            Assert.IsTrue(bultos[0].VerFotoCommand.CanExecute(null));
            Assert.IsTrue(bultos[0].DescargarCommand.CanExecute(null));
            Assert.IsTrue(bultos[0].TieneEnlacePublico);
            Assert.AreEqual("Bulto 2 · 3,5 kg · cerrado por Andre el 09/10/26 10:32 · sin foto", bultos[1].Texto);
            Assert.IsFalse(bultos[1].VerFotoCommand.CanExecute(null));
            Assert.IsFalse(bultos[1].DescargarCommand.CanExecute(null));
            Assert.IsFalse(bultos[1].CopiarEnlaceCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_VariasEntregas_AgrupaPorEnvio()
        {
            ConBultos(Bulto(17, 1, envio: 5001), Bulto(20, 1, envio: 5002), Bulto(18, 2, envio: 5001));

            await _vm.Cargar("1", 928123);

            Assert.IsTrue(_vm.HayVariosEnvios);
            Assert.AreEqual("Envío 5001", _vm.Grupos[0].Titulo);
            CollectionAssert.AreEqual(new[] { 17, 18 }, _vm.Grupos[0].Bultos.Select(b => b.Id).ToList());
            Assert.AreEqual("Envío 5002", _vm.Grupos[1].Titulo);
        }

        [TestMethod]
        public async Task Cargar_OtroPedidoMientrasTanto_DescartaLaRespuestaDelAnterior()
        {
            var lenta = new TaskCompletionSource<List<BultoAriadna>>();
            A.CallTo(() => _servicio.LeerBultosDelPedido("1", 928123)).Returns(lenta.Task);
            A.CallTo(() => _servicio.LeerBultosDelPedido("1", 928124)).Returns(new List<BultoAriadna>());

            Task primera = _vm.Cargar("1", 928123);
            await _vm.Cargar("1", 928124);
            lenta.SetResult(new List<BultoAriadna> { Bulto(17, 1) });
            await primera;

            Assert.IsFalse(_vm.HayBultos);
        }

        [TestMethod]
        public async Task VerFoto_PideLaFotoLaGuardaEnTemporalYLaAbre()
        {
            ConBultos(Bulto(17, 2));
            A.CallTo(() => _servicio.DescargarFoto(17)).Returns(new byte[] { 1, 2, 3 });
            await _vm.Cargar("1", 928123);

            await Unico().VerFotoCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.DescargarFoto(17)).MustHaveHappenedOnceExactly();
            string abierto = _acciones.Abiertos.Single();
            StringAssert.StartsWith(abierto, @"C:\Temporal\Pedido_928123_bulto_2_");
            StringAssert.EndsWith(abierto, ".jpg");
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, _acciones.Guardados[abierto]);
        }

        [TestMethod]
        public async Task VerFoto_DosVeces_PideElEnlaceCadaVez()
        {
            ConBultos(Bulto(17, 2));
            A.CallTo(() => _servicio.DescargarFoto(17)).Returns(new byte[] { 1 });
            await _vm.Cargar("1", 928123);

            await Unico().VerFotoCommand.ExecuteAsync(null);
            await Unico().VerFotoCommand.ExecuteAsync(null);

            // El enlace temporal caduca en minutos: se vuelve a pedir en cada clic
            A.CallTo(() => _servicio.DescargarFoto(17)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task VerFoto_LaApiFalla_LoDiceSinRomper()
        {
            ConBultos(Bulto(17, 2));
            A.CallTo(() => _servicio.DescargarFoto(17)).ThrowsAsync(new HttpRequestException("Sin conexión"));
            await _vm.Cargar("1", 928123);

            await Unico().VerFotoCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _acciones.Abiertos.Count);
            StringAssert.Contains(_vm.Mensaje, "No se ha podido abrir la foto del bulto 2");
            StringAssert.Contains(_vm.Mensaje, "Sin conexión");
        }

        [TestMethod]
        public async Task Descargar_ProponeElNombreYGuardaDondeSeElige()
        {
            ConBultos(Bulto(17, 2));
            A.CallTo(() => _servicio.DescargarFoto(17)).Returns(new byte[] { 9, 8 });
            await _vm.Cargar("1", 928123);

            await Unico().DescargarCommand.ExecuteAsync(null);

            Assert.AreEqual("Pedido_928123_bulto_2.jpg", _acciones.Propuestos.Single());
            CollectionAssert.AreEqual(new byte[] { 9, 8 }, _acciones.Guardados[@"D:\Reclamaciones\Pedido_928123_bulto_2.jpg"]);
            Assert.AreEqual(@"Foto guardada en D:\Reclamaciones\Pedido_928123_bulto_2.jpg", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Descargar_SiSeCancelaElDialogo_NoDescargaNada()
        {
            ConBultos(Bulto(17, 2));
            _acciones.RutaElegida = null;
            await _vm.Cargar("1", 928123);

            await Unico().DescargarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.DescargarFoto(A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(0, _acciones.Guardados.Count);
            Assert.IsFalse(_vm.HayMensaje);
        }

        [TestMethod]
        public async Task Descargar_LaFotoYaNoExiste_LoDice()
        {
            ConBultos(Bulto(17, 2));
            A.CallTo(() => _servicio.DescargarFoto(17)).Returns(Task.FromResult<byte[]>(null));
            await _vm.Cargar("1", 928123);

            await Unico().DescargarCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _acciones.Guardados.Count);
            Assert.AreEqual("El bulto 2 ya no tiene foto.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task CopiarEnlace_ComponeLaUrlPublicaConElServidorYAvisa()
        {
            ConBultos(Bulto(17, 2));
            await _vm.Cargar("1", 928123);

            Unico().CopiarEnlaceCommand.Execute(null);

            Assert.AreEqual("http://api.nuevavision.es/api/Almacen/Fotos/17-firma", _acciones.Copiados.Single());
            StringAssert.Contains(_vm.Mensaje, "cualquiera que tenga el enlace puede ver la foto");
        }

        [TestMethod]
        public async Task CopiarEnlace_ApiSinRutaPublica_NoSeOfrece()
        {
            BultoAriadna bulto = Bulto(17, 2);
            bulto.RutaFotoPublica = null; // API anterior o sin clave para firmar el enlace
            ConBultos(bulto);
            await _vm.Cargar("1", 928123);

            Assert.IsFalse(Unico().TieneEnlacePublico);
            Assert.IsFalse(Unico().CopiarEnlaceCommand.CanExecute(null));
            Assert.IsTrue(Unico().VerFotoCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task CopiarEnlace_ElPortapapelesFalla_LoDiceSinRomper()
        {
            ConBultos(Bulto(17, 2));
            _acciones.FallarAlCopiar = true;
            await _vm.Cargar("1", 928123);

            Unico().CopiarEnlaceCommand.Execute(null);

            StringAssert.Contains(_vm.Mensaje, "No se ha podido copiar el enlace");
        }

        [TestMethod]
        public async Task Cargar_OtroPedido_BorraElMensajeAnterior()
        {
            ConBultos(Bulto(17, 2));
            await _vm.Cargar("1", 928123);
            Unico().CopiarEnlaceCommand.Execute(null);

            await _vm.Cargar("1", 0);

            Assert.IsFalse(_vm.HayMensaje);
            Assert.IsFalse(_vm.HayBultos);
        }
    }
}
