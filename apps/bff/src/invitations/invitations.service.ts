import {
  BadRequestException,
  ConflictException,
  Inject,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { hash as argonHash } from '@node-rs/argon2';
import { createHash, randomUUID } from 'node:crypto';
import { Pool } from 'pg';
import { PG_POOL } from '../database/database.module';
import { TokenService } from '../auth/token.service';

export interface AcceptInput {
  token: string;
  password: string;
  displayName?: string;
}

interface InvitationRow {
  id: string;
  tenant_id: string;
  email: string;
  role: string;
  status: string;
  expires_at: string;
  slug: string;
  tenant_name: string;
}

/**
 * Aceite público do convite de membro (D-01). O core gera o convite e guarda apenas o hash do token
 * (SHA-256 hex); aqui recomputamos o hash do token recebido, validamos (pendente + não expirado) e,
 * como o BFF é o dono das credenciais (Argon2) e do `catalog` de identidade, criamos o usuário + a
 * membership e fazemos auto-login. Uso único: o aceite marca o convite como `accepted`.
 */
@Injectable()
export class InvitationsService {
  constructor(
    @Inject(PG_POOL) private readonly pool: Pool,
    private readonly tokens: TokenService,
  ) {}

  private hashToken(token: string): string {
    return createHash('sha256').update(token.trim()).digest('hex');
  }

  /** Convite pendente e válido para o token, ou null. Não vaza se o e-mail já é usuário. */
  private async findValid(token: string): Promise<InvitationRow | null> {
    const { rows } = await this.pool.query<InvitationRow>(
      `SELECT i.id, i.tenant_id, i.email, i.role, i.status, i.expires_at,
              t.slug, t.name AS tenant_name
         FROM catalog.invitations i
         JOIN catalog.tenants t ON t.id = i.tenant_id
        WHERE i.token_hash = $1 AND i.status = 'pending' AND i.expires_at > now()`,
      [this.hashToken(token)],
    );
    return rows[0] ?? null;
  }

  /** Dados públicos do convite para a tela de aceite (e-mail + instituição). */
  async describe(token: string): Promise<{ email: string; tenantName: string; role: string }> {
    const inv = await this.findValid(token);
    if (!inv) throw new NotFoundException('Convite inválido ou expirado.');
    return { email: inv.email, tenantName: inv.tenant_name, role: inv.role };
  }

  async accept(input: AcceptInput) {
    if (!input.password || input.password.length < 6)
      throw new BadRequestException('Senha deve ter ao menos 6 caracteres.');

    const inv = await this.findValid(input.token);
    if (!inv) throw new NotFoundException('Convite inválido ou expirado.');

    // Corrida: se o e-mail virou usuário entre o convite e o aceite, não recria — pede login.
    const existing = await this.pool.query('SELECT id FROM catalog.users WHERE email = $1', [inv.email]);
    if (existing.rowCount) throw new ConflictException('Já existe uma conta com este e-mail. Faça login.');

    const client = await this.pool.connect();
    try {
      await client.query('BEGIN');

      // Revalida o convite dentro da transação (uso único): só prossegue se ainda pendente.
      const locked = await client.query(
        `UPDATE catalog.invitations
            SET status = 'accepted', accepted_at = now()
          WHERE id = $1 AND status = 'pending' AND expires_at > now()
          RETURNING id`,
        [inv.id],
      );
      if (!locked.rowCount) {
        await client.query('ROLLBACK');
        throw new NotFoundException('Convite inválido ou expirado.');
      }

      const userId = randomUUID();
      const passwordHash = await argonHash(input.password);
      await client.query(
        'INSERT INTO catalog.users (id, email, password_hash, display_name, created_at) VALUES ($1, $2, $3, $4, now())',
        [userId, inv.email, passwordHash, input.displayName ?? null],
      );
      await client.query(
        'INSERT INTO catalog.memberships (id, user_id, tenant_id, role, created_at) VALUES ($1, $2, $3, $4, now())',
        [randomUUID(), userId, inv.tenant_id, inv.role],
      );

      await client.query('COMMIT');

      return {
        user: { id: userId, email: inv.email, displayName: input.displayName ?? null },
        accessToken: this.tokens.signAccess({ sub: userId, email: inv.email, tenant: inv.slug, role: inv.role }),
        refreshToken: this.tokens.signRefresh(userId),
        activeTenant: inv.slug,
        tenant: { slug: inv.slug, name: inv.tenant_name, role: inv.role },
      };
    } catch (err) {
      await client.query('ROLLBACK').catch(() => undefined);
      throw err;
    } finally {
      client.release();
    }
  }
}
