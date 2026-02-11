param location string
param applicationName string
param environmentName string

resource brokerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${applicationName}-broker-${environmentName}'
  location: location
}

output brokerIdentityId string = brokerIdentity.id
output brokerPrincipalId string = brokerIdentity.properties.principalId
