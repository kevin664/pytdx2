using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Tdx.Core.Utils;

namespace Tdx.Core.Parsers
{
    // --- Response Records ---
    public record CompanyInfoCategory(string Name, string Filename, uint Start, uint Length);
    public record CompanyInfoContent(ushort Market, string Code, ushort MarketOr, ushort Length, string Content);
    public record FinanceInfo(
        Market Market, string Code, float Liutongguben, ushort Province, ushort Industry, uint UpdatedDate, uint IpoDate,
        float Zongguben, float Guojiagu, float FaQiRenFaRenGu, float FaRenGu, float BGu, float HGu, float MeiGuShouYi,
        float ZiChanZongJi, float LiuDongZiChanZongJi, float GuDingZiChanJinE, float WuXingZiChan, float GuDongRenShu,
        float LiuDongFuZhaiHeJi, float Changqifuzhai, float ZiBenGongJiJin, float GuiMuQuanYiHeJi, float YinYeZongShouRu,
        float YinYeChengBen, float YingShouZhangKuan, float YinYeLiRun, float TouZiShouYi, float JingYinXianJinLiuJinE,
        float Zongxianjinliu, float CunHuo, float LiRunZongE, float ShuiHouLiRun, float GuiMuJinLiRun, float WeiFenLiRun,
        float MeiGuJinZiChan, float Baoliu2
    );
    public record XdxrInfo(
        Market Market, string Code, DateTime Date, string Name, float? Fenhong, float? Peigujia, float? Songzhuangu,
        float? Peigu, float? Suogu, float? Xingquanjia, float? Fenshu, float? Panqianliutong, float? Qianzongguben,
        float? Panhouliutong, float? Houzongguben
    );

    // --- Parsers ---

    public class CompanyInfoCategoryParser : BaseParser<List<CompanyInfoCategory>>
    {
        public CompanyInfoCategoryParser(Market market, string code)
        {
            MsgId = 0x2cf;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            writer.Write((uint)0);
            Body = stream.ToArray();
        }

