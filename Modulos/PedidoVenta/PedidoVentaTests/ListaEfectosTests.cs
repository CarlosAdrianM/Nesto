using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;
using System.Linq;

namespace PedidoVentaTests
{
    [TestClass]
    public class ListaEfectosTests
    {
        [TestMethod]
        public void ListaEfectos_SiSoloHayUnEfectoYCambiaElImporte_ModificaElImporteDelEfecto()
        {
            var lista = new ListaEfectos(1);

            lista.ImporteTotal = 2;

            Assert.AreEqual(2, lista.Single().Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlAnnadirUnEfecto_LePoneImporteTotalEntreNumeroDeEfectos()
        {
            var lista = new ListaEfectos(10);
            
            lista.AnnadirEfectoCommand.Execute();

            Assert.AreEqual(5, lista.Last().Importe);
            Assert.AreEqual(5, lista.First().Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlBorrarUnEfecto_LePoneImporteQueFaltaAlUltimo()
        {
            var lista = new ListaEfectos(10);
            lista.AnnadirEfectoCommand.Execute();

            lista.BorrarEfectoCommand.Execute(lista.First());

            Assert.AreEqual(10, lista.Last().Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlModificarElUltimoUnEfecto_ModificaElPrimerEfectoParaQueSigaSumandoElTotal()
        {
            var lista = new ListaEfectos(10);
            lista.AnnadirEfectoCommand.Execute();

            lista.Last().Importe = 6;

            Assert.AreEqual(4, lista.First().Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlModificarUnEfecto_ModificaElUltimoEfectoParaQueSigaSumandoElTotal()
        {
            var lista = new ListaEfectos(10);
            lista.AnnadirEfectoCommand.Execute();

            lista.First().Importe = 6;

            Assert.AreEqual(4, lista.Last().Importe);
        }


        [TestMethod]
        public void ListaEfectos_SiCambiaElImporteYNoHayNingunEfecto_AnnadeUno()
        {
            var lista = new ListaEfectos();

            lista.ImporteTotal = 2;

            Assert.AreEqual(1, lista.Count);
        }

        [TestMethod]
        public void ListaEfectos_SiCambiaElImporteYSoloHayUnEfecto_CambiaElImporteDelEfecto()
        {
            var lista = new ListaEfectos(1);

            lista.ImporteTotal = 2;

            Assert.AreEqual(2, lista.Single().Importe);
        }

        [TestMethod]
        public void ListaEfectos_SiCambiaElImporteYHayVariosEfectos_CambiaElImporteDelUltimoEfecto()
        {
            var lista = new ListaEfectos(10);
            lista.AnnadirEfectoCommand.Execute();

            lista.ImporteTotal = 20;

            Assert.AreEqual(5, lista.First().Importe);
            Assert.AreEqual(15, lista.Last().Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlAnnadirUnEfecto_SiTodosLosEfectosTienenElMismoImporteDespuesDeAnnadirloSiguenTeniendoTodosElMismoImporte()
        {
            var lista = new ListaEfectos(10);
            lista.AnnadirEfectoCommand.Execute();

            lista.AnnadirEfectoCommand.Execute();

            Assert.AreEqual(3.33M, lista.ElementAt(0).Importe);
            Assert.AreEqual(3.33M, lista.ElementAt(1).Importe);
            Assert.AreEqual(3.34M, lista.ElementAt(2).Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlAnnadirUnEfecto_SiElUltimoEfectoEsDiferentePorUnCentimoDeCuadreTambienConsideraQueTodosSonIguales()
        {
            var lista = new ListaEfectos(10);
            lista.AnnadirEfectoCommand.Execute();
            lista.AnnadirEfectoCommand.Execute(); // Aquí el último es de 3.34M y los demás de 3.33M

            lista.AnnadirEfectoCommand.Execute(); 

            Assert.AreEqual(2.50M, lista.ElementAt(0).Importe);
            Assert.AreEqual(2.50M, lista.ElementAt(1).Importe);
            Assert.AreEqual(2.50M, lista.ElementAt(2).Importe);
            Assert.AreEqual(2.50M, lista.ElementAt(3).Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlCopiarOtraLista_SiElImporteTotalEsCeroNoHaceElCuadre()
        {
            List<Efecto> lista = new()
            {
                new Efecto { Importe = 1 },
                new Efecto { Importe = 2 }
            };
            ListaEfectos listaCopiada = new();

            lista.ToList().ForEach(listaCopiada.Add);

            Assert.AreEqual(1, listaCopiada.First().Importe);
            Assert.AreEqual(2, listaCopiada.Last().Importe);
        }

        [TestMethod]
        public void ListaEfectos_AlCopiarOtraLista_NoAnnadeElRegistroInicial()
        {
            List<Efecto> lista = new()
            {
                new Efecto { Importe = 1 },
                new Efecto { Importe = 2 }
            };
            ListaEfectos listaCopiada = new();

            lista.ToList().ForEach(listaCopiada.Add);

            Assert.AreEqual(2, listaCopiada.Count);
        }


    }
}
