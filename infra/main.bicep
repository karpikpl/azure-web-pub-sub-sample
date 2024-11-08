targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@minLength(1)
@description('Primary location for all resources')
param location string

param principalId string

// App based params

// Subsciber
param solverContainerAppName string = ''
param solverServiceName string = 'solver'
param solverAppExists bool = false

// WebPubSub Server
param wpsServerContainerAppName string = ''
param wpsServerServiceName string = 'wps-server'
param wpsServerAppExists bool = false

param applicationInsightsDashboardName string = ''
param applicationInsightsName string = ''
param logAnalyticsName string = ''

param containerAppsEnvironmentName string = ''
param containerRegistryName string = ''

param resourceGroupName string = ''
// Optional parameters to override the default azd resource naming conventions. Update the main.parameters.json file to provide values. e.g.,:
// "resourceGroupName": {
//      "value": "myGroupName"
// }

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }

// WebPubSub Resource
var webPubSubName = '${abbrs.webpubsub}${resourceToken}'
var webPubSubHubName = 'AspHub'

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// Monitor application with Azure Monitor
module monitoring './core/monitor/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    tags: tags
    logAnalyticsName: !empty(logAnalyticsName) ? logAnalyticsName : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    applicationInsightsName: !empty(applicationInsightsName) ? applicationInsightsName : '${abbrs.insightsComponents}${resourceToken}'
    applicationInsightsDashboardName: !empty(applicationInsightsDashboardName) ? applicationInsightsDashboardName : '${abbrs.portalDashboards}${resourceToken}'
  }
}

module serviceBusResources './app/servicebus.bicep' = {
  name: 'sb-resources'
  scope: rg
  params: {
    location: location
    tags: tags
    resourceToken: resourceToken
    skuName: 'Standard'
  }
}

module serviceBusAccess './app/access.bicep' = {
  name: 'sb-access'
  scope: rg
  params: {
    location: location
    serviceBusName: serviceBusResources.outputs.serviceBusName
    managedIdentityName: '${abbrs.managedIdentityUserAssignedIdentities}${resourceToken}'
  }
}

// Shared App Env with Dapr configuration for db
module appEnv './app/app-env.bicep' = {
  name: 'app-env'
  scope: rg
  params: {
    containerAppsEnvName: !empty(containerAppsEnvironmentName) ? containerAppsEnvironmentName : '${abbrs.appManagedEnvironments}${resourceToken}'
    containerRegistryName: !empty(containerRegistryName) ? containerRegistryName : '${abbrs.containerRegistryRegistries}${resourceToken}'
    location: location
    logAnalyticsWorkspaceName: monitoring.outputs.logAnalyticsWorkspaceName
    serviceBusName: serviceBusResources.outputs.serviceBusName
    applicationInsightsName: monitoring.outputs.applicationInsightsName
    daprEnabled: true
    managedIdentityClientId: serviceBusAccess.outputs.managedIdentityClientlId
  }
}


module solverApp './app/solver.bicep' = {
  name: 'web-resources'
  scope: rg
  params: {
    name: !empty(solverContainerAppName) ? solverContainerAppName : '${abbrs.appContainerApps}${solverServiceName}-${resourceToken}'
    location: location
    containerRegistryName: appEnv.outputs.registryName
    containerAppsEnvironmentName: appEnv.outputs.environmentName
    serviceName: solverServiceName
    managedIdentityName: serviceBusAccess.outputs.managedIdentityName
    exists: solverAppExists
    webPubSubServerUrl: webPubSubServerApp.outputs.SERVICE_WPS_SERVER_URI
  }
}

module webpubsub './core/pubsub/webpubsub.bicep' = {
  name: 'webpubsub'
  scope: rg
  params: {
    webPubSubName: webPubSubName
    hubName: webPubSubHubName
    eventHandlerUrl: '${webPubSubServerApp.outputs.SERVICE_WPS_SERVER_URI}/eventhandler'
    location: location
    managedIdentityName: serviceBusAccess.outputs.managedIdentityName
  }
}

module AccessForUser './app/accessForUser.bicep' = {
  name: 'access-for-user'
  scope: rg
  params: {
    serviceBusName: serviceBusResources.outputs.serviceBusName
    userObjectId: principalId
    webPubSubName: webpubsub.outputs.webPubSubResourceName
  }
}

module webPubSubServerApp './app/webpubsubServer.bicep' = {
  name: 'wps-server'
  scope: rg
  params: {
    name: !empty(wpsServerContainerAppName) ? wpsServerContainerAppName : '${abbrs.appContainerApps}${wpsServerServiceName}-${resourceToken}'
    location: location
    containerRegistryName: appEnv.outputs.registryName
    containerAppsEnvironmentName: appEnv.outputs.environmentName
    serviceName: wpsServerServiceName
    managedIdentityName: serviceBusAccess.outputs.managedIdentityName
    exists: wpsServerAppExists
    webPubSubHostname: '${webPubSubName}.webpubsub.azure.com'
    webPubSubHubName: webPubSubHubName
    serviceBusNamespace: '${serviceBusResources.outputs.serviceBusName}.servicebus.windows.net'
    appInsightsConnectionString: monitoring.outputs.applicationInsightsConnectionString
  }
}

module accessToWebPubSub './app/accessToWebPubSub.bicep' = {
  name: 'access-to-webpubsub'
  scope: rg
  params: {
    managedIdentityName: serviceBusAccess.outputs.managedIdentityName
    webPubSubName: webpubsub.outputs.webPubSubResourceName
  }
}

output SERVICE_SOLVER_NAME string = solverApp.outputs.SERVICE_SOLVER_NAME
output SERVICE_SOLVER_IMAGE_NAME string = solverApp.outputs.SERVICE_SOLVER_IMAGE_NAME
output SERVICEBUS_ENDPOINT string = serviceBusResources.outputs.SERVICEBUS_ENDPOINT
output SERVICEBUS_NAME string = serviceBusResources.outputs.serviceBusName
output SERVICEBUS_TOPIC_NAME string = serviceBusResources.outputs.topicName
output APPINSIGHTS_INSTRUMENTATIONKEY string = monitoring.outputs.applicationInsightsInstrumentationKey
output APPINSIGHTS_CONNECTIONSTRING string = monitoring.outputs.applicationInsightsConnectionString
output APPLICATIONINSIGHTS_NAME string = monitoring.outputs.applicationInsightsName
output AZURE_CONTAINER_ENVIRONMENT_NAME string = appEnv.outputs.environmentName
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = appEnv.outputs.registryLoginServer
output AZURE_CONTAINER_REGISTRY_NAME string = appEnv.outputs.registryName
output AZURE_MANAGED_IDENTITY_NAME string = serviceBusAccess.outputs.managedIdentityName
output AZURE_WEBPUBSUB_NAME string = webpubsub.outputs.webPubSubResourceName
output AZURE_WEBPUBSUB_HUB_NAME string = webPubSubHubName
output AZURE_WEBPUBSUB_HOSTNAME string = webpubsub.outputs.webPubSubHostName
output SERVICE_WPS_SERVER_IMAGE_NAME string = webPubSubServerApp.outputs.SERVICE_WPS_SERVER_IMAGE_NAME
output SERVICE_WPS_SERVER_NAME string = webPubSubServerApp.outputs.SERVICE_WPS_SERVER_NAME
output SERVICE_WPS_SERVER_URI string = webPubSubServerApp.outputs.SERVICE_WPS_SERVER_URI
