using System;
using System.IO;
using System.Net;

using CoreFramework;

namespace Fog.Common
{
    public abstract class CommunicationNode
    {
        protected CommandConsole CmdConsole { get; private set; }
        protected WebClient Client { get; private set; }

        private AsyncCallback onRecvCallback;

        public CommunicationNode(CommandConsole console)
        {
            CmdConsole = console;
            CmdConsole.RegisterCommand("save_state", new EventCommand(new Action<object, EventCmdArgs>(SaveState)));//, "Usage: save_state\nSaves the state of the node"));
            CmdConsole.RegisterCommand("load_state", new EventCommand(new Action<object, EventCmdArgs>(LoadState)));//, "Usage: load_state\nLoads the state of the node"));

            Client = new WebClient();
        }
        protected bool TryWrite(Stream stream, byte[] data)
        {
            try
            {
                stream.Write(data, 0, data.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected virtual void SaveState(object sender, EventCmdArgs args)
        {
            SaveState();
        }
        protected virtual void LoadState(object sender, EventCmdArgs args)
        {
            LoadState();
        }

        public abstract void SaveState();
        public abstract void LoadState();

        public abstract Guid Register(string accessToken, string name);
        public abstract Guid AddStore(Guid token, string name);
        public abstract void CheckIn(Guid token, IPEndPoint endPoint);
        public abstract OpTicket GetList(Guid store);
        public abstract string RepairRequest(Guid token, Guid storeID, string path);
        public abstract void ReceiveOpTicket(OpTicket ticket);
        public abstract byte[] GetFile(Guid opID);
    }
}
