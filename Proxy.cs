﻿using System;
using System.IO.Pipes;
using System.Threading;

namespace KeePassHttpProxy
{
    class Proxy
    {
        static void Main(string[] args)
        {
            var proxy = new Proxy();
            proxy.Run();
        }

        private const string PipeName = "KeePassHttp";
        private const int ConnectTimeout = 5000;
        private NamedPipeClientStream _client;
        private Thread _clientThread;
        private bool _active;
        private readonly object _writeLock;

        private Proxy()
        {
            _writeLock = new object();
        }

        private void Run()
        {
            _active = true;

            try
            {
                _client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                _client.Connect(ConnectTimeout);
            }
            catch (Exception)
            {
                return;
            }

            _clientThread = new Thread(ClientReadThread)
            {
                Name = "ClientReadThread"
            };
            _clientThread.Start();

            do
            {
                var data = ConsoleRead();
                if (data != null)
                {
                    ClientWrite(data);
                }
                else
                {
                    _active = false;
                }
            } while (_active && _client.IsConnected);

            _client.Close();
            _clientThread.Join();
        }

        private void ClientWrite(byte[] data)
        {
            if (_active && _client.IsConnected)
            {
                var hdr = BitConverter.GetBytes(data.Length);
                _client.Write(hdr, 0, hdr.Length);
                _client.Write(data, 0, data.Length);
            }
        }

        private void ClientReadThread()
        {
            while (_active && _client.IsConnected)
            {
                var hdr = new byte[4];
                var bytes = _client.Read(hdr, 0, hdr.Length);
                if (bytes == hdr.Length)
                {
                    var dataLen = BitConverter.ToInt32(hdr, 0);
                    var data = new byte[dataLen];
                    bytes = _client.Read(data, 0, data.Length);
                    if (bytes == dataLen)
                    {
                        ConsoleWrite(data);
                    }
                }
            }
        }

        private byte[] ConsoleRead()
        {
            var stdin = Console.OpenStandardInput();
            var readBytes = new byte[4];
            var read = stdin.Read(readBytes, 0, readBytes.Length);
            if (read < readBytes.Length) return null;
            var length = BitConverter.ToInt32(readBytes, 0);
            var data = new byte[length];
            read = stdin.Read(data, 0, data.Length);
            if (read > 0)
            {
                if (read < data.Length)
                {
                    var newData = new byte[read];
                    Array.Copy(data, newData, newData.Length);
                    return newData;
                }
                return data;
            }
            return null;
        }

        private void ConsoleWrite(byte[] data)
        {
            lock (_writeLock)
            {
                var stdout = Console.OpenStandardOutput();
                var bytes = BitConverter.GetBytes(data.Length);
                stdout.Write(bytes, 0, bytes.Length);
                stdout.Write(data, 0, data.Length);
                stdout.Flush();
            }
        }
    }
}