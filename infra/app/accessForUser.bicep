param userObjectId string
param serviceBusName string
param webPubSubName string

// See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#azure-service-bus-data-sender
var roleIdS = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39' // Azure Service Bus Data Sender
// See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles#azure-service-bus-data-receiver
var roleIdR = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0' // Azure Service Bus Data Receiver

// See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/web-and-mobile#web-pubsub-service-owner
var roleIdOwner = '12cf5a90-567b-43ae-8102-96cf46c7d9b4' // Web PubSub Service Owner

resource serviceBus 'Microsoft.ServiceBus/namespaces@2021-11-01' existing = {
  name: serviceBusName
}

resource webPubSub 'Microsoft.SignalRService/webPubSub@2024-04-01-preview' existing = {
  name: webPubSubName
}

// Grant permissions to the current user to specific role to servicebus
resource roleAssignmentUserReceiver 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(serviceBus.id, roleIdR, userObjectId)
  scope: serviceBus
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIdR)
    principalId: userObjectId
    principalType: 'User'
  }
  dependsOn: [
    serviceBus
  ]
}

// Grant permissions to the current user to specific role to servicebus
resource roleAssignmentUserSender 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(serviceBus.id, roleIdS, userObjectId)
  scope: serviceBus
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIdS)
    principalId: userObjectId
    principalType: 'User'
  }
  dependsOn: [
    serviceBus
  ]
}

// Grant permissions to the current user to specific role to webpubsub
resource roleAssignmentUserOwner 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = {
  name: guid(webPubSub.id, roleIdOwner, userObjectId)
  scope: webPubSub
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIdOwner)
    principalId: userObjectId
    principalType: 'User'
  }
  dependsOn: [
    webPubSub
  ]
}
