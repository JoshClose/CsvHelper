using System;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace ACN.Csv.Converters
{
    public class ByteArrayConverter : ITypeConverter
    {
        private readonly ByteArrayConverterOptions options;
        private readonly string HexFormatString;

        public ByteArrayConverter(ByteArrayConverterOptions options)
        {
            this.options = options;
            HexFormatString = ( options & ByteArrayConverterOptions.HexDashes )
                == ByteArrayConverterOptions.HexDashes
                    ? "-X2"
                    : "X2" ;
        }

        public bool CanConvertFrom(Type type)
        {
            return type == typeof(string);
        }

        public bool CanConvertTo(Type type)
        {
            return type == typeof(string);
        }

        public string ConvertToString(object value, ICsvWriterRow row, CsvPropertyMapData propertyMapData)
        {
            return ByteArrayToHexString(value as byte[]);
        }

        public object ConvertFromString(string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData)
        {
            return HexStringToByteArray(text);
        }
        // ReSharper disable once InconsistentNaming - actual text of hex prefix is 0x
        public string ByteArrayToHexString(byte[] b)
        {
            var sb = new StringBuilder();
            if ((options & ByteArrayConverterOptions.HexInclude0x) == ByteArrayConverterOptions.HexInclude0x)
            {
                sb.Append("0x");
            }
            
            for (var i = 0; i < b.Length; i++)
            {
                sb.Append(b[0].ToString(HexFormatString));
            }
            return sb.ToString();
        }

        public static byte[] HexStringToByteArray(string hex)
        {
            var has0x = hex.StartsWith("0x");
            byte[] ba = new byte[has0x ? (hex.Length - 2) / 2 : hex.Length / 2];
            for (var i = has0x ? 2 : 0; i < hex.Length / 2; i++)
            {
                ba[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return ba;
        }
    }

    [Flags]
    public enum ByteArrayConverterOptions
    {
        //TypeOptions
        HexDecimal = 0,
        Base64 = 1,

        //HexFormattingOptions
        HexDashes = 2,
        HexInclude0x = 4,


    }
}