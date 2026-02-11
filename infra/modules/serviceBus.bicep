param location string
param applicationName string
param environmentName string
param brokerPrincipalId string

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: '${applicationName}-${environmentName}-sb'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
}

resource serviceBusDataOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespace.id, brokerPrincipalId, 'ServiceBusDataOwner')
  scope: serviceBusNamespace
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '090c5cfd-751d-490a-894a-3ce6f1109419')
    principalId: brokerPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output namespace string = serviceBusNamespace.properties.serviceBusEndpoint
