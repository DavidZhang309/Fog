using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;

using CoreFramework;
using Fog.Common;
using Fog.Common.Command;
using Fog.Common.Extension;

namespace Fog.Node.LocalNode
{
    class LocalNode : CommunicationNode
    {
        public List<FileStore> FileStores { get; private set; }

        public Guid ID { get; private set; }
        public string Name { get; private set; }
        public string Host { get; private set; }

        private Dictionary<Guid, OpTicket> TicketStore;

        public LocalNode(CommandConsole console)
            : base(console)
        {
            CmdConsole.RegisterCommand("state_dir", new CommandValue() { Value = "./FogState/" });
            CmdConsole.RegisterCommand("register", new EventCommand(new Action<object, EventCmdArgs>(Register)));
            CmdConsole.RegisterCommand("add_store", new EventCommand(new Action<object, EventCmdArgs>(AddStore)));
            CmdConsole.RegisterCommand("poll_list", new EventCommand(new Action<object, EventCmdArgs>(PollList)));
            CmdConsole.RegisterCommand("list_stores", new EventCommand(new Action<object, EventCmdArgs>(ListStores)));
            CmdConsole.RegisterCommand("list_store_entries", new EventCommand(new Action<object, EventCmdArgs>(ListStoreEntries)));
            CmdConsole.RegisterCommand("validate", new EventCommand(new Action<object, EventCmdArgs>(Validate)));

            TicketStore = new Dictionary<Guid, OpTicket>();
            FileStores = new List<FileStore>();
        }

        public string StateDirectory
        {
            get { return ((CommandValue)CmdConsole.GetCommand("state_dir")).Value; }
        }

