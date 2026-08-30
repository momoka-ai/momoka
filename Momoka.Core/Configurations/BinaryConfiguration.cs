using System.Text;

namespace Momoka.Core.Configurations;

/// <summary>
/// 二进制配置：把配置值树 + 版本序列化为紧凑二进制 BLOB（小端、UTF8 长度前缀，
/// 魔数 + 格式版本头）。适合插件数据的紧凑 / 不透明持久化；结构与文件 / 数据库配置一致（值树 + 版本）。
/// </summary>
public sealed class BinaryConfiguration : Configuration
{
    private const int FormatVersion = 1;

    private static readonly byte[] Magic = "MCFG"u8.ToArray();

    private const byte TagNull = 0;
    private const byte TagString = 1;
    private const byte TagBool = 2;
    private const byte TagLong = 3;
    private const byte TagDouble = 4;
    private const byte TagDateTime = 5;
    private const byte TagList = 6;
    private const byte TagTable = 7;

    /// <summary>创建二进制配置（迁移链与目标版本见 <see cref="Configuration"/>；数据经 <see cref="FromBytes"/> 装载）。</summary>
    public BinaryConfiguration(IEnumerable<Migration>? migrations = null, Version? targetVersion = null)
        : base(migrations, targetVersion)
    {
    }

    /// <summary>从 BLOB 反序列化（魔数 / 格式版本非法抛 <see cref="ConfigurationException"/>）。</summary>
    public static BinaryConfiguration FromBytes(
        byte[] data,
        IEnumerable<Migration>? migrations = null,
        Version? targetVersion = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var configuration = new BinaryConfiguration(migrations, targetVersion);
        configuration.LoadBytes(data);
        return configuration;
    }

    /// <summary>把当前值树 + 版本序列化为 BLOB。</summary>
    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(Magic);
            writer.Write(FormatVersion);
            WriteString(writer, Version.ToString());

            Dictionary<string, object?> tree = SnapshotValues();
            writer.Write(tree.Count);
            foreach (var (key, value) in tree)
            {
                WriteString(writer, key);
                WriteValue(writer, value);
            }
        }

        return stream.ToArray();
    }

    private void LoadBytes(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
        {
            throw new ConfigurationException("Invalid binary configuration magic.");
        }

        if (reader.ReadInt32() != FormatVersion)
        {
            throw new ConfigurationException($"Unsupported binary configuration format version.");
        }

        string storedVersion = ReadString(reader);
        if (!Version.TryParse(storedVersion, out Version? version))
        {
            throw new ConfigurationException($"Invalid binary configuration version '{storedVersion}'.");
        }

        var tree = new Dictionary<string, object?>(StringComparer.Ordinal);
        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            string key = ReadString(reader);
            tree[key] = ReadValue(reader);
        }

        LoadValues(tree, version);
    }

    private static void WriteValue(BinaryWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.Write(TagNull);
                break;
            case string text:
                writer.Write(TagString);
                WriteString(writer, text);
                break;
            case bool flag:
                writer.Write(TagBool);
                writer.Write(flag);
                break;
            case long number:
                writer.Write(TagLong);
                writer.Write(number);
                break;
            case double number:
                writer.Write(TagDouble);
                writer.Write(number);
                break;
            case DateTime dateTime:
                writer.Write(TagDateTime);
                writer.Write(dateTime.ToBinary());
                break;
            case List<object?> list:
                writer.Write(TagList);
                writer.Write(list.Count);
                foreach (object? item in list)
                {
                    WriteValue(writer, item);
                }

                break;
            case Dictionary<string, object?> table:
                writer.Write(TagTable);
                writer.Write(table.Count);
                foreach (var (key, item) in table)
                {
                    WriteString(writer, key);
                    WriteValue(writer, item);
                }

                break;
            default:
                throw new ConfigurationException(
                    $"Value of type '{value.GetType()}' cannot be written to binary configuration.");
        }
    }

    private static object? ReadValue(BinaryReader reader)
    {
        byte tag = reader.ReadByte();
        switch (tag)
        {
            case TagNull:
                return null;
            case TagString:
                return ReadString(reader);
            case TagBool:
                return reader.ReadBoolean();
            case TagLong:
                return reader.ReadInt64();
            case TagDouble:
                return reader.ReadDouble();
            case TagDateTime:
                return DateTime.FromBinary(reader.ReadInt64());
            case TagList:
            {
                int count = reader.ReadInt32();
                var list = new List<object?>(count);
                for (int i = 0; i < count; i++)
                {
                    list.Add(ReadValue(reader));
                }

                return list;
            }

            case TagTable:
            {
                int count = reader.ReadInt32();
                var table = new Dictionary<string, object?>(count, StringComparer.Ordinal);
                for (int i = 0; i < count; i++)
                {
                    table[ReadString(reader)] = ReadValue(reader);
                }

                return table;
            }

            default:
                throw new ConfigurationException($"Unknown binary configuration value tag '{tag}'.");
        }
    }

    private static void WriteString(BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    private static string ReadString(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        if (length < 0)
        {
            throw new ConfigurationException($"Invalid binary configuration string length '{length}'.");
        }

        return Encoding.UTF8.GetString(reader.ReadBytes(length));
    }
}
