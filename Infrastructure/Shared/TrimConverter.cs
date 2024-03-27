using System;
using Newtonsoft.Json;

namespace Nesto.Infrastructure.Shared
{
    public class TrimConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            // Este convertidor se aplicará a objetos de tipo string
            return objectType == typeof(string);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Se lee el valor original del campo
            var value = reader.Value?.ToString();

            // Si el valor no es nulo, se le aplica Trim()
            if (!string.IsNullOrEmpty(value))
            {
                value = value.Trim();
            }

            return value;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Se escribe el valor tal como está, ya que este convertidor solo se aplica durante la deserialización
            writer.WriteValue(value);
        }
    }

}
