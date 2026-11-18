using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Sugerencia 1339 (Paloma, 18/11/26): el tamaño del producto en la lista del buscador de productos.
    /// </summary>
    [TestClass]
    public class TamanoProductoBuscadorTests
    {
        #region Regla pura: TamanoProducto.Texto

        [TestMethod]
        public void Texto_Entero_SinDecimales()
        {
            Assert.AreEqual("50 ml", TamanoProducto.Texto(50m, "ml"));
        }

        [TestMethod]
        public void Texto_ConCerosDecimales_LosQuita()
        {
            Assert.AreEqual("50 ml", TamanoProducto.Texto(50.00m, "ml"));
        }

        [TestMethod]
        public void Texto_ConDecimales_ComaEspanola()
        {
            Assert.AreEqual("0,5 l", TamanoProducto.Texto(0.5m, "l"));
        }

        [TestMethod]
        public void Texto_UnidadEnMayusculasYConEspacios_EnMinusculas()
        {
            Assert.AreEqual("138 gr", TamanoProducto.Texto(138m, " GR  "));
        }

        [TestMethod]
        public void Texto_SinTamano_Null()
        {
            Assert.IsNull(TamanoProducto.Texto(null, "ml"));
        }

        [TestMethod]
        public void Texto_TamanoCero_Null()
        {
            Assert.IsNull(TamanoProducto.Texto(0m, "ml"));
        }

        [TestMethod]
        public void Texto_TamanoNegativo_Null()
        {
            Assert.IsNull(TamanoProducto.Texto(-1m, "ml"));
        }

        [TestMethod]
        public void Texto_SinUnidad_Null()
        {
            Assert.IsNull(TamanoProducto.Texto(50m, null));
            Assert.IsNull(TamanoProducto.Texto(50m, "  "));
        }

        #endregion

        #region Regla pura: TamanoProducto.NombreConTamano

        [TestMethod]
        public void NombreConTamano_ConTamano_DetrasDelNombre()
        {
            Assert.AreEqual("HIDRATANTE OPTIMA CREAM 50 ml", TamanoProducto.NombreConTamano("HIDRATANTE OPTIMA CREAM", 50m, "ml"));
        }

        [TestMethod]
        public void NombreConTamano_SinTamano_ElNombreTalCual()
        {
            Assert.AreEqual("HIDRATANTE OPTIMA CREAM", TamanoProducto.NombreConTamano("HIDRATANTE OPTIMA CREAM", null, null));
        }

        [TestMethod]
        public void NombreConTamano_ElNombreYaLoDice_NoLoRepite()
        {
            Assert.AreEqual("CHAMPÚ 250 ML", TamanoProducto.NombreConTamano("CHAMPÚ 250 ML", 250m, "ml"));
            Assert.AreEqual("CHAMPÚ 250ML", TamanoProducto.NombreConTamano("CHAMPÚ 250ML", 250m, "ml"));
        }

        [TestMethod]
        public void NombreConTamano_ElNombreDiceOtroNumeroQueAcabaIgual_SiLoPone()
        {
            // «150 ML» no es «50 ml»
            Assert.AreEqual("CREMA 150 ML 50 ml", TamanoProducto.NombreConTamano("CREMA 150 ML", 50m, "ml"));
        }

        [TestMethod]
        public void NombreConTamano_UnidadPegadaAOtraPalabra_SiLoPone()
        {
            // «20 VOL.» no dice «20 v»
            Assert.AreEqual("AGUA OXIGENADA 20 VOL. 20 v", TamanoProducto.NombreConTamano("AGUA OXIGENADA 20 VOL.", 20m, "v"));
        }

        #endregion

        #region AutocompleteItem.TextoMostrar

        [TestMethod]
        public void TextoMostrar_ConTamanoYFamilia()
        {
            var item = new AutocompleteItem
            {
                Id = "44256",
                Texto = "HIDRATANTE OPTIMA CREAM",
                TextoSecundario = "Belclinic",
                Tamanno = 50m,
                UnidadMedida = "ml"
            };
            Assert.AreEqual("44256 - HIDRATANTE OPTIMA CREAM 50 ml (Belclinic)", item.TextoMostrar);
        }

        [TestMethod]
        public void TextoMostrar_ConTamanoSinFamilia()
        {
            var item = new AutocompleteItem { Id = "44256", Texto = "HIDRATANTE OPTIMA CREAM", Tamanno = 200m, UnidadMedida = "ml" };
            Assert.AreEqual("44256 - HIDRATANTE OPTIMA CREAM 200 ml", item.TextoMostrar);
        }

        [TestMethod]
        public void TextoMostrar_SinTamano_ComoSiempre()
        {
            var item = new AutocompleteItem { Id = "44256", Texto = "HIDRATANTE OPTIMA CREAM", TextoSecundario = "Belclinic" };
            Assert.AreEqual("44256 - HIDRATANTE OPTIMA CREAM (Belclinic)", item.TextoMostrar);
        }

        [TestMethod]
        public void TextoMostrar_TamanoSinUnidad_ComoSiempre()
        {
            var item = new AutocompleteItem { Id = "44256", Texto = "HIDRATANTE OPTIMA CREAM", TextoSecundario = "Belclinic", Tamanno = 50m };
            Assert.AreEqual("44256 - HIDRATANTE OPTIMA CREAM (Belclinic)", item.TextoMostrar);
        }

        #endregion

        #region ServicioBusquedaProductos: lee Tamanno y UnidadMedida si la API los manda

        [TestMethod]
        public async Task ServicioBusquedaProductos_ConTamanoEnLaRespuesta_LoPasaAlItem()
        {
            string json = "[{\"Tipo\":\"producto\",\"Id\":\"44256\",\"Nombre\":\"HIDRATANTE OPTIMA CREAM\",\"Familia\":\"Belclinic\",\"Tamanno\":50,\"UnidadMedida\":\"ml\"}]";
            var servicio = CrearServicio(json);

            IList<AutocompleteItem> resultado = await servicio.BuscarSugerenciasAsync("optima", "1", 10);

            Assert.AreEqual(1, resultado.Count);
            Assert.AreEqual("44256 - HIDRATANTE OPTIMA CREAM 50 ml (Belclinic)", resultado[0].TextoMostrar);
        }

        [TestMethod]
        public async Task ServicioBusquedaProductos_SinTamanoEnLaRespuesta_ComoSiempre()
        {
            string json = "[{\"Tipo\":\"producto\",\"Id\":\"44256\",\"Nombre\":\"HIDRATANTE OPTIMA CREAM\",\"Familia\":\"Belclinic\",\"Anulado\":false}]";
            var servicio = CrearServicio(json);

            IList<AutocompleteItem> resultado = await servicio.BuscarSugerenciasAsync("optima", "1", 10);

            Assert.AreEqual(1, resultado.Count);
            Assert.IsNull(resultado[0].Tamanno);
            Assert.AreEqual("44256 - HIDRATANTE OPTIMA CREAM (Belclinic)", resultado[0].TextoMostrar);
        }

        private static ServicioBusquedaProductos CrearServicio(string json)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() => new HttpClient(new ManejadorFijo(json))
            {
                BaseAddress = new Uri("http://localhost/api/")
            });
            return new ServicioBusquedaProductos(A.Fake<IConfiguracion>(), factoria);
        }

        private class ManejadorFijo : HttpMessageHandler
        {
            private readonly string _json;

            public ManejadorFijo(string json)
            {
                _json = json;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_json, Encoding.UTF8, "application/json")
                });
            }
        }

        #endregion
    }
}
