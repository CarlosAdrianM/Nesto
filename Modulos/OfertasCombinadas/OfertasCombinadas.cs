using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Services;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Nesto.Modulos.OfertasCombinadas.Views;

namespace Nesto.Modulos.OfertasCombinadas
{
    public class OfertasCombinadas : IModuloNesto, IOfertasCombinadas
    {
        public void Registrar(IRegistroNesto registro)
        {
            registro.Vista<OfertasCombinadasView>("OfertasCombinadasView");
            registro.Transitorio<IOfertasCombinadasService, OfertasCombinadasService>();

            // Nesto#490 (4C.5): lo que Unity construía sin registrar
            registro.Transitorio<OfertasCombinadasMenuBar>();
            registro.Transitorio<OfertasCombinadasMenuBarViewModel>();
            // Los ViewModels de sus vistas
            registro.Transitorio<OfertasCombinadasViewModel>();
        }

        public void Inicializar(IResolutorNesto resolutor)
        {
            var view = resolutor.Resolver<OfertasCombinadasMenuBar>();
            if (view != null)
            {
                resolutor.Resolver<IServicioCinta>().AnadirMenu(view);
            }
        }
    }

    public interface IOfertasCombinadas
    {
    }
}
