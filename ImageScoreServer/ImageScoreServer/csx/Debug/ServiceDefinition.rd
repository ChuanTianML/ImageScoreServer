<?xml version="1.0" encoding="utf-8"?>
<serviceModel xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema" name="ImageScoreServer" generation="1" functional="0" release="0" Id="d3695543-8270-49b6-898b-f05f26676cdb" dslVersion="1.2.0.0" xmlns="http://schemas.microsoft.com/dsltools/RDSM">
  <groups>
    <group name="ImageScoreServerGroup" generation="1" functional="0" release="0">
      <componentports>
        <inPort name="ImageScoreWeb:Endpoint1" protocol="http">
          <inToChannel>
            <lBChannelMoniker name="/ImageScoreServer/ImageScoreServerGroup/LB:ImageScoreWeb:Endpoint1" />
          </inToChannel>
        </inPort>
      </componentports>
      <settings>
        <aCS name="ImageScoreWeb:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWeb:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </maps>
        </aCS>
        <aCS name="ImageScoreWeb:StorageConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWeb:StorageConnectionString" />
          </maps>
        </aCS>
        <aCS name="ImageScoreWebInstances" defaultValue="[1,1,1]">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWebInstances" />
          </maps>
        </aCS>
        <aCS name="ImageScoreWorker:ImageScoreDbConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWorker:ImageScoreDbConnectionString" />
          </maps>
        </aCS>
        <aCS name="ImageScoreWorker:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWorker:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </maps>
        </aCS>
        <aCS name="ImageScoreWorker:StorageConnectionString" defaultValue="">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWorker:StorageConnectionString" />
          </maps>
        </aCS>
        <aCS name="ImageScoreWorkerInstances" defaultValue="[1,1,1]">
          <maps>
            <mapMoniker name="/ImageScoreServer/ImageScoreServerGroup/MapImageScoreWorkerInstances" />
          </maps>
        </aCS>
      </settings>
      <channels>
        <lBChannel name="LB:ImageScoreWeb:Endpoint1">
          <toPorts>
            <inPortMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWeb/Endpoint1" />
          </toPorts>
        </lBChannel>
      </channels>
      <maps>
        <map name="MapImageScoreWeb:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWeb/Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </setting>
        </map>
        <map name="MapImageScoreWeb:StorageConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWeb/StorageConnectionString" />
          </setting>
        </map>
        <map name="MapImageScoreWebInstances" kind="Identity">
          <setting>
            <sCSPolicyIDMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWebInstances" />
          </setting>
        </map>
        <map name="MapImageScoreWorker:ImageScoreDbConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorker/ImageScoreDbConnectionString" />
          </setting>
        </map>
        <map name="MapImageScoreWorker:Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorker/Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" />
          </setting>
        </map>
        <map name="MapImageScoreWorker:StorageConnectionString" kind="Identity">
          <setting>
            <aCSMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorker/StorageConnectionString" />
          </setting>
        </map>
        <map name="MapImageScoreWorkerInstances" kind="Identity">
          <setting>
            <sCSPolicyIDMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorkerInstances" />
          </setting>
        </map>
      </maps>
      <components>
        <groupHascomponents>
          <role name="ImageScoreWeb" generation="1" functional="0" release="0" software="D:\work\imagescore\server3\ImageScoreServer\ImageScoreServer\csx\Debug\roles\ImageScoreWeb" entryPoint="base\x64\WaHostBootstrapper.exe" parameters="base\x64\WaIISHost.exe " memIndex="-1" hostingEnvironment="frontendadmin" hostingEnvironmentVersion="2">
            <componentports>
              <inPort name="Endpoint1" protocol="http" portRanges="80" />
            </componentports>
            <settings>
              <aCS name="Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="" />
              <aCS name="StorageConnectionString" defaultValue="" />
              <aCS name="__ModelData" defaultValue="&lt;m role=&quot;ImageScoreWeb&quot; xmlns=&quot;urn:azure:m:v1&quot;&gt;&lt;r name=&quot;ImageScoreWeb&quot;&gt;&lt;e name=&quot;Endpoint1&quot; /&gt;&lt;/r&gt;&lt;r name=&quot;ImageScoreWorker&quot; /&gt;&lt;/m&gt;" />
            </settings>
            <resourcereferences>
              <resourceReference name="DiagnosticStore" defaultAmount="[4096,4096,4096]" defaultSticky="true" kind="Directory" />
              <resourceReference name="EventStore" defaultAmount="[1000,1000,1000]" defaultSticky="false" kind="LogStore" />
            </resourcereferences>
          </role>
          <sCSPolicy>
            <sCSPolicyIDMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWebInstances" />
            <sCSPolicyUpdateDomainMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWebUpgradeDomains" />
            <sCSPolicyFaultDomainMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWebFaultDomains" />
          </sCSPolicy>
        </groupHascomponents>
        <groupHascomponents>
          <role name="ImageScoreWorker" generation="1" functional="0" release="0" software="D:\work\imagescore\server3\ImageScoreServer\ImageScoreServer\csx\Debug\roles\ImageScoreWorker" entryPoint="base\x64\WaHostBootstrapper.exe" parameters="base\x64\WaWorkerHost.exe " memIndex="-1" hostingEnvironment="consoleroleadmin" hostingEnvironmentVersion="2">
            <settings>
              <aCS name="ImageScoreDbConnectionString" defaultValue="" />
              <aCS name="Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString" defaultValue="" />
              <aCS name="StorageConnectionString" defaultValue="" />
              <aCS name="__ModelData" defaultValue="&lt;m role=&quot;ImageScoreWorker&quot; xmlns=&quot;urn:azure:m:v1&quot;&gt;&lt;r name=&quot;ImageScoreWeb&quot;&gt;&lt;e name=&quot;Endpoint1&quot; /&gt;&lt;/r&gt;&lt;r name=&quot;ImageScoreWorker&quot; /&gt;&lt;/m&gt;" />
            </settings>
            <resourcereferences>
              <resourceReference name="DiagnosticStore" defaultAmount="[4096,4096,4096]" defaultSticky="true" kind="Directory" />
              <resourceReference name="EventStore" defaultAmount="[1000,1000,1000]" defaultSticky="false" kind="LogStore" />
            </resourcereferences>
          </role>
          <sCSPolicy>
            <sCSPolicyIDMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorkerInstances" />
            <sCSPolicyUpdateDomainMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorkerUpgradeDomains" />
            <sCSPolicyFaultDomainMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWorkerFaultDomains" />
          </sCSPolicy>
        </groupHascomponents>
      </components>
      <sCSPolicy>
        <sCSPolicyUpdateDomain name="ImageScoreWebUpgradeDomains" defaultPolicy="[5,5,5]" />
        <sCSPolicyUpdateDomain name="ImageScoreWorkerUpgradeDomains" defaultPolicy="[5,5,5]" />
        <sCSPolicyFaultDomain name="ImageScoreWebFaultDomains" defaultPolicy="[2,2,2]" />
        <sCSPolicyFaultDomain name="ImageScoreWorkerFaultDomains" defaultPolicy="[2,2,2]" />
        <sCSPolicyID name="ImageScoreWebInstances" defaultPolicy="[1,1,1]" />
        <sCSPolicyID name="ImageScoreWorkerInstances" defaultPolicy="[1,1,1]" />
      </sCSPolicy>
    </group>
  </groups>
  <implements>
    <implementation Id="004db9cb-4287-4e00-aa81-aa90f47f8794" ref="Microsoft.RedDog.Contract\ServiceContract\ImageScoreServerContract@ServiceDefinition">
      <interfacereferences>
        <interfaceReference Id="36fc06a5-2ced-4276-bbd7-d76499d63272" ref="Microsoft.RedDog.Contract\Interface\ImageScoreWeb:Endpoint1@ServiceDefinition">
          <inPort>
            <inPortMoniker name="/ImageScoreServer/ImageScoreServerGroup/ImageScoreWeb:Endpoint1" />
          </inPort>
        </interfaceReference>
      </interfacereferences>
    </implementation>
  </implements>
</serviceModel>