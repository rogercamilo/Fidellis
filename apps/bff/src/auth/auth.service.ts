import { Inject, Injectable, UnauthorizedException } from '@nestjs/common';
import { verify as argonVerify } from '@node-rs/argon2';
import { Pool } from 'pg';
import { PG_POOL } from '../database/database.module';
import { TokenService } from './token.service';

export interface UserRow {
  id: string;
  email: string;
  password_hash: string;
  display_name: string | null;
}

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

@Injectable()
export class AuthService {
  constructor(
    @Inject(PG_POOL) private readonly pool: Pool,
    private readonly tokens: TokenService,
  ) {}

  /** Valida credenciais contra `catalog.users`. Retorna o usuário ou lança 401. */
  async validateCredentials(email: string, password: string): Promise<UserRow> {
    const { rows } = await this.pool.query<UserRow>(
      'SELECT id, email, password_hash, display_name FROM catalog.users WHERE email = $1',
      [email.trim().toLowerCase()],
    );
    const user = rows[0];
    if (!user || !(await argonVerify(user.password_hash, password))) {
      throw new UnauthorizedException('Credenciais inválidas.');
    }
    return user;
  }

  /** Tenants aos quais o usuário pertence (via `catalog.memberships`). */
  async getMemberships(userId: string): Promise<TenantMembership[]> {
    const { rows } = await this.pool.query<TenantMembership>(
      `SELECT t.id AS "tenantId", t.slug, t.name, m.role
         FROM catalog.memberships m
         JOIN catalog.tenants t ON t.id = m.tenant_id
        WHERE m.user_id = $1
        ORDER BY t.slug`,
      [userId],
    );
    return rows;
  }

  async login(email: string, password: string, tenantSlug?: string): Promise<LoginResult> {
    const user = await this.validateCredentials(email, password);
    const tenants = await this.getMemberships(user.id);

    let activeTenant: string | null = null;
    if (tenantSlug) {
      const match = tenants.find((t) => t.slug === tenantSlug.trim().toLowerCase());
      if (!match) throw new UnauthorizedException(`Usuário não pertence ao tenant '${tenantSlug}'.`);
      activeTenant = match.slug;
    } else if (tenants.length === 1) {
      activeTenant = tenants[0].slug;
    }

    return {
      accessToken: this.tokens.signAccess({
        sub: user.id,
        email: user.email,
        tenant: activeTenant ?? undefined,
      }),
      refreshToken: this.tokens.signRefresh(user.id),
      user: { id: user.id, email: user.email, displayName: user.display_name },
      tenants,
      activeTenant,
    };
  }

  /** Emite novo access token (opcionalmente assumindo um tenant) a partir do refresh. */
  async refresh(refreshToken: string, tenantSlug?: string): Promise<{ accessToken: string; activeTenant: string | null }> {
    let sub: string;
    try {
      const claims = this.tokens.verify<{ sub: string; typ?: string }>(refreshToken);
      if (claims.typ !== 'refresh') throw new Error('token não é refresh');
      sub = claims.sub;
    } catch {
      throw new UnauthorizedException('Refresh token inválido ou expirado.');
    }

    const { rows } = await this.pool.query<UserRow>(
      'SELECT id, email, password_hash, display_name FROM catalog.users WHERE id = $1',
      [sub],
    );
    const user = rows[0];
    if (!user) throw new UnauthorizedException('Usuário não encontrado.');

    let activeTenant: string | null = null;
    if (tenantSlug) {
      const tenants = await this.getMemberships(user.id);
      const match = tenants.find((t) => t.slug === tenantSlug.trim().toLowerCase());
      if (!match) throw new UnauthorizedException(`Usuário não pertence ao tenant '${tenantSlug}'.`);
      activeTenant = match.slug;
    }

    return {
      accessToken: this.tokens.signAccess({ sub: user.id, email: user.email, tenant: activeTenant ?? undefined }),
      activeTenant,
    };
  }
}
