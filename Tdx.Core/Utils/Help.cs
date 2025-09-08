using System;

namespace Tdx.Core.Utils
{
    public static class Help
    {
        /// <summary>
        /// Determines the market for a given stock code.
        /// </summary>
        /// <param name="code">The stock code.</param>
        /// <returns>The market, or null if not found.</returns>
        public static Market? QueryMarket(string code)
        {
            if (code.StartsWith("50") || code.StartsWith("51") || code.StartsWith("60") || code.StartsWith("68") || code.StartsWith("90") || code.StartsWith("110") || code.StartsWith("113") || code.StartsWith("132") || code.StartsWith("204"))
            {
                return Market.SH;
            }

            if (code.StartsWith("00") || code.StartsWith("12") || code.StartsWith("13") || code.StartsWith("18") || code.StartsWith("15") || code.StartsWith("16") || code.StartsWith("18") || code.StartsWith("20") || code.StartsWith("30") || code.StartsWith("39") || code.StartsWith("115") || code.StartsWith("1318"))
            {
                return Market.SZ;
            }

            // These are broader checks, should come after more specific ones.
            if (code.StartsWith("5") || code.StartsWith("6") || code.StartsWith("7") || code.StartsWith("9"))
            {
                return Market.SH;
            }

            if (code.StartsWith("4") || code.StartsWith("8"))
            {
                return Market.BJ;
            }

            Console.WriteLine($"Unknown market for code: {code}"); // Simple logging
            return null;
        }

        /// <summary>
        /// Decodes a price from a custom variable-length integer format.
        /// </summary>
        /// <param name="data">The byte array.</param>
        /// <param name="pos">The starting position.</param>
        /// <returns>A tuple containing the decoded price and the new position in the array.</returns>
        public static (int Price, int NewPos) GetPrice(byte[] data, int pos)
        {
            int posByte = 6;
            byte bdata = data[pos];
            int intdata = bdata & 0x3F;
            bool sign = (bdata & 0x40) != 0;

            if ((bdata & 0x80) != 0)
            {
                while (true)
                {
                    pos++;
                    bdata = data[pos];
                    intdata += (bdata & 0x7F) << posByte;
                    posByte += 7;

                    if ((bdata & 0x80) == 0)
                    {
                        break;
                    }
                }
            }

            pos++;

            if (sign)
            {
                intdata = -intdata;
            }

            return (intdata, pos);
        }

        /// <summary>
        /// Converts an integer representation to a DateTime object.
        /// </summary>
        /// <param name="num">The integer to convert.</param>
        /// <param name="withTime">Whether the integer includes time information.</param>
        /// <returns>A DateTime object.</returns>
        public static DateTime ToDateTime(uint num, bool withTime = false)
        {
            int year, month, day, hour = 15, minute = 0;

            if (withTime)
            {
                uint zipData = num & 0xFFFF;
                year = (int)((zipData >> 11) + 2004);
                month = (int)((zipData & 0x7FF) / 100);
                day = (int)((zipData & 0x7FF) % 100);

                uint tminutes = num >> 16;
                hour = (int)(tminutes / 60);
                minute = (int)(tminutes % 60);
            }
            else
            {
                year = (int)(num / 10000);
                month = (int)((num % 10000) / 100);
                day = (int)(num % 100);
            }

            if (year > DateTime.Now.Year + 1) // Allow for next year
            {
                throw new ArgumentException("Year is too far in the future", nameof(num));
            }

            return new DateTime(year, month, day, hour, minute, 0);
        }

        /// <summary>
        /// Gets time (hour, minute) from a 2-byte little-endian unsigned short.
        /// </summary>
        /// <param name="buffer">The byte array.</param>
        /// <param name="pos">The starting position.</param>
        /// <returns>A tuple containing the hour, minute, and the new position.</returns>
        public static (int Hour, int Minute, int NewPos) GetTime(byte[] buffer, int pos)
        {
            ushort tminutes = BitConverter.ToUInt16(buffer, pos);
            int hour = tminutes / 60;
            int minute = tminutes % 60;
            return (hour, minute, pos + 2);
        }
    }
}
