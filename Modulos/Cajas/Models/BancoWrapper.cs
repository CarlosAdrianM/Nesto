using Prism.Mvvm;

namespace Nesto.Modulos.Cajas.Models
{
    internal class BancoWrapper : BindableBase
    {
        public BancoWrapper(BancoDTO bancoDTO)
        {
            Model = bancoDTO;
        }
        public BancoDTO Model { get; }
        public string Empresa => Model.Empresa;
        public string Codigo
        {
            get => Model.Codigo;
            set => Model.Codigo = value;
        }
        public string Nombre
        {
            get => Model.Nombre;
            set => Model.Nombre = value;
        }
        public string CuentaContable => Model.CuentaContable;
        public string Entidad => Model.Entidad;
        public string Oficina => Model.Oficina;
        public string NumeroCuenta => Model.NumeroCuenta;
    }
}
