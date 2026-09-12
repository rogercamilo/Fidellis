'use client';

import { useCallback, useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { PageHeader, StatusBadge } from '../../components/Fiori';
import { Panel } from '../../components/Panel';
import {
  bootstrapStatus, completeOnboarding, createInvite, getFinanceSettings, listInvitations, listTeam, resendInvite,
  revokeInvite, roleLabelWith, setMemberRole, teamRoles,
  type BootstrapStatus, type Invitation, type LoginResult, type TeamMember,
} from '../../lib/api';

const INVITER_ROLES = new Set(['admin', 'coordinator']);

export default function EquipePage() {
  const router = useRouter();
  const [token, setToken] = useState<string | null>(null);
  const [role, setRole] = useState<string>('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [welcome, setWelcome] = useState(false);

  const [team, setTeam] = useState<TeamMember[]>([]);
  const [roles, setRoles] = useState<string[]>([]);
  const [invites, setInvites] = useState<Invitation[]>([]);
  const [status, setStatus] = useState<BootstrapStatus | null>(null);
  const [roleLabels, setRoleLabels] = useState<Record<string, string>>({});

  const [email, setEmail] = useState('');
  const [inviteRole, setInviteRole] = useState('coordinator');

  const isAdmin = role === 'admin';
  const canInvite = INVITER_ROLES.has(role);

  const refresh = useCallback(async (t: string) => {
    try {
      const [tm, rs, iv, st, cfg] = await Promise.all([
        listTeam(t), teamRoles(t), listInvitations(t), bootstrapStatus(t), getFinanceSettings(t),
      ]);
      setTeam(tm);
      setRoles(rs);
      setInvites(iv);
      setStatus(st);
      setRoleLabels(cfg.roleLabels ?? {});
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar.');
    }
  }, []);

  useEffect(() => {
    const raw = sessionStorage.getItem('fidellis.session');
    if (!raw) return;
    const s = JSON.parse(raw) as LoginResult;
    setToken(s.accessToken);
    setRole(s.tenants.find((x) => x.slug === s.activeTenant)?.role ?? '');
    setWelcome(new URLSearchParams(window.location.search).get('welcome') === '1');
    void refresh(s.accessToken);
  }, [refresh]);

  async function invite(e: React.FormEvent) {
    e.preventDefault();
    if (!token) return;
    setError(null);
    setNotice(null);
    try {
      const r = await createInvite(token, { email: email.trim(), role: inviteRole });
      setEmail('');
      setNotice(
        r.outcome === 'Invited' ? 'Convite enviado por e-mail.'
          : r.outcome === 'MemberAdded' ? 'Pessoa já tinha conta — vinculada à equipe e notificada.'
            : 'Essa pessoa já faz parte da equipe.',
      );
      await refresh(token);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao convidar.');
    }
  }

  function guard(fn: () => Promise<unknown>) {
    return async () => {
      if (!token) return;
      setError(null);
      try { await fn(); await refresh(token); }
      catch (err) { setError(err instanceof Error ? err.message : 'Erro inesperado.'); }
    };
  }

  async function changeRole(userId: string, r: string) {
    if (!token) return;
    setError(null);
    try { await setMemberRole(token, userId, r); await refresh(token); }
    catch (err) { setError(err instanceof Error ? err.message : 'Erro ao atribuir papel.'); }
  }

  async function skip() {
    if (!token) return;
    try { await completeOnboarding(token); } catch { /* best-effort */ }
    router.push('/dashboard');
  }

  return (
    <>
      <PageHeader
        title={welcome ? 'Monte sua equipe e conselho' : 'Equipe & conselho'}
        subtitle={
          welcome
            ? 'Convide as pessoas que operam as finanças e governam a instituição. Você pode pular e fazer isso depois — mas a aprovação de pagamentos exige ao menos duas pessoas distintas.'
            : 'Convide membros por e-mail e papel; acompanhe convites pendentes e o estado de governança da instituição.'
        }
        actions={welcome && <button className="btn btn-ghost" onClick={skip}>Pular por agora</button>}
      />

      {error && <p className="error-text">{error}</p>}
      {notice && <p className="badge ok" style={{ display: 'inline-block', marginBottom: '0.75rem' }}>{notice}</p>}

      {status && (
        <div className="rise" style={{ marginBottom: '1rem' }}>
          <Panel title="Governança">
            <p style={{ margin: 0 }}>
              {status.teamReady ? (
                <><span className="badge ok">Equipe pronta</span>{' '}
                  {status.approverCount} pessoas com papel de aprovação — a segregação lançador ≠ aprovador está garantida.</>
              ) : (
                <><span className="badge warn">Em configuração</span>{' '}
                  {status.approverCount} de 2 aprovadores distintos. Enquanto não houver dois, o administrador consegue
                  destravar o primeiro pagamento com registro em auditoria — convide ao menos mais um aprovador.</>
              )}
            </p>
          </Panel>
        </div>
      )}

      {canInvite && (
        <div className="rise rise-2" style={{ marginBottom: '1rem' }}>
          <Panel title="Convidar membro">
            <form onSubmit={invite} style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', alignItems: 'flex-end' }}>
              <div className="field" style={{ flex: 1, minWidth: 220, marginBottom: 0 }}>
                <label htmlFor="inv-email">E-mail</label>
                <input id="inv-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} required placeholder="pessoa@instituicao.org" />
              </div>
              <div className="field" style={{ minWidth: 200, marginBottom: 0 }}>
                <label htmlFor="inv-role">Papel</label>
                <select id="inv-role" value={inviteRole} onChange={(e) => setInviteRole(e.target.value)}>
                  {roles.map((r) => <option key={r} value={r}>{roleLabelWith(roleLabels, r)}</option>)}
                </select>
              </div>
              <button className="btn btn-primary" type="submit">Enviar convite</button>
            </form>
          </Panel>
        </div>
      )}

      <div className="rise rise-3" style={{ marginBottom: '1rem' }}>
        <Panel title="Convites pendentes" flush>
          {invites.length === 0 ? (
            <p className="muted" style={{ padding: '0.75rem 1rem' }}>Nenhum convite pendente.</p>
          ) : (
            <table className="table">
              <thead><tr><th>E-mail</th><th>Papel</th><th>Expira em</th><th>Situação</th>{canInvite && <th />}</tr></thead>
              <tbody>
                {invites.map((i) => (
                  <tr key={i.id}>
                    <td>{i.email}</td>
                    <td>{roleLabelWith(roleLabels, i.role)}</td>
                    <td className="muted">{new Date(i.expiresAt).toLocaleDateString('pt-BR')}</td>
                    <td><StatusBadge status={i.status} /></td>
                    {canInvite && (
                      <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                        <button className="btn btn-ghost btn-sm" onClick={guard(() => resendInvite(token!, i.id))}>Reenviar</button>{' '}
                        <button className="btn btn-ghost btn-sm" onClick={guard(() => revokeInvite(token!, i.id))}>Revogar</button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </Panel>
      </div>

      <div className="rise rise-4">
        <Panel title="Membros" flush>
          <table className="table">
            <thead><tr><th>Usuário</th><th>E-mail</th><th>Papel</th></tr></thead>
            <tbody>
              {team.map((m) => (
                <tr key={m.userId}>
                  <td>{m.displayName || '—'}</td>
                  <td className="muted">{m.email}</td>
                  <td>
                    {isAdmin ? (
                      <select value={m.role} onChange={(e) => changeRole(m.userId, e.target.value)}>
                        {roles.map((r) => <option key={r} value={r}>{roleLabelWith(roleLabels, r)}</option>)}
                      </select>
                    ) : roleLabelWith(roleLabels, m.role)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Panel>
      </div>
    </>
  );
}