        private void Register(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 3)
            {
                Host = "http://" + args.Arguments[0] + "/";
                Name = args.Arguments[2];
                Register(args.Arguments[1], Name);
            }
            else
            {
                args.ConsoleCaller.Print("Usage: register [host] [access_token] [name]");
            }
        }
        private void AddStore(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 2)
            {
                Guid storeID = AddStore(ID, args.Arguments[0]);
                FileStores.Add(new FileStore(storeID, args.Arguments[0], args.Arguments[1]));
            }
            else
                CmdConsole.Print("Usage: add_store [name] [physical_path]");
        }
        private void PollList(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 1)
                PollList(FileStores[Convert.ToInt32(args.Arguments[0])]);
            else
                CmdConsole.Print("Usage: poll_list [store]");
        }
        private void ListStores(object sender, EventCmdArgs args)
        {
            string printout = "List of Stores:\n";
            for (int i = 0; i < FileStores.Count; i++)
                printout += i + ". " + FileStores[i].Name + "\n";
            args.ConsoleCaller.Print(printout);
        }
        private void ListStoreEntries(object sender, EventCmdArgs args)
        {
            if (args.Arguments.Length == 1)
            {
                StringBuilder builder = new StringBuilder();
                foreach (FileEntry entry in FileStores[Convert.ToInt32(args.Arguments[0])].EntryTree.Entries)
                    builder.AppendLine(entry.ToString());
                CmdConsole.Print(builder.ToString());
            }
            else
                CmdConsole.Print("Usage: list_store_entries [store]");
        }
        private void Validate(object sender, EventCmdArgs args)
        {
            Validate();
        }

        public OpTicket PollList(FileStore store)
        {
            //Get Ticket
            OpTicket ticket = GetList(store.ID);

            string printout = "Ticket ID: " + ticket.OpID + "\nFile Entries:\n";
            foreach (FileEntry file in ticket.Files)
            {
                //TODO: log ticket data
                
                //TODO: update list
                store.EntryTree.ReplaceFile(file);
            }
            return ticket;
        }
        public void Validate()
        {
            foreach (FileStore store in FileStores)
            {
                CmdConsole.Print("Checking Store: " + store.Name);
                foreach (FileEntry entry in store.EntryTree.Entries)
                {
                    CmdConsole.Print("Checking: " + entry.VirtualPath);
                    if (!File.Exists(store.StorePath + entry.VirtualPath))
                    {
                        CmdConsole.Print("-> File doesn't exist, repairing");
                        RepairFile(store, entry);
                        continue;
                    }
                    if (!store.CheckHash(entry))
                    {
                        CmdConsole.Print("-> File is broken, repairing");
                        RepairFile(store, entry);
                    }
                }
            }
        }
        private void RepairFile(FileStore store, FileEntry entry)
        {
            //CmdConsole.Print("Sending repair request: " + entry.VirtualPath);
            string link = RepairRequest(ID, store.ID, entry.VirtualPath);
            System.Threading.Thread.Sleep(1000);
            //CmdConsole.Print("Downloading file link: " + link);
            Directory.CreateDirectory(store.StorePath + entry.VirtualPath.Substring(0, entry.VirtualPath.LastIndexOf("/")));
            Client.DownloadFile(link, store.StorePath + entry.VirtualPath);
            CmdConsole.Print("Downloaded");
        }
        public void Checkin()
        {
            Client.DownloadData(Host + "checkin?token=" + ID.ToHexString());
        }

        private FileStore GetStore(Guid storeID)
        {
            foreach (FileStore store in FileStores)
                if (store.ID == storeID) return store;
            return null;
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
        public override void SaveState()
        {
            string path = StateDirectory;
            File.WriteAllText(path + "token.txt", ID.ToByteArray().ToHexString() + "\t" + Host + "\t" + Name);
            //Save Stores
            string storePath = path + "Stores/";
            if (!Directory.Exists(storePath)) Directory.CreateDirectory(path);
            foreach (FileStore store in FileStores)
                File.WriteAllText(storePath + store.ID.ToHexString() + ".txt", StoreToString(store));           
                
        }
        public override void LoadState()
        {
            string[] data = File.ReadAllText(StateDirectory + "token.txt").Split('\t');
            ID = data[0].HexStringToGuid();
            Host = data[1];
            Name = data[2];
            if (Directory.Exists(StateDirectory + "Stores/"))
                foreach (string path in Directory.GetFiles(StateDirectory + "Stores/"))
                    FileStores.Add(StringToStore(File.ReadAllLines(path)));
        }
        //protected void RequestFile(string path)
        //{
        //    //Request for new file, central will return a ticket for file
        //    byte[] ticketData = Client.DownloadData(Host + "repair?file" + path);
        //    OpTicket ticket = new OpTicket();// = new OpTicket(ticketData);
        //}

        public override Guid Register(string accessToken, string name)
        {
            ID = new Guid(Client.DownloadData(Host + "register?access_token=" + accessToken + "&name=" + name));
            CmdConsole.Print("Registered - ID: " + ID.ToHexString());
            return ID;
        }
        public override Guid AddStore(Guid token, string name)
        {
            Guid storeID = new Guid(Client.DownloadData(Host + "add_store?token=" + ID.ToHexString() + "&name=" + name));
            CmdConsole.Print("Added Store - ID: " + storeID.ToHexString());
            return storeID;
        }
        public override OpTicket GetList(Guid store)
        {
            byte[] data = Client.DownloadData(Host + "poll_list?token=" + store.ToHexString());
            return new OpTicket(data);
        }
        public override string RepairRequest(Guid token, Guid storeID, string path)
        {
            return Client.DownloadString(Host + "repair?token=" + ID.ToHexString() + "&store=" + storeID.ToHexString() + "&path=" + Uri.EscapeUriString(path));
        }
        public override void CheckIn(Guid token, IPEndPoint endPoint)
        {
            Client.DownloadData(Host + "checkin?token=" + token.ToHexString());
        }
        public override void ReceiveOpTicket(OpTicket ticket)
        {
            //CmdConsole.Print("Got Ticket: " + ticket.OpID + "/" + ticket.OpID.ToHexString()); 
            TicketStore.Add(ticket.OpID, ticket);
        }
        public override byte[] GetFile(Guid opID)
        {
            OpTicket ticket = TicketStore[opID];
            TicketStore.Remove(opID);

            FileStore store = GetStore(ticket.StoreID);

            return File.ReadAllBytes(store.StorePath + ticket.Files[0].VirtualPath.Replace('/', '\\'));
        }
    }
}
