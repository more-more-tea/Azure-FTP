using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Reflection;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace AzureFTPServer_WorkerRole
{
    enum TransferMode { ASCII, BINARY, UNKNOWN };
    enum ConnectionMode { ACTIVE, POSITIVE, UNKNOWN };
    /******************************************
     * TODO
     *  REFACTOR can be made on this class
     *  a Strategy-Pattern is more proper to
     *  handle different commands(verbs).
     *  
     ******************************************/

    /*
     * simulate an FTP server.
     * by default, an FTP server has a root account(or we cannot
     * configure the system properly).
     * root's password is empty("") by default.
     */
    class FTPServer
    {
        /*
         * initialize a FTPServer with a reference to file system and
         * a port number.
         */
        public FTPServer(IFileSystem filesystem, IPEndPoint port)
        {
            this._filesystem = filesystem;
            this._port = port;
            this._passivePorts = new HashSet<IPEndPoint>();
        }

        public void addPassivePort(IPEndPoint port)
        {
            _passivePorts.Add(port);
        }

        public IPEndPoint acquirePassivePort()
        {
            if (_sem == null)
            {
                _sem = new Semaphore(_passivePorts.Count, _passivePorts.Count);
            }

            _sem.WaitOne();
            
            IPEndPoint available = _passivePorts.First();
            _passivePorts.Remove(available);

            return available;
        }

        public void releasePassivePort(IPEndPoint endPoint)
        {
            _passivePorts.Add(endPoint);
            _sem.Release();
        }

        /* server startup */
        public void run()
        {
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(_port);
                listener.ExclusiveAddressUse = false;
                listener.Start();
            }
            catch (SocketException e)
            {
                System.Diagnostics.Trace.TraceError("An error occurs when " +
                    "binding server-side socket {0}. {1}", _port, e.Message);

                return;
            }

            while (true)
            {
                /* block accept cooperate with multi-threads */
                TcpClient client = listener.AcceptTcpClient();
                FTPServerSlave slave = new FTPServerSlave(this, _accounts, _filesystem, client);
                Thread thread = new Thread(new ThreadStart(slave.run));
                thread.Start();
            }
        }

        private IFileSystem _filesystem;
        private Dictionary<string, string> _accounts;
        /*
         * account accepted by the ftp server,
         * will be flushed when server shut down.
         */
        private IPEndPoint _port;
        private HashSet<IPEndPoint> _passivePorts;
        private Semaphore _sem;
    }
}
