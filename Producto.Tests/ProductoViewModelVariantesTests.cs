using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Nesto.Modules.Producto;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#477: bloque "Variantes para la web" de la ficha. Misma mecánica que las categorías
    /// web: la familia se carga con el producto, se edita como lista y se guarda de una vez.
    /// </summary>
    [TestClass]
    public class ProductoViewModelVariantesTests
    {
        private const string CHECK = "45813";

        private static ProductoViewModel CrearViewModel(out IProductoService servicio, out IDialogService dialogService)
        {
            var fake = A.Fake<IProductoService>();
            A.CallTo(() => fake.LeerProducto(CHECK)).Returns(new ProductoModel
            {
                Producto = CHECK,
                Nombre = "SILLON DE BARBERO CHECK",
                Grupo = "APA",
                SubgrupoCodigo = "MOB",
                Subgrupo = "Mobiliario"
            });
            A.CallTo(() => fake.LeerProducto("45814")).Returns(new ProductoModel { Producto = "45814", Nombre = "SILLON DE BARBERO CHECK BR" });
            A.CallTo(() => fake.LeerProducto("99999")).Returns((ProductoModel)null);
            A.CallTo(() => fake.LeerVariantes(CHECK)).Returns(new List<VarianteModel>());
            dialogService = A.Fake<IDialogService>();
            servicio = fake;
            return new ProductoViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), fake,
                A.Fake<IEventAggregator>(), dialogService, A.Fake<IServicioAutenticacion>());
        }

        private static VarianteModel Variante(string numero, string valor, int orden, string nombre = null)
        {
            return new VarianteModel { Numero = numero, Principal = CHECK, Atributo = "Color", Valor = valor, Orden = orden, Nombre = nombre };
        }

        [TestMethod]
        public void AlCargar_LasVariantesLleganEnSuOrdenYLaPrincipalEsLaDeLaFamilia()
        {
            var sut = CrearViewModel(out var servicio, out _);
            A.CallTo(() => servicio.LeerVariantes(CHECK)).Returns(new List<VarianteModel>
            {
                Variante(CHECK, "Negro", 1, "SILLON DE BARBERO CHECK"),
                Variante("45814", "Marrón", 2, "SILLON DE BARBERO CHECK BR")
            });

            sut.ReferenciaBuscar = CHECK;

            Assert.AreEqual(2, sut.Variantes.Count);
            Assert.AreEqual("45813 — SILLON DE BARBERO CHECK · Color: Negro (principal)", sut.Variantes[0].Descripcion);
            Assert.AreEqual("45814 — SILLON DE BARBERO CHECK BR · Color: Marrón", sut.Variantes[1].Descripcion);
            Assert.AreEqual(CHECK, sut.PrincipalVariantes);
            StringAssert.Contains(sut.TextoPrincipalVariantes, "esta ficha");
        }

        [TestMethod]
        public void AlCargar_SinFamilia_LaPrincipalEsEstaFicha()
        {
            var sut = CrearViewModel(out _, out _);

            sut.ReferenciaBuscar = CHECK;

            Assert.AreEqual(0, sut.Variantes.Count);
            Assert.AreEqual(CHECK, sut.PrincipalVariantes);
            StringAssert.Contains(sut.TextoPrincipalVariantes, "sin familia");
        }

        [TestMethod]
        public void AlCargarUnaHermana_LaPrincipalEsLaDeLaFamiliaNoEstaFicha()
        {
            var sut = CrearViewModel(out var servicio, out _);
            A.CallTo(() => servicio.LeerProducto("45814")).Returns(new ProductoModel { Producto = "45814", Nombre = "SILLON DE BARBERO CHECK BR", Grupo = "APA", SubgrupoCodigo = "MOB", Subgrupo = "Mobiliario" });
            A.CallTo(() => servicio.LeerVariantes("45814")).Returns(new List<VarianteModel>
            {
                Variante(CHECK, "Negro", 1), Variante("45814", "Marrón", 2)
            });

            sut.ReferenciaBuscar = "45814";

            Assert.AreEqual(CHECK, sut.PrincipalVariantes);
            StringAssert.Contains(sut.TextoPrincipalVariantes, "variante suya");
        }

        [TestMethod]
        public void Annadir_ReferenciaQueExiste_LaPoneAlFinalConSuNombreYLimpiaLaEntrada()
        {
            var sut = CrearViewModel(out _, out _);
            sut.ReferenciaBuscar = CHECK;

            sut.NuevaVarianteReferencia = "45814";
            sut.NuevaVarianteValor = "Marrón";
            Assert.IsTrue(sut.AnnadirVarianteCommand.CanExecute(null));
            sut.AnnadirVarianteCommand.Execute(null);

            Assert.AreEqual(1, sut.Variantes.Count);
            Assert.AreEqual("45814", sut.Variantes[0].Numero);
            Assert.AreEqual(CHECK, sut.Variantes[0].Principal, "la principal de una familia nueva es esta ficha");
            Assert.AreEqual("SILLON DE BARBERO CHECK BR", sut.Variantes[0].Nombre);
            Assert.AreEqual("Color", sut.Variantes[0].Atributo);
            Assert.IsNull(sut.NuevaVarianteReferencia);
            Assert.IsNull(sut.NuevaVarianteValor);
        }

        [TestMethod]
        public void Annadir_SinValor_NoSePuede()
        {
            var sut = CrearViewModel(out _, out _);
            sut.ReferenciaBuscar = CHECK;

            sut.NuevaVarianteReferencia = "45814";

            Assert.IsFalse(sut.AnnadirVarianteCommand.CanExecute(null));
        }

        [TestMethod]
        public void Annadir_ReferenciaInexistente_AvisaYNoAnnade()
        {
            var sut = CrearViewModel(out _, out _);
            sut.ReferenciaBuscar = CHECK;

            sut.NuevaVarianteReferencia = "99999";
            sut.NuevaVarianteValor = "Gris";
            sut.AnnadirVarianteCommand.Execute(null);

            Assert.AreEqual(0, sut.Variantes.Count);
        }

        [TestMethod]
        public void Annadir_LaMismaReferenciaDosVeces_NoLaDuplica()
        {
            var sut = CrearViewModel(out var servicio, out _);
            A.CallTo(() => servicio.LeerVariantes(CHECK)).Returns(new List<VarianteModel> { Variante("45814", "Marrón", 1) });
            sut.ReferenciaBuscar = CHECK;

            sut.NuevaVarianteReferencia = " 45814 ";
            sut.NuevaVarianteValor = "Gris";
            sut.AnnadirVarianteCommand.Execute(null);

            Assert.AreEqual(1, sut.Variantes.Count);
        }

        [TestMethod]
        public void Guardar_MandaLaPrincipalYLaListaCompleta()
        {
            var sut = CrearViewModel(out var servicio, out _);
            A.CallTo(() => servicio.LeerVariantes(CHECK)).Returns(new List<VarianteModel>
            {
                Variante(CHECK, "Negro", 1), Variante("45814", "Marrón", 2)
            });
            sut.ReferenciaBuscar = CHECK;

            sut.GuardarVariantesCommand.Execute(null);

            A.CallTo(() => servicio.GuardarVariantes(CHECK, A<List<VarianteModel>>.That.Matches(l =>
                    l.Count == 2 && l[0].Numero == CHECK && l[1].Numero == "45814")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void QuitarYBajar_ReordenanLaListaQueSeGuarda()
        {
            var sut = CrearViewModel(out var servicio, out _);
            A.CallTo(() => servicio.LeerVariantes(CHECK)).Returns(new List<VarianteModel>
            {
                Variante(CHECK, "Negro", 1), Variante("45814", "Marrón", 2), Variante("45815", "Gris", 3)
            });
            sut.ReferenciaBuscar = CHECK;

            sut.VarianteSeleccionada = sut.Variantes[0];
            sut.BajarVarianteCommand.Execute(null);
            sut.VarianteSeleccionada = sut.Variantes.Single(v => v.Numero == "45815");
            sut.QuitarVarianteCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { "45814", CHECK }, sut.Variantes.Select(v => v.Numero).ToArray());
        }
    }
}
