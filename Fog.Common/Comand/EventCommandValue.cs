using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CoreFramework;

namespace Fog.Common.Command
{
    public class CmdValueEventArgs : EventArgs
    {
        public string Value { get; set; }

        public CmdValueEventArgs(string value)
        {
            Value = value;
        }
    }

    public class EventCommandValue : ICommandHandler
    {
        public event EventHandler<CmdValueEventArgs> OnChange;

        public void SetCommand(CommandConsole console, string[] args)
        {
            if (args.Length == 0)
                console.Print("=" + Value);
            else
            {
                CmdValueEventArgs eventArg = new CmdValueEventArgs(args[0]);
                if (OnChange != null) OnChange(this, eventArg);
                Value = eventArg.Value;
            }
        }

        public string Value { get; set; }
    }
}
