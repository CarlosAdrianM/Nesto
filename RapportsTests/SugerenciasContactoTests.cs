using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Unity;

namespace RapportsTests
{
    /// <summary>
    /// NestoAPI#603 corte 2: la lista de clientes para contactar sale de GET api/Clientes/SugerenciasContacto
    /// (prioridad, cadencia, motivo y ritmo). Si la API publicada aún no tiene el endpoint (404), cae al antiguo.
    /// </summary>
    [TestClass]
    public class SugerenciasContactoTests
    {
        private const string JSON_SUGERENCIAS = @"{
  ""Vendedor"": ""MPP"",
  ""Fecha"": ""2026-10-07T09:30:00"",
  ""Ritmo"": {
    ""ContactosHoy"": 9, ""ContactosSemana"": 42, ""ContactosMes"": 79,
    ""ObjetivoMes"": 310, ""ObjetivoHoy"": 14, ""DiasLaborablesRestantesMes"": 17,
    ""PendientesMaxima"": 12, ""PendientesAlta"": 85, ""PendientesMedia"": 160, ""PendientesBaja"": 120,
    ""Frase"": ""Llevas 9 contactos hoy; para cubrir la cartera este mes necesitas 14 al día.""
  },
  ""Sugerencias"": [
    {
      ""SugerenciaId"": 1234, ""Cliente"": ""15191"", ""Contacto"": ""0"", ""Nombre"": ""PELUQUERÍA UNO"", ""Direccion"": ""C/ Mayor 1"", ""CodigoPostal"": ""28001"",
      ""Telefono"": ""910000000"", ""Poblacion"": ""MADRID"", ""Provincia"": ""MADRID"",
      ""Prioridad"": ""Máxima"", ""Orden"": 1,
      ""Motivo"": ""Compra cada 10 días y lleva 34 días sin hablar contigo; probabilidad de pedido del 72 %"",
      ""Probabilidad"": 0.72, ""DiasDesdeUltimoContacto"": 34, ""DiasDesdeUltimoPedido"": 12, ""DiasDesdeUltimaInteraccion"": 34,
      ""CadenciaDias"": 10, ""PedidosUltimos12Meses"": 36, ""ImporteUltimos12Meses"": 5186.32,
      ""GrupoSubgrupoMasVendido"": ""COSCRE"", ""Atendida"": true
    },
    {
      ""SugerenciaId"": 1235, ""Cliente"": ""20000"", ""Contacto"": ""1"", ""Nombre"": ""ESTÉTICA DOS"",
      ""Prioridad"": ""Alta"", ""Orden"": 2, ""Motivo"": ""Lleva 45 días sin contacto"",
      ""Probabilidad"": 0.4, ""DiasDesdeUltimoContacto"": null, ""DiasDesdeUltimoPedido"": 50, ""DiasDesdeUltimaInteraccion"": 9999,
      ""CadenciaDias"": 30, ""PedidosUltimos12Meses"": 4, ""ImporteUltimos12Meses"": 300, ""Atendida"": false
    }
  ]
}";

        // ---- Servicio ----

        [TestMethod]
        public async Task CargarSugerenciasContacto_EndpointNuevo_DeserializaSugerenciasYRitmo()
        {
            var handler = new HandlerPorRuta(ruta => ruta.Contains("Clientes/SugerenciasContacto")
                ? Respuesta(HttpStatusCode.OK, JSON_SUGERENCIAS)
                : Respuesta(HttpStatusCode.InternalServerError, ""));
            var servicio = CrearServicio(handler);

            var resultado = await servicio.CargarSugerenciasContacto("MPP", "Llamada", "");

            Assert.AreEqual(2, resultado.Sugerencias.Count);
            var primera = resultado.Sugerencias[0];
            Assert.AreEqual("15191", primera.cliente);
            Assert.AreEqual("0", primera.contacto);
            Assert.AreEqual("PELUQUERÍA UNO", primera.nombre);
            Assert.AreEqual("MADRID", primera.poblacion);
            Assert.AreEqual("910000000", primera.telefono);
            Assert.AreEqual("Máxima", primera.Prioridad);
            Assert.AreEqual(1, primera.Orden);
            Assert.AreEqual(34, primera.DiasDesdeUltimoContacto);
            Assert.AreEqual(10, primera.CadenciaDias);
            Assert.AreEqual(1234, primera.SugerenciaId);
            Assert.IsTrue(primera.Atendida);
            Assert.AreEqual(0.72f, primera.Probabilidad, 0.0001f);
            Assert.IsNull(resultado.Sugerencias[1].DiasDesdeUltimoContacto);

            Assert.IsNotNull(resultado.Ritmo);
            Assert.AreEqual("Hoy 9 · Semana 42 · Mes 79 de 310", resultado.Ritmo.TextoContactos);
            Assert.AreEqual("Objetivo hoy: 14", resultado.Ritmo.TextoObjetivoHoy);
            Assert.AreEqual("Máxima 12 · Alta 85 · Media 160 · Baja 120", resultado.Ritmo.TextoPendientes);
            Assert.AreEqual(79 * 100.0 / 310, resultado.Ritmo.PorcentajeMes, 0.0001);
            Assert.IsTrue(handler.Rutas.Single().Contains("numero=20"));
            Assert.IsTrue(handler.Rutas.Single().Contains("vendedor=MPP"));
        }

