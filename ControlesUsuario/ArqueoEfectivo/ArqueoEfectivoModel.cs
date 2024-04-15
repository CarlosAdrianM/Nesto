using Prism.Mvvm;
using System.Collections.Specialized;
using System.Linq;
using System.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Windows.Data;

namespace ControlesUsuario
{
    public class ArqueoEfectivoModel : BindableBase, IDataErrorInfo
    {
        

        public ArqueoEfectivoModel()
        {
            Cantidades = new()
            {
                // Añadimos los tipos de billetes y monedas legales
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 500),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 200),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 100),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 50),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 20),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 10),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Billete, 5),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, 2),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, 1),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, .5M),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, .2M),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, .1M),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, .05M),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, .02M),
                new RecuentoEfectivo(RecuentoEfectivo.TipoEfectivo.Moneda, .01M)
            };

            foreach (var item in Cantidades)
            {
                item.PropertyChanged += RecuentoEfectivo_PropertyChanged;
            }
        }

        public ObservableCollection<RecuentoEfectivo> Cantidades { get; }

        public decimal TotalArqueo => Cantidades.Sum(kv => kv.Valor * kv.Recuento);
        public decimal TotalBilletes => Cantidades
            .Where(kv => kv.Tipo == RecuentoEfectivo.TipoEfectivo.Billete)
            .Sum(kv => kv.Valor * kv.Recuento);
        public decimal TotalMonedas => Cantidades
            .Where(kv => kv.Tipo == RecuentoEfectivo.TipoEfectivo.Moneda)
            .Sum(kv => kv.Valor * kv.Recuento);


        public string this[string columnName]
        {
            get
            {
                if (columnName == "Cantidades")
                {
                    if (Cantidades.Any(c => c.Recuento < 0))
                    {
                        return "La cantidad tiene que ser positiva";
                    }
                }
                return null;
            }
        }

        public string Error => null;



        private void RecuentoEfectivo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(Cantidades));
            RaisePropertyChanged(nameof(TotalArqueo));
            RaisePropertyChanged(nameof(TotalBilletes));
            RaisePropertyChanged(nameof(TotalMonedas));
        }
    }

    public class RecuentoEfectivo : IComparable<RecuentoEfectivo>, INotifyPropertyChanged
    {
        public RecuentoEfectivo(TipoEfectivo tipo, decimal valor)
        {
            Tipo = tipo;
            Valor = valor;
        }
        public enum TipoEfectivo
        {
            Billete,
            Moneda
        }
        public TipoEfectivo Tipo { get; set; }
        public decimal Valor { get; set; }
        private int _recuento;
        public int Recuento { 
            get => _recuento; 
            set
            {
                if (_recuento != value)
                {
                    _recuento = value;
                    OnPropertyChanged(nameof(Recuento));
                    OnPropertyChanged(nameof(Total));
                }                
            } 
        }

        public decimal Total => Valor * Recuento;

        public event PropertyChangedEventHandler PropertyChanged;

        public int CompareTo(RecuentoEfectivo other)
        {
            // Comparación basada en el valor
            return Valor.CompareTo(other.Valor);
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
                return false;

            RecuentoEfectivo other = (RecuentoEfectivo)obj;
            return Valor == other.Valor;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Valor);
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
