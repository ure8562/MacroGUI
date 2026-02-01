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
                macro.Memo = m.Memo ?? string.Empty;

                macro.Trigger = new PiTrigger();
                macro.Trigger.Keys = new List<string>();

                foreach (string k in m.TriggerKeys)
                {
                    if (string.IsNullOrWhiteSpace(k))
                        continue;

                    macro.Trigger.Keys.Add(k.Trim());
                }

                macro.Steps = new List<PiStep>();

                foreach (MacroStepVM s in m.Steps)
                {
                    if (s == null)
                        continue;

                    PiStep step = new PiStep();
                    step.Type = s.Type ?? string.Empty;

                    if (step.Type.Equals("TAP", StringComparison.OrdinalIgnoreCase))
                    {
                        step.Key = s.Key ?? string.Empty;
                        step.DurationMs = null;   // ❗ 아예 안 씀
                    }
                    else if (step.Type.Equals("DELAY", StringComparison.OrdinalIgnoreCase))
                    {
                        // Delay는 key가 필요 없으니 비움 (null이면 위 IgnoreCondition으로 출력 안 됨)
                        step.Key = null;

                        int minMs = s.MinMs;
                        int maxMs = s.MaxMs;

                        // ✅ min/max가 있으면 range 저장
                        if (minMs > 0 || maxMs > 0)
                        {
                            step.MinMs = minMs;
                            step.MaxMs = maxMs;
                            step.DurationMs = null;
                        }
                        else
                        {
                            // ✅ range 없으면 duration 저장
                            step.DurationMs = s.DurationMs;
                            step.MinMs = null;
                            step.MaxMs = null;
                        }
                    }
                    macro.Steps.Add(step);
                }

                root.Macros.Add(macro);
            }

            JsonSerializerOptions opt = new JsonSerializerOptions();
            opt.WriteIndented = true;
            opt.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            opt.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;

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
                vm.Memo = m.Memo ?? string.Empty;
                vm.TriggerKeys.Clear();

                if (m.Trigger != null && m.Trigger.Keys != null)
                {
                    foreach (string k in m.Trigger.Keys)
                    {
                        if (string.IsNullOrWhiteSpace(k))
                            continue;

                        vm.TriggerKeys.Add(k.Trim());
                    }
                }

                if (m.Steps != null)
                {
                    foreach (PiStep s in m.Steps)
                    {
                        string type = s.Type ?? string.Empty;
                        string key = s.Key ?? string.Empty;

                        // DELAY
                        if (string.Equals(type, "DELAY", StringComparison.OrdinalIgnoreCase))
                        {
                            int minMs = s.MinMs ?? 0;
                            int maxMs = s.MaxMs ?? 0;
                            int durationMs = s.DurationMs ?? 0;

                            if (string.IsNullOrWhiteSpace(key))
                                key = "-";

                            // ✅ min/max가 있으면 범위 생성자 사용 → ToString에서 50~130ms 출력
                            if (minMs > 0 || maxMs > 0)
                                vm.Steps.Add(new MacroStepVM(type, key, 0, minMs, maxMs));
                            else
                                vm.Steps.Add(new MacroStepVM(type, key, durationMs));

                            continue;
                        }

                        // TAP / KEYDOWN / KEYUP etc.
                        vm.Steps.Add(new MacroStepVM(type, key, 0));
                    }
                }

                list.Add(vm);
            }

            return list;
        }

        private sealed class PiTrigger
        {
            [JsonPropertyName("keys")]
            public List<string>? Keys { get; set; }
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

            [JsonPropertyName("memo")]
            public string? Memo { get; set; }   // ✅ 추가

            // ✅ 추가
            [JsonPropertyName("trigger")]
            public PiTrigger? Trigger { get; set; }

            [JsonPropertyName("steps")]
            public List<PiStep>? Steps { get; set; }
        }

        internal sealed class PiStep
        {
            [JsonPropertyName("type")]
            public string? Type { get; set; }

            [JsonPropertyName("key")]
            public string? Key { get; set; }

            [JsonPropertyName("durationMs")]
            public int? DurationMs { get; set; }   // 🔴 int → int?

            [JsonPropertyName("minMs")]
            public int? MinMs { get; set; }        // ✅ 추가

            [JsonPropertyName("maxMs")]
            public int? MaxMs { get; set; }        // ✅ 추가
        }
    }
}
