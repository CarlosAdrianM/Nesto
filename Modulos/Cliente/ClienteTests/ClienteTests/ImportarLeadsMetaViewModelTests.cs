using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>NestoAPI#623 corte 4: «Importar leads de Meta» (ensayo y luego de verdad). Datos inventados.</summary>
    [TestClass]
    public class ImportarLeadsMetaViewModelTests
    {
        private const string META = "META-HARMONIA-0926";

        private readonly IOrigenesClienteService origenes = A.Fake<IOrigenesClienteService>();
        private readonly IImportarLeadsMetaService servicio = A.Fake<IImportarLeadsMetaService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly byte[] fichero = new byte[] { 1, 2, 3 };

        public ImportarLeadsMetaViewModelTests()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Returns(new List<OrigenClienteModel>
            {
                new OrigenClienteModel { Codigo = "OTRO", Descripcion = "Otro", Activo = true },
                new OrigenClienteModel { Codigo = META, Descripcion = "Lead de Meta", Campana = "Harmonia", Activo = true }
            });
            A.CallTo(() => servicio.Importar(A<string>._, true, A<byte[]>._, A<string>._)).Returns(Ensayo());
            A.CallTo(() => servicio.Importar(A<string>._, false, A<byte[]>._, A<string>._)).Returns(new ResultadoImportacionLeadsModel
            {
                Ensayo = false, Leidos = 2, Creados = 1, YaExistian = 1,
                Leads = new List<LeadImportadoModel>
                {
                    new LeadImportadoModel { Fila = 2, Lead = "1", Codigo = "CREADO", Accion = "Creado", Cliente = "99001" },
                    new LeadImportadoModel { Fila = 3, Lead = "2", Codigo = "YA_EXISTIA", Accion = "Ya existía", Cliente = "15000" }
                }
            });
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
        }

        private static ResultadoImportacionLeadsModel Ensayo() => new ResultadoImportacionLeadsModel
        {
            Ensayo = true, Leidos = 2, Creados = 1, YaExistian = 1,
            Leads = new List<LeadImportadoModel>
            {
                new LeadImportadoModel { Fila = 2, Lead = "1", Nombre = "Ana Inventada", Codigo = "CREAR", Accion = "Se creará" },
                new LeadImportadoModel { Fila = 3, Lead = "2", Codigo = "YA_EXISTIA", Accion = "Ya existía", Cliente = "15000" }
            }
        };

        private async Task<ImportarLeadsMetaViewModel> Abierto(string ruta = @"C:\x\leads.xml")
        {
            var vm = new ImportarLeadsMetaViewModel(origenes, servicio, dialogos)
            {
                ElegirFichero = () => ruta,
                LeerFichero = _ => fichero
            };
            await vm.CargarAsync();
            return vm;
        }

        [TestMethod]
        public async Task AlAbrir_SoloOfreceOrigenesDeCampana()
        {
            // NestoAPI#848: los orígenes de canal (web, Amazon, tiendas, cursos) no son leads: la API los rechaza al importar.
            A.CallTo(() => origenes.LeerOrigenes()).Returns(new List<OrigenClienteModel>
            {
                new OrigenClienteModel { Codigo = "TIENDA-REINA", Descripcion = "Tienda de Reina", Activo = true, EsLead = false, PropuestoParaElUsuario = true },
                new OrigenClienteModel { Codigo = META, Descripcion = "Lead de Meta", Campana = "Harmonia", Activo = true, EsLead = true },
                new OrigenClienteModel { Codigo = "ANTIGUO", Descripcion = "API sin EsLead", Activo = true }
            });

            ImportarLeadsMetaViewModel vm = await Abierto();

            CollectionAssert.AreEqual(new[] { META, "ANTIGUO" }, vm.Origenes.ConvertAll(o => o.Codigo));
            Assert.AreEqual(META, vm.OrigenSeleccionado.Codigo);
        }

        [TestMethod]
        public async Task AlAbrir_EligeElOrigenDeHarmoniaPorDefecto()
        {
            ImportarLeadsMetaViewModel vm = await Abierto();

            Assert.AreEqual(META, vm.OrigenSeleccionado.Codigo);
            Assert.IsFalse(vm.PuedeImportar, "sin fichero ni ensayo no se puede importar");
        }

        [TestMethod]
        public async Task AlElegirElFichero_HaceElEnsayoYEnseñaLosLeads()
        {
            ImportarLeadsMetaViewModel vm = await Abierto();

            await vm.ElegirFicheroAsync();

            A.CallTo(() => servicio.Importar(META, true, fichero, @"C:\x\leads.xml")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Importar(A<string>._, false, A<byte[]>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(2, vm.Leads.Count);
            Assert.AreEqual("leads.xml", vm.NombreFichero);
            StringAssert.Contains(vm.Resumen, "Ensayo");
            StringAssert.Contains(vm.Resumen, "No se ha grabado nada");
            Assert.IsTrue(vm.PuedeImportar);
        }

        [TestMethod]
        public async Task Cancelar_ElDialogo_NoHaceNada()
        {
            ImportarLeadsMetaViewModel vm = await Abierto(ruta: null);

            await vm.ElegirFicheroAsync();

            A.CallTo(() => servicio.Importar(A<string>._, A<bool>._, A<byte[]>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Importar_TrasConfirmar_LoHaceDeVerdadYEnseñaLosNumerosDeCliente()
        {
            ImportarLeadsMetaViewModel vm = await Abierto();
            await vm.ElegirFicheroAsync();

            await vm.ImportarAsync();

            A.CallTo(() => servicio.Importar(META, false, fichero, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("99001", vm.Leads[0].Cliente);
            StringAssert.Contains(vm.Resumen, "Importado");
            Assert.IsFalse(vm.PuedeImportar, "ya importado: hay que volver a ensayar");
        }

        [TestMethod]
        public async Task Importar_SinConfirmar_NoGraba()
        {
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);
            ImportarLeadsMetaViewModel vm = await Abierto();
            await vm.ElegirFicheroAsync();

            await vm.ImportarAsync();

            A.CallTo(() => servicio.Importar(A<string>._, false, A<byte[]>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CambiarDeOrigen_ObligaAEnsayarOtraVez()
        {
            ImportarLeadsMetaViewModel vm = await Abierto();
            await vm.ElegirFicheroAsync();

            vm.OrigenSeleccionado = vm.Origenes[0];

            Assert.IsFalse(vm.PuedeImportar);
            Assert.AreEqual(0, vm.Leads.Count);
        }

        [TestMethod]
        public async Task ErrorDeLaApi_SeEnseña()
        {
            A.CallTo(() => servicio.Importar(A<string>._, true, A<byte[]>._, A<string>._)).Throws(new System.Exception("El origen no está activo."));
            ImportarLeadsMetaViewModel vm = await Abierto();

            await vm.ElegirFicheroAsync();

            A.CallTo(() => dialogos.ShowError("El origen no está activo.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public void PuedeVer_LosGruposDelEndpoint()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            Assert.IsFalse(ImportarLeadsMetaViewModel.PuedeVer(configuracion));

            A.CallTo(() => configuracion.UsuarioEnGrupo("Vendedores")).Returns(true);
            Assert.IsTrue(ImportarLeadsMetaViewModel.PuedeVer(configuracion));
        }

        [TestMethod]
        public void Url_LlevaElOrigenYElEnsayo()
        {
            Assert.AreEqual("Clientes/Origenes/ImportarLeads?origen=META-HARMONIA-0926&ensayo=true", ImportarLeadsMetaService.Url(META, true));
        }
    }
}
