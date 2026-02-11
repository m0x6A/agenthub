param location string
param applicationName string
param environmentName string
param brokerIdentityId string
param cosmosDbEndpoint string
param serviceBusNamespace string
param appInsightsConnectionString string
param containerRegistryName string

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${applicationName}-${environmentName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
  }
}

resource brokerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${applicationName}-broker-${environmentName}'
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${brokerIdentityId}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
      }
    }
    template: {
      containers: [
        {
          name: 'broker'
          image: '${containerRegistryName}.azurecr.io/agentbus-broker:latest'
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            {
              name: 'CosmosDb__ConnectionString'
              value: cosmosDbEndpoint
            }
            {
              name: 'ServiceBus__ConnectionString'
              value: serviceBusNamespace
            }
            {
              name: 'ApplicationInsights__ConnectionString'
              value: appInsightsConnectionString
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 10
      }
    }
  }
}

output url string = brokerApp.properties.configuration.ingress.fqdn
