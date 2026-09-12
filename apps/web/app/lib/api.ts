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

// ---- Onboarding (cadastro da instituição + primeiro admin) ----

export interface OnboardingInput {
  slug: string;
  tenantName: string;
  email: string;
  password: string;
  displayName?: string;
  organizationName?: string;
}

export interface OnboardingResult {
  user: { id: string; email: string; displayName: string | null };
  tenant: { id: string; slug: string; name: string; rootOrganizationId: string | null };
  accessToken: string;
  refreshToken: string;
  activeTenant: string | null;
}

/** Cria tenant + organização-raiz + primeiro admin (já vinculado). Faz auto-login. */
export async function onboarding(input: OnboardingInput): Promise<OnboardingResult> {
  const res = await fetch(`${BFF_URL}/onboarding`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { message?: string };
    throw new Error(body.message ?? `Falha no cadastro (${res.status}).`);
  }
  return res.json() as Promise<OnboardingResult>;
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

/** Unidades do usuário logado (as suas + filiais). */
export async function listMyOrganizations(token: string): Promise<Organization[]> {
  const res = await fetch(`${BFF_URL}/api/organizations/mine`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao listar minhas unidades (${res.status}).`);
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
  /** 'pix' (default) | 'boleto'. Cartão depende da tokenização Pagar.me.js (futuro). */
  method?: string;
  /** Tipo da entrada (D-06). Default 'donation'. */
  entryType?: EntryType;
}

export interface DonationCheckout {
  donationId: string;
  status: string;
  method?: string;
  qrCode: string;
  qrCodeUrl?: string | null;
  expiresAt?: string | null;
  boletoLine?: string | null;
  boletoUrl?: string | null;
  dueDate?: string | null;
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
  /** 'tithe' (dízimo, default) ou 'donation' (doação recorrente do apoiador — D-07). */
  entryType?: EntryType;
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

// ---- Contabilidade: recibos + balancete ----

export interface ReceiptSummary {
  id: string;
  number: string;
  organizationId: string;
  donorName: string;
  amount: number;
  issuedAt: string;
}

export interface ReceiptDetail extends ReceiptSummary {
  organizationName: string | null;
  donorDocument: string | null;
}

export interface TrialBalanceRow {
  ledgerAccountId: string | null;
  code: string | null;
  name: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface TrialBalance {
  from: string | null;
  to: string | null;
  totalDebit: number;
  totalCredit: number;
  accounts: TrialBalanceRow[];
}

export async function listReceipts(token: string): Promise<ReceiptSummary[]> {
  const res = await fetch(`${BFF_URL}/api/accounting/receipts`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao listar recibos (${res.status}).`);
  return res.json() as Promise<ReceiptSummary[]>;
}

export async function getReceipt(token: string, id: string): Promise<ReceiptDetail> {
  const res = await fetch(`${BFF_URL}/api/accounting/receipts/${id}`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao consultar recibo (${res.status}).`);
  return res.json() as Promise<ReceiptDetail>;
}

/** Baixa o PDF do recibo (gerado no core; arquivado no R2 quando configurado). */
export async function downloadReceiptPdf(token: string, id: string, number: string): Promise<void> {
  const res = await fetch(`${BFF_URL}/api/accounting/receipts/${id}/pdf`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao baixar o PDF (${res.status}).`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `recibo-${number.replace('/', '-')}.pdf`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export async function trialBalance(token: string): Promise<TrialBalance> {
  const res = await fetch(`${BFF_URL}/api/accounting/trial-balance`, {
    headers: { authorization: `Bearer ${token}` },
  });
  if (!res.ok) throw new Error(`Falha ao consultar o balancete (${res.status}).`);
  return res.json() as Promise<TrialBalance>;
}

// ---- Plano de contas + razão (extrato por conta) ----

export interface LedgerAccount { id: string; code: string; name: string; type: string; normalBalance: string; postable: boolean; parentId: string | null }
export interface LedgerEntry { date: string; debit: number; credit: number; description: string | null; balance: number }
export interface LedgerView { accountId: string; balance: number; lines: LedgerEntry[] }

export const listLedgerAccounts = (token: string) =>
  authGet<LedgerAccount[]>(token, '/api/accounting/accounts', 'plano de contas');
export const accountLedger = (token: string, accountId: string) =>
  authGet<LedgerView>(token, `/api/accounting/ledger?accountId=${accountId}`, 'razão');

// ---- CRM (doadores) ----

export interface DonorSummary {
  id: string;
  name: string;
  email?: string | null;
  document?: string | null;
  phone?: string | null;
  totalPaid: number;
  donations: number;
  lastPaidAt?: string | null;
  situacao: string;
}

export interface DonorDetail {
  donor: { id: string; name: string; email?: string | null; document?: string | null; phone?: string | null };
  donations: { id: string; amount: number; status: string; method: string; createdAt: string; paidAt?: string | null }[];
  recurring: { id: string; amount: number; dayOfMonth: number; status: string; nextChargeAt: string }[];
  messages: { id: string; channel: string; eventType: string; status: string; subject?: string | null; createdAt: string; sentAt?: string | null }[];
}

export async function listDonors(token: string): Promise<DonorSummary[]> {
  const res = await fetch(`${BFF_URL}/api/crm/donors`, { headers: { authorization: `Bearer ${token}` } });
  if (!res.ok) throw new Error(`Falha ao listar doadores (${res.status}).`);
  return res.json() as Promise<DonorSummary[]>;
}

export async function getDonor(token: string, id: string): Promise<DonorDetail> {
  const res = await fetch(`${BFF_URL}/api/crm/donors/${id}`, { headers: { authorization: `Bearer ${token}` } });
  if (!res.ok) throw new Error(`Falha ao consultar doador (${res.status}).`);
  return res.json() as Promise<DonorDetail>;
}

// ---- Relatórios (Reporting) ----

export interface ReportingOverview {
  totalRaised: number;
  donationsCount: number;
  avgTicket: number;
  activeDonors: number;
  activeRecurring: number;
  byMethod: { method: string; total: number; count: number }[];
}

export interface MonthPoint {
  month: string;
  total: number;
  count: number;
}

export interface UnitReport {
  organizationId: string;
  name: string;
  parentId: string | null;
  total: number;
  count: number;
}

async function authGet<T>(token: string, path: string, label: string): Promise<T> {
  const res = await fetch(`${BFF_URL}${path}`, { headers: { authorization: `Bearer ${token}` } });
  if (!res.ok) throw new Error(`Falha ao carregar ${label} (${res.status}).`);
  return res.json() as Promise<T>;
}

export const reportingOverview = (token: string) =>
  authGet<ReportingOverview>(token, '/api/reporting/overview', 'resumo');

export const reportingTimeseries = (token: string, months = 12) =>
  authGet<MonthPoint[]>(token, `/api/reporting/timeseries?months=${months}`, 'série temporal');

export const reportingByUnit = (token: string) =>
  authGet<UnitReport[]>(token, '/api/reporting/by-unit', 'consolidação por unidade');

// ---- Auditoria + LGPD ----

export interface AuditEntry {
  id: string;
  actorUserId: string | null;
  action: string;
  entity: string;
  entityId: string | null;
  createdAt: string;
}

export const listAuditLog = (token: string) => authGet<AuditEntry[]>(token, '/api/audit/log', 'auditoria');

export async function exportDonor(token: string, id: string): Promise<unknown> {
  return authGet<unknown>(token, `/api/crm/donors/${id}/export`, 'exportação');
}

async function authPost(token: string, path: string, label: string): Promise<void> {
  const res = await fetch(`${BFF_URL}${path}`, { method: 'POST', headers: { authorization: `Bearer ${token}` } });
  if (!res.ok) throw new Error(`Falha em ${label} (${res.status}).`);
}

export const anonymizeDonor = (token: string, id: string) => authPost(token, `/api/crm/donors/${id}/anonymize`, 'anonimizar');
export const optOutDonor = (token: string, id: string) => authPost(token, `/api/crm/donors/${id}/opt-out`, 'opt-out');

// ---- Portal público do doador ----

export interface PublicOrg {
  id: string;
  name: string;
  parentId: string | null;
}

export interface PortalData {
  donor: { name: string; email: string | null };
  donations: { id: string; amount: number; status: string; method: string; createdAt: string; paidAt?: string | null }[];
  receipts: { id: string; number: string; amount: number; issuedAt: string }[];
}

export async function publicOrganizations(tenant: string): Promise<PublicOrg[]> {
  const res = await fetch(`${BFF_URL}/api/public/${tenant}/organizations`);
  if (!res.ok) throw new Error(`Instituição não encontrada (${res.status}).`);
  return res.json() as Promise<PublicOrg[]>;
}

export async function publicCreateDonation(
  tenant: string,
  input: { organizationId: string; amount: number; donor: { name: string; email?: string; document: string } },
): Promise<DonationCheckout> {
  const res = await fetch(`${BFF_URL}/api/public/${tenant}/donations`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(input),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { error?: string };
    throw new Error(body.error ?? `Falha ao gerar doação (${res.status}).`);
  }
  return res.json() as Promise<DonationCheckout>;
}

export async function publicGetDonation(tenant: string, id: string): Promise<{ status: string; qrCode?: string | null; qrCodeUrl?: string | null }> {
  const res = await fetch(`${BFF_URL}/api/public/${tenant}/donations/${id}`);
  if (!res.ok) throw new Error(`Falha ao consultar (${res.status}).`);
  return res.json() as Promise<{ status: string; qrCode?: string | null; qrCodeUrl?: string | null }>;
}

export async function requestMagicLink(tenant: string, email: string): Promise<void> {
  await fetch(`${BFF_URL}/api/public/${tenant}/magic-link`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ email }),
  });
}

