param location string
param applicationName string
param environmentName string

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: replace('${applicationName}${environmentName}acr', '-', '')
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
  }
}

output name string = acr.name
output loginServer string = acr.properties.loginServer
