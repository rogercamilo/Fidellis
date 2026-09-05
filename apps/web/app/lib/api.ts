export const BFF_URL = process.env.NEXT_PUBLIC_BFF_URL ?? 'http://localhost:4000';

export interface TenantMembership {
  tenantId: string;
  slug: string;
  name: string;
  role: string;
}

export interface LoginResult {
  accessToken: string;
  refreshToken: string;
  user: { id: string; email: string; displayName: string | null };
  tenants: TenantMembership[];
  activeTenant: string | null;
}

export async function login(email: string, password: string, tenant?: string): Promise<LoginResult> {
  const res = await fetch(`${BFF_URL}/auth/login`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email, password, tenant }),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { message?: string };
    throw new Error(body.message ?? `Falha no login (${res.status}).`);
  }
  return res.json() as Promise<LoginResult>;
}
