using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

using CoreFramework;
using Fog.Common;
using Fog.Common.Extension;

namespace Fog.Node.Central
{
    class CentralNode : CommunicationNode
    {
        public EntryTree Entries { get; private set; }

        private Dictionary<Guid, NodeInfo> nodes;
        private List<NodeInfo> nodeList; //for console id use

        public CentralNode(CommandConsole console)
            : base(console)
        {
            CmdConsole.RegisterCommand("allow_register", new CommandValue { Value = "1" });
            CmdConsole.RegisterCommand("access_token", new CommandValue() { Value = Guid.NewGuid().ToHexString() });
            CmdConsole.RegisterCommand("db_path", new CommandValue() { Value = "./FogData.sdf" });
            CmdConsole.RegisterCommand("state_dir", new CommandValue() { Value = "./FogState/" });
            CmdConsole.RegisterCommand("list_nodes", new EventCommand(new Action<object, EventCmdArgs>(ListNodes)));
            CmdConsole.RegisterCommand("list_node_entries", new EventCommand(new Action<object, EventCmdArgs>(ListNodeEntries)));
            CmdConsole.RegisterCommand("list_entries", new EventCommand(new Action<object, EventCmdArgs>(ListEntries)));
            CmdConsole.RegisterCommand("permit_entry", new EventCommand(new Action<object, EventCmdArgs>(PermitEntry)));
            CmdConsole.RegisterCommand("revoke_entry", new EventCommand(new Action<object, EventCmdArgs>(RevokeEntry)));

            Entries = new EntryTree();
            nodes = new Dictionary<Guid, NodeInfo>();
            nodeList = new List<NodeInfo>();
            Listener.Prefixes.Add("http://*:6680/");
        }

        public NodeInfo[] Nodes
        {
            get
            {
                return nodes.Values.ToArray();
            }
        }
        public string DatabasePath
        {
            get { return ((CommandValue)CmdConsole.GetCommand("db_path")).Value; }
        }
        public string AccessToken
        {
            get { return ((CommandValue)CmdConsole.GetCommand("access_token")).Value; }
        }
        public string StateDirectory
        {
            get { return ((CommandValue)CmdConsole.GetCommand("state_dir")).Value; }
        }
        public bool AllowRegistration
        {
            get
            {
                try
                {
                    int val = Convert.ToInt32(((CommandValue)CmdConsole.GetCommand("allow_register")).Value);
                    return val == 1;
                }
                catch (FormatException)
                {
                    //CmdConsole.Print(
                    return false;
                }
            }
        }
        
