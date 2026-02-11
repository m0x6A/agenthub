targetScope = 'subscription'

@description('Primary Azure region for resources')
param location string = 'eastus'

@description('Environment name (dev, staging, prod)')
param environmentName string = 'dev'

@description('Application name')
param applicationName string = 'agentbus'

// Create resource group
resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: '${applicationName}-${environmentName}-rg'
  location: location
  tags: {
    application: applicationName
    environment: environmentName
  }
}

// Deploy modules
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  scope: rg
  params: {
    location: location
    applicationName: applicationName
    environmentName: environmentName
  }
}

module identity 'modules/identity.bicep' = {
  name: 'identity-deployment'
  scope: rg
  params: {
    location: location
    applicationName: applicationName
    environmentName: environmentName
  }
}

module cosmosDb 'modules/cosmosDb.bicep' = {
  name: 'cosmosdb-deployment'
  scope: rg
  params: {
    location: location
    applicationName: applicationName
    environmentName: environmentName
  }
}

module serviceBus 'modules/serviceBus.bicep' = {
  name: 'servicebus-deployment'
  scope: rg
  params: {
    location: location
    applicationName: applicationName
    environmentName: environmentName
    brokerPrincipalId: identity.outputs.brokerPrincipalId
  }
}

module containerRegistry 'modules/containerRegistry.bicep' = {
  name: 'acr-deployment'
  scope: rg
  params: {
    location: location
    applicationName: applicationName
    environmentName: environmentName
  }
}

module containerApps 'modules/containerApps.bicep' = {
  name: 'containerapp-deployment'
  scope: rg
  params: {
    location: location
    applicationName: applicationName
    environmentName: environmentName
    brokerIdentityId: identity.outputs.brokerIdentityId
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    serviceBusNamespace: serviceBus.outputs.namespace
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    containerRegistryName: containerRegistry.outputs.name
  }
}

output resourceGroupName string = rg.name
output containerAppUrl string = containerApps.outputs.url
