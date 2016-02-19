using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using MySql.Data.MySqlClient;

using CoreFramework;
using Fog.Common;
using Fog.Common.Extension;

namespace Fog.Node.Central
{
    struct AvailableStore
    {
        public NodeInfo Node { get; private set; }
        public FileStore Store { get; private set; }

        public AvailableStore(NodeInfo node, FileStore store)
            : this()
        {
            Node = node;
            Store = store;
        }
    }

    class CentralNode : CommunicationNode
    {
        public MySqlConnection connection { get; set; }
        public EntryTree Entries { get; private set; } //universal entry set

        private Dictionary<Guid, NodeInfo> nodes;
        private List<NodeInfo> nodeList; //for console id use

        private Dictionary<Guid, FileStore> stores;
        private List<FileStore> storeList;

        public CentralNode(CommandConsole console)
            : base(console)
        {
            CmdConsole.RegisterCommand("allow_register", new CommandValue { Value = "1" });
            CmdConsole.RegisterCommand("access_token", new CommandValue() { Value = Guid.NewGuid().ToHexString() });
            CmdConsole.RegisterCommand("db_path", new CommandValue() { Value = "./FogData.sdf" });
            CmdConsole.RegisterCommand("state_dir", new CommandValue() { Value = "./FogState/" });
            CmdConsole.RegisterCommand("list_nodes", new EventCommand(new Action<object, EventCmdArgs>(ListNodes)));
            CmdConsole.RegisterCommand("list_stores", new EventCommand(new Action<object, EventCmdArgs>(ListStores)));
            CmdConsole.RegisterCommand("list_store_entries", new EventCommand(new Action<object, EventCmdArgs>(ListStoreEntries)));
            CmdConsole.RegisterCommand("list_entries", new EventCommand(new Action<object, EventCmdArgs>(ListEntries)));
            CmdConsole.RegisterCommand("permit_entry", new EventCommand(new Action<object, EventCmdArgs>(PermitEntry)));
            CmdConsole.RegisterCommand("revoke_entry", new EventCommand(new Action<object, EventCmdArgs>(RevokeEntry)));

            Entries = new EntryTree();
            stores = new Dictionary<Guid, FileStore>();
            storeList = new List<FileStore>();
            nodes = new Dictionary<Guid, NodeInfo>();
            nodeList = new List<NodeInfo>();
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
        public FileStore[] FileStores
        {
            get { return storeList.ToArray(); }
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
        private void ListStores(object sender, EventCmdArgs args)
        {
            string printout = "List of Stores:\n";
            for (int i = 0; i < storeList.Count; i++)
                printout += i + ". " + storeList[i].Name + "\n";
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
        //list store file entries
        private void ListStoreEntries(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 1)
            {
                int id = Convert.ToInt32(args.Arguments[0]);
                string printout = "File Entries:\n";

                foreach (FileEntry file in storeList[id].EntryTree.Entries)
                    printout += file.ToString() + "\n";
                args.ConsoleCaller.Print(printout);
            }
            else
            {
                CmdConsole.Print("Usage: list_store_entries [id]");
            }
        }
        //allow node access to file
        private void PermitEntry(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 2)
            {
                int id = Convert.ToInt32(args.Arguments[0]);
                if (id < 0 || id >= storeList.Count)
                {
                    CmdConsole.Print(VerboseTag.Warning, "Node ID doesn't exist", true);
                    return;
                }
                PermitEntry(storeList[id], args.Arguments[1]);
            }
            else
            {
                CmdConsole.Print("Usage: permit_entry [store] [virtual_path]");
            }
        }
        //disallow node access to file
        private void RevokeEntry(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 2)
            {
                int id = Convert.ToInt32(args.Arguments[0]);
                RevokeEntry(storeList[id], args.Arguments[1]);
            }
            else
            {
                CmdConsole.Print("Usage: revoke_entry [store] [virtual_path]");
            }
        }

        public void AddStore(FileStore store)
        {
            stores.Add(store.ID, store);
            storeList.Add(store);
        }
        public void AddNode(NodeInfo node)
        {
            nodes.Add(node.TokenID, node);
            nodeList.Add(node);
        }

        /// <summary>
        /// Gives specified node access to services to file entry
        /// </summary>
        /// <param name="node">Node being given access</param>
        /// <param name="entry">File entry path</param>
        /// <returns>If the entry is added</returns>
        public bool PermitEntry(FileStore store, string entry)
        {
            FileEntry fileNode = Entries.GetFile(entry);
            if (fileNode == null)
            {
                CmdConsole.Print(VerboseTag.Warning, "PermitEntry: entry '" + entry + "' does not exist", true);
                return false;
            }
            store.EntryTree.AddFile(fileNode);
            //TODO: Log action

            return true;
        }
        /// <summary>
        /// Revokes services for the specified Node on the specified file entry
        /// </summary>
        /// <param name="node">The node losing access</param>
        /// <param name="entry">File entry path</param>
        public bool RevokeEntry(FileStore store, string entry)
        {
            //Find Node
            FileEntry fileNode = store.EntryTree.GetFile(entry);
            if (fileNode == null)
            {
                CmdConsole.Print(VerboseTag.Warning, "RevokeEntry: entry '" + entry + "' does not exist in node", true);
                return false;
            }
            //Remove Node
            store.EntryTree.DeleteEntry(fileNode);
            //TODO: Log action
            
            return true;
        }

