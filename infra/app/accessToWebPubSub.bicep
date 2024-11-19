param managedIdentityName string
param webPubSubName string
@description('Assign role assignments to the managed identity')
param doRoleAssignments bool

// See https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles/web-and-mobile#web-pubsub-service-owner
var roleIdOwner = '12cf5a90-567b-43ae-8102-96cf46c7d9b4' // Web PubSub Service Owner

// user assigned managed identity to use throughout
resource userIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = if (!empty(managedIdentityName)) {
  name: managedIdentityName
}

resource webPubSub 'Microsoft.SignalRService/webPubSub@2024-04-01-preview' existing = {
  name: webPubSubName
}

// Grant permissions to the current user to specific role to webpubsub
resource roleAssignmentOwner 'Microsoft.Authorization/roleAssignments@2020-04-01-preview' = if(doRoleAssignments) {
  name: guid(webPubSub.id, roleIdOwner, managedIdentityName)
  scope: webPubSub
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIdOwner)
    principalId: userIdentity.properties.principalId
    principalType: 'ServicePrincipal' // managed identity is a form of service principal
  }
  dependsOn: [
    webPubSub
  ]
}

output missingRoleAssignments string = doRoleAssignments ? '' : 'Assignment for ${managedIdentityName} to ${webPubSubName} is not enabled. Add service "Web PubSub Service Owner" role to ${userIdentity.properties.principalId}'
