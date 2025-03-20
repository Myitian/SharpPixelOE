using System.Globalization;
using System.Numerics;
using System.Windows.Data;

namespace Myitian.Converters;

public class BooleanNotConverter : IValueConverter
{
    public object? Convert(object values, Type targetType, object parameter, CultureInfo culture)
    {
        bool result = false;
        switch (values)
        {
            case null:
            case bool b when !b:
            case sbyte i8 when i8.Equals(0):
            case byte u8 when u8.Equals(0):
            case short i16 when i16.Equals(0):
            case ushort u16 when u16.Equals(0):
            case int i32 when i32.Equals(0):
            case uint u32 when u32.Equals(0):
            case long i64 when i64.Equals(0):
            case ulong u64 when u64.Equals(0):
            case nint ni when ni.Equals(0):
            case nuint nu when nu.Equals(0):
            case float fp32 when fp32.Equals(0):
            case double fp64 when fp64.Equals(0):
            case decimal dec when dec.Equals(0):
            case Half fp16 when fp16.Equals(Half.Zero):
            case Int128 i128 when i128.Equals(Int128.Zero):
            case UInt128 u128 when u128.Equals(UInt128.Zero):
            case BigInteger bi when bi.IsZero:
                result = true;
                break;
        }
        if (parameter is IValueConverter converter)
            return converter.Convert(result, null, null, culture);
        else if (parameter is IMultiValueConverter multiConverter)
            return multiConverter.Convert([result], null, null, culture);
        else
            return result;
    }

    public object ConvertBack(object value, Type targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
