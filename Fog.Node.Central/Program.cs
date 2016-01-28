using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using CoreFramework;
using Fog.Common;

namespace Fog.Node.Central
{
    class Program
    {
        static void Main(string[] args)
        {
            bool running = true;
            CommandConsole console = new CommandConsole() { PrintTimestamp = true, VerboseLevel = VerboseTag.Info };
            CentralNode node = new CentralNode(console);
            HttpNodeConnector connector = new HttpNodeConnector(node, 6680, true);
            FileManagment fManager = new FileManagment(node.Entries) { StorePath = ".\\" };

            #region "Commands"
            //TODO: Package commands in a more elegant manner
            console.RegisterCommand("help", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {
                console.Print("List of commands: \n-" + string.Join("\n-", console.GetCommandList()));
            })));
            console.RegisterCommand("exec", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {
                if (eventArgs.Arguments.Length == 1)
                {
                    string[] lines = File.ReadAllLines(eventArgs.Arguments[0]);
                    foreach (string line in lines)
                        console.Call(line, true, true);
                }
                else
                {
                    console.Print("Usage: exec [file]"); 
                }
            })));
            console.RegisterCommand("quit", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {
                running = false;
            })));
            console.RegisterCommand("add_file", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {
                if (eventArgs.Arguments.Length == 2)
                {
                    string virtualPath = eventArgs.Arguments[0];
                    Stream s = File.OpenRead(eventArgs.Arguments[1]);
                    node.Entries.AddFile(fManager.CreateEntry(virtualPath, s, DateTime.Now));
                    s.Close();
                    console.Print("Added: " + virtualPath);
                }
                else
                {
                    console.Print("Usage: add_file [virtual_path] [physical_path]");
                }
            })));
            console.RegisterCommand("add_dir", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {
                if (eventArgs.Arguments.Length == 2)
                {
                    string virtualDir = eventArgs.Arguments[0];
                    string physicalDir = eventArgs.Arguments[1];
                    foreach (string path in Directory.GetFiles(physicalDir))
                    {
                        string name = Path.GetFileName(path);
                        console.Call("add_file \"" + virtualDir + name + "\" \"" + path + "\"", false, true);
                    }
                }
                else
                {
                    console.Print("Usage: add_dir [virtual_dir] [physical_dir]");
                }
            })));
            console.RegisterCommand("add_entry", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {

            })));
            console.RegisterCommand("permit_dir", new EventCommand(new Action<object, EventCmdArgs>((sender, eventArgs) =>
            {
                EntryDirectoryNode dirNode = node.Entries.Navigate(eventArgs.Arguments[1], false);
                if (dirNode == null)
                {
                    console.Print("directory does not exist");
                    return;
                }
                foreach (FileEntry entry in dirNode.Entries.Values)
                {
                    NodeInfo clientNode = node.Nodes[Convert.ToInt32(eventArgs.Arguments[0])];
                    clientNode.EntryTree.AddFile(entry);
                }
            })));
            #endregion

            //node.LoadState();
            console.Print(VerboseTag.Info, "Starting Service", true);
            //node.Start();
            console.Print(VerboseTag.Info, "Started, Type 'help' for list of commands.", true);

            while (running)
            {
                string input = Console.ReadLine();
                console.Call(input, false, true);
            }

            console.Print("Stopping...");
        }
    }
}
