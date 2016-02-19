using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.IO;
using Fog.Common.Extension;

namespace Fog.Common
{
    public abstract class BaseNodeConnector
    {
        protected CommunicationNode DestinationNode { get; private set; }

        public BaseNodeConnector(CommunicationNode node)
        {
            DestinationNode = node;
        }
    }

    public class DirectNodeConnector : BaseNodeConnector
    {
        public DirectNodeConnector(CommunicationNode destNode)
            : base (destNode)
        { }

        public Guid Register(string accessToken, string name)
        {
            return DestinationNode.Register(accessToken, name);
        }
        public Guid Add_Store(Guid token, string name)
        {
            return DestinationNode.AddStore(token, name);
        }
        public void CheckIn(Guid token, IPEndPoint endPoint)
        {
            DestinationNode.CheckIn(token, endPoint);
        }
        public OpTicket GetList(Guid storeId)
        {
            return DestinationNode.GetList(storeId);
        }
        public string RepairRequest(Guid token, Guid storeID, string path)
        {
            return DestinationNode.RepairRequest(token, storeID, path);
        }
        public void ReceiveOpTicket(OpTicket ticket)
        {
            DestinationNode.ReceiveOpTicket(ticket);
        }
        public byte[] GetFile(Guid opID)
        {
            return DestinationNode.GetFile(opID);
        }
    }
    public class HttpNodeConnector : BaseNodeConnector
    {
        private HttpListener Listener { get; set; }
        private WebClient Client { get; set; }

        public HttpNodeConnector(CommunicationNode node, int port, bool startListening)
            : base (node)
        {
            Client = new WebClient();
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://*:" + port + "/");

            if (startListening) Start();
        }

        public void Start()
        {
            Listener.Start();
            Listener.BeginGetContext(new AsyncCallback(HttpReceive), null);
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

        private void HttpReceive(IAsyncResult result)
        {
            HttpListenerContext webContext = Listener.EndGetContext(result);
            Listener.BeginGetContext(new AsyncCallback(HttpReceive), null);
            
            string rawToken = webContext.Request.QueryString["token"];
            Guid token = Guid.Empty;
            if (rawToken != null)
                token = rawToken.HexStringToGuid();

            switch (webContext.Request.Url.LocalPath)
            {
                case "/register":
                    string accessToken = webContext.Request.QueryString["access_token"];
                    string name = webContext.Request.QueryString["name"];
                    Guid resultToken = DestinationNode.Register(accessToken, name);
                    TryWrite(webContext.Response.OutputStream, resultToken.ToByteArray());
                    break;
                case "/add_store":
                    string storeName = webContext.Request.QueryString["name"];
                    Guid storeID = DestinationNode.AddStore(token, storeName);
                    TryWrite(webContext.Response.OutputStream, storeID.ToByteArray());
                    break;
                case "/checkin":
                    DestinationNode.CheckIn(token, webContext.Request.RemoteEndPoint);
                    break;
                case "/repair":
                    Guid store = webContext.Request.QueryString["store"].HexStringToGuid();
                    string path = webContext.Request.QueryString["path"];
                    string link = DestinationNode.RepairRequest(token, store, path);
                    TryWrite(webContext.Response.OutputStream, Encoding.UTF8.GetBytes(link));
                    break;
                case "/poll_list":
                    OpTicket ticket = DestinationNode.GetList(token);
                    TryWrite(webContext.Response.OutputStream, ticket.Serialize());
                    break;
                case "/op_ticket":
                    StreamReader reader = new StreamReader(webContext.Request.InputStream, webContext.Request.ContentEncoding);
                    DestinationNode.ReceiveOpTicket(new OpTicket(webContext.Request.ContentEncoding.GetBytes(reader.ReadToEnd())));
                    break;
                case "/file":
                    Guid opID = webContext.Request.QueryString["ticket"].HexStringToGuid();
                    TryWrite(webContext.Response.OutputStream, DestinationNode.GetFile(opID));
                    break;
                default:
                    webContext.Response.StatusCode = 404;
                    break;
            }
            webContext.Response.OutputStream.Close();
        }
    }
}
