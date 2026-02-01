using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MacroGUI.Converters
{
    public sealed class PiStepDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            string type = GetString(value, "Type");
            if (string.Equals(type, "Tap", StringComparison.OrdinalIgnoreCase))
            {
                string key = GetString(value, "Key");
                return string.IsNullOrWhiteSpace(key) ? "Tap" : $"Tap {key}";
            }

            if (string.Equals(type, "Delay", StringComparison.OrdinalIgnoreCase))
            {
                int? min = GetInt(value, "MinMs");
                int? max = GetInt(value, "MaxMs");

                if (min.HasValue && max.HasValue)
                    return $"Delay {min.Value}~{max.Value}ms";
                if (min.HasValue)
                    return $"Delay {min.Value}ms";
                if (max.HasValue)
                    return $"Delay ~{max.Value}ms";

                int? dur = GetInt(value, "DurationMs");
                if (dur.HasValue)
                    return $"Delay {dur.Value}ms";

                return "Delay";
            }

            return string.IsNullOrWhiteSpace(type) ? value.ToString() ?? string.Empty : type;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;

        private static string GetString(object obj, string propName)
        {
            PropertyInfo pi = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object v = pi?.GetValue(obj);
            return v as string ?? string.Empty;
        }

        private static int? GetInt(object obj, string propName)
        {
            PropertyInfo pi = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            object v = pi?.GetValue(obj);
            return v as int?; // nullable int 그대로
        }
    }
}
