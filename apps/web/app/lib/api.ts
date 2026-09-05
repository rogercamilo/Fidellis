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

// ---- Unidades (organizations) ----

export interface Organization {
  id: string;
  name: string;
  parentId?: string | null;
}

export async function listOrganizations(token: string): Promise<Organization[]> {
  const res = await fetch(`${BFF_URL}/api/organizations`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao listar unidades (${res.status}).`);
  return res.json() as Promise<Organization[]>;
}

export async function createOrganization(token: string, name: string): Promise<Organization> {
  const res = await fetch(`${BFF_URL}/api/organizations`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: JSON.stringify({ name }),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { error?: string };
    throw new Error(body.error ?? `Falha ao criar unidade (${res.status}).`);
  }
  return res.json() as Promise<Organization>;
}

// ---- Cobrança / doações (passa pelo BFF, que anexa o tenant e encaminha ao core) ----

export interface CreateDonationInput {
  organizationId: string;
  amount: number;
  donor: { name: string; email?: string; document: string };
  description?: string;
}

export interface DonationCheckout {
  donationId: string;
  status: string;
  qrCode: string;
  qrCodeUrl?: string | null;
  expiresAt?: string | null;
}

export interface DonationStatus {
  id: string;
  status: string;
  pspStatus?: string | null;
  amount: number;
  qrCode?: string | null;
  qrCodeUrl?: string | null;
  paidAt?: string | null;
}

export async function createDonation(token: string, input: CreateDonationInput): Promise<DonationCheckout> {
  const res = await fetch(`${BFF_URL}/api/finance/donations`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: JSON.stringify(input),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { error?: string };
    throw new Error(body.error ?? `Falha ao criar cobrança (${res.status}).`);
  }
  return res.json() as Promise<DonationCheckout>;
}

export async function getDonation(token: string, id: string): Promise<DonationStatus> {
  const res = await fetch(`${BFF_URL}/api/finance/donations/${id}`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao consultar doação (${res.status}).`);
  return res.json() as Promise<DonationStatus>;
}

// ---- Doações recorrentes (dízimo mensal) ----

export interface CreateRecurringInput {
  organizationId: string;
  amount: number;
  dayOfMonth: number;
  donor: { name: string; email?: string; document: string };
}

export interface RecurringDonation {
  id: string;
  organizationId: string;
  amount: number;
  dayOfMonth: number;
  status: string;
  nextChargeAt: string;
  attempt: number;
}

export async function createRecurring(token: string, input: CreateRecurringInput): Promise<RecurringDonation> {
  const res = await fetch(`${BFF_URL}/api/finance/recurring-donations`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: JSON.stringify(input),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { error?: string };
    throw new Error(body.error ?? `Falha ao criar recorrência (${res.status}).`);
  }
  return res.json() as Promise<RecurringDonation>;
}

export async function listRecurring(token: string): Promise<RecurringDonation[]> {
  const res = await fetch(`${BFF_URL}/api/finance/recurring-donations`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao listar recorrências (${res.status}).`);
  return res.json() as Promise<RecurringDonation[]>;
}

export async function actOnRecurring(
  token: string,
  id: string,
  action: 'pause' | 'resume' | 'cancel',
): Promise<RecurringDonation> {
  const res = await fetch(`${BFF_URL}/api/finance/recurring-donations/${id}/${action}`, {
    method: 'POST',
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao ${action} recorrência (${res.status}).`);
  return res.json() as Promise<RecurringDonation>;
}
