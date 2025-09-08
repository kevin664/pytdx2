using System.Collections.Generic;

namespace Tdx.Core;

/// <summary>
/// Represents a TDX server host with its name, IP address, and port.
/// </summary>
public readonly record struct TdxHost(string Name, string Ip, int Port);

public enum Market
{
    /// <summary>
    /// Shenzhen Stock Exchange (深圳)
    /// </summary>
    SZ = 0,
    /// <summary>
    /// Shanghai Stock Exchange (上海)
    /// </summary>
    SH = 1,
    /// <summary>
    /// Beijing Stock Exchange (北京)
    /// </summary>
    BJ = 2
}

public enum Category
{
    /// <summary>
    /// Shanghai A-shares (上证A)
    /// </summary>
    SH = 0,
    /// <summary>
    /// Shenzhen A-shares (深证A)
    /// </summary>
    SZ = 2,
    /// <summary>
    /// All A-shares (A股)
    /// </summary>
    A = 6,
    /// <summary>
    /// All B-shares (B股)
    /// </summary>
    B = 7,
    /// <summary>
    /// STAR Market (科创板)
    /// </summary>
    KCB = 8,
    /// <summary>
    /// Beijing A-shares (北证A)
    /// </summary>
    BJ = 12,
    /// <summary>
    /// ChiNext (创业板)
    /// </summary>
    CYB = 14
}

public enum KlineType
{
    FiveMin = 0,
    FifteenMin = 1,
    ThirtyMin = 2,
    OneHour = 3,
    Daily = 4,
    Weekly = 5,
    Monthly = 6,
    Exhq1Min = 7,
    OneMin = 8,
    DayK = 9,
    ThreeMonth = 10,
    Yearly = 11
}

public enum BlockFileType
{
    Default,   // block.dat
    ZS,        // block_zs.dat
    FG,        // block_fg.dat
    GN,        // block_gn.dat
    TDXHY      // tdxhy.cfg
}

public static class BlockFileTypeExtensions
{
    public static string GetFileName(this BlockFileType type)
    {
        return type switch
        {
            BlockFileType.Default => "block.dat",
            BlockFileType.ZS => "block_zs.dat",
            BlockFileType.FG => "block_fg.dat",
            BlockFileType.GN => "block_gn.dat",
            BlockFileType.TDXHY => "tdxhy.cfg",
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Not expected block file type: {type}"),
        };
    }
}


public static class TdxConstants
{
    public static readonly List<TdxHost> TdxHosts = new()
    {
        new("通达信深圳双线主站1", "110.41.147.114", 7709),
        new("通达信深圳双线主站2", "110.41.2.72", 7709),
        new("通达信深圳双线主站3", "110.41.4.4", 7709),
        new("通达信深圳双线主站4", "47.113.94.204", 7709),
        new("通达信深圳双线主站5", "8.129.174.169", 7709),
        new("通达信深圳双线主站6", "110.41.154.219", 7709),
        new("通达信上海双线主站1", "124.70.176.52", 7709),
        new("通达信上海双线主站2", "47.100.236.28", 7709),
        new("通达信上海双线主站3", "123.60.186.45", 7709),
        new("通达信上海双线主站4", "123.60.164.122", 7709),
        new("通达信上海双线主站5", "47.116.105.28", 7709),
        new("通达信上海双线主站6", "124.70.199.56", 7709),
        new("通达信北京双线主站1", "121.36.54.217", 7709),
        new("通达信北京双线主站2", "121.36.81.195", 7709),
        new("通达信北京双线主站3", "123.249.15.60", 7709),
        new("通达信广州双线主站1", "124.71.85.110", 7709),
        new("通达信广州双线主站2", "139.9.51.18", 7709),
        new("通达信广州双线主站3", "139.159.239.163", 7709),
        new("通达信上海双线主站7", "106.14.201.131", 7709),
        new("通达信上海双线主站8", "106.14.190.242", 7709),
        new("通达信上海双线主站9", "121.36.225.169", 7709),
        new("通达信上海双线主站10", "123.60.70.228", 7709),
        new("通达信上海双线主站11", "123.60.73.44", 7709),
        new("通达信上海双线主站12", "124.70.133.119", 7709),
        new("通达信上海双线主站13", "124.71.187.72", 7709),
        new("通达信上海双线主站14", "124.71.187.122", 7709),
        new("通达信武汉电信主站1", "119.97.185.59", 7709),
        new("通达信深圳双线主站7", "47.107.64.168", 7709),
        new("通达信北京双线主站4", "124.70.75.113", 7709),
        new("通达信广州双线主站4", "124.71.9.153", 7709),
        new("通达信上海双线主站15", "123.60.84.66", 7709),
        new("通达信深圳双线主站8", "47.107.228.47", 7719),
        new("通达信北京双线主站5", "120.46.186.223", 7709),
        new("通达信北京双线主站6", "124.70.22.210", 7709),
        new("通达信北京双线主站7", "139.9.133.247", 7709),
        new("通达信广州双线主站5", "116.205.163.254", 7709),
        new("通达信广州双线主站6", "116.205.171.132", 7709),
        new("通达信广州双线主站7", "116.205.183.150", 7709),
    };

    public static readonly List<TdxHost> BrokerHosts = new()
    {
        new("上证云成都电信一", "218.6.170.47", 7709),
        new("上证云北京联通一", "123.125.108.14", 7709),
        new("上海电信主站Z1", "180.153.18.170", 7709),
        new("上海电信主站Z80", "180.153.18.172", 80),
        new("北京联通主站Z80", "202.108.253.139", 80),
        new("杭州电信主站J1", "60.191.117.167", 7709),
        new("杭州电信主站J2", "115.238.56.198", 7709),
        new("杭州电信主站J3", "218.75.126.9", 7709),
        new("杭州电信主站J4", "115.238.90.165", 7709),
        new("安信", "59.36.5.11", 7709),
        new("广发", "119.29.19.242", 7709),
        new("广发", "183.60.224.177", 7709),
        new("广发", "183.60.224.178", 7709),
        new("国泰君安", "117.34.114.13", 7709),
        new("国泰君安", "117.34.114.14", 7709),
        new("国泰君安", "117.34.114.15", 7709),
        new("国泰君安", "117.34.114.16", 7709),
        new("国泰君安", "117.34.114.17", 7709),
        new("国泰君安", "117.34.114.18", 7709),
        new("国泰君安", "117.34.114.20", 7709),
        new("国泰君安", "117.34.114.27", 7709),
        new("国泰君安", "117.34.114.30", 7709),
        new("国泰君安", "117.34.114.31", 7709),
        new("国信", "182.131.3.252", 7709),
        new("国信", "58.63.254.247", 7709),
        new("海通", "123.125.108.90", 7709),
        new("海通", "175.6.5.153", 7709),
        new("海通", "182.118.47.151", 7709),
        new("海通", "182.131.3.245", 7709),
        new("海通", "202.100.166.27", 7709),
        new("海通", "58.63.254.191", 7709),
        new("海通", "58.63.254.217", 7709),
        new("华林", "202.100.166.21", 7709),
        new("华林", "202.96.138.90", 7709),
    };
}
