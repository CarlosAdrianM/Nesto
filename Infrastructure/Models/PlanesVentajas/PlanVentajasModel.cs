using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nesto.Infrastructure.Models.PlanesVentajas
{
    public class PlanVentajasModel : INotifyPropertyChanged
    {
        private int _numero;
        public int Numero
        {
            get => _numero;
            set => Set(ref _numero, value);
        }

        private string _empresa;
        public string Empresa
        {
            get => _empresa;
            set => Set(ref _empresa, value);
        }

        private string _empresaNombre;
        public string EmpresaNombre
        {
            get => _empresaNombre;
            set => Set(ref _empresaNombre, value);
        }

        private DateTime _fechaInicio;
        public DateTime FechaInicio
        {
            get => _fechaInicio;
            set => Set(ref _fechaInicio, value);
        }

        private DateTime _fechaFin;
        public DateTime FechaFin
        {
            get => _fechaFin;
            set => Set(ref _fechaFin, value);
        }

        private decimal _importe;
        public decimal Importe
        {
            get => _importe;
            set => Set(ref _importe, value);
        }

        private string _familia;
        public string Familia
        {
            get => _familia;
            set => Set(ref _familia, value);
        }

        private int _estado;
        public int Estado
        {
            get => _estado;
            set => Set(ref _estado, value);
        }

        private string _estadoDescripcion;
        public string EstadoDescripcion
        {
            get => _estadoDescripcion;
            set => Set(ref _estadoDescripcion, value);
        }

        private string _comentarios;
        public string Comentarios
        {
            get => _comentarios;
            set => Set(ref _comentarios, value);
        }

        public List<string> Clientes { get; set; } = new List<string>();

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (!Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
