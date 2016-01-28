using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fog.Common.Extension
{
    public static class GuidExtensions
    {
        public static string ToHexString(this Guid id)
        {
            return id.ToByteArray().ToHexString();
        }
        
        public static Guid HexStringToGuid(this string id)
        {
            return new Guid(id.HexStringToArray());
        }
    }
}
