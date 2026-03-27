using ControlesUsuario.Dialogs;
using Microsoft.VisualBasic;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using System.Windows.Input;

namespace Nesto.Modulos.CanalesExternos.ViewModels
{
    public class CanalesExternosPagosViewModel : BindableBase
    {
        private const string BANCO_AMAZON = "57200013";
        private const string CUENTA_COMISIONES = "62300023";
        private const string CUENTA_DIFERENCIA_NEGATIVA_CAMBIO = "66800001";
        private const string CUENTA_DIFERENCIA_POSITIVA_CAMBIO = "76800000";

        private IDialogService dialogService { get; }

        private readonly ICanalesExternosPagosService _servicio;
        private readonly IConfiguracion _configuracion;

        public CanalesExternosPagosViewModel(IDialogService dialogService, ICanalesExternosPagosService canalesExternosPagosService, IConfiguracion configuracion)
        {
            Titulo = "Canales Externos Pagos";
            this.dialogService = dialogService;
            _servicio = canalesExternosPagosService;
            _configuracion = configuracion;

            CargarPagosCommand = new DelegateCommand(OnCargarPagos);
            CargarDetallePagoCommand = new DelegateCommand(OnCargarDetallePago);
            ContabilizarPagoCommand = new DelegateCommand(OnContabilizarPago, CanContabilizarPago);
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado; set => SetProperty(ref _estaOcupado, value);
        }

        private DateTime _fechaDesde = DateTime.Today.AddMonths(-2);
        public DateTime FechaDesde
        {
            get => _fechaDesde; set => SetProperty(ref _fechaDesde, value);
        }

        private ObservableCollection<PagoCanalExterno> _listaPagos;
        public ObservableCollection<PagoCanalExterno> ListaPagos
        {
            get => _listaPagos; set => SetProperty(ref _listaPagos, value);
        }

        private int _numeroMaxGruposEventos = 40;
        public int NumeroMaxGruposEventos
        {
            get => _numeroMaxGruposEventos; set => SetProperty(ref _numeroMaxGruposEventos, value);
        }

        private PagoCanalExterno _pagoSeleccionado;
        public PagoCanalExterno PagoSeleccionado
        {
            get => _pagoSeleccionado;
            set
            {
                _ = SetProperty(ref _pagoSeleccionado, value);
                CargarDetallePagoCommand.Execute(null);
                ((DelegateCommand)ContabilizarPagoCommand).RaiseCanExecuteChanged();
            }
        }

        private string _titulo;
        public string Titulo
        {
            get => _titulo; set => SetProperty(ref _titulo, value);
        }

