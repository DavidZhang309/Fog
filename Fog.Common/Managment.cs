using System;
using System.Security.Cryptography;
using System.IO;
using System.Linq;
using System.Text;

using CoreFramework;

namespace Fog.Common
{
    public class FileManagment
    {
        private MD5CryptoServiceProvider hashProvider;

        public string StorePath { get; set; }
        public EntryTree Entries { get; private set; }

        public FileManagment(EntryTree tree)
        {
            Entries = tree;
            hashProvider = new MD5CryptoServiceProvider();
        }

        public bool CheckHash(string path)
        {
            FileEntry entry = Entries.GetFile(path);
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
            string[] pathData = EntryTree.GetPathData(path);
            EntryDirectoryNode pathNode = Entries.Navigate(pathData[0], true);
            return new FileEntry(path, hashProvider.ComputeHash(stream), timeOfUpdate);
        }
    }
}