export async function portalMe(tenant: string, token: string): Promise<PortalData> {
  const res = await fetch(`${BFF_URL}/api/public/${tenant}/me?token=${encodeURIComponent(token)}`);
  if (!res.ok) throw new Error(`Link inválido ou expirado (${res.status}).`);
  return res.json() as Promise<PortalData>;
}

// =====================================================================
// Finanças (Ondas 1–2) — FE-0: camada de cliente sobre o proxy do BFF.
// =====================================================================

async function authSend<T>(token: string, method: string, path: string, body: unknown, label: string): Promise<T> {
  const res = await fetch(`${BFF_URL}${path}`, {
    method,
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!res.ok) {
    const b = (await res.json().catch(() => ({}))) as { error?: string };
    throw new Error(b.error ?? `Falha em ${label} (${res.status}).`);
  }
  return res.json() as Promise<T>;
}

// ---- Dimensões (centros de custo / fundos / projetos) ----

export interface CostCenter { id: string; code: string; name: string; isDefault: boolean; active: boolean }
export interface Fund { id: string; code: string; name: string; restriction: string; purpose: string | null; isDefault: boolean; active: boolean }
export interface Project { id: string; code: string; name: string; fundId: string | null; budgetAmount: number | null; startsAt: string | null; endsAt: string | null; status: string }

export const listCostCenters = (t: string) => authGet<CostCenter[]>(t, '/api/finance/cost-centers', 'centros de custo');
export const createCostCenter = (t: string, body: { code: string; name: string }) => authSend<CostCenter>(t, 'POST', '/api/finance/cost-centers', body, 'criar centro de custo');
export const listFunds = (t: string) => authGet<Fund[]>(t, '/api/finance/funds', 'fundos');
export const createFund = (t: string, body: { code: string; name: string; restriction?: string; purpose?: string }) => authSend<Fund>(t, 'POST', '/api/finance/funds', body, 'criar fundo');
export const listProjects = (t: string) => authGet<Project[]>(t, '/api/finance/projects', 'projetos');
export const createProject = (t: string, body: { code: string; name: string; fundId?: string; budgetAmount?: number }) => authSend<Project>(t, 'POST', '/api/finance/projects', body, 'criar projeto');

// ---- Configuração (nomenclatura / tipos de doador / rubricas) ----

export interface FinanceSettings {
  recurringLabel: string; onetimeLabel: string;
  titheLabel: string; offeringLabel: string; donationLabel: string;
}

/** Tipos de entrada (D-06): chaves técnicas estáveis + rótulo default (customizável por tenant). */
export type EntryType = 'tithe' | 'offering' | 'donation';
export const ENTRY_TYPES: { value: EntryType; label: string }[] = [
  { value: 'tithe', label: 'Dízimo' },
  { value: 'offering', label: 'Oferta' },
  { value: 'donation', label: 'Doação' },
];
export interface DonorTypeItem { id: string; name: string; isRecurringDefault: boolean; active: boolean }
export interface FinanceCategoryItem { id: string; kind: string; name: string; ledgerAccountId: string | null; active: boolean }

export const getFinanceSettings = (t: string) => authGet<FinanceSettings>(t, '/api/finance/settings', 'configurações');
export const updateFinanceSettings = (t: string, body: FinanceSettings) => authSend<FinanceSettings>(t, 'PUT', '/api/finance/settings', body, 'salvar configurações');
export const listDonorTypes = (t: string) => authGet<DonorTypeItem[]>(t, '/api/finance/donor-types', 'tipos de doador');
export const createDonorType = (t: string, body: { name: string; isRecurringDefault?: boolean }) => authSend<DonorTypeItem>(t, 'POST', '/api/finance/donor-types', body, 'criar tipo de doador');
export const listCategories = (t: string, kind?: string) => authGet<FinanceCategoryItem[]>(t, `/api/finance/categories${kind ? `?kind=${kind}` : ''}`, 'rubricas');
export const createCategory = (t: string, body: { kind: string; name: string; ledgerAccountId?: string }) => authSend<FinanceCategoryItem>(t, 'POST', '/api/finance/categories', body, 'criar rubrica');

// ---- Tesouraria ----

export interface TreasuryAccount { id: string; organizationId: string; name: string; kind: string; openingBalance: number; balance: number; active: boolean }
export interface CashFlowProjection { horizonDays: number; opening: number; expectedInflows: number; expectedOutflows: number; projected: number }

export const listTreasuryAccounts = (t: string) => authGet<TreasuryAccount[]>(t, '/api/finance/treasury/accounts', 'contas de tesouraria');
export const createTreasuryAccount = (t: string, body: { organizationId: string; name: string; kind?: string; openingBalance?: number }) => authSend<TreasuryAccount>(t, 'POST', '/api/finance/treasury/accounts', body, 'criar conta');
export const treasuryBalance = (t: string, organizationId?: string) => authGet<{ organizationIds: string[]; balance: number }>(t, `/api/finance/treasury/balance${organizationId ? `?organizationId=${organizationId}` : ''}`, 'saldo');
export const treasuryTransfer = (t: string, body: { fromAccountId: string; toAccountId: string; amount: number; description?: string }) => authSend<{ outflowId: string; inflowId: string; amount: number }>(t, 'POST', '/api/finance/treasury/transfers', body, 'transferência');
export const treasuryCashflow = (t: string, organizationId?: string) => authGet<CashFlowProjection[]>(t, `/api/finance/treasury/cashflow${organizationId ? `?organizationId=${organizationId}` : ''}`, 'fluxo de caixa');

// ---- Contas a Receber ----

export interface Receivable { id: string; organizationId: string; donorId: string | null; source: string; description: string | null; amount: number; receivedAmount: number; dueDate: string; status: string; donationId: string | null }
export interface AgingReport { notDue: number; overdue1To30: number; overdue31To60: number; overdue60Plus: number; totalOutstanding: number }

export const listReceivables = (t: string, status?: string) => authGet<Receivable[]>(t, `/api/finance/receivables${status ? `?status=${status}` : ''}`, 'contas a receber');
export const receivablesAging = (t: string) => authGet<AgingReport>(t, '/api/finance/receivables/aging', 'aging');
export const createReceivable = (t: string, body: { organizationId: string; amount: number; dueDate: string; source?: string; donorId?: string; description?: string }) => authSend<Receivable>(t, 'POST', '/api/finance/receivables', body, 'criar recebível');
export const settleReceivable = (t: string, id: string, body: { amount: number; donationId?: string }) => authSend<Receivable>(t, 'POST', `/api/finance/receivables/${id}/settle`, body, 'baixar recebível');

// ---- Contas a Pagar ----

export interface Payee { id: string; name: string; document: string | null; pixKey: string | null; kind: string; active: boolean }
export interface Payable { id: string; payeeId: string; description: string; amount: number; dueDate: string; status: string; categoryId: string | null; paidAt: string | null }
export interface ApprovalTier { id: string; minAmount: number; maxAmount: number | null; signatures: number; rolesCsv: string }

export const listPayees = (t: string) => authGet<Payee[]>(t, '/api/finance/payees', 'credores');
export const createPayee = (t: string, body: { name: string; document?: string; pixKey?: string; kind?: string }) => authSend<Payee>(t, 'POST', '/api/finance/payees', body, 'criar credor');
export const listPayables = (t: string, status?: string) => authGet<Payable[]>(t, `/api/finance/payables${status ? `?status=${status}` : ''}`, 'contas a pagar');
export const createPayable = (t: string, body: { payeeId: string; amount: number; dueDate: string; description: string; categoryId?: string; costCenterId?: string; projectId?: string; fundId?: string }) => authSend<Payable>(t, 'POST', '/api/finance/payables', body, 'criar título');
export const approvePayable = (t: string, id: string) => authSend<{ id: string; status: string }>(t, 'POST', `/api/finance/payables/${id}/approve`, {}, 'aprovar');
export const rejectPayable = (t: string, id: string) => authSend<{ id: string; status: string }>(t, 'POST', `/api/finance/payables/${id}/reject`, {}, 'rejeitar');
export const payPayable = (t: string, id: string, body: { treasuryAccountId: string }) => authSend<{ id: string; status: string }>(t, 'POST', `/api/finance/payables/${id}/pay`, body, 'pagar');
export const listApprovalTiers = (t: string) => authGet<ApprovalTier[]>(t, '/api/finance/approval-tiers', 'faixas de alçada');

// ---- Caixa físico ----

export interface CashSession { id: string; accountId: string; eventLabel: string | null; status: string; countedAmount: number | null; openedBy: string; confirmedBy: string | null; closedAt: string | null; depositedMovementId: string | null }

export const listCashSessions = (t: string, status?: string) => authGet<CashSession[]>(t, `/api/finance/cash-sessions${status ? `?status=${status}` : ''}`, 'sessões de caixa');
export const openCashSession = (t: string, body: { accountId: string; eventLabel?: string }) => authSend<CashSession>(t, 'POST', '/api/finance/cash-sessions/open', body, 'abrir caixa');

// ---- Lançamento manual de entrada (recebimento fora do PSP — D-05) ----

export interface ManualEntry { id: string; amount: number; source: string; status: string }

export const createManualEntry = (
  t: string,
  body: { treasuryAccountId: string; amount: number; entryType?: EntryType; donorName?: string; donorEmail?: string; donorDocument?: string; costCenterId?: string; projectId?: string; fundId?: string; occurredAt?: string },
) => authSend<ManualEntry>(t, 'POST', '/api/finance/entries', body, 'lançar entrada');

/** Linha discriminada da coleta de caixa (D-06): tipo + valor (+ dimensões opcionais). */
export interface CashLine { entryType: EntryType; amount: number; costCenterId?: string; projectId?: string; fundId?: string }
export const closeCashSession = (t: string, id: string, body: { countedAmount: number; lines?: CashLine[] }) => authSend<CashSession>(t, 'POST', `/api/finance/cash-sessions/${id}/close`, body, 'fechar caixa');
export const depositCashSession = (t: string, id: string, body: { bankAccountId: string }) => authSend<{ id: string; depositedMovementId: string | null }>(t, 'POST', `/api/finance/cash-sessions/${id}/deposit`, body, 'depositar');

// ---- Fechamento de período ----

export interface AccountingPeriod { year: number; month: number; status: string; closedAt: string | null }

export const listPeriods = (t: string) => authGet<AccountingPeriod[]>(t, '/api/finance/periods', 'períodos');
export const closePeriod = (t: string, year: number, month: number) => authSend<AccountingPeriod>(t, 'POST', `/api/finance/periods/${year}/${month}/close`, {}, 'fechar período');
export const reopenPeriod = (t: string, year: number, month: number) => authSend<AccountingPeriod>(t, 'POST', `/api/finance/periods/${year}/${month}/reopen`, {}, 'reabrir período');

// ---- Conciliação bancária (Onda 3) ----

export interface BankStatement { id: string; accountId: string; format: string; reference: string | null; importedAt: string }
export interface StatementLine { id: string; fitId: string | null; postedAt: string; amount: number; memo: string | null; status: string; matchedType: string | null; matchedId: string | null }
export interface MatchCandidate { type: string; id: string; description: string; amount: number; date: string }

export const importStatement = (t: string, body: { accountId: string; content: string; format?: string; reference?: string }) =>
  authSend<{ id: string; imported: number; skipped: number }>(t, 'POST', '/api/finance/statements/import', body, 'importar extrato');
export const listStatements = (t: string) => authGet<BankStatement[]>(t, '/api/finance/statements', 'extratos');
export const statementLines = (t: string, id: string) => authGet<StatementLine[]>(t, `/api/finance/statements/${id}/lines`, 'linhas do extrato');
export const lineSuggestions = (t: string, lineId: string) => authGet<MatchCandidate[]>(t, `/api/finance/statement-lines/${lineId}/suggestions`, 'sugestões');
export const matchLine = (t: string, lineId: string, body: { type: string; id: string }) =>
  authSend<{ id: string; status: string }>(t, 'POST', `/api/finance/statement-lines/${lineId}/match`, body, 'casar linha');
export const ignoreLine = (t: string, lineId: string) =>
  authSend<{ id: string; status: string }>(t, 'POST', `/api/finance/statement-lines/${lineId}/ignore`, {}, 'ignorar linha');

// ---- Orçamento (Onda 3) ----

export interface Budget { id: string; year: number; kind: string; amount: number; costCenterId: string | null; projectId: string | null; fundId: string | null; revision: number; active: boolean }
export interface BudgetActual { budgetId: string; year: number; kind: string; costCenterId: string | null; projectId: string | null; fundId: string | null; budgeted: number; realized: number; variance: number; overBudget: boolean }

export const listBudgets = (t: string, year?: number, includeInactive?: boolean) =>
  authGet<Budget[]>(t, `/api/finance/budgets?${year ? `year=${year}&` : ''}${includeInactive ? 'includeInactive=true' : ''}`, 'orçamentos');
export const budgetActual = (t: string, year: number) => authGet<BudgetActual[]>(t, `/api/finance/budgets/actual?year=${year}`, 'previsto × realizado');
export const createBudget = (t: string, body: { year: number; kind: string; amount: number; costCenterId?: string; projectId?: string; fundId?: string }) =>
  authSend<Budget>(t, 'POST', '/api/finance/budgets', body, 'criar orçamento');
export const reviseBudget = (t: string, id: string, body: { amount: number }) =>
  authSend<Budget>(t, 'POST', `/api/finance/budgets/${id}/revise`, body, 'revisar orçamento');

// ---- Equipe / papéis financeiros (DT-04) ----

export interface TeamMember { userId: string; email: string; displayName: string | null; role: string }

export const listTeam = (t: string) => authGet<TeamMember[]>(t, '/api/finance/team', 'equipe');
export const teamRoles = (t: string) => authGet<string[]>(t, '/api/finance/team/roles', 'papéis');
export const setMemberRole = (t: string, userId: string, role: string) =>
  authSend<{ userId: string; role: string }>(t, 'PUT', `/api/finance/team/${userId}/role`, { role }, 'atribuir papel');

// ---- Convites de membro + estado de bootstrap (D-01) ----

export interface Invitation { id: string; email: string; role: string; status: string; expiresAt: string; createdAt: string }
export interface BootstrapStatus { onboardingCompletedAt: string | null; approverCount: number; teamReady: boolean; inBootstrap: boolean }
export interface InviteResult { outcome: 'MemberAdded' | 'Invited' | 'AlreadyMember'; invitation: Invitation | null }

export const listInvitations = (t: string) => authGet<Invitation[]>(t, '/api/finance/team/invitations', 'convites');
export const bootstrapStatus = (t: string) => authGet<BootstrapStatus>(t, '/api/finance/team/bootstrap', 'estado da equipe');
export const createInvite = (t: string, body: { email: string; role: string }) =>
  authSend<InviteResult>(t, 'POST', '/api/finance/team/invitations', body, 'convidar membro');
export const resendInvite = (t: string, id: string) =>
  authSend<Invitation>(t, 'POST', `/api/finance/team/invitations/${id}/resend`, {}, 'reenviar convite');
export const revokeInvite = (t: string, id: string) =>
  authSend<{ id: string; status: string }>(t, 'DELETE', `/api/finance/team/invitations/${id}`, undefined, 'revogar convite');
export const completeOnboarding = (t: string) =>
  authSend<BootstrapStatus>(t, 'POST', '/api/finance/team/onboarding/complete', {}, 'concluir onboarding');

/** Rótulos amigáveis dos papéis (governança de conselho). Cobre chaves atuais e as da D-02. */
export const ROLE_LABELS: Record<string, string> = {
  admin: 'Administrador',
  treasurer: 'Coordenador (tesouraria)',
  coordinator: 'Coordenador',
  manager: 'Conselheiro responsável',
  council_officer: 'Conselheiro responsável',
  council_chair: 'Moderador / presidente',
  fiscal_council: 'Conselho fiscal',
  accountant: 'Contador',
  member: 'Membro',
};
export const roleLabel = (role: string) => ROLE_LABELS[role] ?? role;

// ---- Aceite público de convite (BFF direto; token no path) ----

export interface InvitationInfo { email: string; tenantName: string; role: string }
export interface AcceptInvitationResult {
  user: { id: string; email: string; displayName: string | null };
  accessToken: string;
  refreshToken: string;
  activeTenant: string;
  tenant: { slug: string; name: string; role: string };
}

export async function getInvitation(token: string): Promise<InvitationInfo> {
  const res = await fetch(`${BFF_URL}/invitations/${encodeURIComponent(token)}`);
  if (!res.ok) throw new Error('Convite inválido ou expirado.');
  return res.json() as Promise<InvitationInfo>;
}

export async function acceptInvitation(token: string, password: string, displayName?: string): Promise<AcceptInvitationResult> {
  const res = await fetch(`${BFF_URL}/invitations/${encodeURIComponent(token)}/accept`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ password, displayName }),
  });
  if (!res.ok) {
    const body = (await res.json().catch(() => ({}))) as { message?: string };
    throw new Error(body.message ?? `Falha ao aceitar o convite (${res.status}).`);
  }
  return res.json() as Promise<AcceptInvitationResult>;
}