        [TestMethod]
        public async Task CargarSugerenciasContacto_ApiSinElEndpoint404_CaeAlAntiguoSinRitmo()
        {
            var handler = new HandlerPorRuta(ruta => ruta.Contains("Clientes/SugerenciasContacto")
                ? Respuesta(HttpStatusCode.NotFound, "")
                : Respuesta(HttpStatusCode.OK, @"[{""cliente"":""15191"",""contacto"":""0"",""nombre"":""UNO"",""Probabilidad"":0.8,""DiasDesdeUltimoPedido"":5,""DiasDesdeUltimaInteraccion"":10}]"));
            var servicio = CrearServicio(handler);

            var resultado = await servicio.CargarSugerenciasContacto("MPP", "Llamada", "");

            Assert.IsNull(resultado.Ritmo);
            Assert.AreEqual(1, resultado.Sugerencias.Count);
            Assert.AreEqual("15191", resultado.Sugerencias[0].cliente);
            Assert.IsFalse(resultado.Sugerencias[0].TienePrioridad);
            Assert.AreEqual(2, handler.Rutas.Count);
            Assert.IsTrue(handler.Rutas[1].Contains("Clientes/GetClientesProbabilidadVenta"));
        }

        [TestMethod]
        public async Task CargarSugerenciasContacto_ErrorDelServidor_DevuelveListaVaciaNoNull()
        {
            var handler = new HandlerPorRuta(_ => Respuesta(HttpStatusCode.InternalServerError, ""));
            var servicio = CrearServicio(handler);

            var resultado = await servicio.CargarSugerenciasContacto("MPP", "Llamada", "");

            Assert.IsNotNull(resultado);
            Assert.IsNotNull(resultado.Sugerencias);
            Assert.AreEqual(0, resultado.Sugerencias.Count);
            Assert.AreEqual(1, handler.Rutas.Count, "Un 500 no es «endpoint sin publicar»: no cae al antiguo");
        }

        // ---- Orden ----

        [TestMethod]
        public void Ordenar_AtendidasAlFinalSinDesaparecer_CadaGrupoPorOrden()
        {
            var lista = new List<ClienteProbabilidadVenta>
            {
                new ClienteProbabilidadVenta { cliente = "A", Orden = 1, Atendida = true },
                new ClienteProbabilidadVenta { cliente = "B", Orden = 3 },
                new ClienteProbabilidadVenta { cliente = "C", Orden = 2 },
                new ClienteProbabilidadVenta { cliente = "D", Orden = 4, Atendida = true },
            };

            var ordenada = OrdenSugerenciasContacto.Ordenar(lista);

            CollectionAssert.AreEqual(new[] { "C", "B", "A", "D" }, ordenada.Select(c => c.cliente).ToArray());
        }

        [TestMethod]
        public void Ordenar_EndpointAntiguoSinOrden_RespetaElOrdenDeLlegada()
        {
            var lista = new List<ClienteProbabilidadVenta>
            {
                new ClienteProbabilidadVenta { cliente = "X", Probabilidad = 0.9f },
                new ClienteProbabilidadVenta { cliente = "Y", Probabilidad = 0.7f },
                new ClienteProbabilidadVenta { cliente = "Z", Probabilidad = 0.5f },
            };

            var ordenada = OrdenSugerenciasContacto.Ordenar(lista);

            CollectionAssert.AreEqual(new[] { "X", "Y", "Z" }, ordenada.Select(c => c.cliente).ToArray());
        }

        [TestMethod]
        public void Ordenar_Nothing_DevuelveListaVacia()
        {
            Assert.AreEqual(0, OrdenSugerenciasContacto.Ordenar(null).Count);
        }

        // ---- Converter de prioridad ----

