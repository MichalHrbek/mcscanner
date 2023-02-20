using System;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace mcscanner
{
    class HostList
    {
        private int i;
        private byte[] ips;
        public HostList(byte[] ips, int start = 0) 
        {
            this.ips = ips;
            i = start * 6;
        }
        public (IPAddress, ushort) nextHost()
        {
            lock(this)
            {
                if (i >= ips.Length)
                {
                    return (IPAddress.None, 0);
                }
                var ip = new IPAddress(new Span<byte>(ips, i, 4));
                var port = BinaryPrimitives.ReadUInt16BigEndian(new Span<byte>(ips, i + 4, 2));
                i += 6;
                return (ip, port);
            }
        }
    }

    class ServerPing
    {
        public Queue<string> output = new();
        private HostList hostList;
        private int workThread = 0;
        private int waitms;
        public bool done = false;
        public int successful = 0;
        public int unsuccessful = 0;

        public ServerPing(HostList  hostList, int threadCount, int waitms)
        {
            for (int i = 0; i < threadCount; i++)
            {
                this.hostList = hostList;
                this.waitms = waitms;
                Thread thread1 = new Thread(new ThreadStart(start));
                thread1.Start();
                workThread++;
            }
        }

        void start()
        {
            
            while (true)
            {
                (IPAddress, ushort) host = hostList.nextHost();
                if (host.Item1 == IPAddress.None)
                {
                    if (Interlocked.Decrement(ref workThread) == 0)
                    {
                        Console.WriteLine("Scan Complete !!!");
                        done = true;
                    }
                    return;
                }
                var ip = host.Item1.ToString();
                var port = host.Item2;
                var response = "null";
                try
                {
                    response = getStatus(ip, port, waitms);
                }
                catch (Exception)
                {
                    unsuccessful++;
                }
                lock(output) {
                    output.Enqueue($"{ip}:{port};{response}");
                }
            }

        }

        string getStatus(string ip, ushort port, int waitms)
        {
            NetworkStream _stream;
            List<byte> _buffer;
            int _offset = 0;
            var client = new TcpClient();
            var task = client.ConnectAsync(ip, port).Wait(waitms);

            if (!client.Connected)
            {
                unsuccessful++;
                return "null";
            }

            _buffer = new List<byte>();
            _stream = client.GetStream();

            /*
             * Send a "Handshake" packet
             * http://wiki.vg/Server_List_Ping#Ping_Process
             */
            WriteVarInt(47);
            WriteString(ip);
            WriteUShort(port);
            WriteVarInt(1);
            Flush(0);

            /*
             * Send a "Status Request" packet
             * http://wiki.vg/Server_List_Ping#Ping_Process
             */
            Flush(0);

            var buffer = new byte[Int16.MaxValue];
            _stream.Read(buffer, 0, buffer.Length);

            try
            {
                var length = ReadVarInt(buffer);
                var packet = ReadVarInt(buffer);
                var jsonLength = ReadVarInt(buffer);
                var json = ReadString(buffer, jsonLength).Split("\"favicon\"")[0].Split("\"modinfo\"")[0].Replace("\0", "");
                successful++;
                return json;
            }
            catch (IOException ex)
            {
                /*
                 * If an IOException is thrown then the server didn't 
                 * send us a VarInt or sent us an invalid one.
                 */
                unsuccessful++;
                return "null";
            }
            #region Read/Write methods
            byte ReadByte(byte[] buffer)
            {
                var b = buffer[_offset];
                _offset += 1;
                return b;
            }

            byte[] Read(byte[] buffer, int length)
            {
                var data = new byte[length];
                Array.Copy(buffer, _offset, data, 0, length);
                _offset += length;
                return data;
            }

            int ReadVarInt(byte[] buffer)
            {
                var value = 0;
                var size = 0;
                int b;
                while (((b = ReadByte(buffer)) & 0x80) == 0x80)
                {
                    value |= (b & 0x7F) << (size++ * 7);
                    if (size > 5)
                    {
                        throw new IOException("This VarInt is an imposter!");
                    }
                }
                return value | ((b & 0x7F) << (size * 7));
            }

            string ReadString(byte[] buffer, int length)
            {
                var data = Read(buffer, length);
                return Encoding.UTF8.GetString(data);
            }

            void WriteVarInt(int value)
            {
                while ((value & 128) != 0)
                {
                    _buffer.Add((byte)(value & 127 | 128));
                    value = (int)((uint)value) >> 7;
                }
                _buffer.Add((byte)value);
            }

            void WriteShort(short value)
            {
                _buffer.AddRange(BitConverter.GetBytes(value));
            }

            void WriteUShort(ushort value)
            {
                _buffer.AddRange(BitConverter.GetBytes(value));
            }

            void WriteString(string data)
            {
                var buffer = Encoding.UTF8.GetBytes(data);
                WriteVarInt(buffer.Length);
                _buffer.AddRange(buffer);
            }

            void Write(byte b)
            {
                _stream.WriteByte(b);
            }

            void Flush(int id = -1)
            {
                var buffer = _buffer.ToArray();
                _buffer.Clear();

                var add = 0;
                var packetData = new[] { (byte)0x00 };
                if (id >= 0)
                {
                    WriteVarInt(id);
                    packetData = _buffer.ToArray();
                    add = packetData.Length;
                    _buffer.Clear();
                }

                WriteVarInt(buffer.Length + add);
                var bufferLength = _buffer.ToArray();
                _buffer.Clear();

                _stream.Write(bufferLength, 0, bufferLength.Length);
                _stream.Write(packetData, 0, packetData.Length);
                _stream.Write(buffer, 0, buffer.Length);
            }
            #endregion
        }
    }
}
