using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace FolderLauncher
{
    public sealed class Favorite
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Group { get; set; }
    }

    internal sealed class Library
    {
        internal const string DefaultGroup = "未分组";
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(List<Favorite>));
        private readonly string file;
        internal List<Favorite> Items { get; private set; }

        internal Library(string file)
        {
            this.file = file;
            Items = new List<Favorite>();
            if (File.Exists(file))
            {
                using (var stream = File.OpenRead(file))
                    Items = (List<Favorite>)Serializer.Deserialize(stream);
            }
        }

        internal void Save(List<Favorite> items)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string pending = file + ".tmp";
            using (var stream = File.Create(pending))
            {
                Serializer.Serialize(stream, items);
                stream.Flush(true);
            }

            if (File.Exists(file))
                File.Replace(pending, file, file + ".bak");
            else
                File.Move(pending, file);

            Items = items;
        }

        internal static string Normalize(string input)
        {
            string value = input.Trim().Trim('"');
            if (value.StartsWith(@"\\.\", StringComparison.Ordinal) ||
                value.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                !(value.StartsWith(@"\\", StringComparison.Ordinal) ||
                  (value.Length >= 3 && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))))
                throw new ArgumentException("请输入完整的 Windows 文件夹路径。");

            string full = Path.GetFullPath(value);
            return full.Length == Path.GetPathRoot(full).Length
                ? full
                : full.TrimEnd(Path.DirectorySeparatorChar);
        }

        internal static bool SamePath(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        internal static string Caption(string folder)
        {
            string name = Path.GetFileName(folder);
            return string.IsNullOrEmpty(name) ? folder : name;
        }
    }

}
