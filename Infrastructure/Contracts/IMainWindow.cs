using System.Windows.Controls.Ribbon;

namespace Nesto.Infrastructure.Contracts
{
    public interface IMainWindow
    {
        public Ribbon mainRibbon { get; set; }
    }
}
