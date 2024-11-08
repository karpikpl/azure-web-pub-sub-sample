param location string = resourceGroup().location
param tags object = {}

param containerAppsEnvironmentName string
param containerRegistryName string
param name string = ''
param serviceName string = 'wps-server'
param managedIdentityName string = ''
param exists bool = false

@description('Web PubSub Server Hostname')
param webPubSubHostname string

@description('Web PubSub Server Hub Name')
param webPubSubHubName string

@description('Service Bus Namespace')
param serviceBusNamespace string

@description('Application Insights Connection String')
param appInsightsConnectionString string

resource userIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = if (!empty(managedIdentityName)) {
  name: managedIdentityName
}

module webPubSubServer '../core/host/container-app-upsert.bicep' = {
  name: '${serviceName}-container-app-module'
  params: {
    name: name
    location: location
    tags: union(tags, { 'azd-service-name': 'wps-server' })
    containerAppsEnvironmentName: containerAppsEnvironmentName
    containerRegistryName: containerRegistryName
    containerCpuCoreCount: '0.5'
    containerMemory: '1.0Gi'
    containerName: serviceName
    ingressEnabled: true
    identityType: 'UserAssigned'
    identityName: managedIdentityName
    exists: exists
    targetPort: 7001
    env: [
      {
        name: 'WebPubSub__Hostname'
        value: webPubSubHostname
      }
      {
        name: 'WebPubSub__HubName'
        value: webPubSubHubName
      }
      {
        name: 'ServiceBus__Namespace'
        value: serviceBusNamespace
      }
      {
        name: 'azureClientId'
        value: userIdentity.properties.clientId
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: appInsightsConnectionString
      }
    ]
  }
}


output SERVICE_WPS_SERVER_IMAGE_NAME string = webPubSubServer.outputs.imageName
output SERVICE_WPS_SERVER_NAME string = webPubSubServer.outputs.name
output SERVICE_WPS_SERVER_URI string = webPubSubServer.outputs.uri
