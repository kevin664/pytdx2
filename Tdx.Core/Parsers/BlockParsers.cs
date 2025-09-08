using System.IO;
using System.Text;
using Tdx.Core.Utils;

namespace Tdx.Core.Parsers
{
    // --- Response Records ---
    public record ReportResponse(uint Size, byte[] Data);
    public record MetaResponse(uint Size, byte[] HashValue);

    // --- Parsers ---

    public class ReportParser : BaseParser<ReportResponse>
    {
        // For inheritance
        protected ReportParser() { }

        public ReportParser(string fileName, uint start = 0, uint size = 0x7530)
        {
            MsgId = 0x6b9;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(start);
            writer.Write(size);
            writer.Write(Encoding.UTF8.GetBytes(fileName.PadRight(100, '\0')));
            Body = stream.ToArray();
        }

        public override ReportResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var size = reader.ReadUInt32();
            var data = reader.ReadBytes((int)(responseBody.Length - 4));
            return new ReportResponse(size, data);
        }
    }

    public class MetaParser : BaseParser<MetaResponse>
    {
        public MetaParser(BlockFileType blockFileType)
        {
            MsgId = 0x2c5;
            var fileName = blockFileType.GetFileName();
            Body = Encoding.UTF8.GetBytes(fileName.PadRight(40, '\0'));
        }

        public override MetaResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var size = reader.ReadUInt32();
            reader.ReadByte(); // unknown1
            var hashValue = reader.ReadBytes(32);
            reader.ReadByte(); // unknown2
            return new MetaResponse(size, hashValue);
        }
    }

    public class BlockInfoParser : ReportParser
    {
        public BlockInfoParser(BlockFileType blockFileType, uint start, uint size)
            : base(blockFileType.GetFileName(), start, size)
        {
        }
    }
}