        public override List<CompanyInfoCategory> Deserialize(byte[] responseBody)
        {
            var result = new List<CompanyInfoCategory>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var name = gbk.GetString(reader.ReadBytes(64)).TrimEnd('\0');
                var filename = gbk.GetString(reader.ReadBytes(80)).TrimEnd('\0');
                var start = reader.ReadUInt32();
                var length = reader.ReadUInt32();
                result.Add(new CompanyInfoCategory(name, filename, start, length));
            }
            return result;
        }
    }

    public class CompanyInfoContentParser : BaseParser<CompanyInfoContent>
    {
        public CompanyInfoContentParser(Market market, string code, string filename, uint start, uint length)
        {
            MsgId = 0x2d0;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            var utf8 = Encoding.UTF8;

            writer.Write((ushort)market);
            writer.Write(utf8.GetBytes(code.PadRight(6, '\0')));
            writer.Write((ushort)0);
            writer.Write(utf8.GetBytes(filename.PadRight(80, '\0')));
            writer.Write(start);
            writer.Write(length);
            writer.Write((uint)0);
            Body = stream.ToArray();
        }

        public override CompanyInfoContent Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");

            var market = reader.ReadUInt16();
            var code = gbk.GetString(reader.ReadBytes(6)).TrimEnd('\0');
            var marketOr = reader.ReadUInt16();
            var length = reader.ReadUInt16();
            var content = gbk.GetString(reader.ReadBytes(length)).TrimEnd('\0');
            return new CompanyInfoContent(market, code, marketOr, length, content);
        }
    }

    public class FinanceInfoParser : BaseParser<FinanceInfo?>
    {
        public FinanceInfoParser(Market market, string code)
        {
            MsgId = 0x10;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)1);
            writer.Write((byte)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            Body = stream.ToArray();
        }

        public override FinanceInfo? Deserialize(byte[] responseBody)
        {
            if (responseBody.Length < 151) return null;

            using var reader = new BinaryReader(new MemoryStream(responseBody));
            reader.ReadUInt16(); // num
            var market = (Market)reader.ReadByte();
            var code = Encoding.UTF8.GetString(reader.ReadBytes(6)).TrimEnd('\0');
            var liutongguben = reader.ReadSingle();
            var province = reader.ReadUInt16();
            var industry = reader.ReadUInt16();
            var updatedDate = reader.ReadUInt32();
            var ipoDate = reader.ReadUInt32();

            // Read all float values
            var floats = new float[30];
            for (int i = 0; i < 30; i++)
            {
                floats[i] = reader.ReadSingle();
            }

            return new FinanceInfo(
                market, code, liutongguben, province, industry, updatedDate, ipoDate,
                floats[0], floats[1], floats[2], floats[3], floats[4], floats[5], floats[6], floats[7],
                floats[8], floats[9], floats[10], floats[11], floats[12], floats[13], floats[14], floats[15],
                floats[16], floats[17], floats[18], floats[19], floats[20], floats[21], floats[22], floats[23],
                floats[24], floats[25], floats[26], floats[27], floats[28], floats[29]
            );
        }
    }

    public class XdxrInfoParser : BaseParser<List<XdxrInfo>>
    {
        private static readonly Dictionary<int, string> XdxrCategoryMapping = new()
        {
            { 1, "除权除息" }, { 2, "送配股上市" }, { 3, "非流通股上市" }, { 4, "未知股本变动" },
            { 5, "股本变化" }, { 6, "增发新股" }, { 7, "股份回购" }, { 8, "增发新股上市" },
            { 9, "转配股上市" }, { 10, "可转债上市" }, { 11, "扩缩股" }, { 12, "非流通股缩股" },
            { 13, "送认购权证" }, { 14, "送认沽权证" }
        };

        public XdxrInfoParser(Market market, string code)
        {
            MsgId = 0xf;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)1);
            writer.Write((byte)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            Body = stream.ToArray();
        }

        public override List<XdxrInfo> Deserialize(byte[] responseBody)
        {
            var result = new List<XdxrInfo>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));

            var market = (Market)reader.ReadByte();
            reader.ReadByte(); // marketOR
            var code = Encoding.UTF8.GetString(reader.ReadBytes(6)).TrimEnd('\0');
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var catMarket = (Market)reader.ReadByte();
                var catCode = Encoding.UTF8.GetString(reader.ReadBytes(6)).TrimEnd('\0');
                reader.ReadByte(); // unknown
                var date = Help.ToDateTime(reader.ReadUInt32());
                var category = reader.ReadByte();

                var name = XdxrCategoryMapping.GetValueOrDefault(category, category.ToString());
                var floatData = new float[4];
                for (int j = 0; j < 4; j++) floatData[j] = reader.ReadSingle();

                float? fenhong = null, peigujia = null, songzhuangu = null, peigu = null, suogu = null;
                float? xingquanjia = null, fenshu = null, panqianliutong = null, qianzongguben = null;
                float? panhouliutong = null, houzongguben = null;

                if (category == 1)
                {
                    fenhong = floatData[0]; peigujia = floatData[1]; songzhuangu = floatData[2]; peigu = floatData[3];
                }
                else if (category == 11 || category == 12)
                {
                    suogu = floatData[2];
                }
                else if (category == 13 || category == 14)
                {
                    xingquanjia = floatData[0]; fenshu = floatData[2];
                }
                else
                {
                    panqianliutong = floatData[0]; qianzongguben = floatData[1]; panhouliutong = floatData[2]; houzongguben = floatData[3];
                }

                result.Add(new XdxrInfo(catMarket, catCode, date, name, fenhong, peigujia, songzhuangu, peigu, suogu, xingquanjia, fenshu, panqianliutong, qianzongguben, panhouliutong, houzongguben));
            }
            return result;
        }
    }
}
