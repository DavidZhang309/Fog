using System;
using System.Collections.Generic;
using System.Text;

using CoreFramework;

using System.IO;
using Fog.Common;

namespace Fog.Node.LocalNode
{
    public class Program
    {
        static bool Running = true;

        static void Quit(object sender, EventCmdArgs args)
        {
            Running = false;
        }

        public static void Main(string[] args)
        {
            CommandConsole console = new CommandConsole();
            LocalNode node = new LocalNode(console);
            HttpNodeConnector connector = new HttpNodeConnector(node, 6681, false);

            #region "Commands"
            console.RegisterCommand("help", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
                {
                    console.Print("List of commands: \n-" + string.Join("\n-", console.GetCommandList()));
                })));
            console.RegisterCommand("quit", new EventCommand(new Action<object, EventCmdArgs>(Quit)));
            console.RegisterCommand("checkin", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
                {
                    node.Checkin();
                })));
            #endregion

            connector.Start();
            Console.WriteLine("Started, Type 'help' for list of commands.");

            while (Running)
            {
                string input = Console.ReadLine();
                console.Call(input, false, true);
            }
            //deregister

        }
    }
}
