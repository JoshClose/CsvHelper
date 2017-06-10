using System;
using System.Text;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace CsvHelper.Converters
{
    // ReSharper disable InconsistentNaming - actual text of hex prefix is 0x
    public class ByteArrayConverter : ITypeConverter
    {
        private readonly Options options;
        private readonly string HexStringPrefix;
        private readonly byte ByteLength;

        //Defaults to the literal format used by C# for whole numbers, and SQL Server for binary data
        public ByteArrayConverter(Options options = Options.HexDecimal | Options.HexInclude0x)
        {
            this.options = options;
            ValidateSetting(options);
            HexStringPrefix = ( options & Options.HexDashes )
                == Options.HexDashes
                    ? "-"
                    : "" ;
            ByteLength = ( options & Options.HexDashes ) == Options.HexDashes ? (byte)3 : (byte)2;
        }

        private static void ValidateSetting(Options opt)
        {
            if( ( opt & Options.Base64 ) == Options.Base64 )
            {
                if( ( opt & ( Options.HexInclude0x | Options.HexDashes | Options.HexDecimal ) ) != Options.None )
                {
                    throw new CsvConfigurationException($"{nameof(ByteArrayConverter)} must be configured exclusively with HexDecimal options, or exclusively with Base64 options.  Was {opt.ToString()}")
                    {
                        Data = {{"options",opt}}
                    };
                }
            }
        }

        public string ConvertToString(object value, ICsvWriterRow row, CsvPropertyMapData propertyMapData)
        {
            if( ( options & Options.Base64 ) == Options.Base64 )
            {
                return Convert.ToBase64String( value as byte[] );
            }
            return ByteArrayToHexString(value as byte[]);
        }

        public object ConvertFromString(string text, ICsvReaderRow row, CsvPropertyMapData propertyMapData)
        {
            if( ( options & Options.Base64 ) == Options.Base64 )
            {
                return Convert.FromBase64String(text);
            }
            return HexStringToByteArray(text);
        }

        

        internal string ByteArrayToHexString(byte[] b)
        {
            var sb = new StringBuilder();
            if ((options & Options.HexInclude0x) == Options.HexInclude0x)
            {
                sb.Append("0x");
            }
            if( b.Length >= 1 )
            {
                sb.Append(b[0].ToString( "X2" ) );
            }
            for (var i = 1; i < b.Length; i++)
            {
                sb.Append(HexStringPrefix + b[i].ToString("X2"));
            }
            return sb.ToString();
        }

        internal byte[] HexStringToByteArray(string hex)
        {
            var has0x = hex.StartsWith("0x");

            byte[] ba = new byte[has0x ? (hex.Length - 1) / ByteLength : hex.Length + 1 / ByteLength];
            var has0xOffset = has0x ? 1 : 0;
            for (var stringIndex = has0xOffset * 2; stringIndex < hex.Length; stringIndex += ByteLength)
            {
                ba[(stringIndex - has0xOffset) /ByteLength ] = Convert.ToByte(hex.Substring(stringIndex, 2), 16);
            }
            return ba;
        }

        public bool CanConvertFrom(Type type)
        {
            return type == typeof(string);
        }

        public bool CanConvertTo(Type type)
        {
            return type == typeof(string);
        }
        [Flags]
        public enum Options
        {
            None = 0,
            //TypeOptions
            HexDecimal = 1,
            Base64 = 2,

            //HexFormattingOptions
            HexDashes = 4,
            HexInclude0x = 8,


        }
    }

    
}