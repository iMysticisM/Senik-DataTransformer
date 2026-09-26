using System;
using System.Globalization;
using System.Windows.Data;

namespace Senik.DataTransformer.Converters
{
    public class MandatoryToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMandatory)
            {
                return isMandatory ? "اجباری *" : "اختیاری";
            }
            return "نامشخص";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}