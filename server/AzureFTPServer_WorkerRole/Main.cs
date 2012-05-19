using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;

namespace AzureFTPServer_WorkerRole
{
    public class Main : RoleEntryPoint
    {
        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.WriteLine("$projectname$ entry point called", "Information");

            try
            {
                _ftp.run();
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.TraceError(e.Message);
            }

            /* program never goes here */
            /*
            while (true)
            {
                Thread.Sleep(10000);
                Trace.WriteLine("Working", "Information");
            }
            */
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

  
            IPEndPoint port =
                RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["AzureFTPServerEndPoint"].IPEndpoint;
            _ftp = new FTPServer(port);
            IPEndPoint passivePort = null;
            passivePort = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["AzureFTPServerPassiveEndPointA"].IPEndpoint;
            _ftp.addPassivePort(passivePort);
            passivePort = RoleEnvironment.CurrentRoleInstance.InstanceEndpoints["AzureFTPServerPassiveEndPointB"].IPEndpoint;
            _ftp.addPassivePort(passivePort);

            return base.OnStart();
        }

        private IFileSystem _filesystem;
        private FTPServer _ftp;
    }
}