        public ICommand CargarDetallePagoCommand { get; private set; }
        private async void OnCargarDetallePago()
        {
            if (PagoSeleccionado == null)
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                await Task.Run(() =>
                {
                    CabeceraDetallePagoCanalExterno cabeceraPago = AmazonApiFinancesService.LeerFinancialEvents(PagoSeleccionado.PagoExternalId, NumeroMaxGruposEventos);
                    PagoSeleccionado.AjusteRetencion = cabeceraPago.AjusteRetencion;
                    PagoSeleccionado.RestoAjustes = cabeceraPago.RestoAjustes;
                    PagoSeleccionado.Comision = cabeceraPago.Comision;
                    PagoSeleccionado.Publicidad = cabeceraPago.Publicidad;
                    PagoSeleccionado.FacturaPublicidad = cabeceraPago.FacturaPublicidad;
                    PagoSeleccionado.DetallesPago = cabeceraPago.DetallePagos;
                    if (PagoSeleccionado.MonedaOriginal != Constantes.Empresas.MONEDA_CONTABILIDAD)
                    {
                        if (PagoSeleccionado.CambioDivisas == 1M)
                        {
                            PagoCanalExterno pagoCambio = ListaPagos.Where(p => p.FechaFinal > PagoSeleccionado.FechaFinal && p.CambioDivisas != 1M).OrderBy(p => p.FechaFinal).FirstOrDefault();
                            if (pagoCambio != null)
                            {
                                PagoSeleccionado.CambioDivisas = pagoCambio.CambioDivisas;
                            }
                            else
                            {
                                try
                                {
                                    PagoSeleccionado.CambioDivisas = AmazonApiOrdersService.CalculaDivisa(PagoSeleccionado.MonedaOriginal, Constantes.Empresas.MONEDA_CONTABILIDAD);
                                }
                                catch { }
                            }
                        }
                        PagoSeleccionado.Importe = Math.Round(PagoSeleccionado.ImporteOriginal * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.ImporteRecibidoBanco = PagoSeleccionado.Importe;
                        PagoSeleccionado.AjusteRetencion = Math.Round(PagoSeleccionado.AjusteRetencion * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.RestoAjustes = Math.Round(PagoSeleccionado.RestoAjustes * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.Comision = Math.Round(PagoSeleccionado.Comision * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        PagoSeleccionado.Publicidad = Math.Round(PagoSeleccionado.Publicidad * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        foreach (DetallePagoCanalExterno detalle in PagoSeleccionado.DetallesPago)
                        {
                            detalle.Comisiones = Math.Round(detalle.Comisiones * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                            detalle.Importe = Math.Round(detalle.Importe * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                            detalle.Promociones = Math.Round(detalle.Promociones * PagoSeleccionado.CambioDivisas, 2, MidpointRounding.AwayFromZero);
                        }
                    }
                });

                RaisePropertyChanged(nameof(PagoSeleccionado));
            }
            catch (Exception ex)
            {
                dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }

        }

        public ICommand CargarPagosCommand { get; private set; }
        private async void OnCargarPagos()
        {
            try
            {
                EstaOcupado = true;
                var listaProvisional = AmazonApiFinancesService.LeerFinancialEventGroups(FechaDesde, NumeroMaxGruposEventos);
                ListaPagos = await _servicio.BuscarAsientos(listaProvisional);
            }
            catch (Exception ex)
            {
                dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }

        }

        public ICommand ContabilizarPagoCommand { get; private set; }
        private bool CanContabilizarPago()
        {
            return PagoSeleccionado != null;
        }
        private async void OnContabilizarPago()
        {
            bool continuar = dialogService.ShowConfirmationAnswer("Contabilizar pago", "¿Desea contabilizar el pago?");

            if (!continuar)
            {
                return;
            }

            EstaOcupado = true;
            const int TIEMPO_ESPERA = 360;
            TransactionOptions transactionOptions = new()
            {
                Timeout = TimeSpan.FromSeconds(TIEMPO_ESPERA + 10) //para asegurarnos que la transacción se deshace aunque prdContabilizar tarde mucho
            };
            using TransactionScope scope = new(TransactionScopeOption.Required, transactionOptions);
            using NestoEntities db = new();
            db.Database.CommandTimeout = TIEMPO_ESPERA;

            var apuntes = GenerarApuntesContables(PagoSeleccionado);
            foreach (var apunte in apuntes)
            {
                _ = db.PreContabilidad.Add(apunte);
            }

            try
            {
                await Task.Run(() =>
                {
                    _ = db.SaveChanges();
                    _ = db.prdContabilizar(Constantes.Empresas.EMPRESA_DEFECTO, Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS, _configuracion.usuario);
                    CargarPagosCommand.Execute(null);
                });
            }
            catch (Exception ex)
            {
                scope.Dispose();
                EstaOcupado = false;
                dialogService.ShowError(DbValidationErrorHelper.ExtraerMensajeError(ex));
                return;
            }


            scope.Complete();

            // Buscamos los nº de orden y liquidamos 
            // A desarrollar cuando ya todo lo pendiente del proveedor hecho automático

            EstaOcupado = false;
            dialogService.ShowNotification("Pago contabilizado", "Se ha contabilizado correctamente el pago");

        }

        internal static List<PreContabilidad> GenerarApuntesContables(PagoCanalExterno pago)
        {
            var apuntes = new List<PreContabilidad>();
            string nombreMarket = pago.DetallesPago.Any()
                ? DatosMarkets.Buscar(DatosMarkets.Mercados.Single(m => m.CuentaContablePago == pago.DetallesPago.First().CuentaContablePago).Id).NombreMarket
                : "sin detalle";
            string documento = "AMZ" + pago.FechaPago.ToString("ddMMyy");
            DateTime fechaPago = new(pago.FechaPago.Year, pago.FechaPago.Month, pago.FechaPago.Day);

            // Asiento 1: Pago (Proveedor + Banco + Diferencia cambio)
            if (pago.Importe != 0)
            {
                apuntes.Add(new PreContabilidad
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                    Asiento = 1,
                    Fecha = fechaPago,
                    FechaVto = fechaPago,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                    Nº_Cuenta = Constantes.Proveedores.Especiales.PROVEEDOR_AMAZON,
                    Contacto = Constantes.Proveedores.Especiales.CONTACTO_PROVEEDOR_AMAZON,
                    Concepto = string.Format("Pago {0}", nombreMarket),
                    Haber = pago.Importe,
                    Nº_Documento = documento,
                    NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                    Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                });

                decimal importeBanco = pago.ImporteRecibidoBanco != 0
                    ? pago.ImporteRecibidoBanco
                    : pago.Importe;
                apuntes.Add(new PreContabilidad
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                    Asiento = 1,
                    Fecha = fechaPago,
                    FechaVto = fechaPago,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                    Nº_Cuenta = BANCO_AMAZON,
                    Concepto = string.Format("Pago {0}", nombreMarket),
                    Debe = importeBanco,
                    Nº_Documento = documento,
                    NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                    Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                });

                decimal diferenciaCambio = importeBanco - pago.Importe;
                if (diferenciaCambio != 0)
                {
                    apuntes.Add(new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 1,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Nº_Cuenta = diferenciaCambio > 0 ? CUENTA_DIFERENCIA_POSITIVA_CAMBIO : CUENTA_DIFERENCIA_NEGATIVA_CAMBIO,
                        Concepto = string.Format("Dif. cambio {0}", nombreMarket),
                        Debe = diferenciaCambio < 0 ? -diferenciaCambio : 0,
                        Haber = diferenciaCambio > 0 ? diferenciaCambio : 0,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    });
                }
            }

            // Asiento 2: Liquidación pagos
            if (pago.TotalDetallePagos != 0)
            {
                apuntes.Add(new PreContabilidad
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                    Asiento = 2,
                    Fecha = fechaPago,
                    FechaVto = fechaPago,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                    Nº_Cuenta = Constantes.Proveedores.Especiales.PROVEEDOR_AMAZON,
                    Contacto = Constantes.Proveedores.Especiales.CONTACTO_PROVEEDOR_AMAZON,
                    Concepto = string.Format("Liq. Pagos {0}. Retenido {1}", nombreMarket, (-pago.AjusteRetencion).ToString("C")),
                    Debe = pago.TotalDetallePagos,
                    Nº_Documento = documento,
                    NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                    Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                });
            }

            // Asiento 3: Gastos (proveedor)
            if (-pago.TotalDetalleComisiones - pago.Comision - pago.TotalDetallePromociones - pago.Publicidad != 0)
            {
                apuntes.Add(new PreContabilidad
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                    Asiento = 3,
                    Fecha = fechaPago,
                    FechaVto = fechaPago,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                    Nº_Cuenta = Constantes.Proveedores.Especiales.PROVEEDOR_AMAZON,
                    Contacto = Constantes.Proveedores.Especiales.CONTACTO_PROVEEDOR_AMAZON,
                    Concepto = string.Format("Gastos {0}", nombreMarket),
                    Haber = -pago.TotalDetalleComisiones - pago.Comision - pago.TotalDetallePromociones - pago.Publicidad,
                    Nº_Documento = documento,
                    NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                    Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                });
            }

            if (pago.Comision != 0)
            {
                apuntes.Add(new PreContabilidad
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                    Asiento = 3,
                    Fecha = fechaPago,
                    FechaVto = fechaPago,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                    Nº_Cuenta = CUENTA_COMISIONES,
                    Concepto = string.Format("Comisiones Cabecera {0}", nombreMarket),
                    Debe = -pago.Comision,
                    Nº_Documento = documento,
                    NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                    Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO,
                    CentroCoste = "CA", // Esto hay que meejorarlo
                    Departamento = "ADM"
                });
            }

            if (pago.Publicidad != 0)
            {
                apuntes.Add(new PreContabilidad
                {
                    Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                    Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                    Asiento = 3,
                    Fecha = fechaPago,
                    FechaVto = fechaPago,
                    TipoApunte = Constantes.TiposApunte.PAGO,
                    TipoCuenta = Constantes.TiposCuenta.PROVEEDOR,
                    Nº_Cuenta = Constantes.Proveedores.Especiales.PROVEEDOR_AMAZON,
                    Contacto = Constantes.Proveedores.Especiales.CONTACTO_PROVEEDOR_AMAZON,
                    Concepto = string.Format("Publicidad {0} {1}", nombreMarket, pago.FacturaPublicidad),
                    Debe = -pago.Publicidad,
                    Nº_Documento = documento,
                    NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                    Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                    FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                });
            }

            // Detalle por pedido
            foreach (DetallePagoCanalExterno detalle in pago.DetallesPago)
            {
                if (detalle.Importe != 0)
                {
                    apuntes.Add(new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 2,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Nº_Cuenta = detalle.CuentaContablePago,
                        Concepto = string.Format("Liq. Pago Pedido {0}", detalle.ExternalId),
                        Haber = detalle.Importe,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    });
                }

                if (detalle.Comisiones != 0)
                {
                    apuntes.Add(new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 3,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Nº_Cuenta = pago.DetallesPago.First().CuentaContableComisiones,
                        Concepto = string.Format("Liq. Comisiones {0}", detalle.ExternalId),
                        Debe = -detalle.Comisiones,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    });
                }

                if (detalle.Promociones != 0)
                {
                    apuntes.Add(new PreContabilidad
                    {
                        Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
                        Diario = Constantes.DiariosContables.DIARIO_PAGO_REEMBOLSOS,
                        Asiento = 3,
                        Fecha = fechaPago,
                        FechaVto = fechaPago,
                        TipoApunte = Constantes.TiposApunte.PAGO,
                        TipoCuenta = Constantes.TiposCuenta.CUENTA_CONTABLE,
                        Nº_Cuenta = pago.DetallesPago.First().CuentaContableComisiones,
                        Concepto = string.Format("Liq. Promociones {0}", detalle.ExternalId),
                        Debe = -detalle.Promociones,
                        Nº_Documento = documento,
                        NºDocumentoProv = Strings.Right(pago.PagoExternalId, 20),
                        Delegación = Constantes.Empresas.DELEGACION_DEFECTO,
                        FormaVenta = Constantes.Empresas.FORMA_VENTA_DEFECTO
                    });
                }
            }

            return apuntes;
        }
    }
}
