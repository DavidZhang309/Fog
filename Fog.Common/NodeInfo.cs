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
        public EntryTree EntryTree { get; private set; }

        public NodeInfo(string name)
        {
            Name = name;
            TokenID = Guid.NewGuid();
            EntryTree = new EntryTree();
        }
        public NodeInfo(Guid token, string name)
        {
            Name = name;
            TokenID = token;
            EntryTree = new EntryTree();
        }

        public override string ToString()
        {
            return "[" + TokenID.ToHexString() + "] " + Name;
        }
    }
}
