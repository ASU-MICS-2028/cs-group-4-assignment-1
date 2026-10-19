// Browser side of the passkey ceremonies. The server sends options as JSON with
// base64url-encoded binary fields; the browser API needs ArrayBuffers, and the
// response goes back the other way. All verification happens on the server.

interface DescriptorJson {
  type: 'public-key';
  id: string;
  transports?: AuthenticatorTransport[];
}

interface CreationOptionsJson {
  rp: PublicKeyCredentialRpEntity;
  user: { id: string; name: string; displayName: string };
  challenge: string;
  pubKeyCredParams: PublicKeyCredentialParameters[];
  timeout?: number;
  attestation?: AttestationConveyancePreference;
  authenticatorSelection?: AuthenticatorSelectionCriteria;
  excludeCredentials?: DescriptorJson[];
}

interface RequestOptionsJson {
  challenge: string;
  timeout?: number;
  rpId?: string;
  allowCredentials?: DescriptorJson[];
  userVerification?: UserVerificationRequirement;
}

function toBuffer(value: string): ArrayBuffer {
  const base64 = value
    .replace(/-/g, '+')
    .replace(/_/g, '/')
    .padEnd(Math.ceil(value.length / 4) * 4, '=');
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}

function toBase64Url(buffer: ArrayBuffer): string {
  let binary = '';
  for (const byte of new Uint8Array(buffer)) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

const toDescriptor = (d: DescriptorJson): PublicKeyCredentialDescriptor => ({
  type: d.type,
  id: toBuffer(d.id),
  transports: d.transports,
});

export const passkeysSupported = () => typeof window.PublicKeyCredential !== 'undefined';

export function describeError(error: unknown): string {
  if (error instanceof DOMException && error.name === 'NotAllowedError')
    return 'The passkey prompt was cancelled or timed out.';
  if (error instanceof DOMException && error.name === 'InvalidStateError')
    return 'This device already holds a passkey for this account.';
  return error instanceof Error ? error.message : 'Something went wrong.';
}

export async function createPasskey(json: unknown) {
  const options = json as CreationOptionsJson;
  const credential = (await navigator.credentials.create({
    publicKey: {
      rp: options.rp,
      user: { ...options.user, id: toBuffer(options.user.id) },
      challenge: toBuffer(options.challenge),
      pubKeyCredParams: options.pubKeyCredParams,
      timeout: options.timeout,
      attestation: options.attestation,
      authenticatorSelection: options.authenticatorSelection,
      excludeCredentials: (options.excludeCredentials ?? []).map(toDescriptor),
    },
  })) as PublicKeyCredential | null;

  if (!credential) throw new Error('No passkey was created.');
  const response = credential.response as AuthenticatorAttestationResponse;

  return {
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    response: {
      attestationObject: toBase64Url(response.attestationObject),
      clientDataJSON: toBase64Url(response.clientDataJSON),
      transports: response.getTransports?.() ?? [],
    },
    clientExtensionResults: credential.getClientExtensionResults(),
  };
}

export async function getPasskey(json: unknown) {
  const options = json as RequestOptionsJson;
  const credential = (await navigator.credentials.get({
    publicKey: {
      challenge: toBuffer(options.challenge),
      timeout: options.timeout,
      rpId: options.rpId,
      allowCredentials: (options.allowCredentials ?? []).map(toDescriptor),
      userVerification: options.userVerification,
    },
  })) as PublicKeyCredential | null;

  if (!credential) throw new Error('No passkey was selected.');
  const response = credential.response as AuthenticatorAssertionResponse;

  return {
    id: credential.id,
    rawId: toBase64Url(credential.rawId),
    type: credential.type,
    response: {
      authenticatorData: toBase64Url(response.authenticatorData),
      clientDataJSON: toBase64Url(response.clientDataJSON),
      signature: toBase64Url(response.signature),
      userHandle: response.userHandle ? toBase64Url(response.userHandle) : null,
    },
    clientExtensionResults: credential.getClientExtensionResults(),
  };
}
