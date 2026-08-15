import { createGatewayClient } from './http';

let gatewayClient: ReturnType<typeof createGatewayClient> | null = null;

export function initializeGatewayClient(baseUrl?: string) {
  gatewayClient = createGatewayClient(baseUrl);
  return gatewayClient;
}

export function getGatewayClient() {
  if (!gatewayClient) {
    gatewayClient = createGatewayClient();
  }
  return gatewayClient;
}
