using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections.Generic;

using CoreFramework;
using Fog.Common;
using Fog.Common.Extension;

namespace Fog.Node.LocalNode
{
    class LocalNode : CommunicationNode
    {
        public FileManagment FileManager { get; private set; }
        public EntryTree Entries { get; private set; }

        public Guid ID { get; private set; }
        public string Name { get; private set; }
        public string Host { get; private set; }

        private Dictionary<Guid, OpTicket> TicketStore;

        public LocalNode(CommandConsole console)
            : base(console)
        {
            EventCommandValue storeDir = new EventCommandValue() { Value = ".\\FogContent\\" };
            storeDir.OnChange += new EventHandler<CmdValueEventArgs>((sender, eventArgs) => FileManager.StorePath = storeDir.Value);
            CmdConsole.RegisterCommand("store_dir", new CommandValue() { Value = ".\\FogContent\\" });
            CmdConsole.RegisterCommand("state_dir", new CommandValue() { Value = "./FogState/" });
            CmdConsole.RegisterCommand("register", new EventCommand(new Action<object, EventCmdArgs>(Register)));
            CmdConsole.RegisterCommand("poll_list", new EventCommand(new Action<object, EventCmdArgs>(PollList)));
            CmdConsole.RegisterCommand("list_entries", new EventCommand(new Action<object, EventCmdArgs>(ListEntries)));
            CmdConsole.RegisterCommand("validate", new EventCommand(new Action<object, EventCmdArgs>(Validate)));

            TicketStore = new Dictionary<Guid, OpTicket>();
            Entries = new EntryTree();
            FileManager = new FileManagment(Entries) { StorePath = storeDir.Value };
            Listener.Prefixes.Add("http://*:6681/");
            //Listener.Prefixes.Add("http://192.168.*:6675/");
            //Listener.Prefixes.Add("http://172.16.*:6675/");
            //Listener.Prefixes.Add("http://10.*:6675/");
        }

        public string StateDirectory
        {
            get { return ((CommandValue)CmdConsole.GetCommand("state_dir")).Value; }
        }
        public string StorePath
        {
            get { return ((CommandValue)CmdConsole.GetCommand("store_dir")).Value; }
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
        private void PollList(object sender, EventCmdArgs args)
        {
            PollList();
        }
        private void ListEntries(object sender, EventCmdArgs args)
        {
            StringBuilder builder = new StringBuilder();
            foreach (FileEntry entry in Entries.Entries)
                builder.AppendLine(entry.ToString());
            CmdConsole.Print(builder.ToString());
        }
        private void Validate(object sender, EventCmdArgs args)
        {
            Validate();
        }

        public OpTicket PollList()
        {
            CmdConsole.Print("Polling for entries");
            //get ticket
            OpTicket ticket = GetList(ID);

            string printout = "Ticket ID: " + ticket.OpID + "\nFile Entries:\n";
            foreach (FileEntry file in ticket.Files)
            {
                //log ticket data
                
                //TODO: update list
                Entries.AddFile(file);
            }
            return ticket;
        }
        public void Validate()
        {
            CmdConsole.Print("Store: " + StorePath);
            foreach (FileEntry entry in Entries.Entries)
            {
                CmdConsole.Print("Checking: " + entry.VirtualPath);
                //if (!File.Exists(FileManager.StorePath + entry.VirtualPath))
                if (!File.Exists(StorePath + entry.VirtualPath))
                {
                    RepairFile(entry);
                    break;
                }
                if (!FileManager.CheckHash(entry))
                    RepairFile(entry);
            }
        }
        private void RepairFile(FileEntry entry)
        {
            CmdConsole.Print("Sending repair request: " + entry.VirtualPath);
            string link = Client.DownloadString(Host + "repair?token=" + ID.ToHexString() + "&path=" + Uri.EscapeUriString(entry.VirtualPath));
            System.Threading.Thread.Sleep(1000);
            CmdConsole.Print("Downloading file link: " + link);
            Client.DownloadFile(link, FileManager.StorePath + entry.VirtualPath);
            CmdConsole.Print("Downloaded");
        }
        public void Checkin()
        {
            Client.DownloadData(Host + "checkin?token=" + ID.ToHexString());
        }

        public override void SaveState()
        {
            string path = StateDirectory;
            File.WriteAllText(path + "token.txt", Convert.ToBase64String(ID.ToByteArray()) + "\t" + Host + "\t" + Name);
            File.WriteAllText(path + "entries.txt", Entries.SaveToString());
        }
        public override void LoadState()
        {
            string path = StateDirectory;
            string[] data = File.ReadAllText(path + "token.txt").Split('\t');
            ID = new Guid(Convert.FromBase64String(data[0]));
            Host = data[1];
            Name = data[2];
            Entries.LoadFromString(File.ReadAllText(path + "entries.txt"));
        }
        //protected void RequestFile(string path)
        //{
        //    //Request for new file, central will return a ticket for file
        //    byte[] ticketData = Client.DownloadData(Host + "repair?file" + path);
        //    OpTicket ticket = new OpTicket();// = new OpTicket(ticketData);
        //}

        private OpTicket ReadTicket(Stream stream, Encoding enc)
        {
            StreamReader reader = new StreamReader(stream, enc);
            return new OpTicket(enc.GetBytes(reader.ReadToEnd()));
        }

        protected override void HttpReceive(HttpListenerContext context)
        {
            string req = context.Request.Url.LocalPath.Substring(1);
            CmdConsole.Print(VerboseTag.Info, "Request: " + context.Request.Url, true);
            OpTicket ticket;

            switch (req)
            {
                case "repairOp": //server giving info about upcoming request
                    CmdConsole.Print("Reading ticket");
                    ticket = ReadTicket(context.Request.InputStream, context.Request.ContentEncoding);
                    CmdConsole.Print("Storing ticket");
                    TicketStore.Add(ticket.OpID, ticket);
                    
                    break;
                case "repair": //another node getting file
                    Guid ticketId = context.Request.QueryString["ticket"].HexStringToGuid();
                    if (!TicketStore.ContainsKey(ticketId))
                    {
                        context.Response.StatusCode = 404;
                        break;
                    }
                    ticket = TicketStore[ticketId];
                    byte[] file = File.ReadAllBytes(FileManager.StorePath + ticket.Files[0].VirtualPath);
                    TryWrite(context.Response.OutputStream, file);
                    TicketStore.Remove(ticketId);
                    break;
            }
            
            context.Response.Close();
        }

        public override Guid Register(string accessToken, string name)
        {
            ID = new Guid(Client.DownloadData(Host + "register?access_token=" + accessToken + "&name=" + name));
            CmdConsole.Print("Registered - ID: " + ID.ToHexString());
            return ID;
        }
        public override OpTicket GetList(Guid token)
        {
            byte[] data = Client.DownloadData(Host + "get_list?token=" + ID.ToHexString());
            return new OpTicket(data);
        }
        public override string GetFile(Guid token, string path)
        {
            string link = Client.DownloadString(Host + "repair?token=" + token + "&path=" + Uri.EscapeUriString(path));
            return link;
        }
        public override void CheckIn(Guid token, IPEndPoint endPoint)
        {
            Client.DownloadData(Host + "checkin?token=" + token);
        }

        //public 
    }
}
