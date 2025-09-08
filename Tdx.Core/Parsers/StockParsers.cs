using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tdx.Core.Utils;

namespace Tdx.Core.Parsers
{
    // --- Response Records for Stock Parsers ---

    public record StockBar(DateTime DateTime, float Open, float Close, float High, float Low, float Vol, float Amount, int? UpCount = null, int? DownCount = null);
    public record StockCountResponse(int Count);
    public record StockListEntry(string Code, ushort Vol, string Name, byte DecimalPoint, float PreClose);
    public record Order(float Price, int Vol, int Unknown);
    public record Transaction(string Time, float Price, int Vol, int Trans, string Action, int Unknown);
    public record HistoryTransaction(string Time, float Price, int Vol, string Action, int Unknown);
    public record ChartSamplingResponse(Market Market, string Code, float Price, List<float> Prices);
    public record Quote(Market Market, string Code, float Price, float Open, float High, float Low, float LastClose, string ServerTime, float AfterHour, int Vol, int CurVol, float Amount, int SVol, int BVol, float SAmount, float BAmount, ushort V1, string Unknown, short V2, ushort Active1, ushort Active2);
    public record UnusualEntry(ushort Index, Market Market, string Code, string Time, string Description, string Value);

    // --- Stock Parsers ---

    public class BarsParser : BaseParser<List<StockBar>>
    {
        private readonly KlineType _klineType;

        public BarsParser(Market market, string code, KlineType klineType, int start, ushort count)
        {
            MsgId = 0x52d;
            _klineType = klineType;

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write((ushort)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            writer.Write((ushort)klineType);
            writer.Write((ushort)1);
            writer.Write((ushort)start);
            writer.Write(count);
            writer.Write(new byte[10]); // 10s

            Body = stream.ToArray();
        }

        public override List<StockBar> Deserialize(byte[] responseBody)
        {
            var result = new List<StockBar>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));

            var count = reader.ReadUInt16();
            if (count == 0) return result;

            bool minuteCategory = (int)_klineType < 4 || (int)_klineType == 7 || (int)_klineType == 8;
            float preDiffBase = 0;

            for (int i = 0; i < count; i++)
            {
                var date = reader.ReadUInt32();
                var dt = Help.ToDateTime(date, minuteCategory);

                var (open, pos1) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos1;
                var (close, pos2) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos2;
                var (high, pos3) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos3;
                var (low, pos4) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos4;

                var vol = reader.ReadSingle();
                var amount = reader.ReadSingle();

                int? upCount = null;
                int? downCount = null;
                // This logic is complex and error-prone in Python, simplifying in C#
                if (i < count - 1 && reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
                {
                    var nextDateRaw = reader.ReadUInt32();
                    try
                    {
                        var nextDate = Help.ToDateTime(nextDateRaw, minuteCategory);
                        if (result.Any() && nextDate.Year < result.Last().DateTime.Year)
                        {
                           throw new FormatException("Year decreased");
                        }
                        // If successful, reset position
                        reader.BaseStream.Position -= 4;
                    }
                    catch (Exception)
                    {
                         reader.BaseStream.Position -= 4;
                         upCount = reader.ReadUInt16();
                         downCount = reader.ReadUInt16();
                    }
                }

                float openPrice = open + preDiffBase;
                float closePrice = close + openPrice;
                float highPrice = high + openPrice;
                float lowPrice = low + openPrice;
                preDiffBase = closePrice;

                result.Add(new StockBar(dt, openPrice, closePrice, highPrice, lowPrice, vol, amount, upCount, downCount));
            }
            return result;
        }
    }

    public class StockCountParser : BaseParser<StockCountResponse>
    {
        public StockCountParser(Market market)
        {
            MsgId = 0x44e;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var today = DateTime.Today;
            uint dateInt = (uint)(today.Year * 10000 + today.Month * 100 + today.Day);
            writer.Write((ushort)market);
            writer.Write(dateInt);
            Body = stream.ToArray();
        }

