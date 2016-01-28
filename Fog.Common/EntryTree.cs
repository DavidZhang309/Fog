using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fog.Common
{
    public abstract class BaseEntry
    {
        private string virtualPath;
        public string VirtualName
        {
            get;
            protected set;
        }
        public string VirtualPath
        {
            get
            {
                return virtualPath;
            }
            protected set
            {
                string path = value.Replace('\\', '/');
                string[] paths = value.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                VirtualName = paths.Length == 0 ? "" : paths[paths.Length - 1];
                virtualPath = path;
            }
        } //Nesessary?
    }

    public class EntryDirectoryNode : BaseEntry
    {
        public Dictionary<string, EntryDirectoryNode> Directories { get; private set; }
        public Dictionary<string, FileEntry> Entries { get; private set; }

        public EntryDirectoryNode(string path)
        {
            VirtualPath = path;
            Directories = new Dictionary<string,EntryDirectoryNode>();
            Entries = new Dictionary<string,FileEntry>();
        }
        public void AddDirectory(string name)
        {
            string path = VirtualPath + name + "/";
            Directories.Add(name, new EntryDirectoryNode(path));
        }
        public void AddEntry(FileEntry entry)
        {
            string path = VirtualPath + entry.VirtualName;
            Entries.Add(entry.VirtualName, entry);
        }
    }

    public class FileEntry : BaseEntry
    {
        public byte[] VerifiedHash { get; private set; }
        public DateTime TimeOfUpdate { get; private set; }

        public FileEntry(string path, byte[] hash, DateTime updateTime)
        {
            VirtualPath = path;
            VerifiedHash = hash;
            TimeOfUpdate = updateTime;
        }
        public FileEntry(byte[] data)
        {
            TimeOfUpdate = DateTime.FromBinary(BitConverter.ToInt64(data, 0));
            VerifiedHash = new byte[data[8]];
            Array.Copy(data, 9, VerifiedHash, 0, VerifiedHash.Length);
            VirtualPath = Encoding.UTF8.GetString(data, 9 + VerifiedHash.Length, data.Length - 9 - VerifiedHash.Length);
        }
        public FileEntry(string line)
        {
            string[] data = line.Split('\t');
            TimeOfUpdate = DateTime.FromBinary(BitConverter.ToInt64(Convert.FromBase64String(data[0]), 0));
            VerifiedHash = Convert.FromBase64String(data[1]);
            VirtualPath = data[2];
        }

        public override string ToString()
        {
            return TimeOfUpdate + "\t" + BitConverter.ToString(VerifiedHash).Replace("-", "") + "\t" + VirtualPath;
        }
        //Only Serializes this node only(no children)
        public byte[] SerializeCurrent()
        {
            byte[] pathData = Encoding.UTF8.GetBytes(VirtualPath);
            byte[] result = new byte[VerifiedHash.Length + pathData.Length + 9];

            BitConverter.GetBytes(TimeOfUpdate.ToBinary()).CopyTo(result, 0);
            result[8] = (byte)VerifiedHash.Length;
            VerifiedHash.CopyTo(result, 9);
            pathData.CopyTo(result, 9 + VerifiedHash.Length);

            return result;
        }
        //for storing to disk in readable form
        public string ToLine()
        {
            string time = Convert.ToBase64String(BitConverter.GetBytes(TimeOfUpdate.ToBinary()));
            string hash = Convert.ToBase64String(VerifiedHash);
            return string.Format("{0}\t{1}\t{2}", time, hash, VirtualPath);
        }

        public override bool Equals(object obj)
        {
            FileEntry entry = obj as FileEntry;
            return obj == null ? false : (VirtualPath == entry.VirtualPath && TimeOfUpdate == entry.TimeOfUpdate && Enumerable.SequenceEqual(VerifiedHash, entry.VerifiedHash));
        }
    }

    public class EntryTree// : IEnumerator
    {
        private EntryDirectoryNode RootNode;
        private List<FileEntry> entries;

        public EntryTree()
        {
            RootNode = new EntryDirectoryNode("/");
            entries = new List<FileEntry>();
        }
        public static string[] GetPathData(string path)
        {
            int splitIndex = path.LastIndexOf('/');
            return new string[] { path.Substring(0, splitIndex), path.Substring(splitIndex + 1) };
        }

        public EntryDirectoryNode Navigate(string path, bool createDirectories)
        {
            path = path.Replace('\\', '/');
            string[] pathArr = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            string currentPath = "/";
            EntryDirectoryNode current = RootNode;
            foreach (string pathPart in pathArr)
            {
                currentPath += pathPart;
                if (current.Directories.ContainsKey(pathPart))
                    current = current.Directories[pathPart];
                else if (createDirectories)
                {
                    current.AddDirectory(pathPart);
                    current = current.Directories[pathPart];
                }
                else
                    return null;
            }
            return current;
        }
        public FileEntry GetFile(string path)
        {
            string[] pathData = GetPathData(path);
            EntryDirectoryNode dirNode = Navigate(pathData[0], true);
            if (dirNode.Entries.ContainsKey(pathData[1]))
                return dirNode.Entries[pathData[1]];
            else
                return null;
        }

        public void AddFile(FileEntry entry)
        {
            //Get Directory where entry exists
            string[] pathData = GetPathData(entry.VirtualPath);
            EntryDirectoryNode pathNode = Navigate(pathData[0], true);
            //check if entry exists
            if (pathNode.Entries.ContainsKey(pathData[1]))
            {
                FileEntry currentEntry = pathNode.Entries[pathData[1]];
                if (!currentEntry.Equals(entry))
                {
                    //TODO: Figure out what to do with a conflict
                }
            }
            else
            {
                //add new entry
                pathNode.Entries.Add(entry.VirtualName, entry);
                entries.Add(entry);
            }
        }
        public void DeleteEntry(FileEntry node)
        {

            entries.Remove(node);
        }
        public FileEntry[] Entries
        {
            get { return entries.ToArray(); }
        }

        public string SaveToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (FileEntry node in Entries)
                builder.AppendLine(node.ToLine());
            return builder.ToString();
        }
        public void LoadFromString(string data)
        {
            string[] lines = data.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
                AddFile(new FileEntry(line));
        }

        //public object Current
        //{
        //    get { throw new NotImplementedException(); }
        //}

        //public bool MoveNext()
        //{
        //    throw new NotImplementedException();
        //}

        //public void Reset()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
