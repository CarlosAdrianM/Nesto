using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Carlos (19/11/26): «Copiar datos», «Copiar con comentarios» y «Copiar comentario» del botón derecho en Novedades, y el
    /// número visible (con clic, se copia). El texto lo compone <see cref="TextoCopiaNovedad"/> (puro).
    /// </summary>
    [TestClass]
    public class NovedadesCopiarTests
    {
        private static NovedadUsuario Sugerencia(int id = 1362, bool incidencia = false) => new()
        {
            Id = id,
            Categoria = incidencia ? "Incidencia" : "Mejorado",
            EsIncidencia = incidencia,
            Titulo = "Inscritos en una rendija",
            TextoOriginal = "No se ve la lista de inscritos con la ventana mediana",
            SugeridaNombre = "Pilar",
            SugeridaFecha = new DateTime(2026, 11, 19, 10, 21, 0),
            Estado = "Pendiente",
            TieneImagen = true,
            NumeroComentarios = 3,
            VotosPositivos = 0,
            VotosNegativos = 0
        };

        private static ComentarioItem Comentario(int id, string autor, string texto, int minuto = 30, int capturas = 0)
        {
            var capturasLista = new List<CapturaNovedad>();
            for (int i = 0; i < capturas; i++)
            {
                capturasLista.Add(new CapturaNovedad { Id = i + 1 });
            }
            return new ComentarioItem(new ComentarioNovedad
            {
                Id = id, NovedadId = 1362, NombreVisible = autor, Texto = texto, Fecha = new DateTime(2026, 11, 19, 10, minuto, 0),
                TieneImagen = capturas > 0, Capturas = capturasLista
            });
        }

        [TestMethod]
        public void Datos_DeUnaSugerencia_ComoPideCarlos()
        {
            string texto = TextoCopiaNovedad.Datos(Sugerencia(), 3);

            Assert.AreEqual(
                "Sugerencia 1362 (Nesto) — Pilar, 19/11/2026 10:21 — Estado: Pendiente\r\n" +
                "Inscritos en una rendija\r\n" +
                "No se ve la lista de inscritos con la ventana mediana\r\n" +
                "Capturas: 1 · Comentarios: 3", texto);
        }

        [TestMethod]
        public void Datos_ConDescripcionRedactadaDistinta_TambienLaLleva()
        {
            NovedadUsuario n = Sugerencia();
            n.Descripcion = "Separadores móviles y lista imprimible.";
            n.Capturas = new List<CapturaNovedad> { new() { Id = 1 }, new() { Id = 2 } };

            string texto = TextoCopiaNovedad.Datos(n, 0);

            StringAssert.Contains(texto, "Descripción: Separadores móviles y lista imprimible.\r\n");
            StringAssert.EndsWith(texto, "Capturas: 2 · Comentarios: 0");
        }

        [TestMethod]
        public void Cabecera_Incidencia_YNovedadPublicadaConVersion()
        {
            Assert.IsTrue(TextoCopiaNovedad.Cabecera(Sugerencia(1356, incidencia: true)).StartsWith("Incidencia 1356 (Nesto) — Pilar"));

            var publicada = new NovedadUsuario { Id = 1350, Version = "1.10.64.0", Ambito = "Ariadna", Fecha = new DateTime(2026, 11, 18), Titulo = "Botonera nueva", Descripcion = "Más clara." };
            Assert.AreEqual("Novedad 1350 (Ariadna, versión 1.10.64.0) — 18/11/2026", TextoCopiaNovedad.Cabecera(publicada));
            Assert.AreEqual("Novedad 1350 (Ariadna, versión 1.10.64.0) — 18/11/2026\r\nBotonera nueva\r\nMás clara.\r\nCapturas: 0 · Comentarios: 0",
                TextoCopiaNovedad.Datos(publicada, 0));
        }

        [TestMethod]
        public void ConComentarios_UnoPorLinea()
        {
            string texto = TextoCopiaNovedad.DatosConComentarios(Sugerencia(), new[]
            {
                Comentario(1, "Carlos", " Lo miramos. "),
                Comentario(2, "Pilar", "Gracias", 45, capturas: 2)
            });

            StringAssert.EndsWith(texto,
                "Capturas: 1 · Comentarios: 2\r\n\r\nComentarios:\r\n" +
                "Carlos, 19/11/2026 10:30: Lo miramos.\r\n" +
                "Pilar, 19/11/2026 10:45: Gracias [2 capturas]");
        }

        [TestMethod]
        public void ComentarioSuelto_ConElNumeroDeSuNovedad()
        {
            Assert.AreEqual("Comentario de la sugerencia 1362 (Nesto) — Laura, 19/11/2026 10:30: ¿Y en Reina?",
                TextoCopiaNovedad.Comentario(Sugerencia(), Comentario(9, "Laura", "¿Y en Reina?")));
        }

        private static (NovedadItem item, IServicioPortapapeles portapapeles, INovedadesService servicio) Item(NovedadUsuario n = null)
        {
            var portapapeles = A.Fake<IServicioPortapapeles>();
            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(true);
            var servicio = A.Fake<INovedadesService>();
            var item = new NovedadItem(n ?? Sugerencia(), servicio, null, null, portapapelesTexto: portapapeles);
            return (item, portapapeles, servicio);
        }

        [TestMethod]
        public void NumeroVisible_YConClicSeCopiaSolo()
        {
            var (item, portapapeles, _) = Item();

            Assert.AreEqual("nº 1362", item.TextoNumero);
            item.CopiarNumeroCommand.Execute(null);

            A.CallTo(() => portapapeles.Copiar("1362")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Copiado el número 1362.", item.AvisoCopia);
            Assert.IsFalse(item.AvisoCopiaEsError);
        }

        [TestMethod]
        public void CopiarDatos_SiWindowsNoDeja_MensajeDeUstedSinExcepcion()
        {
            var (item, portapapeles, _) = Item();
            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(false);

            Assert.IsFalse(item.CopiarDatos());
            Assert.AreEqual(NovedadItem.ERROR_COPIAR, item.AvisoCopia);
            Assert.IsTrue(item.AvisoCopiaEsError);

            A.CallTo(() => portapapeles.Copiar(A<string>._)).Throws(new System.Runtime.InteropServices.COMException("CLIPBRD_E_CANT_OPEN"));
            Assert.IsFalse(item.CopiarDatos());
            Assert.AreEqual(NovedadItem.ERROR_COPIAR, item.AvisoCopia);
        }

        [TestMethod]
        public async Task CopiarConComentarios_SinAbrirlos_LosLeeDeLaApi()
        {
            var (item, portapapeles, servicio) = Item();
            A.CallTo(() => servicio.LeerComentarios(1362)).Returns(new List<ComentarioNovedad>
            {
                new() { Id = 1, NovedadId = 1362, NombreVisible = "Carlos", Texto = "Hecho", Fecha = new DateTime(2026, 11, 19, 12, 0, 0) }
            });

            Assert.IsTrue(await item.CopiarConComentarios());

            A.CallTo(() => portapapeles.Copiar(A<string>.That.EndsWith("Comentarios:\r\nCarlos, 19/11/2026 12:00: Hecho"))).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, item.Comentarios.Count, "No se abren en la ventana");
        }

        [TestMethod]
        public void CopiarComentario_ConElDeLaNovedad()
        {
            var (item, portapapeles, _) = Item();

            item.CopiarComentarioCommand.Execute(Comentario(9, "Laura", "¿Y en Reina?"));
            item.CopiarComentarioCommand.Execute(null);

            A.CallTo(() => portapapeles.Copiar("Comentario de la sugerencia 1362 (Nesto) — Laura, 19/11/2026 10:30: ¿Y en Reina?")).MustHaveHappenedOnceExactly();
        }
    }
}
