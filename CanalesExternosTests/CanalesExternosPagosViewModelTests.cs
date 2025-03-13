using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos.Models;
using Nesto.Modulos.CanalesExternos.ViewModels;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


// Refactorización necesaria:
// 1. Crear un interfaz ICanalesExternosPagosService
// 2. Crear un interfaz que encapsule AmazonApiFinancesService y poner un campo de ese tipo en ICanalesExternosPagosService
// 3. Por inyección de dependencias, pasar un objeto de tipo ICanalesExternosPagosService al constructor de CanalesExternosPagosViewModel
// 4. Con esos cambios ya se pueden crear los tests

//namespace CanalesExternosTests
//{
//    [TestClass]
//    public class CanalesExternosPagosViewModelTests
//    {
//        /*
//        [TestMethod]
//        public void CanalesExternosPagosViewModel_CargarDetallePagoCommand_SiEsUnPagoEnDivisasSeCalculaElGastoPorCambioDeDivisa()
//        {
//            // Arrange
//            var _dialogService = A.Fake<IDialogService>();
//            var _vm = new CanalesExternosPagosViewModel(_dialogService);
//            _vm.PagoSeleccionado = new PagoCanalExterno();
//            _vm.PagoSeleccionado.MonedaOriginal = "GBP"; // cualquiera distinta a EUR
//            //A.CallTo()

//            // Act
//            _vm.CargarDetallePagoCommand.Execute(null);

//            // Assert

//        }
//        */
//    }
//}
