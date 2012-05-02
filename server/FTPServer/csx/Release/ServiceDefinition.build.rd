<?xml version="1.0" encoding="utf-8"?>
<serviceModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" name="FTPServer" generation="1" functional="0" release="0" Id="c44352bd-b70c-49d9-a8e3-553881cf9476" dslVersion="1.2.0.0" xmlns="http://schemas.microsoft.com/dsltools/RDSM">
  <groups>
    <group name="FTPServerGroup" generation="1" functional="0" release="0">
      <componentports>
        <inPort name="AzureFTPServer_WorkerRole:AzureFTPServerEndPoint" protocol="tcp">
          <inToChannel>
            <lBChannelMoniker name="/FTPServer/FTPServerGroup/LB:AzureFTPServer_WorkerRole:AzureFTPServerEndPoint" />
          </inToChannel>
        </inPort>
        <inPort name="AzureFTPServer_WorkerRole:AzureFTPServerPassiveEndPointA" protocol="tcp">
          <inToChannel>
            <lBChannelMoniker name="/FTPServer/FTPServerGroup/LB:AzureFTPServer_WorkerRole:AzureFTPServerPassiveEndPointA" />
          </inToChannel>
        </inPort>
      </componentports>
      <settings>
        <aCS name="AzureFTPServer_WorkerRole:DataConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/FTPServer/FTPServerGroup/MapAzureFTPServer_WorkerRole:DataConnectionString" />
          </maps>
        </aCS>
        <aCS name="AzureFTPServer_WorkerRole:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/FTPServer/FTPServerGroup/MapAzureFTPServer_WorkerRole:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </maps>
        </aCS>
        <aCS name="AzureFTPServer_WorkerRoleInstances" defaultValue="[1,1,1]">
          <maps>
            <mapMoniker name="/FTPServer/FTPServerGroup/MapAzureFTPServer_WorkerRoleInstances" />
          </maps>
        </aCS>
      </settings>
      <channels>
        <lBChannel name="LB:AzureFTPServer_WorkerRole:AzureFTPServerEndPoint">
          <toPorts>
            <inPortMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRole/AzureFTPServerEndPoint" />
          </toPorts>
        </lBChannel>
        <lBChannel name="LB:AzureFTPServer_WorkerRole:AzureFTPServerPassiveEndPointA">
          <toPorts>
            <inPortMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRole/AzureFTPServerPassiveEndPointA" />
          </toPorts>
        </lBChannel>
      </channels>
      <maps>
        <map name="MapAzureFTPServer_WorkerRole:DataConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRole/DataConnectionString" />
          </setting>
        </map>
        <map name="MapAzureFTPServer_WorkerRole:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRole/Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </setting>
        </map>
        <map name="MapAzureFTPServer_WorkerRoleInstances" kind="Identity">
          <setting>
            <sCSPolicyIDMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRoleInstances" />
          </setting>
        </map>
      </maps>
      <components>
        <groupHascomponents>
          <role name="AzureFTPServer_WorkerRole" generation="1" functional="0" release="0" software="D:\workspace\visual studio\FTPServer\FTPServer\csx\Release\roles\AzureFTPServer_WorkerRole" entryPoint="base\x64\WaHostBootstrapper.exe" parameters="base\x64\WaWorkerHost.exe " memIndex="1792" hostingEnvironment="consoleroleadmin" hostingEnvironmentVersion="2">
            <componentports>
              <inPort name="AzureFTPServerEndPoint" protocol="tcp" portRanges="21" />
              <inPort name="AzureFTPServerPassiveEndPointA" protocol="tcp" portRanges="45000" />
            </componentports>
            <settings>
              <aCS name="DataConnectionString" defaultValue="" />
              <aCS name="Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="" />
              <aCS name="__ModelData" defaultValue="&lt;m role=&quot;AzureFTPServer_WorkerRole&quot; xmlns=&quot;urn:azure:m:v1&quot;&gt;&lt;r name=&quot;AzureFTPServer_WorkerRole&quot;&gt;&lt;e name=&quot;AzureFTPServerEndPoint&quot; /&gt;&lt;e name=&quot;AzureFTPServerPassiveEndPointA&quot; /&gt;&lt;/r&gt;&lt;/m&gt;" />
            </settings>
            <resourcereferences>
              <resourceReference name="DiagnosticStore" defaultAmount="[4096,4096,4096]" defaultSticky="true" kind="Directory" />
              <resourceReference name="EventStore" defaultAmount="[1000,1000,1000]" defaultSticky="false" kind="LogStore" />
            </resourcereferences>
          </role>
          <sCSPolicy>
            <sCSPolicyIDMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRoleInstances" />
            <sCSPolicyFaultDomainMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRoleFaultDomains" />
          </sCSPolicy>
        </groupHascomponents>
      </components>
      <sCSPolicy>
        <sCSPolicyFaultDomain name="AzureFTPServer_WorkerRoleFaultDomains" defaultPolicy="[2,2,2]" />
        <sCSPolicyID name="AzureFTPServer_WorkerRoleInstances" defaultPolicy="[1,1,1]" />
      </sCSPolicy>
    </group>
  </groups>
  <implements>
    <implementation Id="81bd1b83-ee50-438b-935e-522368c61a6a" ref="Microsoft.RedDog.Contract\ServiceContract\FTPServerContract@ServiceDefinition.build">
      <interfacereferences>
        <interfaceReference Id="6e15625f-b40e-41b1-81af-f5367ba9c910" ref="Microsoft.RedDog.Contract\Interface\AzureFTPServer_WorkerRole:AzureFTPServerEndPoint@ServiceDefinition.build">
          <inPort>
            <inPortMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRole:AzureFTPServerEndPoint" />
          </inPort>
        </interfaceReference>
        <interfaceReference Id="0006d9b4-1b6b-43bc-9d5f-b1e853d664c2" ref="Microsoft.RedDog.Contract\Interface\AzureFTPServer_WorkerRole:AzureFTPServerPassiveEndPointA@ServiceDefinition.build">
          <inPort>
            <inPortMoniker name="/FTPServer/FTPServerGroup/AzureFTPServer_WorkerRole:AzureFTPServerPassiveEndPointA" />
          </inPort>
        </interfaceReference>
      </interfacereferences>
    </implementation>
  </implements>
</serviceModel>