// =====================================================================
// Demonstrações contábeis (Onda 4) — DRE/DRP, Balanço, DFC, DMPL, segregação.
// Endpoints aceitam organizationId opcional (DT-14): ausente = consolidado da rede.
// =====================================================================

export interface LedgerLineDto { code: string; name: string; type: string; debit: number; credit: number; balance: number }
export interface ReportTrialBalance { year: number; organizationId: string | null; totalDebit: number; totalCredit: number; accounts: LedgerLineDto[] }
export interface ReportIncome { year: number; revenues: number; expenses: number; surplus: number; revenueLines: LedgerLineDto[]; expenseLines: LedgerLineDto[] }
export interface ReportBalanceSheet {
  year: number; assets: number; liabilities: number; equityAccounts: number; surplus: number;
  totalLiabilitiesAndEquity: number; balanced: boolean;
  assetLines: LedgerLineDto[]; liabilityLines: LedgerLineDto[]; equityLines: LedgerLineDto[];
}
export interface ResultBlock { revenues: number; expenses: number; surplus: number }
export interface ReportSegregated { year: number; free: ResultBlock; restricted: ResultBlock; total: ResultBlock }
export interface ReportDmpl { year: number; openingEquity: number; surplus: number; closingEquity: number; freeSurplus: number; restrictedSurplus: number }
export interface ReportCashFlow { year: number; openingCash: number; inflows: number; outflows: number; netCash: number; closingCash: number }