        [TestMethod]
        public void PrioridadToBrushConverter_ConYSinTilde_DevuelveElPincelDeCadaPrioridad()
        {
            var conv = new PrioridadToBrushConverter
            {
                Maxima = Brushes.Red, Alta = Brushes.Orange, Media = Brushes.Blue, Baja = Brushes.Gray
            };

            Assert.AreSame(Brushes.Red, conv.Convert("Máxima", typeof(Brush), null, null));
            Assert.AreSame(Brushes.Red, conv.Convert("Maxima", typeof(Brush), null, null));
            Assert.AreSame(Brushes.Orange, conv.Convert("Alta", typeof(Brush), null, null));
            Assert.AreSame(Brushes.Blue, conv.Convert("Media", typeof(Brush), null, null));
            Assert.AreSame(Brushes.Gray, conv.Convert("Baja", typeof(Brush), null, null));
            Assert.AreSame(Brushes.Transparent, conv.Convert(null, typeof(Brush), null, null));
        }

        // ---- ViewModel ----

        [TestMethod]
        public async Task ActualizarClientesProbabilidad_ConSugerencias_AtendidasAlFinalYRitmoEnElPanel()
        {
            var servicio = A.Fake<IRapportService>();
            var ritmo = new RitmoContactoDTO { ContactosHoy = 9, ObjetivoMes = 310 };
            A.CallTo(() => servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new SugerenciasContactoRespuesta
                {
                    Ritmo = ritmo,
                    Sugerencias = new List<ClienteProbabilidadVenta>
                    {
                        new ClienteProbabilidadVenta { cliente = "A", Orden = 1, Prioridad = "Máxima", Atendida = true },
                        new ClienteProbabilidadVenta { cliente = "B", Orden = 2, Prioridad = "Alta" },
                    }
                }));
            var vm = CrearViewModel(servicio);

            await vm.ActualizarClientesProbabilidadAsync("");

            CollectionAssert.AreEqual(new[] { "B", "A" }, vm.ListaClientesProbabilidad.Select(c => c.cliente).ToArray());
            Assert.AreSame(ritmo, vm.RitmoContacto);
            Assert.IsTrue(vm.HayRitmoContacto);
            Assert.IsFalse(vm.IsLoadingClientesProbabilidad);
        }

        [TestMethod]
        public async Task ActualizarClientesProbabilidad_ConLaApiAntigua_ListaSinPanelDeRitmo()
        {
            var servicio = A.Fake<IRapportService>();
            A.CallTo(() => servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new SugerenciasContactoRespuesta
                {
                    Sugerencias = new List<ClienteProbabilidadVenta> { new ClienteProbabilidadVenta { cliente = "X" } }
                }));
            var vm = CrearViewModel(servicio);

            await vm.ActualizarClientesProbabilidadAsync("");

            Assert.AreEqual(1, vm.ListaClientesProbabilidad.Count);
            Assert.IsNull(vm.RitmoContacto);
            Assert.IsFalse(vm.HayRitmoContacto);
        }

        [TestMethod]
        public async Task ActualizarClientesProbabilidad_ServicioDevuelveNothing_ListaVaciaSinReventar()
        {
            var servicio = A.Fake<IRapportService>();
            A.CallTo(() => servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult<SugerenciasContactoRespuesta>(null));
            var vm = CrearViewModel(servicio);

            await vm.ActualizarClientesProbabilidadAsync("");

            Assert.AreEqual(0, vm.ListaClientesProbabilidad.Count);
            Assert.IsNull(vm.RitmoContacto);
        }

        // ---- Utilidades ----

        private static ListaRapportsViewModel CrearViewModel(IRapportService servicio)
        {
            return new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
        }

        private static RapportService CrearServicio(HttpMessageHandler handler)
        {
            var factory = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factory.Crear())
                .ReturnsLazily(() => new HttpClient(handler, false) { BaseAddress = new Uri("http://localhost/api/") });
            return new RapportService(A.Fake<IConfiguracion>(), null, A.Fake<IServicioAutenticacion>(), factory);
        }

        private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string contenido)
            => new HttpResponseMessage(codigo) { Content = new StringContent(contenido) };

        private sealed class HandlerPorRuta : HttpMessageHandler
        {
            private readonly Func<string, HttpResponseMessage> _responder;
            public List<string> Rutas { get; } = new List<string>();

            public HandlerPorRuta(Func<string, HttpResponseMessage> responder) => _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var ruta = request.RequestUri.PathAndQuery;
                Rutas.Add(ruta);
                return Task.FromResult(_responder(ruta));
            }
        }
    }
}
