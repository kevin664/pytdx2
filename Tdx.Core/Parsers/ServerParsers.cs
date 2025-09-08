using System;
using System.IO;
using System.Text;
using Tdx.Core.Utils;

namespace Tdx.Core.Parsers
{
    // Response Models
    public record ExchangeAnnouncementResponse(byte V, string Content);
    public record HeartBeatResponse(uint Date);
    public record AnnouncementResponse(DateTime ExpireDate, string Title, string Author, string Content);
    public record LoginResponse(DateTime ServerTime, string ServerName, string Category);
    public record InfoResponse(uint Delay, string Info, string Content, string ServerSign, DateTime TimeNow);
    public record UpgradeTipResponse(bool Had, string Tips, string Link, string? Message);
    public record TodoFDEResponse(uint U1, ushort U2, string U4Hex);


    // Parsers

    public class ExchangeAnnouncementParser : BaseParser<ExchangeAnnouncementResponse?>
    {
        public ExchangeAnnouncementParser()
        {
            MsgId = 0x2;
        }

        public override ExchangeAnnouncementResponse? Deserialize(byte[] responseBody)
        {
            if (responseBody.Length < 1) return null;

            var v = responseBody[0];
            var content = Encoding.GetEncoding("GB18030").GetString(responseBody, 1, responseBody.Length - 1);
            return new ExchangeAnnouncementResponse(v, content);
        }
    }

    public class HeartBeatParser : BaseParser<HeartBeatResponse>
    {
        public HeartBeatParser()
        {
            MsgId = 0x4;
        }

        public override HeartBeatResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            reader.ReadBytes(6); // Skip 6s
            var date = reader.ReadUInt32();
            return new HeartBeatResponse(date);
        }
    }

    public class AnnouncementParser : BaseParser<AnnouncementResponse?>
    {
        public AnnouncementParser()
        {
            MsgId = 0xa;
            Body = new byte[54]; // struct.pack('<54s', b'')
        }

        public override AnnouncementResponse? Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var had = reader.ReadByte();
            if (had != 0x01) return null;

            var expireDateInt = reader.ReadUInt32();
            var titleLen = reader.ReadUInt16();
            var authorLen = reader.ReadUInt16();
            var contentLen = reader.ReadUInt16();

            var title = Encoding.GetEncoding("GB18030").GetString(reader.ReadBytes(titleLen));
            var author = Encoding.GetEncoding("GB18030").GetString(reader.ReadBytes(authorLen));
            var content = Encoding.GetEncoding("GB18030").GetString(reader.ReadBytes(contentLen));

            var expireDate = new DateTime((int)(expireDateInt / 10000), (int)(expireDateInt % 10000 / 100), (int)(expireDateInt % 100));

            return new AnnouncementResponse(expireDate, title, author, content);
        }
    }

    public class LoginParser : BaseParser<LoginResponse>
    {
        public LoginParser()
        {
            MsgId = 0xd;
            Body = new byte[] { 1 }; // struct.pack('<B', 1)
        }

        public override LoginResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));

            reader.ReadByte(); // Skip 1 byte
            var year = reader.ReadUInt16();
            var day = reader.ReadByte();
            var month = reader.ReadByte();
            var minute = reader.ReadByte();
            var hour = reader.ReadByte();
            reader.ReadByte(); // Skip 1 byte
            var second = reader.ReadByte();

            reader.ReadBytes(16); // unknown1
            reader.ReadBytes(16); // unknown2
            reader.ReadByte();    // unknown3
            reader.ReadUInt32();  // date
            reader.ReadUInt16();  // a1
            reader.ReadUInt16();  // b1
            reader.ReadUInt32();  // date2
            reader.ReadUInt16();  // a2
            reader.ReadUInt16();  // b2
            reader.ReadBytes(5);  // unknown4
            reader.ReadBytes(22); // unknown5

            var serverName = Encoding.GetEncoding("GB18030").GetString(reader.ReadBytes(64)).TrimEnd('\0');
            reader.ReadBytes(6); // unknown7
            var category = Encoding.GetEncoding("GB18030").GetString(reader.ReadBytes(30)).TrimEnd('\0');

            var serverTime = new DateTime(year, month, day, hour, minute, second);

            return new LoginResponse(serverTime, serverName, category);
        }
    }

    public class InfoParser : BaseParser<InfoResponse>
    {
        public InfoParser()
        {
            MsgId = 0x15;
        }

        public override InfoResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));

            var gbk = Encoding.GetEncoding("GB18030");

            var maybeDelay = reader.ReadUInt32();
            reader.ReadBytes(10); // unknown_aH, _
            var info = gbk.GetString(reader.ReadBytes(55)).TrimEnd('\0');
            reader.ReadBytes(10); // unknown10s
            var content = gbk.GetString(reader.ReadBytes(255)).TrimEnd('\0');
            var serverSign = gbk.GetString(reader.ReadBytes(20)).TrimEnd('\0');
            reader.ReadBytes(43); // A bunch of unknown fields
            var dateNowInt = reader.ReadUInt32();
            var timeNowInt = reader.ReadUInt32();

            var year = (int)(dateNowInt / 10000);
            var month = (int)(dateNowInt % 10000 / 100);
            var day = (int)(dateNowInt % 100);
            var hour = (int)(timeNowInt / 10000);
            var minute = (int)(timeNowInt % 10000 / 100);
            var second = (int)(timeNowInt % 100);
            var timeNow = new DateTime(year, month, day, hour, minute, second);

            return new InfoResponse(maybeDelay, info, content, serverSign, timeNow);
        }
    }

    public class UpgradeTipParser : BaseParser<UpgradeTipResponse>
    {
        public UpgradeTipParser()
        {
            MsgId = 0xfdb;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var gbk = Encoding.GetEncoding("GB18030");
            writer.Write(gbk.GetBytes("tdxlevel".PadRight(8, '\0')));
            writer.Write(new byte[] { 0x00, 0x00, 0x00, 0xa4, 0x70, 0xf5, 0x40, 0x07, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05 });
            Body = stream.ToArray();
        }

        public override UpgradeTipResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");

            var had = reader.ReadByte() == 0x01;
            reader.ReadUInt16(); // unknow2
            var tips = gbk.GetString(reader.ReadBytes(50)).TrimEnd('\0');
            reader.ReadBytes(5); // unknow5
            var link = gbk.GetString(reader.ReadBytes(120)).TrimEnd('\0');

            string? msg = null;
            if (had && reader.BaseStream.Position < reader.BaseStream.Length)
            {
                msg = gbk.GetString(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position))).TrimEnd('\0');
            }

            return new UpgradeTipResponse(had, tips, link, msg);
        }
    }

    public class TodoFDEParser : BaseParser<TodoFDEResponse>
    {
        public override TodoFDEResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var u1 = reader.ReadUInt32();
            var u2 = reader.ReadUInt16();
            reader.ReadBytes(165); // u3
            var u4 = reader.ReadBytes(16);
            return new TodoFDEResponse(u1, u2, BitConverter.ToString(u4).Replace("-", ""));
        }
    }
}