        public override StockCountResponse Deserialize(byte[] responseBody)
        {
            var count = BitConverter.ToUInt16(responseBody, 0);
            return new StockCountResponse(count);
        }
    }

    public class StockListParser : BaseParser<List<StockListEntry>>
    {
        public StockListParser(Market market, ushort start, ushort count = 1600)
        {
            MsgId = 0x44d;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)market);
            writer.Write(start);
            writer.Write(count);
            writer.Write(0); // unknown
            Body = stream.ToArray();
        }

        public override List<StockListEntry> Deserialize(byte[] responseBody)
        {
            var result = new List<StockListEntry>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var code = gbk.GetString(reader.ReadBytes(6)).TrimEnd('\0');
                var vol = reader.ReadUInt16();
                var name = gbk.GetString(reader.ReadBytes(8)).TrimEnd('\0');
                reader.ReadBytes(8); // skip unknown
                reader.ReadBytes(4); // skip unknown
                var decimalPoint = reader.ReadByte();
                var preClose = reader.ReadSingle();
                reader.ReadBytes(4); // skip unknown
                result.Add(new StockListEntry(code, vol, name, decimalPoint, preClose));
            }
            return result;
        }
    }

    public class OrdersParser : BaseParser<List<Order>>
    {
        public OrdersParser(Market market, string code)
        {
            MsgId = 0x51d;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var gbk = Encoding.GetEncoding("GB18030");

            writer.Write((ushort)market);
            writer.Write(gbk.GetBytes(code.PadRight(6, '\0')));
            writer.Write((uint)0);
            Body = stream.ToArray();
        }

        public override List<Order> Deserialize(byte[] responseBody)
        {
            var result = new List<Order>();
            var count = BitConverter.ToUInt16(responseBody, 0);
            int pos = 2;
            float lastPrice = 0;

            for (int i = 0; i < count; i++)
            {
                var (price, newPos1) = Help.GetPrice(responseBody, pos);
                var (unknown, newPos2) = Help.GetPrice(responseBody, newPos1);
                var (vol, newPos3) = Help.GetPrice(responseBody, newPos2);
                pos = newPos3;

                lastPrice += price;
                result.Add(new Order(lastPrice, vol, unknown));
            }
            return result;
        }
    }

    public class HistoryOrdersParser : BaseParser<List<Order>>
    {
        public HistoryOrdersParser(Market market, string code, DateTime date)
        {
            MsgId = 0xfb4;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            uint dateInt = (uint)(date.Year * 10000 + date.Month * 100 + date.Day);

            writer.Write(dateInt);
            writer.Write((byte)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            Body = stream.ToArray();
        }

        public override List<Order> Deserialize(byte[] responseBody)
        {
             var result = new List<Order>();
            var count = BitConverter.ToUInt16(responseBody, 0);
            int pos = 2;
            float lastPrice = 0;

            // skip unknown
            pos += 4;

            for (int i = 0; i < count; i++)
            {
                var (price, newPos1) = Help.GetPrice(responseBody, pos);
                var (unknown, newPos2) = Help.GetPrice(responseBody, newPos1);
                var (vol, newPos3) = Help.GetPrice(responseBody, newPos2);
                pos = newPos3;

                lastPrice += price;
                result.Add(new Order(lastPrice, vol, unknown));
            }
            return result;
        }
    }

    public class TransactionParser : BaseParser<List<Transaction>>
    {
        public TransactionParser(Market market, string code, ushort start, ushort count)
        {
            MsgId = 0xfc5;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((ushort)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            writer.Write(start);
            writer.Write(count);
            Body = stream.ToArray();
        }

        public override List<Transaction> Deserialize(byte[] responseBody)
        {
            var result = new List<Transaction>();
            var count = BitConverter.ToUInt16(responseBody, 0);
            int pos = 2;
            float lastPrice = 0;

            for (int i = 0; i < count; i++)
            {
                var (hour, minute, newPos0) = Help.GetTime(responseBody, pos);
                var (price, newPos1) = Help.GetPrice(responseBody, newPos0);
                var (vol, newPos2) = Help.GetPrice(responseBody, newPos1);
                var (trans, newPos3) = Help.GetPrice(responseBody, newPos2);
                var (buyOrSell, newPos4) = Help.GetPrice(responseBody, newPos3);
                var (unknown, newPos5) = Help.GetPrice(responseBody, newPos4);
                pos = newPos5;

                lastPrice += price;
                var action = buyOrSell == 1 ? "SELL" : "BUY";
                result.Add(new Transaction($"{hour:D2}:{minute:D2}", lastPrice, vol, trans, action, unknown));
            }
            return result;
        }
    }

    public class HistoryTransactionParser : BaseParser<List<HistoryTransaction>>
    {
        public HistoryTransactionParser(Market market, string code, DateTime date, ushort start, ushort count)
        {
            MsgId = 0xfb5;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            uint dateInt = (uint)(date.Year * 10000 + date.Month * 100 + date.Day);
            writer.Write(dateInt);
            writer.Write((ushort)market);
            writer.Write(Encoding.UTF8.GetBytes(code.PadRight(6, '\0')));
            writer.Write(start);
            writer.Write(count);
            Body = stream.ToArray();
        }

        public override List<HistoryTransaction> Deserialize(byte[] responseBody)
        {
            var result = new List<HistoryTransaction>();
            var count = BitConverter.ToUInt16(responseBody, 0);
            int pos = 6; // Skip 4 bytes unknown
            float lastPrice = 0;

            for (int i = 0; i < count; i++)
            {
                var (hour, minute, newPos0) = Help.GetTime(responseBody, pos);
                var (price, newPos1) = Help.GetPrice(responseBody, newPos0);
                var (vol, newPos2) = Help.GetPrice(responseBody, newPos1);
                var (buyOrSell, newPos3) = Help.GetPrice(responseBody, newPos2);
                var (unknown, newPos4) = Help.GetPrice(responseBody, newPos3);
                pos = newPos4;

                lastPrice += price;
                var action = buyOrSell == 1 ? "SELL" : "BUY";
                result.Add(new HistoryTransaction($"{hour:D2}:{minute:D2}", lastPrice, vol, action, unknown));
            }
            return result;
        }
    }

    public class QuotesParser : QuotesListParser
    {
        public QuotesParser(IEnumerable<(Market market, string code)> stocks)
        {
            MsgId = 0x54c;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var gbk = Encoding.GetEncoding("GB18030");

            var stockList = stocks.ToList();
            writer.Write((ushort)5);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)stockList.Count);

            foreach (var (market, code) in stockList)
            {
                writer.Write((byte)market);
                writer.Write(gbk.GetBytes(code.PadRight(6, '\0')));
            }
            Body = stream.ToArray();
        }
    }

    public class QuotesListParser : BaseParser<List<Quote>>
    {
        // For inheritance by QuotesParser
        protected QuotesListParser() { }

        public QuotesListParser(Category category, ushort start = 0, ushort count = 80)
        {
            MsgId = 0x54b;
            Body = new byte[18]; // struct.pack('<HHHHHHHHH', ...)
            using var writer = new BinaryWriter(new MemoryStream(Body));
            writer.Write((ushort)category);
            writer.Write((ushort)0);
            writer.Write(start);
            writer.Write(count);
            writer.Write((ushort)0);
            writer.Write((ushort)5);
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)0);
        }

        public override List<Quote> Deserialize(byte[] responseBody)
        {
            var result = new List<Quote>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");

            var block = reader.ReadUInt16(); // Not used
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var market = (Market)reader.ReadByte();
                var code = gbk.GetString(reader.ReadBytes(6)).TrimEnd('\0');
                var active1 = reader.ReadUInt16();

                var (price, pos1) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos1;
                var (lastClose, pos2) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos2;
                var (open, pos3) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos3;
                var (high, pos4) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos4;
                var (low, pos5) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos5;
                var (serverTimeRaw, pos6) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos6;
                var (afterHour, pos7) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos7;
                var (vol, pos8) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos8;
                var (curVol, pos9) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos9;

                var amount = reader.ReadSingle();

                var (sVol, pos10) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos10;
                var (bVol, pos11) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos11;
                var (sAmount, pos12) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos12;
                var (bAmount, pos13) = Help.GetPrice(responseBody, (int)reader.BaseStream.Position);
                reader.BaseStream.Position = pos13;

                // Skip handicap
                reader.ReadBytes(16);

                var v1 = reader.ReadUInt16();
                var v2 = reader.ReadInt16();
                var unknown1 = BitConverter.ToString(reader.ReadBytes(8));
                reader.ReadBytes(10);
                var unknown2 = BitConverter.ToString(reader.ReadBytes(8));
                reader.ReadBytes(24);
                var active2 = reader.ReadUInt16();

                float actualLastClose = price + lastClose;
                float actualOpen = price + open;
                float actualHigh = price + high;
                float actualLow = price + low;

                result.Add(new Quote(market, code, price, actualOpen, actualHigh, actualLow, actualLastClose, serverTimeRaw.ToString(), afterHour, vol, curVol, amount, sVol, bVol, sAmount, bAmount, v1, unknown1, v2, active1, active2));
            }
            return result;
        }
    }

    public class ListBParser : BaseParser<List<StockListEntry>>
    {
        public ListBParser(Market market, ushort start)
        {
            MsgId = 0x450;
            Body = new byte[4];
            using var writer = new BinaryWriter(new MemoryStream(Body));
            writer.Write((ushort)market);
            writer.Write(start);
        }

        public override List<StockListEntry> Deserialize(byte[] responseBody)
        {
            var result = new List<StockListEntry>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var code = gbk.GetString(reader.ReadBytes(6)).TrimEnd('\0');
                var vol = reader.ReadUInt16();
                var name = gbk.GetString(reader.ReadBytes(8)).TrimEnd('\0');
                reader.ReadBytes(4); // unknown
                var decimalPoint = reader.ReadByte();
                var preClose = reader.ReadSingle();
                reader.ReadBytes(4); // unknown
                result.Add(new StockListEntry(code, vol, name, decimalPoint, preClose));
            }
            return result;
        }
    }

    public class ChartSamplingParser : BaseParser<ChartSamplingResponse>
    {
        public ChartSamplingParser(Market market, string code)
        {
            MsgId = 0xfd1;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            var gbk = Encoding.GetEncoding("GB18030");
            writer.Write((ushort)market);
            writer.Write(gbk.GetBytes(code.PadRight(6, '\0')));
            writer.Write(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x14, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 });
            Body = stream.ToArray();
        }

        public override ChartSamplingResponse Deserialize(byte[] responseBody)
        {
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");

            var market = (Market)reader.ReadUInt16();
            var code = gbk.GetString(reader.ReadBytes(6)).TrimEnd('\0');
            reader.BaseStream.Position = 34;
            var num = reader.ReadUInt16();
            var price = reader.ReadSingle();
            reader.ReadUInt16(); // unknown

            var prices = new List<float>(num);
            for (int i = 0; i < num; i++)
            {
                prices.Add(reader.ReadSingle());
            }
            return new ChartSamplingResponse(market, code, price, prices);
        }
    }

    public class UnusualParser : BaseParser<List<UnusualEntry>>
    {
        public UnusualParser(Market market, uint start, ushort count = 600)
        {
            MsgId = 0x563;
            Body = new byte[10];
            using var writer = new BinaryWriter(new MemoryStream(Body));
            writer.Write((ushort)market);
            writer.Write(start);
            writer.Write(count);
        }

        public override List<UnusualEntry> Deserialize(byte[] responseBody)
        {
            var result = new List<UnusualEntry>();
            using var reader = new BinaryReader(new MemoryStream(responseBody));
            var gbk = Encoding.GetEncoding("GB18030");
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var slice = reader.ReadBytes(32);
                using var sliceReader = new BinaryReader(new MemoryStream(slice));

                var market = (Market)sliceReader.ReadUInt16();
                var code = gbk.GetString(sliceReader.ReadBytes(6)).TrimEnd('\0');
                sliceReader.ReadByte(); // unknown
                var typeId = sliceReader.ReadByte();
                sliceReader.ReadByte(); // unknown
                var index = sliceReader.ReadUInt16();
                sliceReader.ReadUInt16(); // unknown

                var (desc, val) = UnpackByType(typeId, slice, 15);

                var hour = slice[29];
                var minuteSec = BitConverter.ToUInt16(slice, 30);
                var time = $"{hour:D2}:{minuteSec / 100:D2}:{minuteSec % 100:D2}";

                result.Add(new UnusualEntry(index, market, code, time, desc, val));
            }
            return result;
        }

        private (string Description, string Value) UnpackByType(byte typeId, byte[] data, int offset)
        {
            using var reader = new BinaryReader(new MemoryStream(data, offset, data.Length - offset));
            var v1_byte = reader.ReadByte();
            var v2 = reader.ReadSingle();
            var v3 = reader.ReadSingle();
            var v4 = reader.ReadSingle();

            string desc = "", val = "";

            switch (typeId)
            {
                case 0x03:
                    desc = v1_byte == 0x00 ? "主力买入" : "主力卖出";
                    val = $"{v2:F2}/{v3:F2}";
                    break;
                case 0x04: desc = "加速拉升"; val = $"{v2 * 100:F2}%"; break;
                case 0x05: desc = "加速下跌"; break;
                case 0x06: desc = "低位反弹"; val = $"{v2 * 100:F2}%"; break;
                case 0x07: desc = "高位回落"; val = $"{v2 * 100:F2}%"; break;
                case 0x08: desc = "撑杆跳高"; val = $"{v2 * 100:F2}%"; break;
                case 0x09: desc = "平台跳水"; val = $"{v2 * 100:F2}%"; break;
                case 0x0a:
                    desc = "单笔冲" + (v2 < 0 ? "跌" : "涨");
                    val = $"{v2 * 100:F2}%";
                    break;
                case 0x0b:
                    desc = "区间放量";
                    val = $"{v2:F1}倍";
                    if (v3 == 0) desc += "平";
                    else desc += v3 < 0 ? "跌" : "涨";
                    if (v3 != 0) val += $"{v3 * 100:F2}%";
                    break;
                case 0x0c: desc = "区间缩量"; break;
                case 0x10: desc = "大单托盘"; val = $"{v4:F2}/{v3:F2}"; break;
                case 0x11: desc = "大单压盘"; val = $"{v2:F2}/{v3:F2}"; break;
                case 0x12: desc = "大单锁盘"; break;
                case 0x13: desc = "竞价试买"; val = $"{v2:F2}/{v3:F2}"; break;
                case 0x14:
                    var direction = v1_byte == 0x00 ? "涨" : "跌";
                    // Special unpacking for this type
                    var type_14 = data[offset + 1];
                    var v2_14 = BitConverter.ToSingle(data, offset + 2);
                    var v3_14 = BitConverter.ToSingle(data, offset + 6);
                    switch (type_14)
                    {
                        case 0x01: desc = $"逼近{direction}停"; break;
                        case 0x02: desc = $"封{direction}停板"; break;
                        case 0x04: desc = $"封{direction}大减"; break;
                        case 0x05: desc = $"打开{direction}停"; break;
                    }
                    val = $"{v2_14:F2}/{v3_14:F2}";
                    break;
                case 0x15:
                    desc = v1_byte switch { 0x01 => "尾盘对倒", 0x02 => "尾盘拉升", _ => "尾盘??" };
                    val = $"{v2 * 100:F2}%/{v3:F2}";
                    break;
                case 0x16:
                    desc = "盘中" + (v2 < 0 ? "弱势" : "强势");
                    val = $"{v2 * 100:F2}%";
                    break;
                case 0x1d: desc = "急速拉升"; val = $"{v2 * 100:F2}%"; break;
                case 0x1e: desc = "急速下跌"; val = $"{v2 * 100:F2}%"; break;
                default:
                    desc = $"未知类型 {typeId}";
                    break;
            }
            return (desc, val);
        }
    }
}
