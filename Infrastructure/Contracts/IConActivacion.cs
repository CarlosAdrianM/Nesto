namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.4, región propia): una vista (o su DataContext) que quiere saber si es la activa de su región: en
    /// MainRegion, si su pestaña es la seleccionada. Sustituye a <c>Prism.IActiveAware</c>. Lo implementa
    /// <c>ListaRapportsViewModel</c> (se da de alta en los mensajes de rapports solo mientras su pestaña está activa).
    ///
    /// Lo hace cumplir la región propia (<c>Nesto.Infrastructure.Navegacion.RegionNesto</c>) igual que Prism hacía con
    /// <c>IActiveAware</c>: <see cref="IsActive"/> pasa a true al activar la vista y a false al desactivarla o quitarla,
    /// a la vista y a su DataContext, a cada uno si lo implementa. Mismo nombre de propiedad que en Prism.
    /// </summary>
    public interface IConActivacion
    {
        bool IsActive { get; set; }
    }
}