        //Console Commands
        //list all registered nodes
        private void ListNodes(object sender, EventCmdArgs args)
        {
            string printout = "List of Nodes:\n";
            for (int i = 0; i < nodeList.Count; i++)
                printout += i + ". " + nodeList[i].ToString() + "\n";
            args.ConsoleCaller.Print(printout);
        }
        //list all file entries on console
        private void ListEntries(object sender, EventCmdArgs args)
        {
            string printout = "File Entries:\n";
            foreach (FileEntry file in Entries.Entries)
                printout += file.ToString() + "\n";
            args.ConsoleCaller.Print(printout);
        }
        //list node file entries
        private void ListNodeEntries(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 1)
            {
                int id = Convert.ToInt32(args.Arguments[0]);
                string printout = "File Entries:\n";

                foreach (FileEntry file in nodeList[id].EntryTree.Entries)
                    printout += file.ToString() + "\n";
                args.ConsoleCaller.Print(printout);
            }
            else
            {
                CmdConsole.Print("Usage: revoke_entry [node] [virtual_path]");
            }
        }
        //allow node access to file
        private void PermitEntry(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 2)
            {
                int id = Convert.ToInt32(args.Arguments[0]);
                if (id < 0 || id >= nodeList.Count)
                {
                    CmdConsole.Print(VerboseTag.Warning, "Node ID doesn't exist", true);
                    return;
                }
                PermitEntry(nodeList[id], args.Arguments[1]);
            }
            else
            {
                CmdConsole.Print("Usage: permit_entry [node] [virtual_path]");
            }
        }
        //disallow node access to file
        private void RevokeEntry(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 2)
            {
                int id = Convert.ToInt32(args.Arguments[0]);
                RevokeEntry(nodeList[id], args.Arguments[1]);
            }
            else
            {
                CmdConsole.Print("Usage: revoke_entry [node] [virtual_path]");
            }
        }

        //tmp: convert file to NodeInfo
        private NodeInfo StringToNode(string[] lines)
        {
            string[] data = lines[0].Split('\t');
            NodeInfo node = new NodeInfo(new Guid(Convert.FromBase64String(data[0])), data[1]);
            for (int i = 1; i < lines.Length; i++)
            {
                node.EntryTree.AddFile(new FileEntry(lines[i]));
            }
            return node;
        }
        //tmp: convert NodeInfo into string file
        private string NodeToString(NodeInfo info)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine(Convert.ToBase64String(info.TokenID.ToByteArray()) + "\t" + info.Name);
            foreach (FileEntry file in info.EntryTree.Entries)
                builder.AppendLine(file.ToLine());
            return builder.ToString();
        }
      
        /// <summary>
        /// Gives specified node access to services to file entry
        /// </summary>
        /// <param name="node">Node being given access</param>
        /// <param name="entry">File entry path</param>
        /// <returns>If the entry is added</returns>
        public bool PermitEntry(NodeInfo node, string entry)
        {
            FileEntry fileNode = Entries.GetFile(entry);
            if (fileNode == null)
            {
                CmdConsole.Print(VerboseTag.Warning, "PermitEntry: entry '" + entry + "' does not exist", true);
                return false;
            }
            node.EntryTree.AddFile(fileNode);
            //TODO: Log action

            return true;
        }
        /// <summary>
        /// Revokes services for the specified Node on the specified file entry
        /// </summary>
        /// <param name="node">The node losing access</param>
        /// <param name="entry">File entry path</param>
        public bool RevokeEntry(NodeInfo node, string entry)
        {
            //Find Node
            FileEntry fileNode = node.EntryTree.GetFile(entry);
            if (fileNode == null)
            {
                CmdConsole.Print(VerboseTag.Warning, "RevokeEntry: entry '" + entry + "' does not exist in node", true);
                return false;
            }
            //Remove Node
            node.EntryTree.DeleteEntry(fileNode);
            //TODO: Log action
            
            return true;
        }
        /// <summary>
        /// Saves the state of the node from 'state_dir' directory var in CommandConsole
        /// </summary>
        public override void SaveState()
        {
            if (!Directory.Exists(StateDirectory)) Directory.CreateDirectory(StateDirectory);
            //Save file entries
            File.WriteAllText(StateDirectory + "entries.txt", Entries.SaveToString());
            //Save console vars
            File.WriteAllLines(StateDirectory + "state.cfg", new string[]{
                "access_token " + AccessToken,
                "state_dir " +  StateDirectory,
                "db_path " + DatabasePath,
                "allow_register " + (AllowRegistration ? "1" : "0")
            });

            //Save node data
            string nodeDir = StateDirectory + "Nodes/";
            if (!Directory.Exists(nodeDir)) Directory.CreateDirectory(nodeDir);

            foreach (NodeInfo node in nodeList)
                File.WriteAllText(nodeDir + node.TokenID.ToHexString() + ".txt", NodeToString(node));
            CmdConsole.Print(VerboseTag.Info, "State saved", true);
        }
        /// <summary>
        /// Loads the state of the node from 'state_dir' directory var in CommandConsole
        /// </summary>
        public override void LoadState()
        {
            //execute state.cfg
            if (File.Exists(StateDirectory + "state.cfg"))
                CmdConsole.Call("exec " + StateDirectory + "state.cfg", true, true);
            //load entries
            Entries.LoadFromString(File.ReadAllText(StateDirectory + "entries.txt"));
            //load nodes
            if (Directory.Exists(StateDirectory + "Nodes/"))
                foreach (string path in Directory.GetFiles(StateDirectory + "Nodes/"))
                {
                    NodeInfo node = StringToNode(File.ReadAllLines(path));
                    nodeList.Add(node);
                    nodes.Add(node.TokenID, node);
                }
            CmdConsole.Print(VerboseTag.Info, "State loaded", true);
        }

        protected override void HttpReceive(HttpListenerContext context)
        {
            Console.WriteLine(context.Request.RawUrl);
            string req = context.Request.Url.LocalPath.Substring(1);
            string token = context.Request.QueryString["token"];
            Guid tokenObj = new Guid();
            NodeInfo node;

            switch (req)
            {
                case "register":
                    string accessToken = context.Request.QueryString["access_token"]; 
                    if (AllowRegistration && accessToken == null || accessToken != AccessToken)
                    {
                        context.Response.StatusCode = 401;
                        break;
                    }
                    string clientName = context.Request.QueryString["name"];
                    if (clientName == null)
                    {
                        context.Response.StatusCode = 400;
                        break;
                    }
                    node = new NodeInfo(clientName);
                    nodes.Add(node.TokenID, node);
                    nodeList.Add(node);
                    TryWrite(context.Response.OutputStream, node.TokenID.ToByteArray());
                    Console.WriteLine("Node: {0} ({1}) registered", node.TokenID.ToHexString(), clientName);
                    break;
                case "checkin":
                    if (token == null) break;
                    tokenObj = token.HexStringToGuid();

                    nodes[tokenObj].Host = "http://" + context.Request.RemoteEndPoint.Address.ToString() + ":6681/";
                    break;
                case "get_list":
                    if (token == null) break;
                    tokenObj = token.HexStringToGuid();
                    if (!nodes.ContainsKey(tokenObj))
                    {
                        CmdConsole.Print(VerboseTag.Warning, context.Request.UserHostAddress + " tried to access with invalid token: " + tokenObj.ToHexString(), true);
                        context.Response.StatusCode = 401;
                        break;
                    }
                    NodeInfo clientNode = nodes[tokenObj];
                    OpTicket ticket = new OpTicket(clientNode.EntryTree.Entries);
                    //TODO: log ticket
                    
                    TryWrite(context.Response.OutputStream, ticket.Serialize());
                    break;
                case "repair":
                    if (token == null) break;
                    Guid guid = token.HexStringToGuid();
                    string brokenPath = context.Request.QueryString["path"];
                    FileEntry entry = Entries.GetFile(brokenPath);
                    if (entry == null)
                    {
                        context.Response.StatusCode = 404;
                        break;
                    }
                    node = GetAvailableNode(nodes[guid], entry);
                    ticket = new OpTicket(entry);
                    byte[] data = ticket.Serialize();
                    //give ticket to available node
                    Client.UploadData(node.Host + "repairOp", data);
                    //give link to download for node in need
                    TryWrite(context.Response.OutputStream, System.Text.Encoding.UTF8.GetBytes(node.Host + "/repair?ticket=" + ticket.OpID.ToHexString()));
                    //TODO: log ticket

                    break;
                default:
                    context.Response.StatusCode = 404;
                    break;
            }
            context.Response.OutputStream.Close();
        }

        private NodeInfo GetAvailableNode(NodeInfo requester, FileEntry entry)
        {
            foreach (NodeInfo node in nodeList)
            {
                if (requester.TokenID != node.TokenID && node.EntryTree.GetFile(entry.VirtualPath) != null)
                    return node;
            }
            return null;
        }
        public override Guid Register(string accessToken, string name)
        {
            if (AllowRegistration && accessToken == null || accessToken != AccessToken)
                return Guid.Empty;
            if (name == null)
                return Guid.Empty;
            NodeInfo node = new NodeInfo(name);
            nodes.Add(node.TokenID, node);
            nodeList.Add(node);
            Console.WriteLine("Node: {0} ({1}) registered", node.TokenID.ToHexString(), name);
            //TryWrite(context.Response.OutputStream, node.TokenID.ToByteArray());
            return node.TokenID;
        }
        public override void CheckIn(Guid token, IPEndPoint endPoint)
        {
            if (token.Equals(Guid.Empty)) return;
            //nodes[token].Host = "http://" + endPoint.Address.ToString() + ":6681/";
        }
        public override OpTicket GetList(Guid token)
        {
            if (token.Equals(Guid.Empty)) return null;
            if (!nodes.ContainsKey(token))
            {
                //CmdConsole.Print(VerboseTag.Warning, context.Request.UserHostAddress + " tried to access with invalid token: " + tokenObj.ToHexString(), true);
                //context.Response.StatusCode = 401;
                return null;
            }
            NodeInfo clientNode = nodes[token];
            OpTicket ticket = new OpTicket(clientNode.EntryTree.Entries);
            //TODO: log ticket

            //TryWrite(context.Response.OutputStream, ticket.Serialize());
            return ticket;
        }
        public override string GetFile(Guid token, string path)
        {
            if (token.Equals(Guid.Empty)) return "BAD TOKEN";
            //Guid guid = token.HexStringToGuid();
            FileEntry entry = Entries.GetFile(path);
            if (entry == null)
            {
                return "ENTRY NOT FOUND";
            }
            NodeInfo node = GetAvailableNode(nodes[token], entry);
            OpTicket ticket = new OpTicket(entry);
            byte[] data = ticket.Serialize();
            //give ticket to available node
            Client.UploadData(node.Host + "repairOp", data);
            //give link to download for node in need
            //TryWrite(context.Response.OutputStream, System.Text.Encoding.UTF8.GetBytes(node.Host + "/repair?ticket=" + ticket.OpID.ToHexString()));
            return node.Host + "/repair?ticket=" + ticket.OpID.ToHexString();
            //TODO: log ticket
        }
    }
}
