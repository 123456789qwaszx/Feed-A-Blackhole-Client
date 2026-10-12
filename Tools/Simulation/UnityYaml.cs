using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace BlackHole.Simulation.Tool
{
    // Unity가 쓰는 에셋 YAML(.asset)의 좁은 부분만 읽는다: 블록 매핑, "- "로 시작하는 목록(Unity처럼 부모 키와 같은 들여쓰기), 한 줄 값.
    // {fileID: …, guid: …} 같은 한 줄 묶음은 문자열 그대로 두고 Guid로 꺼낸다. 여러 줄 문자열·앵커는 읽지 않는다(이 게임의 데이터 에셋에는 없다).
    internal sealed class YamlNode
    {
        public string Scalar;
        public Dictionary<string, YamlNode> Map;
        public List<YamlNode> List;

        public YamlNode this[string key] => Map != null && Map.TryGetValue(key, out YamlNode node) ? node : null;

        public IReadOnlyList<YamlNode> Items => List ?? (IReadOnlyList<YamlNode>)Array.Empty<YamlNode>();

        public string Text(string key) => this[key]?.Scalar;

        public float Float(string key) =>
            float.TryParse(Text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0;

        public int Int(string key) =>
            int.TryParse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        public long Long(string key) =>
            long.TryParse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value) ? value : 0;

        // {fileID: 11400000, guid: …, type: 2}의 guid. 비었거나({fileID: 0}) 없으면 null.
        public string Guid(string key)
        {
            Match match = Regex.Match(Text(key) ?? "", @"guid:\s*([0-9a-fA-F]{32})");
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
        }
    }

    internal static class UnityYaml
    {
        private readonly struct Line
        {
            public readonly int Indent;
            public readonly string Text;

            public Line(int indent, string text)
            {
                Indent = indent;
                Text = text;
            }
        }

        // ScriptableObject 에셋의 본문(MonoBehaviour: 아래 매핑).
        public static YamlNode ReadAsset(string path)
        {
            var lines = new List<Line>();
            bool body = false;

            foreach (string raw in File.ReadAllLines(path))
            {
                if (!body)
                {
                    body = raw.TrimEnd() == "MonoBehaviour:";
                    continue;
                }

                if (raw.Trim().Length == 0)
                    continue;

                int indent = raw.Length - raw.TrimStart(' ').Length;
                lines.Add(new Line(indent, raw.Substring(indent).TrimEnd()));
            }

            if (!body || lines.Count == 0)
                throw new InvalidDataException($"MonoBehaviour 본문이 없는 에셋: {path}");

            int index = 0;
            YamlNode root = ParseBlock(lines, ref index, lines[0].Indent);

            if (index < lines.Count)
                throw new InvalidDataException($"읽지 못한 줄이 남았다({path}, {index + 1}번째 본문 줄): {lines[index].Text}");

            return root;
        }

        private static YamlNode ParseBlock(List<Line> lines, ref int index, int indent) =>
            lines[index].Text.StartsWith("- ", StringComparison.Ordinal) || lines[index].Text == "-"
                ? ParseList(lines, ref index, indent)
                : ParseMap(lines, ref index, indent);

        private static YamlNode ParseMap(List<Line> lines, ref int index, int indent)
        {
            var node = new YamlNode { Map = new Dictionary<string, YamlNode>(StringComparer.Ordinal) };

            while (index < lines.Count && lines[index].Indent == indent && !lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
            {
                string text = lines[index].Text;
                int colon = KeyEnd(text);

                if (colon < 0)
                    throw new InvalidDataException($"키가 없는 줄: {text}");

                string key = text.Substring(0, colon);
                string rest = text.Substring(colon + 1).Trim();
                index++;

                if (rest.Length > 0)
                {
                    node.Map[key] = rest == "[]" ? new YamlNode { List = new List<YamlNode>() } : new YamlNode { Scalar = rest };
                    continue;
                }

                if (index < lines.Count && lines[index].Indent == indent && lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
                    node.Map[key] = ParseList(lines, ref index, indent);
                else if (index < lines.Count && lines[index].Indent > indent)
                    node.Map[key] = ParseBlock(lines, ref index, lines[index].Indent);
                else
                    node.Map[key] = new YamlNode { Scalar = "" };
            }

            return node;
        }

        private static YamlNode ParseList(List<Line> lines, ref int index, int indent)
        {
            var node = new YamlNode { List = new List<YamlNode>() };

            while (index < lines.Count && lines[index].Indent == indent && lines[index].Text.StartsWith("- ", StringComparison.Ordinal))
            {
                string item = lines[index].Text.Substring(2);

                if (KeyEnd(item) < 0)
                {
                    node.List.Add(new YamlNode { Scalar = item.Trim() });
                    index++;
                    continue;
                }

                // "- key: value"는 들여쓰기 indent + 2의 매핑 첫 줄이다.
                lines[index] = new Line(indent + 2, item);
                node.List.Add(ParseMap(lines, ref index, indent + 2));
            }

            return node;
        }

        // "key: value" 또는 "key:"의 콜론 위치. 한 줄 묶음({…})이나 일반 값이면 -1.
        private static int KeyEnd(string text)
        {
            if (text.StartsWith("{", StringComparison.Ordinal))
                return -1;

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ':' && (i + 1 == text.Length || text[i + 1] == ' '))
                    return i;

                if (text[i] == ' ' || text[i] == '{')
                    return -1;
            }

            return -1;
        }
    }
}
