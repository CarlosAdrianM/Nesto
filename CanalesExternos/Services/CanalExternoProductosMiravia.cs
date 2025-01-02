using Iop.Api;
using Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Models;
using Nesto.Modulos.CanalesExternos.ApisExternas.Miravia.Services;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    public class CanalExternoProductosMiravia : ICanalExternoProductos
    {
        public string Nombre => "Miravia";

        public async Task ActualizarProducto(ProductoCanalExterno producto)
        {
            GetProduct();

            var credencial = MiraviaApiProductService.ConexionMiravia().Credential;
            IopClient client = new(credencial.Url, credencial.AppKey, credencial.AppSecret);
            IopRequest request = new IopRequest();
            request.SetApiName("/v2/product/createAndUpdate");

            string payload = CrearPayloadProducto(producto);
            request.AddApiParameter("payload", payload);

            IopResponse response = client.Execute(request, credencial.AccessToken);

            Console.WriteLine(response.IsError());
            Console.WriteLine(response.Body);
        }

        private string CrearPayloadProducto(ProductoCanalExterno producto)
        {
            var productRequest = new ProductRequest
            {
                //CategoryId = "62198631",
                ProductCategoryAttributeFields = new ProductCategoryAttributeFields
                {
                    Name = producto.Nombre ?? producto.ProductoCompleto.Nombre,
                    ShortDescription = producto.DescripcionBreve ?? producto.ProductoCompleto.Nombre,
                    Description = producto.DescripcionCompleta ?? producto.ProductoCompleto.Nombre,
                    Brand = producto.ProductoCompleto.Familia,
                    DoesThisProductHaveASafetyWarning = false,
                    //WarrantyType = "Local (Singapore) manufacturer warranty",
                    //MadeIn = "CHINA",
                    //DeliveryOptionEconomy = 1,
                    //DeliveryOptionSof = "true"
                },
                DefaultImages = new List<string>
                {
                    producto.ProductoCompleto.UrlFoto
                },
                SkuData = new List<SkuData>
                {
                    new SkuData
                    {
                        Quantity = producto.ProductoCompleto.Stock.ToString(), // solo el de ALG de momento
                        SkuImages = new List<string>
                        {
                            producto.ProductoCompleto.UrlFoto
                        },
                        Status = "ACTIVE",
                        SellerSku = producto.ProductoCompleto.Producto,
                        PackageWidth = "10",
                        PackageHeight = "20",
                        PackageLength = "10",
                        PackageWeight = "1",
                        SalePrice = Math.Round(producto.PvpIvaIncluido ?? producto.ProductoCompleto.PrecioPublicoFinal, 2, MidpointRounding.AwayFromZero),
                        Price = Math.Round(producto.PvpIvaIncluido ?? producto.ProductoCompleto.PrecioPublicoFinal, 2, MidpointRounding.AwayFromZero),
                        SkuCategoryAttributeFields = new SkuCategoryAttributeFields
                        {
                            //ColorFamily = "red",
                            //Size = "40",
                            EanCode = producto.ProductoCompleto.CodigoBarras,
                            ManufacturerInfo = "80518",
                            EUResponsible = "21"
                        }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(productRequest, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore // Omitir campos con valor null
            });

            return json;
        }

        private void GetProduct()
        {
            // Solo para pasar el test de integración de Miravia
            var credencial = MiraviaApiProductService.ConexionMiravia().Credential;
            IopClient client = new(credencial.Url, credencial.AppKey, credencial.AppSecret);
            IopRequest request = new IopRequest(); 
            request.SetApiName("/v2/product/get"); 
            request.SetHttpMethod("GET"); 
            //request.AddApiParameter("product_ids", "[41236]"); 
            request.AddApiParameter("seller_skus", "[\"41236\"]"); 
            //request.AddApiParameter("max_created_at", "1704884033000"); 
            request.AddApiParameter("page_size", "10"); 
            request.AddApiParameter("page", "1"); 
            //request.AddApiParameter("extraInfo_filter", "[\"quality_control_log\"]"); 
            request.AddApiParameter("status", "ALL"); 
            request.AddApiParameter("marketplace", "ae"); 
            //request.AddApiParameter("min_created_at", "1704884033000"); 
            IopResponse response = client.Execute(request, credencial.AccessToken); 
            Console.WriteLine(response.IsError()); 
            Console.WriteLine(response.Body);
        }
    }
}
