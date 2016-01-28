using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fog.Common.Extension
{
    public static class BinaryExtensions
    {
        public static string ToHexString(this byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "");
        }
        public static byte[] HexStringToArray(this string data)
        {
            byte[] dataArr = new byte[data.Length / 2];
            for (int i = 0; i < data.Length / 2; i++)
                dataArr[i] = Convert.ToByte(data.Substring(i * 2, 2), 16);
            return dataArr;
        }
    }
}
