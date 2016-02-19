using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fog.Common
{
    public enum OpTicketType { HashList, FileRepair }
    public class OpTicket
    {
        public OpTicketType TicketType { get; private set; }
        public DateTime TicketCreationTime { get; private set; }
        public Guid OpID { get; private set; }
        public Guid StoreID { get; private set; }
        public FileEntry[] Files { get; private set; }

        public OpTicket(Guid storeID, FileEntry[] files)
        {
            TicketType = OpTicketType.HashList;
            TicketCreationTime = DateTime.UtcNow;
            OpID = Guid.NewGuid();
            StoreID = storeID;
            Files = files;
        }

        public OpTicket(Guid storeID, FileEntry file)
        {
            TicketType = OpTicketType.FileRepair;
            TicketCreationTime = DateTime.UtcNow;
            OpID = Guid.NewGuid();
            StoreID = storeID;
            Files = new FileEntry[] { file };
        }

        /// <summary>
        /// Recreates an OpTicket by deserializing OpTicket data
        /// </summary>
        /// <param name="data">The serialized data</param>
        public OpTicket(byte[] data)
        {
            TicketType = (OpTicketType)data[0];
            byte[] guidData = new byte[16];
            Array.Copy(data, 1, guidData, 0, 16);
            OpID = new Guid(guidData);
            Array.Copy(data, 17, guidData, 0, 16);
            StoreID = new Guid(guidData);

            TicketCreationTime = DateTime.FromBinary(BitConverter.ToInt64(data, 33));
            Files = new FileEntry[data[41] * 256 + data[42]];
            int index = 43;
            for (int i = 0; i < Files.Length; i++)
            {
                byte[] fileData = new byte[data[index] * 256 + data[index + 1]];
                Array.Copy(data, index + 2, fileData, 0, fileData.Length);
                Files[i] = new FileEntry(fileData);
                index += fileData.Length + 2;
            }
        }

        /// <summary>
        /// Serializes this OpTicket for transmission
        /// </summary>
        /// <returns>The Serialized Data</returns>
        public byte[] Serialize()
        {
            int fileInfoCount = 0;
            byte[][] fileData = new byte[Files.Length][];
            for (int i = 0; i < Files.Length; i++)
            {
                fileData[i] = Files[i].SerializeCurrent();
                fileInfoCount += fileData[i].Length + 2;
            }

            byte[] result = new byte[43 + fileInfoCount];
            result[0] = (byte)TicketType;
            OpID.ToByteArray().CopyTo(result, 1);
            StoreID.ToByteArray().CopyTo(result, 17);
            BitConverter.GetBytes(TicketCreationTime.ToBinary()).CopyTo(result, 33);
            result[41] = (byte)(Files.Length / 256);
            result[42] = (byte)(Files.Length % 256);
            int index = 43;
            foreach (byte[] data in fileData)
            {
                result[index] = (byte)(data.Length / 256);
                result[index + 1] = (byte)(data.Length % 256);
                data.CopyTo(result, index + 2);
                index += data.Length + 2;
            }

            return result;
        }
    }
}
