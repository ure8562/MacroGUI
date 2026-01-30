using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MacroGUI.ViewModels;

namespace MacroGUI.Services
{
    public static class MacroJsonCodec
    {
        public static string SerializeMacrosJson(ObservableCollection<MacroVM> macros)
        {
            if (macros == null)
                throw new ArgumentNullException(nameof(macros));

            PiMacrosRoot root = new PiMacrosRoot();
            root.Version = 1;
            root.Macros = new List<PiMacro>();

            foreach (MacroVM m in macros)
            {
                if (m == null)
                    continue;

                PiMacro macro = new PiMacro();
                macro.Name = m.Name ?? string.Empty;
                macro.Steps = new List<PiStep>();

                foreach (MacroStepVM s in m.Steps)
                {
                    if (s == null)
                        continue;

                    PiStep step = new PiStep();
                    step.Type = s.Type ?? string.Empty;
                    step.Key = s.Key ?? string.Empty;
                    step.DurationMs = s.DurationMs;
                    macro.Steps.Add(step);
                }

                root.Macros.Add(macro);
            }

            JsonSerializerOptions opt = new JsonSerializerOptions();
            opt.WriteIndented = true;
            opt.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            return JsonSerializer.Serialize(root, opt);
        }

        public static List<MacroVM> DeserializeMacros(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<MacroVM>();

            JsonSerializerOptions opt = new JsonSerializerOptions();
            opt.PropertyNameCaseInsensitive = true;

            PiMacrosRoot? root = JsonSerializer.Deserialize<PiMacrosRoot>(json, opt);
            if (root == null || root.Macros == null)
                return new List<MacroVM>();

            List<MacroVM> list = new List<MacroVM>();

            foreach (PiMacro m in root.Macros)
            {
                string name = m.Name ?? string.Empty;
                MacroVM vm = new MacroVM(name);

                if (m.Steps != null)
                {
                    foreach (PiStep s in m.Steps)
                    {
                        string type = s.Type ?? string.Empty;
                        string key = s.Key ?? string.Empty;
                        int durationMs = s.DurationMs;

                        vm.Steps.Add(new MacroStepVM(type, key, durationMs));
                    }
                }

                list.Add(vm);
            }

            return list;
        }

        private sealed class PiMacrosRoot
        {
            [JsonPropertyName("version")]
            public int Version { get; set; }

            [JsonPropertyName("macros")]
            public List<PiMacro>? Macros { get; set; }
        }

        private sealed class PiMacro
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("steps")]
            public List<PiStep>? Steps { get; set; }
        }

        private sealed class PiStep
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("key")]
            public string? Key { get; set; }

            [JsonPropertyName("durationMs")]
            public int DurationMs { get; set; }
        }
    }
}
