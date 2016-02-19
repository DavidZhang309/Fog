using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Fog.Common.Extension;

namespace Fog.Common
{
    public class NodeInfo
    {
        public string Name { get; private set; }
        public string Host { get; set; }
        public Guid TokenID { get; private set; }
        public Dictionary<Guid, FileStore> FileStores { get; private set; }

        public NodeInfo(string name)
        {
            Name = name;
            TokenID = Guid.NewGuid();
            FileStores = new Dictionary<Guid, FileStore>();
        }
        public NodeInfo(Guid token, string name)
        {
            Name = name;
            TokenID = token;
            FileStores = new Dictionary<Guid, FileStore>();
        }

        public override string ToString()
        {
            return "[" + TokenID.ToHexString() + "] " + Name;
        }
    }
}