const reportQuery = (year: number, organizationId?: string) =>
  `year=${year}${organizationId ? `&organizationId=${organizationId}` : ''}`;

export const reportIncome = (t: string, year: number, org?: string) =>
  authGet<ReportIncome>(t, `/api/finance/reports/income?${reportQuery(year, org)}`, 'DRE');
export const reportBalanceSheet = (t: string, year: number, org?: string) =>
  authGet<ReportBalanceSheet>(t, `/api/finance/reports/balance-sheet?${reportQuery(year, org)}`, 'Balanço');
export const reportSegregated = (t: string, year: number, org?: string) =>
  authGet<ReportSegregated>(t, `/api/finance/reports/income-segregated?${reportQuery(year, org)}`, 'segregação');
export const reportDmpl = (t: string, year: number, org?: string) =>
  authGet<ReportDmpl>(t, `/api/finance/reports/dmpl?${reportQuery(year, org)}`, 'DMPL');
export const reportCashFlow = (t: string, year: number, org?: string) =>
  authGet<ReportCashFlow>(t, `/api/finance/reports/cashflow?${reportQuery(year, org)}`, 'DFC');

// ---- Snapshots / assinatura das demonstrações (DT-10) ----

export interface SnapshotSummary {
  id: string; year: number; quarter: number | null; status: string; hash: string;
  generatedBy: string | null; approvedBy: string | null; approvedAt: string | null; createdAt: string;
}

