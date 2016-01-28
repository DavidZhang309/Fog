using System;
using System.IO;
using System.Net;

using CoreFramework;

namespace Fog.Common
{
    public abstract class CommunicationNode
    {
        protected CommandConsole CmdConsole { get; private set; }
        protected HttpListener Listener { get; private set; }
        protected WebClient Client { get; private set; }

        private AsyncCallback onRecvCallback;

        public CommunicationNode(CommandConsole console)
        {
            CmdConsole = console;
            CmdConsole.RegisterCommand("save_state", new EventCommand(new Action<object, EventCmdArgs>(SaveState)));//, "Usage: save_state\nSaves the state of the node"));
            CmdConsole.RegisterCommand("load_state", new EventCommand(new Action<object, EventCmdArgs>(LoadState)));//, "Usage: load_state\nLoads the state of the node"));

            Listener = new HttpListener();
            Client = new WebClient();

            onRecvCallback = new AsyncCallback(OnReceive);
        }
        private void OnReceive(IAsyncResult result)
        {
            HttpListenerContext context = Listener.EndGetContext(result);
            Listener.BeginGetContext(onRecvCallback, null);

            try
            {
                HttpReceive(context);
            }
            catch (Exception e)
            {
                CmdConsole.Print(VerboseTag.Error, e.Message, true);
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
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

        protected abstract void HttpReceive(HttpListenerContext context);
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
        public virtual void Start()
        {
            Listener.Start();
            Listener.BeginGetContext(onRecvCallback, null);
        }

        /// <summary>
        /// Registration of Node
        /// </summary>
        /// <param name="accessToken"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public abstract Guid Register(string accessToken, string name);
        public abstract void CheckIn(Guid token, IPEndPoint endPoint);
        public abstract OpTicket GetList(Guid token); 
        public abstract string GetFile(Guid token, string path);
    
        //public abstract OpTicket RepairAction(
    }
}
