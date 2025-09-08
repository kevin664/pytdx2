using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Tdx.Core.Utils
{
    public record BlockEntry(string BlockName, ushort BlockType, int CodeIndex, string Code);
    public record BlockGroup(string BlockName, ushort BlockType, int StockCount, IReadOnlyList<string> CodeList);

    public class BlockReader
    {
        private static bool _isEncodingProviderRegistered = false;

        public BlockReader()
        {
            if (!_isEncodingProviderRegistered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _isEncodingProviderRegistered = true;
            }
        }

        private byte[] GetBytes(object fileOrData)
        {
            if (fileOrData is string filePath)
            {
                return File.ReadAllBytes(filePath);
            }
            if (fileOrData is byte[] byteArray)
            {
                return byteArray;
            }
            throw new ArgumentException("Input must be a file path (string) or a byte array.", nameof(fileOrData));
        }

        public List<BlockEntry> GetFlatData(object fileOrData)
        {
            var data = GetBytes(fileOrData);
            var result = new List<BlockEntry>();
            var gbk = Encoding.GetEncoding("GB18030");
            var utf8 = Encoding.UTF8;

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            stream.Seek(384, SeekOrigin.Begin);
            ushort num = reader.ReadUInt16();

            for (int i = 0; i < num; i++)
            {
                stream.Seek(386 + i * 2800, SeekOrigin.Begin);

                var blockNameRaw = reader.ReadBytes(9);
                var blockName = gbk.GetString(blockNameRaw).TrimEnd('\0');

                ushort stockCount = reader.ReadUInt16();
                ushort blockType = reader.ReadUInt16();

                for (int codeIndex = 0; codeIndex < stockCount; codeIndex++)
                {
                    var codeRaw = reader.ReadBytes(7);
                    var code = utf8.GetString(codeRaw).TrimEnd('\0');
                    result.Add(new BlockEntry(blockName, blockType, codeIndex, code));
                }
            }
            return result;
        }

        public List<BlockGroup> GetGroupedData(object fileOrData)
        {
            var data = GetBytes(fileOrData);
            var result = new List<BlockGroup>();
            var gbk = Encoding.GetEncoding("GB18030");
            var utf8 = Encoding.UTF8;

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            stream.Seek(384, SeekOrigin.Begin);
            ushort num = reader.ReadUInt16();

            for (int i = 0; i < num; i++)
            {
                long blockStartPos = 386 + (long)i * 2800;
                stream.Seek(blockStartPos, SeekOrigin.Begin);

                var blockNameRaw = reader.ReadBytes(9);
                var blockName = gbk.GetString(blockNameRaw).TrimEnd('\0');

                ushort stockCount = reader.ReadUInt16();
                ushort blockType = reader.ReadUInt16();

                var codes = new List<string>(stockCount);
                for (int codeIndex = 0; codeIndex < stockCount; codeIndex++)
                {
                    var codeRaw = reader.ReadBytes(7);
                    var code = utf8.GetString(codeRaw).TrimEnd('\0');
                    codes.Add(code);
                }
                result.Add(new BlockGroup(blockName, blockType, stockCount, codes));
            }
            return result;
        }
    }

    public class CustomerBlockReader
    {
        private static bool _isEncodingProviderRegistered = false;

        public CustomerBlockReader()
        {
            if (!_isEncodingProviderRegistered)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                _isEncodingProviderRegistered = true;
            }
        }

        public List<BlockEntry> GetFlatData(string directoryPath)
        {
            var result = new List<BlockEntry>();
            var blocks = ParseBlockInfo(directoryPath);
            foreach (var block in blocks)
            {
                result.AddRange(block.CodeList.Select((code, index) => new BlockEntry(block.BlockName, 0, index, code)));
            }
            return result;
        }

        public List<BlockGroup> GetGroupedData(string directoryPath)
        {
            return ParseBlockInfo(directoryPath).Select(b => new BlockGroup(b.BlockName, 0, b.CodeList.Count, b.CodeList)).ToList();
        }

        private List<(string BlockName, List<string> CodeList)> ParseBlockInfo(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
            }

            var configFile = Path.Combine(directoryPath, "blocknew.cfg");
            if (!File.Exists(configFile))
            {
                throw new FileNotFoundException($"Configuration file not found: {configFile}");
            }

            var gbk = Encoding.GetEncoding("GB18030");
            var result = new List<(string, List<string>)>();
            var configData = File.ReadAllBytes(configFile);

            using var stream = new MemoryStream(configData);
            using var reader = new BinaryReader(stream);

            while (stream.Position < stream.Length)
            {
                var blockNameRaw = reader.ReadBytes(50);
                var fileNameRaw = reader.ReadBytes(70); // Corresponds to n2 in python code

                var blockName = gbk.GetString(blockNameRaw).Split('\0')[0];
                var fileName = gbk.GetString(fileNameRaw).Split('\0')[0];

                var blockFilePath = Path.Combine(directoryPath, fileName + ".blk");
                if (!File.Exists(blockFilePath))
                {
                    // Log or handle missing file
                    continue;
                }

                var codes = File.ReadAllLines(blockFilePath)
                                .Where(line => !string.IsNullOrEmpty(line))
                                .Select(line => line.Length > 1 ? line.Substring(1) : "")
                                .ToList();

                result.Add((blockName, codes));
            }
            return result;
        }
    }
}