        //tmp: FileStore Serialization
        private string StoreToString(FileStore store)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine(store.ID.ToHexString() + "\t" + store.Name + "\t" + store.StorePath);
            foreach (FileEntry entry in store.EntryTree.Entries)
                builder.AppendLine(entry.ToLine());
            return builder.ToString();
        }
        private FileStore StringToStore(string[] lines)
        {
            string[] data = lines[0].Split('\t');
            FileStore store = new FileStore(data[0].HexStringToGuid(), data[1], data[2], new EntryTree());
            for (int i = 1; i < lines.Length; i++)
            {
                store.EntryTree.AddFile(new FileEntry(lines[i]));
            }
            return store;
        }
        //tmp: NodeInfo Serialization
        private string NodeToString(NodeInfo info)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine(info.TokenID.ToHexString() + "\t" + info.Name);
            foreach (Guid storeID in info.FileStores.Keys)
                builder.AppendLine(storeID.ToHexString());
            return builder.ToString();
        }
        private NodeInfo StringToNode(string[] lines)
        {
            string[] data = lines[0].Split('\t');
            NodeInfo node = new NodeInfo(data[0].HexStringToGuid(), data[1]);
            for (int i = 1; i < lines.Length; i++)
            {
                Guid storeID = lines[i].HexStringToGuid();
                node.FileStores.Add(storeID, stores[storeID]);
            }
            return node;
        }
        /// <summary>
        /// Saves the state of the node from 'state_dir' directory var in CommandConsole
        /// </summary>
        public override void SaveState()
        {
            if (!Directory.Exists(StateDirectory)) Directory.CreateDirectory(StateDirectory);
            //Save file entries
            File.WriteAllText(StateDirectory + "entries.txt", Entries.SaveToString());
            //DbAccess.AddEntries(connection, Entries.Entries.ToArray());
            //Save console vars
            File.WriteAllLines(StateDirectory + "state.cfg", new string[]{
                "access_token " + AccessToken,
                "state_dir " +  StateDirectory,
                "db_path " + DatabasePath,
                "allow_register " + (AllowRegistration ? "1" : "0")
            });

            //Save node data
            string storeDir = StateDirectory + "Stores/";
            string nodeDir = StateDirectory + "Nodes/";
            if (!Directory.Exists(storeDir)) Directory.CreateDirectory(storeDir);
            foreach (FileStore store in storeList)
                File.WriteAllText(storeDir + store.ID.ToHexString() + ".txt", StoreToString(store));
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
            //foreach (FileEntry entry in DbAccess.GetEntries(connection))
            //    Entries.AddFile(entry);

            if (Directory.Exists(StateDirectory + "Stores/"))
                foreach (string path in Directory.GetFiles(StateDirectory + "Stores/"))
                    AddStore(StringToStore(File.ReadAllLines(path)));
            //load nodes
            if (Directory.Exists(StateDirectory + "Nodes/"))
                foreach (string path in Directory.GetFiles(StateDirectory + "Nodes/"))
                    AddNode(StringToNode(File.ReadAllLines(path)));
            
            CmdConsole.Print(VerboseTag.Info, "State loaded", true);
        }

        private AvailableStore GetAvailableNode(Guid brokenStoreID, FileEntry entry)
        {
            foreach (NodeInfo node in nodeList)
            {
                foreach (FileStore store in storeList)
                {
                    if (brokenStoreID == store.ID) continue;
                    else if (store.EntryTree.GetFile(entry.VirtualPath) != null)
                        return new AvailableStore(node, store);
                }
            }
            return new AvailableStore();
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
            return node.TokenID;
        }
        public override Guid AddStore(Guid token, string name)
        {
            //TODO: checks

            FileStore newStore = new FileStore(Guid.NewGuid(), name, "");
            AddStore(newStore);
            nodes[token].FileStores.Add(newStore.ID, newStore);
            return newStore.ID;
        }
        public override void CheckIn(Guid token, IPEndPoint endPoint)
        {
            if (token.Equals(Guid.Empty)) return;
            nodes[token].Host = "http://" + endPoint.Address.ToString() + ":6681/";
        }
        //TODO: only allow access to store node
        public override OpTicket GetList(Guid storeID)
        {
            //TODO: checks

            FileStore store = stores[storeID];
            OpTicket ticket = new OpTicket(storeID, store.EntryTree.Entries);
            //TODO: log ticket

            return ticket;
        }
        public override string RepairRequest(Guid token, Guid storeID, string path)
        {
            if (token.Equals(Guid.Empty)) return "BAD TOKEN";
            //Guid guid = token.HexStringToGuid();
            FileEntry entry = Entries.GetFile(path);
            if (entry == null)
            {
                return "ENTRY NOT FOUND";
            }
            AvailableStore available = GetAvailableNode(storeID, entry);
            OpTicket ticket = new OpTicket(available.Store.ID, entry);
            byte[] data = ticket.Serialize();
            //give ticket to available node
            Client.UploadData(available.Node.Host + "op_ticket", data);
            //give link to download for node in need
            //TryWrite(context.Response.OutputStream, System.Text.Encoding.UTF8.GetBytes(node.Host + "/repair?ticket=" + ticket.OpID.ToHexString()));
            return available.Node.Host + "file?ticket=" + ticket.OpID.ToHexString();
            //TODO: log ticket
        }

        public override void ReceiveOpTicket(OpTicket ticket)
        {
            throw new NotImplementedException();
        }
        public override byte[] GetFile(Guid opID)
        {
            throw new NotImplementedException();
        }
    }
}