export const listSnapshots = (t: string, year?: number) =>
  authGet<SnapshotSummary[]>(t, `/api/finance/reports/snapshots/${year ? `?year=${year}` : ''}`, 'snapshots');
export const generateSnapshot = (t: string, year: number) =>
  authSend<SnapshotSummary>(t, 'POST', '/api/finance/reports/snapshots/', { year }, 'gerar snapshot');
export const approveSnapshot = (t: string, id: string) =>
  authSend<SnapshotSummary>(t, 'POST', `/api/finance/reports/snapshots/${id}/approve`, {}, 'aprovar snapshot');

// ---- Exportação para o contador (CSV) ----

async function downloadCsv(token: string, path: string, filename: string): Promise<void> {
  const res = await fetch(`${BFF_URL}${path}`, { headers: { authorization: `Bearer ${token}` } });
  if (!res.ok) throw new Error(`Falha ao exportar (${res.status}).`);
  const blob = await res.blob();
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export const exportLedgerCsv = (t: string, year: number) =>
  downloadCsv(t, `/api/finance/export/ledger?year=${year}`, `razao-${year}.csv`);
export const exportTrialBalanceCsv = (t: string, year: number) =>
  downloadCsv(t, `/api/finance/export/trial-balance?year=${year}`, `balancete-${year}.csv`);

// ---- Transparência pública (sem autenticação; tenant no path) ----

export interface TransparencySummary {
  year: number; quarter: number | null; revenues: number; expenses: number;
  surplus: number; assets: number; liabilities: number; netEquity: number;
}

export async function publicTransparency(tenant: string, year: number, quarter?: number): Promise<TransparencySummary> {
  const res = await fetch(`${BFF_URL}/api/public/${tenant}/transparency?year=${year}${quarter ? `&quarter=${quarter}` : ''}`);
  if (!res.ok) throw new Error(`Não foi possível carregar a transparência (${res.status}).`);
  return res.json() as Promise<TransparencySummary>;
}

// ---- Trabalho voluntário a valor justo (RF-FIN-162) ----

export interface VolunteerWork { id: string; organizationId: string; description: string; fairValue: number; performedOn: string }

export const listVolunteerWork = (t: string) =>
  authGet<VolunteerWork[]>(t, '/api/finance/volunteer-work/', 'trabalho voluntário');
export const recordVolunteerWork = (
  t: string,
  body: { organizationId: string; description: string; fairValue: number; performedOn: string; projectId?: string; fundId?: string; costCenterId?: string },
) => authSend<VolunteerWork>(t, 'POST', '/api/finance/volunteer-work/', body, 'registrar voluntariado');

// ---- Prestação de contas MROSC por projeto (RF-FIN-163) ----

export interface MroscReport { projectId: string; projectName: string; expected: number; received: number; spent: number; balance: number }

export const mroscReport = (t: string, projectId: string) =>
  authGet<MroscReport>(t, `/api/finance/mrosc/${projectId}`, 'prestação MROSC');
