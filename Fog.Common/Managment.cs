using System;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Text;

using CoreFramework;
using Fog.Common.Extension;

namespace Fog.Common
{
    public class FileStore
    {
        private MD5CryptoServiceProvider hashProvider;

        public Guid ID { get; private set; }
        public string Name { get; private set; }
        public string StorePath { get; set; }
        public EntryTree EntryTree { get; private set; }

        public FileStore(Guid id, string name, string storePath)
        {
            ID = id;
            Name = name;
            StorePath = storePath;
            EntryTree = new EntryTree();
            hashProvider = new MD5CryptoServiceProvider();
        }

        public FileStore(Guid id, string name, string storePath, EntryTree tree)
        {
            ID = id;
            Name = name;
            StorePath = storePath;
            EntryTree = tree;
            hashProvider = new MD5CryptoServiceProvider();
        }

        public bool CheckHash(string path)
        {
            FileEntry entry = EntryTree.GetFile(path);
            return CheckHash(entry);
        }
        public bool CheckHash(FileEntry entry)
        {
            Stream stream = File.OpenRead(StorePath + entry.VirtualPath);
            
            byte[] hash = hashProvider.ComputeHash(stream);
            stream.Close();
            return Enumerable.SequenceEqual(hash, entry.VerifiedHash);
        }

        public FileEntry CreateEntry(string path, Stream stream, DateTime timeOfUpdate)
        {
            return new FileEntry(path, hashProvider.ComputeHash(stream), timeOfUpdate);
        }
        public FileStoreEntry GetEntry(string path)
        {
            return new FileStoreEntry(ID, EntryTree.GetFile(path));
        }
    }
}
